using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AwakenServer.Chains;
using AwakenServer.Comparers;
using AwakenServer.Grains;
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
    public interface ITradePairMarketDataProvider
    {
        Task InitializeDataAsync();

        Task UpdateTotalSupplyWithLiquidityEventAsync(string chainId, Guid tradePairId, string Token0Symbol,
            string Token1Symbol, double FeeRate, DateTime timestamp, BigDecimal lpTokenAmount);

        Task UpdateTradeRecordAsync(string chainId, Guid tradePairId, DateTime timestamp, double volume,
            double tradeValue, int tradeCount = 1);

        Task UpdateLiquidityWithSyncEventAsync(string chainId, Guid tradePairId, DateTime timestamp, double price,
            double priceUSD,
            double tvl, double valueLocked0, double valueLocked1);


        Task<Index.TradePairMarketDataSnapshot> GetTradePairMarketDataIndexAsync(string chainId, Guid tradePairId,
            DateTime snapshotTime);

        Task<Index.TradePairMarketDataSnapshot> GetLatestPriceTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime);

        DateTime GetSnapshotTime(DateTime time);

        Task<Index.TradePairMarketDataSnapshot>
            GetLatestTradePairMarketDataIndexAsync(string chainId, Guid tradePairId);

        Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListAsync(string chainId, Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null);


        Task<ITradePairMarketDataSnapshotGrain> GetSnapShotGrain(string chainId, Guid tradePairId,
            DateTime snapshotTime);

        Task<TradePairMarketDataSnapshotGrainDto> GetLatestTradePairMarketDataFromGrainAsync(string chainId,
            Guid tradePairId);

        Task<List<TradePairMarketDataSnapshotGrainDto>> GetTradePairMarketDataListFromGrainAsync(string chainId,
            Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null);
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
        private readonly TradeRecordOptions _tradeRecordOptions;
        private readonly IAbpDistributedLock _distributedLock;
        private readonly IClusterClient _clusterClient;
        private readonly ConcurrentDictionary<string, HashSet<Tuple<string, DateTime>>> _tradePairToGrainIds;
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
            IOptionsSnapshot<TradeRecordOptions> tradeRecordOptions,
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
            _tradeRecordOptions = tradeRecordOptions.Value;
            _tradePairToGrainIds = new ConcurrentDictionary<string, HashSet<Tuple<string, DateTime>>>();
            _clusterClient = clusterClient;
            _blockchainClientProvider = blockchainClientProvider;
            _contractsTokenOptions = contractsTokenOptions.Value;
        }

        private string GenPartOfTradePairGrainId(string chainId, Guid tradePairId)
        {
            return GrainIdHelper.GenerateGrainId(chainId, tradePairId);
        }

        private string GenTradePairGrainId(string chainId, Guid tradePairId, DateTime datetime)
        {
            return GrainIdHelper.GenerateGrainId(chainId, tradePairId, datetime);
        }

        public async Task InitializeDataAsync()
        {
            var tradePairList = await _tradePairIndexRepository.GetListAsync();
            var now = DateTime.Now;
            foreach (var tradePair in tradePairList.Item2)
            {
                var tradePairSnapshots = await GetIndexListAsync(tradePair.ChainId, tradePair.Id, now.AddDays(-7), now);
                foreach (var snapshot in tradePairSnapshots)
                {
                    _tradePairToGrainIds.TryAdd(GenPartOfTradePairGrainId(tradePair.ChainId, tradePair.Id),
                        new HashSet<Tuple<string, DateTime>>());
                    _tradePairToGrainIds[GenPartOfTradePairGrainId(tradePair.ChainId, tradePair.Id)]
                        .Add(new Tuple<string, DateTime>(
                            GenTradePairGrainId(tradePair.ChainId, tradePair.Id, snapshot.Timestamp),
                            snapshot.Timestamp));
                    
                    // for history data before add grain
                    var grain = await GetSnapShotGrain(tradePair.ChainId, tradePair.Id, snapshot.Timestamp);
                    await grain.AddOrUpdateAsync(_objectMapper
                        .Map<Index.TradePairMarketDataSnapshot, TradePairMarketDataSnapshotGrainDto>(snapshot));
                }
                
                // for history data before add grain
                var tradePairGrain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(tradePair.Id));
                var tradePairResult = await tradePairGrain.GetAsync();
                if (!tradePairResult.Success)
                {
                    await tradePairGrain.AddOrUpdateAsync(_objectMapper.Map<Index.TradePair, TradePairGrainDto>(tradePair));
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

        private async Task AddOrUpdateTradePairIndexAsync(TradePairMarketDataSnapshotGrainDto snapshotDto)
        {
            _logger.LogInformation("AddOrUpdateTradePairIndexAsync, lp token {id}, totalSupply: {supply}",
                snapshotDto.TradePairId, snapshotDto.TotalSupply);
            
            var latestSnapshot =
                await GetLatestTradePairMarketDataFromGrainAsync(snapshotDto.ChainId,
                    snapshotDto.TradePairId);

            if (latestSnapshot != null && snapshotDto.Timestamp < latestSnapshot.Timestamp)
            {
                return;
            }

            var grain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(snapshotDto.TradePairId));
            
            var existResult = await grain.GetAsync();
            if (!existResult.Success)
            {
                _logger.LogError($"AddOrUpdateTradePairIndexAsync: {snapshotDto.TradePairId} does not exist");
                return;
            }
            
            var previous7DaysSnapshotDtos = await GetTradePairMarketDataListFromGrainAsync(snapshotDto.ChainId,
                snapshotDto.TradePairId, snapshotDto.Timestamp.AddDays(-7));
            
            var latestBeforeThisSnapshot = await GetLatestPriceTradePairMarketDataFromGrainAsync(snapshotDto.ChainId,
                snapshotDto.TradePairId,
                snapshotDto.Timestamp);
            
            var snapshotGrainResult = await grain.AddOrUpdateFromTradeAsync(snapshotDto, 
                previous7DaysSnapshotDtos, 
                latestBeforeThisSnapshot);
            if (!snapshotGrainResult.Success)
            {
                _logger.LogError($"AddOrUpdateTradePairIndexAsync: updage grain {snapshotDto.TradePairId} failed");
                return;
            }
            
            _logger.LogInformation("AddOrUpdateTradePairIndexAsync: " + JsonConvert.SerializeObject(snapshotGrainResult.Data));

            await _distributedEventBus.PublishAsync(new EntityCreatedEto<TradePairEto>(
                _objectMapper.Map<TradePairGrainDto, TradePairEto>(
                    snapshotGrainResult.Data)
            ));
        }

        public async Task UpdateTotalSupplyWithLiquidityEventAsync(string chainId, Guid tradePairId,
            string Token0Symbol, string Token1Symbol, double FeeRate, DateTime timestamp,
            BigDecimal lpTokenAmount)
        {
            _logger.LogInformation("UpdateTotalSupplyAsync: input supply:{supply}", lpTokenAmount);

            var snapshotTime = GetSnapshotTime(timestamp);
            var lockName = $"{chainId}-{tradePairId}-{snapshotTime}";
            await using var handle = await _distributedLock.TryAcquireAsync(lockName);

            var grain = await GetSnapShotGrain(chainId, tradePairId, snapshotTime);
            var lpTokenCurrentSupply = await GetLpTokenInfoAsync(chainId, Token0Symbol, Token1Symbol, FeeRate);
            var latestBeforeDto =
                await GetLatestTradePairMarketDataFromGrainAsync(chainId, tradePairId, snapshotTime);
            var userTradeAddressCount = await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId,
                tradePairId,
                timestamp.AddDays(-1), 
                timestamp);

            var updateResult = await grain.UpdateTotalSupplyWithLiquidityAsync(new TradePairMarketDataSnapshotGrainDto
                {
                    Id = Guid.NewGuid(),
                    ChainId = chainId,
                    TradePairId = tradePairId,
                    Timestamp = snapshotTime,
                },
                latestBeforeDto, 
                lpTokenAmount, 
                userTradeAddressCount, 
                lpTokenCurrentSupply);

            await _distributedEventBus.PublishAsync(new EntityCreatedEto<TradePairMarketDataSnapshotEto>(
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>(
                    updateResult.Data)
            ));

            await AddOrUpdateTradePairIndexAsync(updateResult.Data);
            
            //nie:The current snapshot is not up-to-date. The latest snapshot needs to update TotalSupply 
            var latestMarketDataGrain =
                _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
                    await GetLatestTradePairGrainId(chainId, tradePairId));

            var updateLatestResult =
                await latestMarketDataGrain.UpdateTotalSupplyAsync(lpTokenAmount, lpTokenCurrentSupply, snapshotTime);

            await _distributedEventBus.PublishAsync(new EntityUpdatedEto<TradePairMarketDataSnapshotEto>(
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>(
                    updateLatestResult.Data)
            ));
            
            await AddOrUpdateTradePairIndexAsync(updateLatestResult.Data);
        }

        public async Task UpdateTradeRecordAsync(string chainId, Guid tradePairId, DateTime timestamp, double volume,
            double tradeValue, int tradeCount = 1)
        {
            _logger.LogInformation(
                "UpdateTradeRecordAsync start.chainId:{chainId},tradePairId:{tradePairId},timestamp:{timestamp},volume:{volume},tradeValue:{tradeValue},tradeCount:{tradeCount}",
                chainId, tradePairId, timestamp, volume, tradeValue, tradeCount);

            var snapshotTime = GetSnapshotTime(timestamp);
            var lockName = $"{chainId}-{tradePairId}-{snapshotTime}";
            await using var handle = await _distributedLock.TryAcquireAsync(lockName);

            var tradeAddressCount24H = await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId,
                tradePairId,
                GetSnapshotTime(timestamp).AddDays(-1), timestamp);

            var grain = await GetSnapShotGrain(chainId, tradePairId, snapshotTime);
            var lastMarketData =
                await GetLatestTradePairMarketDataFromGrainAsync(chainId, tradePairId, snapshotTime);

            var updateResult = await grain.UpdateTradeRecord(new TradePairMarketDataSnapshotGrainDto
                {
                    Id = Guid.NewGuid(),
                    ChainId = chainId,
                    TradePairId = tradePairId,
                    Volume = volume,
                    TradeValue = tradeValue,
                    TradeCount = tradeCount,
                    TradeAddressCount24h = tradeAddressCount24H,
                    Timestamp = snapshotTime,
                },
                lastMarketData);

            await _distributedEventBus.PublishAsync(new EntityUpdatedEto<TradePairMarketDataSnapshotEto>(
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>(
                    updateResult.Data)
            ));
            
            await AddOrUpdateTradePairIndexAsync(updateResult.Data);
        }

        public async Task UpdateLiquidityWithSyncEventAsync(string chainId, Guid tradePairId, DateTime timestamp,
            double price,
            double priceUSD, double tvl,
            double valueLocked0, double valueLocked1)
        {
            _logger.LogInformation(
                "UpdateLiquidityAsync start.chainId:{chainId},tradePairId:{tradePairId},timestamp:{timestamp},price:{price},priceUSD:{priceUSD},tvl:{tvl}",
                chainId, tradePairId, timestamp, price, priceUSD, tvl);

            var snapshotTime = GetSnapshotTime(timestamp);
            var lockName = $"{chainId}-{tradePairId}-{snapshotTime}";
            await using var handle = await _distributedLock.TryAcquireAsync(lockName);

            var grain = await GetSnapShotGrain(chainId, tradePairId, snapshotTime);
            var lastMarketData =
                await GetLatestTradePairMarketDataFromGrainAsync(chainId, tradePairId, snapshotTime);
            var userTradeAddressCount = await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId,
                tradePairId,
                timestamp.AddDays(-1), timestamp);

            var updateResult = await grain.UpdateLiquidityWithSyncEvent(new TradePairMarketDataSnapshotGrainDto
                {
                    Id = Guid.NewGuid(),
                    ChainId = chainId,
                    TradePairId = tradePairId,
                    Price = price,
                    PriceHigh = price,
                    PriceLow = price,
                    PriceLowUSD = priceUSD,
                    PriceHighUSD = priceUSD,
                    PriceUSD = priceUSD,
                    TVL = tvl,
                    ValueLocked0 = valueLocked0,
                    ValueLocked1 = valueLocked1,
                    Timestamp = snapshotTime,
                },
                lastMarketData,
                userTradeAddressCount);

            if (updateResult.Data.Timestamp == DateTime.MinValue)
            {
                _logger.LogError($"UpdateLiquidityAsync failed TradePairId: {tradePairId}");
                return;
            }
            
            await _distributedEventBus.PublishAsync(new EntityUpdatedEto<TradePairMarketDataSnapshotEto>(
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>(
                    updateResult.Data)
            ));
            
            await AddOrUpdateTradePairIndexAsync(updateResult.Data);
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

        public async Task<ITradePairMarketDataSnapshotGrain> GetSnapShotGrain(string chainId, Guid tradePairId,
            DateTime snapshotTime)
        {
            var partOfGrainId = GenPartOfTradePairGrainId(chainId, tradePairId);
            var grainId = GrainIdHelper.GenerateGrainId(chainId, tradePairId, snapshotTime);
            _tradePairToGrainIds.TryAdd(partOfGrainId, new HashSet<Tuple<string, DateTime>>());
            _tradePairToGrainIds[partOfGrainId].Add(new Tuple<string, DateTime>(grainId, snapshotTime));
            return _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(grainId);
        }

        public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestPriceTradePairMarketDataFromGrainAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime)
        {
            if (!_tradePairToGrainIds.ContainsKey(GenPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return null;
            }

            var grainList = _tradePairToGrainIds[GenPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());
            foreach (var grainId in grainList)
            {
                if (GetSnapshotTime(snapshotTime) > grainId.Item2)
                {
                    var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(grainId.Item1);
                    var marketDataResultDto = await grain.GetAsync();
                    if (marketDataResultDto.Success && marketDataResultDto.Data.PriceUSD > 0)
                    {
                        return marketDataResultDto.Data;
                    }
                }
            }

            return null;
        }
        
        private async Task<TradePairMarketDataSnapshotGrainDto> GetLatestTradePairMarketDataFromGrainAsync(
            string chainId,
            Guid tradePairId, DateTime maxTime)
        {
            if (!_tradePairToGrainIds.ContainsKey(GenPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return null;
            }

            var grainList = _tradePairToGrainIds[GenPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());
            foreach (var grainId in grainList)
            {
                if (maxTime >= grainId.Item2)
                {
                    var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(grainId.Item1);
                    var marketDataResultDto = await grain.GetAsync();
                    if (marketDataResultDto.Success)
                    {
                        return marketDataResultDto.Data;
                    }
                }
            }

            return null;
        }

        public async Task<string> GetLatestTradePairGrainId(string chainId,
            Guid tradePairId)
        {
            if (!_tradePairToGrainIds.ContainsKey(GenPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return null;
            }

            var grainList = _tradePairToGrainIds[GenPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());

            if (grainList.IsNullOrEmpty())
            {
                return null;
            }

            return grainList.First().Item1;
        }

        public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestTradePairMarketDataFromGrainAsync(
            string chainId,
            Guid tradePairId)
        {
            var grainId = await GetLatestTradePairGrainId(chainId, tradePairId);
            if (grainId == null)
            {
                return null;
            }

            var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(grainId);
            var marketDataResultDto = await grain.GetAsync();
            if (marketDataResultDto.Success)
            {
                return marketDataResultDto.Data;
            }

            return null;
        }

        public async Task<List<TradePairMarketDataSnapshotGrainDto>> GetTradePairMarketDataListFromGrainAsync(
            string chainId,
            Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null)
        {
            List<TradePairMarketDataSnapshotGrainDto> resultList = new List<TradePairMarketDataSnapshotGrainDto>();
            if (!_tradePairToGrainIds.ContainsKey(GenPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return resultList;
            }

            var grainList = _tradePairToGrainIds[GenPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());
            foreach (var grainId in grainList)
            {
                if (timestampMin != null && grainId.Item2 < timestampMin)
                {
                    continue;
                }

                if (timestampMax != null && grainId.Item2 >= timestampMax)
                {
                    continue;
                }

                var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(grainId.Item1);
                var marketDataResultDto = await grain.GetAsync();
                if (marketDataResultDto.Success)
                {
                    resultList.Add(marketDataResultDto.Data);
                }
            }

            return resultList;
        }

        public class CacheKeys
        {
            HashSet<string> Set { get; set; }
        }
    }
}