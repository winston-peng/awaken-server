using System;
using System.Threading.Tasks;
using AElf.Client.MultiToken;
using AwakenServer.Chains;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AwakenServer.Trade.Handlers
{
    public class NewLiquidityHandler : ILocalEventHandler<NewLiquidityRecordEvent>, ITransientDependency
    {
        private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
        private readonly ILogger<NewLiquidityHandler> _logger;

        public NewLiquidityHandler(ITradePairMarketDataProvider tradePairMarketDataProvider,
            ILogger<NewLiquidityHandler> logger)
        {
            _tradePairMarketDataProvider = tradePairMarketDataProvider;

            _logger = logger;
        }

        public async Task HandleEventAsync(NewLiquidityRecordEvent eventData)
        {
            var lpAmount = BigDecimal.Parse(eventData.LpTokenAmount);
            lpAmount = eventData.Type == LiquidityType.Mint ? lpAmount : -lpAmount;

            await _tradePairMarketDataProvider.UpdateTotalSupplyWithLiquidityEventAsync(eventData.ChainId, eventData.TradePairId,
                eventData.Timestamp, lpAmount);
        }
    }
}