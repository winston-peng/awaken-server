using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.CMS;
using AwakenServer.Provider;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class SwapEventSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly ILogger<SwapEventSyncWorker> _logger;

    public SwapEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider iGraphQlProvider, 
        ITradeRecordAppService tradeRecordAppService, ILogger<SwapEventSyncWorker> logger)
        : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _tradeRecordAppService = tradeRecordAppService;
        _logger = logger;
        timer.Period = WorkerOptions.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        
        _logger.LogInformation("swap TradeRecordEventSwapWorker start");
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Swap);
            _logger.LogInformation("swap first lastEndHeight: {lastEndHeight}", lastEndHeight);
            
            // if(lastEndHeight < 0) continue;
            var queryList = await _graphQlProvider.GetSwapRecordsAsync(chain.Name, lastEndHeight+1, lastEndHeight + WorkerOptions.QueryBlockHeightLimit);

            _logger.LogInformation("swap queryList count: {count}", queryList.Count);
            
            long blockHeight = -1;
            foreach (var queryDto in queryList)
            {
                if(!await _tradeRecordAppService.CreateAsync(queryDto)) continue;
                blockHeight = Math.Max(blockHeight, queryDto.BlockHeight);
            }

            if (blockHeight > 0)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Swap, blockHeight);
                _logger.LogInformation("swap success lastEndHeight: {BlockHeight}", blockHeight);
            }
            
        }
    }
}