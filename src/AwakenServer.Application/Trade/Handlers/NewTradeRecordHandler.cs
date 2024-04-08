using System;
using System.Threading.Tasks;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Price.TradeRecord;
using AwakenServer.Trade.Dtos;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Trade.Handlers
{
    public class NewTradeRecordHandler : ILocalEventHandler<NewTradeRecordEvent>, ITransientDependency
    {
        private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
        private readonly ITradeRecordAppService _tradeRecordAppService;
        private readonly IObjectMapper _objectMapper;


        public NewTradeRecordHandler(ITradePairMarketDataProvider tradePairMarketDataProvider,
            ITradeRecordAppService tradeRecordAppService, IClusterClient clusterClient,
            IObjectMapper objectMapper)
        {
            _tradePairMarketDataProvider = tradePairMarketDataProvider;
            _tradeRecordAppService = tradeRecordAppService;
            _objectMapper = objectMapper;
        }

        public async Task HandleEventAsync(NewTradeRecordEvent eventData)
        {
            var dto = _objectMapper.Map<NewTradeRecordEvent, TradeRecordGrainDto>(eventData);
            await _tradePairMarketDataProvider.AddOrUpdateSnapshotAsync(eventData.TradePairId, async grain =>
            {
                return await grain.UpdateTradeRecordAsync(dto);
            });
        }
    }
}