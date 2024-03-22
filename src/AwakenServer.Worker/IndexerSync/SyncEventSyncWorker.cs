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

public class SyncEventSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ITradePairAppService _tradePairAppService;
    private readonly ILogger<SyncEventSyncWorker> _logger;

    public SyncEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider iGraphQlProvider,
        ITradePairAppService tradePairAppService, ILogger<SyncEventSyncWorker> logger)
        : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _tradePairAppService = tradePairAppService;
        _logger = logger;
        timer.Period = WorkerOptions.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Sync);

            var queryList = await _graphQlProvider.GetSyncRecordsAsync(chain.Name, lastEndHeight + 1, lastEndHeight + WorkerOptions.QueryBlockHeightLimit);
            _logger.LogInformation("sync queryList count: {count} ,chainId:{chainId}", queryList.Count, chain.Name);
            try
            {
                long blockHeight = -1;
                foreach (var queryDto in queryList)
                {
                    await _tradePairAppService.UpdateLiquidityAsync(queryDto);
                    blockHeight = Math.Max(blockHeight, queryDto.BlockHeight);
                }

                if (blockHeight > 0)
                {
                    await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Sync, blockHeight);
                    _logger.LogInformation("sync lastEndHeight: {BlockHeight},:chainId:{chainId}", blockHeight,
                        chain.Name);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "sync event fail.");
            }
        }
    }
}