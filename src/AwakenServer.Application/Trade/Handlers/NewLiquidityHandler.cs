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
        private readonly ITradePairAppService _tradePairAppService;
        private readonly ILogger<NewLiquidityHandler> _logger;

        public NewLiquidityHandler(ITradePairMarketDataProvider tradePairMarketDataProvider,
            ITradePairAppService tradePairAppService,
            ILogger<NewLiquidityHandler> logger)
        {
            _tradePairMarketDataProvider = tradePairMarketDataProvider;
            _tradePairAppService = tradePairAppService;

            _logger = logger;
        }

        public async Task HandleEventAsync(NewLiquidityRecordEvent eventData)
        {
            var lpAmount = BigDecimal.Parse(eventData.LpTokenAmount);
            lpAmount = eventData.Type == LiquidityType.Mint ? lpAmount : -lpAmount;
            
            var tradePairIndexDto = await _tradePairAppService.GetAsync(eventData.TradePairId);

            if (tradePairIndexDto == null)
            {
                _logger.LogError($"NewLiquidityHandler can not find trade pair: {eventData.TradePairId}");
                return;
            }
            
            await _tradePairMarketDataProvider.UpdateTotalSupplyWithLiquidityEventAsync(eventData.ChainId, 
                eventData.TradePairId, 
                tradePairIndexDto.Token0.Symbol, 
                tradePairIndexDto.Token1.Symbol, 
                tradePairIndexDto.FeeRate, 
                eventData.Timestamp, lpAmount);
        }
    }
}