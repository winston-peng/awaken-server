using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using AElf.Client.MultiToken;
using AwakenServer.Chains;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Trade.Handlers
{
    public class NewLiquidityHandler : ILocalEventHandler<NewLiquidityRecordEvent>, ITransientDependency
    {
        private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
        private readonly ITradePairAppService _tradePairAppService;
        private readonly ILogger<NewLiquidityHandler> _logger;
        private readonly IObjectMapper _objectMapper;

        public NewLiquidityHandler(ITradePairMarketDataProvider tradePairMarketDataProvider,
            ITradePairAppService tradePairAppService,
            ILogger<NewLiquidityHandler> logger,
            IObjectMapper objectMapper)
        {
            _tradePairMarketDataProvider = tradePairMarketDataProvider;
            _tradePairAppService = tradePairAppService;
            _objectMapper = objectMapper;
            _logger = logger;
        }

        public async Task HandleEventAsync(NewLiquidityRecordEvent eventData)
        {
            var dto = _objectMapper.Map<NewLiquidityRecordEvent, LiquidityRecordGrainDto>(eventData);
            await _tradePairMarketDataProvider.AddOrUpdateSnapshotAsync(eventData.TradePairId, async grain =>
            {
                return await grain.UpdateTotalSupplyAsync(dto);
            });
        }
    }
}