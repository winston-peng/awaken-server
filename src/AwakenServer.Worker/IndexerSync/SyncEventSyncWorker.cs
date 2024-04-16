using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.CMS;
using AwakenServer.Provider;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class SyncEventSyncWorker : AwakenServerWorkerBase
{
    private readonly ITradePairAppService _tradePairAppService;
    private readonly ILogger<SyncEventSyncWorker> _logger;
    private readonly SyncWorkerSettings _workerSetting;

    public SyncEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider graphQlProvider,
        ITradePairAppService tradePairAppService, ILogger<SyncEventSyncWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory, workerSettings.Value.SyncEvent, graphQlProvider, chainAppService)
    {

        _tradePairAppService = tradePairAppService;
        _logger = logger;
        _workerSetting = workerSettings.Value.SyncEvent;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        PreDoWork(workerContext, _workerSetting.ResetBlockHeightFlag, QueryType.Sync);
        
        _logger.LogInformation($"SyncEventSyncWorker.DoWorkAsync Start with config: " +
                               $"TimePeriod: {_workerSetting.TimePeriod}, " +
                               $"ResetBlockHeightFlag: {_workerSetting.ResetBlockHeightFlag}, " +
                               $"ResetBlockHeight:{_workerSetting.ResetBlockHeight}");
        
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Sync);

            var queryList = await _graphQlProvider.GetSyncRecordsAsync(chain.Name, lastEndHeight + _workerSetting.QueryStartBlockHeightOffset, 0);
            _logger.LogInformation("sync queryList count: {count} ,chainId:{chainId}", queryList.Count, chain.Name);
            try
            {
                foreach (var queryDto in queryList)
                {
                    await _tradePairAppService.UpdateLiquidityAsync(queryDto);
                    await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Sync, queryDto.BlockHeight);
                }
                
                _logger.LogInformation($"sync lastEndHeight: {await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Sync)}, chainId:{chain.Name}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "sync event fail.");
            }
        }
    }
}