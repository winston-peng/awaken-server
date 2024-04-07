using System;
using System.Threading.Tasks;
using AwakenServer.Grains.Grain.Price.TradePair;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AwakenServer.Trade.Handlers
{
    public class NewTradeRecordHandler : ILocalEventHandler<NewTradeRecordEvent>, ITransientDependency
    {
        private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
        private readonly ITradeRecordAppService _tradeRecordAppService;

        public NewTradeRecordHandler(ITradePairMarketDataProvider tradePairMarketDataProvider,
            ITradeRecordAppService tradeRecordAppService, IClusterClient clusterClient)
        {
            _tradePairMarketDataProvider = tradePairMarketDataProvider;
            _tradeRecordAppService = tradeRecordAppService;
        }

        public async Task HandleEventAsync(NewTradeRecordEvent eventData)
        {

            await _tradePairMarketDataProvider.AddOrUpdateSnapshotAsync(new TradePairMarketDataSnapshotGrainDto
            {
                Id = Guid.NewGuid(),
                ChainId = eventData.ChainId,
                TradePairId = eventData.TradePairId,
                Volume = double.Parse(eventData.Token0Amount),
                TradeValue = double.Parse(eventData.Token1Amount),
                TradeCount = 1,
                Timestamp = _tradePairMarketDataProvider.GetSnapshotTime(eventData.Timestamp),
            });
        }
    }
}