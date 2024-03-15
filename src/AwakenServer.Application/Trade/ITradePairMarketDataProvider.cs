using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AwakenServer.Comparers;
using AwakenServer.Grains;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade.Dtos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Nethereum.Util;
using Orleans;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace AwakenServer.Trade
{
    public interface ITradePairMarketDataProvider
    {
        Task InitializeDataAsync();

        Task UpdateTotalSupplyAsync(string chainId, Guid tradePairId, DateTime timestamp, BigDecimal lpTokenAmount,
            string supply = null);

        Task UpdateTradeRecordAsync(string chainId, Guid tradePairId, DateTime timestamp, double volume,
            double tradeValue, int tradeCount = 1);

        Task UpdateLiquidityAsync(string chainId, Guid tradePairId, DateTime timestamp, double price, double priceUSD,
            double tvl, double valueLocked0, double valueLocked1);

        Task<TradePairMarketDataSnapshot> GetLatestTradePairMarketDataAsync(string chainId, Guid tradePairId);

        Task<Index.TradePairMarketDataSnapshot> GetTradePairMarketDataIndexAsync(string chainId, Guid tradePairId,
            DateTime snapshotTime);

        Task<Index.TradePairMarketDataSnapshot> GetLatestPriceTradePairMarketDataIndexAsync(string chainId,
            Guid tradePairId, DateTime snapshotTime);

        DateTime GetSnapshotTime(DateTime time);

        Task<Index.TradePairMarketDataSnapshot>
            GetLatestTradePairMarketDataIndexAsync(string chainId, Guid tradePairId);

        Task<Index.TradePairMarketDataSnapshot>
            GetLatestTradePairMarketDataIndexFromGrainAsync(string chainId, Guid tradePairId);

        Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListAsync(string chainId, Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null);

        Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListFromGrainAsync(string chainId,
            Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null);

        Task<ITradePairSnapshotGrain> GetSnapShotGrain(string chainId, Guid tradePairId,
            DateTime snapshotTime);
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
            IClusterClient clusterClient)
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
        }

        private string genPartOfTradePairGrainId(string chainId, Guid tradePairId)
        {
            return GrainIdHelper.GenerateGrainId(chainId, tradePairId);
        }

        private string genTradePairGrainId(string chainId, Guid tradePairId, DateTime datetime)
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
                    
                    _tradePairToGrainIds.TryAdd(genPartOfTradePairGrainId(tradePair.ChainId, tradePair.Id),
                            new HashSet<Tuple<string, DateTime>>());
                    _tradePairToGrainIds[genPartOfTradePairGrainId(tradePair.ChainId, tradePair.Id)]
                        .Add(new Tuple<string, DateTime>(
                            genTradePairGrainId(tradePair.ChainId, tradePair.Id, snapshot.Timestamp),
                            snapshot.Timestamp));
                    // for history data before add grain
                    var grain = await GetSnapShotGrain(tradePair.ChainId, tradePair.Id, snapshot.Timestamp);
                    grain.AddOrUpdateAsync(snapshot);
                }
            }
        }

        public async Task UpdateTotalSupplyAsync(string chainId, Guid tradePairId, DateTime timestamp,
            BigDecimal lpTokenAmount, string supply = null)
        {
            _logger.LogInformation("UpdateTotalSupplyAsync: input supply:{supply}", supply);

            var snapshotTime = GetSnapshotTime(timestamp);
            var lockName = $"{chainId}-{tradePairId}-{snapshotTime}";
            await using var handle = await _distributedLock.TryAcquireAsync(lockName);

            var grain = await GetSnapShotGrain(chainId, tradePairId, snapshotTime);
            var marketData = await grain.GetAsync();

            if (marketData == null)
            {
                var lastMarketData =
                    await GetLatestTradePairMarketDataIndexFromGrainAsync(chainId, tradePairId, snapshotTime);
                var totalSupply = lpTokenAmount;
                if (lastMarketData != null)
                {
                    totalSupply += BigDecimal.Parse(lastMarketData.TotalSupply);
                }

                marketData = new Index.TradePairMarketDataSnapshot()
                {
                    Id = Guid.NewGuid(),
                    ChainId = chainId,
                    TradePairId = tradePairId,
                    TotalSupply = string.IsNullOrWhiteSpace(supply) ? totalSupply.ToNormalizeString() : supply,
                    Timestamp = snapshotTime
                };
                if (lastMarketData != null)
                {
                    marketData.Price = lastMarketData.Price;
                    marketData.PriceUSD = lastMarketData.PriceUSD;
                    marketData.TVL = lastMarketData.TVL;
                    marketData.ValueLocked0 = lastMarketData.ValueLocked0;
                    marketData.ValueLocked1 = lastMarketData.ValueLocked1;
                }


                marketData.TradeAddressCount24h =
                    await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId, tradePairId,
                        timestamp.AddDays(-1), timestamp);

                await grain.AddOrUpdateAsync(marketData);

                await _snapshotIndexRepository.AddAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }
            else
            {
                var totalSupply = BigDecimal.Parse(marketData.TotalSupply);
                marketData.TotalSupply =
                    string.IsNullOrWhiteSpace(supply) ? (totalSupply + lpTokenAmount).ToNormalizeString() : supply;

                await grain.AddOrUpdateAsync(marketData);

                await _snapshotIndexRepository.UpdateAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }

            _logger.LogInformation("UpdateTotalSupplyAsync: totalSupply:{supply}", marketData.TotalSupply);

            //nie:The current snapshot is not up-to-date. The latest snapshot needs to update TotalSupply 
            var latestMarketData = await GetLatestTradePairMarketDataIndexFromGrainAsync(chainId, tradePairId);
            if (latestMarketData != null && latestMarketData.Timestamp > snapshotTime)
            {
                latestMarketData.TotalSupply = string.IsNullOrWhiteSpace(supply)
                    ? (BigDecimal.Parse(latestMarketData.TotalSupply) + lpTokenAmount).ToNormalizeString()
                    : supply;
                _logger.LogInformation("UpdateTotalSupplyAsync: latest totalSupply:{supply}",
                    latestMarketData.TotalSupply);

                await grain.AddOrUpdateAsync(marketData);

                await _snapshotIndexRepository.UpdateAsync(latestMarketData);
                await AddOrUpdateTradePairIndexAsync(latestMarketData);
            }
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

            var grain = await GetSnapShotGrain(chainId, tradePairId, snapshotTime);
            var marketData = await grain.GetAsync();

            var tradeAddressCount24H = await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId,
                tradePairId,
                GetSnapshotTime(timestamp).AddDays(-1), timestamp);

            if (marketData == null)
            {
                var lastMarketData =
                    await GetLatestTradePairMarketDataIndexFromGrainAsync(chainId, tradePairId, snapshotTime);
                BigDecimal totalSupply = 0;
                if (lastMarketData != null)
                {
                    totalSupply += BigDecimal.Parse(lastMarketData.TotalSupply);
                }

                marketData = new Index.TradePairMarketDataSnapshot()
                {
                    Id = Guid.NewGuid(),
                    ChainId = chainId,
                    TradePairId = tradePairId,
                    Volume = volume,
                    TradeValue = tradeValue,
                    TradeCount = tradeCount,
                    TradeAddressCount24h = tradeAddressCount24H,
                    Timestamp = snapshotTime,
                    TotalSupply = totalSupply.ToNormalizeString()
                };
                if (lastMarketData != null)
                {
                    marketData.Price = lastMarketData.Price;
                    marketData.PriceUSD = lastMarketData.PriceUSD;
                    marketData.TVL = lastMarketData.TVL;
                    marketData.ValueLocked0 = lastMarketData.ValueLocked0;
                    marketData.ValueLocked1 = lastMarketData.ValueLocked1;
                }

                await grain.AddOrUpdateAsync(marketData);
                await _snapshotIndexRepository.AddAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }
            else
            {
                marketData.Volume += volume;
                marketData.TradeValue += tradeValue;
                marketData.TradeCount += tradeCount;
                marketData.TradeAddressCount24h = tradeAddressCount24H;

                await grain.AddOrUpdateAsync(marketData);
                await _snapshotIndexRepository.UpdateAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }
        }

        public async Task UpdateLiquidityAsync(string chainId, Guid tradePairId, DateTime timestamp, double price,
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
            var marketData = await grain.GetAsync();

            if (marketData == null)
            {
                var lastMarketData =
                    await GetLatestTradePairMarketDataIndexFromGrainAsync(chainId, tradePairId, snapshotTime);
                BigDecimal totalSupply = 0;
                if (lastMarketData != null)
                {
                    totalSupply += BigDecimal.Parse(lastMarketData.TotalSupply);
                }

                marketData = new Index.TradePairMarketDataSnapshot
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
                    TotalSupply = totalSupply.ToNormalizeString()
                };
                marketData.TradeAddressCount24h =
                    await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId, tradePairId,
                        timestamp.AddDays(-1), timestamp);
                _logger.LogInformation("UpdateLiquidityAsync, supply:{supply}", marketData.TotalSupply);

                await grain.AddOrUpdateAsync(marketData);
                await _snapshotIndexRepository.AddAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }
            else
            {
                marketData.Price = price;
                marketData.PriceHigh = Math.Max(marketData.PriceHigh, price);
                marketData.PriceHighUSD = Math.Max(marketData.PriceHighUSD, priceUSD);
                marketData.PriceLow = marketData.PriceLow == 0 ? price : Math.Min(marketData.PriceLow, price);
                marketData.PriceLowUSD =
                    marketData.PriceLowUSD == 0 ? price : Math.Min(marketData.PriceLowUSD, priceUSD);
                marketData.PriceUSD = priceUSD;
                marketData.TVL = tvl;
                marketData.ValueLocked0 = valueLocked0;
                marketData.ValueLocked1 = valueLocked1;
                _logger.LogInformation("UpdateLiquidityAsync, supply:{supply}", marketData.TotalSupply);

                await grain.AddOrUpdateAsync(marketData);
                await _snapshotIndexRepository.UpdateAsync(marketData);
                await AddOrUpdateTradePairIndexAsync(marketData);
            }
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


        public async Task<TradePairMarketDataSnapshot> GetLatestTradePairMarketDataAsync(string chainId,
            Guid tradePairId)
        {
            var result = await GetLatestTradePairMarketDataIndexFromGrainAsync(chainId, tradePairId);
            return _objectMapper.Map<Index.TradePairMarketDataSnapshot, TradePairMarketDataSnapshot>(result);
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

        private async Task AddOrUpdateTradePairIndexAsync(Index.TradePairMarketDataSnapshot snapshotDto)
        {
            _logger.LogInformation("AddOrUpdateTradePairIndex, lp token {id}, totalSupply: {supply}",
                snapshotDto.TradePairId, snapshotDto.TotalSupply);
            var latestSnapshot =
                await GetLatestTradePairMarketDataIndexFromGrainAsync(snapshotDto.ChainId,
                    snapshotDto.TradePairId);

            if (latestSnapshot != null && snapshotDto.Timestamp < latestSnapshot.Timestamp)
            {
                return;
            }

            var snapshots = await GetIndexListFromGrainAsync(snapshotDto.ChainId,
                snapshotDto.TradePairId, snapshotDto.Timestamp.AddDays(-2));
            var volume24h = 0d;
            var tradeValue24h = 0d;
            var tradeCount24h = 0;
            var priceHigh24h = snapshotDto.PriceHigh;
            var priceLow24h = snapshotDto.PriceLow;
            var priceHigh24hUSD = snapshotDto.PriceHighUSD;
            var priceLow24hUSD = snapshotDto.PriceLowUSD;

            var daySnapshot = snapshots.Where(s => s.Timestamp >= snapshotDto.Timestamp.AddDays(-1)).ToList();
            foreach (var snapshot in daySnapshot)
            {
                volume24h += snapshot.Volume;
                tradeValue24h += snapshot.TradeValue;
                tradeCount24h += snapshot.TradeCount;

                if (priceLow24h == 0)
                {
                    priceLow24h = snapshot.PriceLow;
                }

                if (snapshot.PriceLow != 0)
                {
                    priceLow24h = Math.Min(priceLow24h, snapshot.PriceLow);
                }

                if (priceLow24hUSD == 0)
                {
                    priceLow24hUSD = snapshot.PriceLowUSD;
                }

                if (snapshot.PriceLowUSD != 0)
                {
                    priceLow24hUSD = Math.Min(priceLow24hUSD, snapshot.PriceLowUSD);
                }

                priceHigh24hUSD = Math.Max(priceHigh24hUSD, snapshot.PriceHighUSD);
                priceHigh24h = Math.Max(priceHigh24h, snapshot.PriceHigh);
            }

            var lastDaySnapshot = snapshots.Where(s => s.Timestamp < snapshotDto.Timestamp.AddDays(-1))
                .OrderByDescending(s => s.Timestamp).ToList();
            var lastDayVolume24h = lastDaySnapshot.Sum(snapshot => snapshot.Volume);
            var lastDayTvl = 0d;
            var lastDayPriceUSD = 0d;

            if (lastDaySnapshot.Count > 0)
            {
                var snapshot = lastDaySnapshot.First();
                lastDayTvl = snapshot.TVL;
                lastDayPriceUSD = snapshot.PriceUSD;
            }
            else
            {
                var snapshot = GetLatestPriceTradePairMarketDataIndexAsync(snapshotDto.ChainId, snapshotDto.TradePairId,
                    snapshotDto.Timestamp);
                if (snapshot != null && snapshot.Result != null)
                {
                    lastDayTvl = snapshot.Result.TVL;
                    lastDayPriceUSD = snapshot.Result.PriceUSD;
                }
            }

            var grain = _clusterClient.GetGrain<ITradePairSyncGrain>(
                GrainIdHelper.GenerateGrainId(snapshotDto.TradePairId));
            var existIndex = await grain.GetAsync();

            existIndex.TotalSupply = snapshotDto.TotalSupply;
            existIndex.Price = snapshotDto.Price;
            existIndex.PriceUSD = snapshotDto.PriceUSD;
            existIndex.TVL = snapshotDto.TVL;
            existIndex.ValueLocked0 = snapshotDto.ValueLocked0;
            existIndex.ValueLocked1 = snapshotDto.ValueLocked1;
            existIndex.Volume24h = volume24h;
            existIndex.TradeValue24h = tradeValue24h;
            existIndex.TradeCount24h = tradeCount24h;
            existIndex.TradeAddressCount24h = snapshotDto.TradeAddressCount24h;
            existIndex.PriceHigh24h = priceHigh24h;
            existIndex.PriceLow24h = priceLow24h;
            existIndex.PriceHigh24hUSD = priceHigh24hUSD;
            existIndex.PriceLow24hUSD = priceLow24hUSD;
            existIndex.PriceChange24h = lastDayPriceUSD == 0
                ? 0
                : existIndex.PriceUSD - lastDayPriceUSD;
            existIndex.PricePercentChange24h = lastDayPriceUSD == 0
                ? 0
                : (existIndex.PriceUSD - lastDayPriceUSD) * 100 / lastDayPriceUSD;
            existIndex.VolumePercentChange24h = lastDayVolume24h == 0
                ? 0
                : (existIndex.Volume24h - lastDayVolume24h) * 100 / lastDayVolume24h;
            existIndex.TVLPercentChange24h = lastDayTvl == 0
                ? 0
                : (existIndex.TVL - lastDayTvl) * 100 / lastDayTvl;

            if (snapshotDto.TVL != 0)
            {
                var volume7d = (await GetIndexListFromGrainAsync(snapshotDto.ChainId,
                        snapshotDto.TradePairId, snapshotDto.Timestamp.AddDays(-7), snapshotDto.Timestamp))
                    .Sum(k => k.Volume);
                volume7d += snapshotDto.Volume;
                existIndex.FeePercent7d = (volume7d * snapshotDto.PriceUSD * existIndex.FeeRate * 365 * 100) /
                                          (snapshotDto.TVL * 7);
            }


            _logger.LogInformation("AddOrUpdateTradePairIndex: " + JsonConvert.SerializeObject(existIndex));

            grain.AddOrUpdateAsync(existIndex);
            await _tradePairIndexRepository.AddOrUpdateAsync(existIndex);
            await _bus.Publish(new NewIndexEvent<TradePairIndexDto>
            {
                Data = _objectMapper.Map<Index.TradePair, TradePairIndexDto>(existIndex)
            });
        }


        public async Task<ITradePairSnapshotGrain> GetSnapShotGrain(string chainId, Guid tradePairId,
            DateTime snapshotTime)
        {
            _tradePairToGrainIds.TryAdd(genPartOfTradePairGrainId(chainId, tradePairId),
                new HashSet<Tuple<string, DateTime>>());
            _tradePairToGrainIds[genPartOfTradePairGrainId(chainId, tradePairId)]
                .Add(new Tuple<string, DateTime>(genTradePairGrainId(chainId, tradePairId, snapshotTime),
                    snapshotTime));
            return _clusterClient.GetGrain<ITradePairSnapshotGrain>(
                GrainIdHelper.GenerateGrainId(chainId, tradePairId, snapshotTime));
        }

        private async Task<Index.TradePairMarketDataSnapshot> GetLatestTradePairMarketDataIndexFromGrainAsync(
            string chainId,
            Guid tradePairId, DateTime maxTime)
        {
            if (!_tradePairToGrainIds.ContainsKey(genPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return null;
            }
            var grainList = _tradePairToGrainIds[genPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());
            foreach (var grainId in grainList)
            {
                if (maxTime >= grainId.Item2)
                {
                    var grain = _clusterClient.GetGrain<ITradePairSnapshotGrain>(grainId.Item1);
                    var marketData = await grain.GetAsync();
                    return marketData;
                }
            }

            return null;
        }

        public async Task<Index.TradePairMarketDataSnapshot> GetLatestTradePairMarketDataIndexFromGrainAsync(
            string chainId,
            Guid tradePairId)
        {
            if (!_tradePairToGrainIds.ContainsKey(genPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return null;
            }

            var grainList = _tradePairToGrainIds[genPartOfTradePairGrainId(chainId, tradePairId)].ToList();
            grainList.Sort(new StringDateTimeDescendingComparer());
            
            if (grainList.IsNullOrEmpty())
            {
                return null;
            }

            var grain = _clusterClient.GetGrain<ITradePairSnapshotGrain>(grainList.First().Item1);
            return await grain.GetAsync();
        }

        public async Task<List<Index.TradePairMarketDataSnapshot>> GetIndexListFromGrainAsync(string chainId,
            Guid tradePairId,
            DateTime? timestampMin = null, DateTime? timestampMax = null)
        {
            List<Index.TradePairMarketDataSnapshot> resultList = new List<Index.TradePairMarketDataSnapshot>();
            if (!_tradePairToGrainIds.ContainsKey(genPartOfTradePairGrainId(chainId, tradePairId)))
            {
                return resultList;
            }
            var grainList = _tradePairToGrainIds[genPartOfTradePairGrainId(chainId, tradePairId)].ToList();
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

                var grain = _clusterClient.GetGrain<ITradePairSnapshotGrain>(grainId.Item1);
                var marketData = await grain.GetAsync();
                resultList.Add(marketData);
            }

            return resultList;
        }

        public class CacheKeys
        {
            HashSet<string> Set { get; set; }
        }
    }
}