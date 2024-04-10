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

public class SyncEventSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ITradePairAppService _tradePairAppService;
    private readonly ILogger<SyncEventSyncWorker> _logger;
    private readonly SyncSettings _setting;

    public SyncEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider iGraphQlProvider,
        ITradePairAppService tradePairAppService, ILogger<SyncEventSyncWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _tradePairAppService = tradePairAppService;
        _logger = logger;
        _setting = workerSettings.Value.SyncEvent;
        timer.Period = _setting.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            if (_setting.ResetBlockHeightFlag)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Sync, _setting.ResetBlockHeight);
                _logger.LogInformation($"sync reset block height: {_setting.ResetBlockHeight}");
            }
            
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Sync);

            var queryList = await _graphQlProvider.GetSyncRecordsAsync(chain.Name, lastEndHeight + 1, 0);
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