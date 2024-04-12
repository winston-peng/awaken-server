using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Client.Proto;
using AElf.Indexing.Elasticsearch;
using AwakenServer.Chains;
using AwakenServer.Comparers;
using AwakenServer.Grains;
using AwakenServer.Grains.Grain;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade.Dtos;
using AwakenServer.Trade.Etos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Nethereum.Util;
using Orleans;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace AwakenServer.Trade
{
    public delegate Task<GrainResultDto<TradePairMarketDataSnapshotUpdateResult>> TradePairMethodDelegate(ITradePairGrain grain);
    
    public interface ITradePairMarketDataProvider
    {
        Task InitializeDataAsync();
        
        Task AddOrUpdateSnapshotAsync(Guid tradePairId, 
            TradePairMethodDelegate methodDelegate);
        
        Task<Index.TradePairMarketDataSnapshot> GetTradePairMarketDataIndexAsync(string chainId, Guid tradePairId,
            DateTime snapshotTime);

        Task<Index.TradePairMarketDataSnapshot> GetLatestPriceTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime);

        DateTime GetSnapshotTime(DateTime time);

        Task<Index.TradePairMarketDataSnapshot>
            GetLatestTradePairMarketDataIndexAsync(string chainId, Guid tradePairId);

        Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListAsync(string chainId, Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null);
        
        Task<TradePairMarketDataSnapshotGrainDto> GetLatestTradePairMarketDataFromGrainAsync(string chainId,
            Guid tradePairId);
        
    }

    public class TradePairMarketDataProvider : ITransientDependency, ITradePairMarketDataProvider
    {
        private readonly INESTRepository<Index.TradePairMarketDataSnapshot, Guid> _snapshotIndexRepository;
        private readonly INESTRepository<Index.TradePair, Guid> _tradePairIndexRepository;
        private readonly ITradeRecordAppService _tradeRecordAppService;
        private readonly IDistributedEventBus _distributedEventBus;
        private readonly IObjectMapper _objectMapper;
        private readonly IBus _bus;
        private readonly ILogger<TradePairMarketDataProvider> _logger;
        private readonly IAbpDistributedLock _distributedLock;
        private readonly IClusterClient _clusterClient;
        private readonly IAElfClientProvider _blockchainClientProvider;
        private readonly ContractsTokenOptions _contractsTokenOptions;

        private static DateTime lastWriteTime;

        private static BigDecimal lastTotal;

        public TradePairMarketDataProvider(
            INESTRepository<Index.TradePairMarketDataSnapshot, Guid> snapshotIndexRepository,
            INESTRepository<Index.TradePair, Guid> tradePairIndexRepository,
            ITradeRecordAppService tradeRecordAppService,
            IDistributedEventBus distributedEventBus,
            IBus bus,
            IObjectMapper objectMapper,
            IAbpDistributedLock distributedLock,
            ILogger<TradePairMarketDataProvider> logger,
            IClusterClient clusterClient,
            IAElfClientProvider blockchainClientProvider, IOptions<ContractsTokenOptions> contractsTokenOptions)
        {
            _snapshotIndexRepository = snapshotIndexRepository;
            _tradePairIndexRepository = tradePairIndexRepository;
            _tradeRecordAppService = tradeRecordAppService;
            _distributedEventBus = distributedEventBus;
            _objectMapper = objectMapper;
            _bus = bus;
            _distributedLock = distributedLock;
            _logger = logger;
            _clusterClient = clusterClient;
            _blockchainClientProvider = blockchainClientProvider;
            _contractsTokenOptions = contractsTokenOptions.Value;
        }

        
        public async Task InitializeDataAsync()
        {
            var tradePairList = await _tradePairIndexRepository.GetListAsync();
            var now = DateTime.Now;
            foreach (var tradePair in tradePairList.Item2)
            {
                // for history data before add grain
                var tradePairGrain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(tradePair.Id));
                var tradePairResult = await tradePairGrain.GetAsync();
                if (!tradePairResult.Success)
                {
                    
                    var tradePairSnapshots = await GetIndexListAsync(tradePair.ChainId, tradePair.Id, now.AddDays(-7), now);
                    foreach (var snapshot in tradePairSnapshots)
                    {
                        await tradePairGrain.AddOrUpdateSnapshotAsync(_objectMapper
                            .Map<Index.TradePairMarketDataSnapshot, TradePairMarketDataSnapshotGrainDto>(snapshot));
                    }
                    
                    await tradePairGrain.AddOrUpdateAsync(_objectMapper.Map<Index.TradePair, TradePairGrainDto>(tradePair));
                    _logger.LogInformation($"sync TradePairGrain grainId: {tradePairGrain.GetPrimaryKeyString()}, address: {tradePair.Address} from es to grain");
                }
            }
        }

        private async Task<string> GetLpTokenInfoAsync(string chainId, string Token0Symbol, string Token1Symbol,
            double FeeRate)
        {
            try
            {
                if (!_contractsTokenOptions.Contracts.TryGetValue(FeeRate.ToString(), out var address))
                {
                    return null;
                }

                var token = await _blockchainClientProvider.GetTokenInfoFromChainAsync(chainId, address,
                    TradePairHelper.GetLpToken(Token0Symbol, Token1Symbol));
                if (token != null)
                {
                    return token.Supply.ToDecimalsString(token.Decimals);
                }

                _logger.LogError("Get lp token info is null:lp token:{0}",
                    TradePairHelper.GetLpToken(Token0Symbol, Token1Symbol));
                return "";
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Get token info failed");
                return null;
            }
        }
        
        public async Task AddOrUpdateSnapshotAsync(Guid tradePairId, TradePairMethodDelegate methodDelegate)
        {
            var grain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(tradePairId));
            if (!(await grain.GetAsync()).Success)
            {
                _logger.LogInformation($"trade pair: {tradePairId} not exist");
                return;
            }
            
            var result = await methodDelegate(grain);

            _logger.LogInformation("AddOrUpdateSnapshotAsync: distributedEventBus.PublishAsync TradePairEto: " + JsonConvert.SerializeObject(result.Data.TradePairDto));

            await _distributedEventBus.PublishAsync(new EntityCreatedEto<TradePairEto>(
                _objectMapper.Map<TradePairGrainDto, TradePairEto>(
                    result.Data.TradePairDto)
            ));
            
            _logger.LogInformation("AddOrUpdateSnapshotAsync: distributedEventBus.PublishAsync TradePairMarketDataSnapshotEto: " + JsonConvert.SerializeObject(result.Data.SnapshotDto));
            
            await _distributedEventBus.PublishAsync(new EntityCreatedEto<TradePairMarketDataSnapshotEto>(
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>(
                    result.Data.SnapshotDto)
            ));
        }
        

        private async Task<Index.TradePairMarketDataSnapshot> GetLatestTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime maxTime)
        {
            return await _snapshotIndexRepository.GetAsync(
                q => q.Term(i => i.Field(f => f.ChainId).Value(chainId))
                     && q.Term(i => i.Field(f => f.TradePairId).Value(tradePairId))
                     && q.DateRange(i => i.Field(f => f.Timestamp).LessThanOrEquals(maxTime)),
                sortExp: s => s.Timestamp, sortType: SortOrder.Descending);
        }

        public async Task<Index.TradePairMarketDataSnapshot> GetLatestPriceTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime)
        {
            return await _snapshotIndexRepository.GetAsync(q =>
                    q.Bool(i =>
                        i.Filter(f =>
                            f.Range(i =>
                                i.Field(f => f.PriceUSD).GreaterThan(0)) &&
                            f.DateRange(i =>
                                i.Field(f => f.Timestamp).LessThan(GetSnapshotTime(snapshotTime))) &&
                            q.Term(i => i.Field(f => f.ChainId).Value(chainId)) &&
                            q.Term(i => i.Field(f => f.TradePairId).Value(tradePairId))
                        )
                    ),
                sortExp: s => s.Timestamp, sortType: SortOrder.Descending);
        }


        public DateTime GetSnapshotTime(DateTime time)
        {
            return time.Date.AddHours(time.Hour);
        }

        public async Task<Index.TradePairMarketDataSnapshot> GetTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime)
        {
            return await _snapshotIndexRepository.GetAsync(
                q => q.Term(i => i.Field(f => f.ChainId).Value(chainId))
                     && q.Term(i => i.Field(f => f.TradePairId).Value(tradePairId))
                     && q.Term(i => i.Field(f => f.Timestamp).Value(snapshotTime)));
        }

        public async Task<Index.TradePairMarketDataSnapshot> GetLatestTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId)
        {
            return await _snapshotIndexRepository.GetAsync(q =>
                    q.Term(i => i.Field(f => f.ChainId).Value(chainId)) &&
                    q.Term(i => i.Field(f => f.TradePairId).Value(tradePairId)),
                sortExp: s => s.Timestamp, sortType: SortOrder.Descending);
        }

        public async Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListAsync(string chainId, Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null)
        {
            var mustQuery =
                new List<Func<QueryContainerDescriptor<Index.TradePairMarketDataSnapshot>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i => i.Field(f => f.ChainId).Value(chainId)));
            mustQuery.Add(q => q.Term(i => i.Field(f => f.TradePairId).Value(tradePairId)));

            if (timestampMin != null)
            {
                mustQuery.Add(q => q.DateRange(i =>
                    i.Field(f => f.Timestamp)
                        .GreaterThanOrEquals(timestampMin.Value)));
            }

            if (timestampMax != null)
            {
                mustQuery.Add(q => q.DateRange(i =>
                    i.Field(f => f.Timestamp)
                        .LessThan(timestampMax)));
            }

            QueryContainer Filter(QueryContainerDescriptor<Index.TradePairMarketDataSnapshot> f) =>
                f.Bool(b => b.Must(mustQuery));

            var list = await _snapshotIndexRepository.GetListAsync(Filter);
            return list.Item2;
        }
        
        
        public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestTradePairMarketDataFromGrainAsync(
            string chainId,
            Guid tradePairId)
        {
            
            var grain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(tradePairId));
            return await grain.GetLatestSnapshotAsync();
        }

        public class CacheKeys
        {
            HashSet<string> Set { get; set; }
        }
    }
}