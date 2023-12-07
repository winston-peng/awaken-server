using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Chains;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;

namespace AwakenServer.Trade;

public class FlushCacheService : ApplicationService, IFlushCacheService
{
    private readonly IChainAppService _chainAppService;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly ILogger<LiquidityAppService> _logger;
    private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;
    private readonly TradeRecordOptions _tradeRecordOptions;

    public FlushCacheService(IChainAppService chainAppService, ITradeRecordAppService tradeRecordAppService,
        ILogger<LiquidityAppService> logger, ITradePairMarketDataProvider tradePairMarketDataProvider,TradeRecordOptions tradeRecordOptions)
    {
        _chainAppService = chainAppService;
        _tradeRecordAppService = tradeRecordAppService;
        _logger = logger;
        _tradePairMarketDataProvider = tradePairMarketDataProvider;
        _tradeRecordOptions = tradeRecordOptions;
    }

    public async Task FlushCache(List<string> cacheKeys)
    {
        foreach (var key in cacheKeys)
        {
             _tradePairMarketDataProvider.FlushTotalSupplyCacheToSnapshotAsync(key);
             _tradePairMarketDataProvider.FlushTradeRecordCacheToSnapshotAsync(key);
        }
        
        
    }
}