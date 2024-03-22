using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AwakenServer.Grains;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using AwakenServer.Trade.Etos;
using MassTransit;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EventBus.Distributed;
using TradePairMarketDataSnapshot = AwakenServer.Trade.Index.TradePairMarketDataSnapshot;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using IObjectMapper = Volo.Abp.ObjectMapping.IObjectMapper;
using TradePair = AwakenServer.Trade.Index.TradePair;

namespace AwakenServer.EntityHandler.Trade
{
    public class TradePairMarketDataIndexHandler : TradeIndexHandlerBase,
        IDistributedEventHandler<EntityCreatedEto<TradePairMarketDataSnapshotEto>>,
        IDistributedEventHandler<EntityUpdatedEto<TradePairMarketDataSnapshotEto>>
    {
        private readonly INESTRepository<TradePairMarketDataSnapshot, Guid> _snapshotIndexRepository;
        private readonly INESTRepository<TradePair, Guid> _tradePairIndexRepository;
        private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
        private readonly IBus _bus;
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<TradePairMarketDataIndexHandler> _logger;
        private readonly IObjectMapper _objectMapper;
        
        public TradePairMarketDataIndexHandler(
            INESTRepository<TradePairMarketDataSnapshot, Guid> snapshotIndexRepository,
            INESTRepository<TradePair, Guid> tradePairIndexRepository,
            ITradePairMarketDataProvider tradePairMarketDataProvider,
            IBus bus,
            IClusterClient clusterClient,
            ILogger<TradePairMarketDataIndexHandler> logger,
            IObjectMapper objectMapper)
        {
            _snapshotIndexRepository = snapshotIndexRepository;
            _tradePairIndexRepository = tradePairIndexRepository;
            _tradePairMarketDataProvider = tradePairMarketDataProvider;
            _bus = bus;
            _clusterClient = clusterClient;
            _logger = logger;
            _objectMapper = objectMapper;
        }

        public async Task HandleEventAsync(EntityCreatedEto<TradePairMarketDataSnapshotEto> eventData)
        {
            await AddOrUpdateIndexAsync(eventData.Entity);
            await AddOrUpdateTradePairIndexAsync(eventData.Entity);
        }

        public async Task HandleEventAsync(EntityUpdatedEto<TradePairMarketDataSnapshotEto> eventData)
        {
            await AddOrUpdateIndexAsync(eventData.Entity);
            await AddOrUpdateTradePairIndexAsync(eventData.Entity);
        }

        private async Task AddOrUpdateIndexAsync(TradePairMarketDataSnapshotEto snapshotEto)
        {
            var index = ObjectMapper.Map<TradePairMarketDataSnapshotEto, TradePairMarketDataSnapshot>(snapshotEto);
            await _snapshotIndexRepository.AddOrUpdateAsync(index);
        }

        private async Task AddOrUpdateTradePairIndexAsync(TradePairMarketDataSnapshotEto snapshotEto)
        {
            _logger.LogInformation("AddOrUpdateTradePairIndexAsync, lp token {id}, totalSupply: {supply}",
                snapshotEto.TradePairId, snapshotEto.TotalSupply);
            var latestSnapshot =
                await _tradePairMarketDataProvider.GetLatestTradePairMarketDataIndexFromGrainAsync(snapshotEto.ChainId,
                    snapshotEto.TradePairId);

            if (latestSnapshot != null && snapshotEto.Timestamp < latestSnapshot.Timestamp)
            {
                return;
            }

            var snapshots = await _tradePairMarketDataProvider.GetIndexListFromGrainAsync(snapshotEto.ChainId,
                snapshotEto.TradePairId, snapshotEto.Timestamp.AddDays(-2));
            var tokenAValue24 = 0d;
            var tokenBValue24 = 0d;
            var tradeCount24h = 0;
            var priceHigh24h = snapshotEto.PriceHigh;
            var priceLow24h = snapshotEto.PriceLow;
            var priceHigh24hUSD = snapshotEto.PriceHighUSD;
            var priceLow24hUSD = snapshotEto.PriceLowUSD;

            var daySnapshot = snapshots.Where(s => s.Timestamp >= snapshotEto.Timestamp.AddDays(-1)).ToList();
            foreach (var snapshot in daySnapshot)
            {
                tokenAValue24 += snapshot.Volume;
                tokenBValue24 += snapshot.TradeValue;
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

            var lastDaySnapshot = snapshots.Where(s => s.Timestamp < snapshotEto.Timestamp.AddDays(-1))
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
                var snapshot = _tradePairMarketDataProvider.GetLatestPriceTradePairMarketDataIndexAsync(snapshotEto.ChainId, snapshotEto.TradePairId,
                    snapshotEto.Timestamp);
                if (snapshot != null && snapshot.Result != null)
                {
                    lastDayTvl = snapshot.Result.TVL;
                    lastDayPriceUSD = snapshot.Result.PriceUSD;
                }
            }

            var grain = _clusterClient.GetGrain<ITradePairSyncGrain>(
                GrainIdHelper.GenerateGrainId(snapshotEto.TradePairId));
            var existIndex = await grain.GetAsync();

            existIndex.TotalSupply = snapshotEto.TotalSupply;
            existIndex.Price = snapshotEto.Price;
            existIndex.PriceUSD = snapshotEto.PriceUSD;
            existIndex.TVL = snapshotEto.TVL;
            existIndex.ValueLocked0 = snapshotEto.ValueLocked0;
            existIndex.ValueLocked1 = snapshotEto.ValueLocked1;
            existIndex.Volume24h = tokenAValue24;
            existIndex.TradeValue24h = tokenBValue24;
            existIndex.TradeCount24h = tradeCount24h;
            existIndex.TradeAddressCount24h = snapshotEto.TradeAddressCount24h;
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

            if (snapshotEto.TVL != 0)
            {
                var volume7d = (await _tradePairMarketDataProvider.GetIndexListFromGrainAsync(snapshotEto.ChainId,
                        snapshotEto.TradePairId, snapshotEto.Timestamp.AddDays(-7), snapshotEto.Timestamp))
                    .Sum(k => k.Volume);
                volume7d += snapshotEto.Volume;
                existIndex.FeePercent7d = (volume7d * snapshotEto.PriceUSD * existIndex.FeeRate * 365 * 100) /
                                          (snapshotEto.TVL * 7);
            }
            
            _logger.LogInformation("AddOrUpdateTradePairIndexAsync: " + JsonConvert.SerializeObject(existIndex));

            grain.AddOrUpdateAsync(existIndex);
            await _tradePairIndexRepository.AddOrUpdateAsync(existIndex);
            await _bus.Publish(new NewIndexEvent<TradePairIndexDto>
            {
                Data = _objectMapper.Map<TradePair, TradePairIndexDto>(existIndex)
            });
        }
    }
}