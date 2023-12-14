using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Chains;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;

namespace AwakenServer.Trade;

public class FlushCacheService : ApplicationService, IFlushCacheService
{
    private readonly ILogger<LiquidityAppService> _logger;
    private readonly ITradePairMarketDataProvider _tradePairMarketDataProvider;

    public FlushCacheService(ILogger<LiquidityAppService> logger,
        ITradePairMarketDataProvider tradePairMarketDataProvider)
    {
        _logger = logger;
        _tradePairMarketDataProvider = tradePairMarketDataProvider;
    }

    public async Task FlushCache(List<string> cacheKeys)
    {
        foreach (var key in cacheKeys)
        {
            _logger.LogInformation($"FlushCacheService start.FlushCache key:{key}");
            _tradePairMarketDataProvider.FlushTotalSupplyCacheToSnapshotAsync(key);
            _tradePairMarketDataProvider.FlushTradeRecordCacheToSnapshotAsync(key);
        }
    }
}