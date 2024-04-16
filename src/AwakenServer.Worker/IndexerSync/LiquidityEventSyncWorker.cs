using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Provider;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

/**
 * sync swap-indexer to awaken-server
 */
public class LiquidityEventSyncWorker : AwakenServerWorkerBase
{
    private readonly ILiquidityAppService _liquidityService;
    private readonly ILogger<LiquidityEventSyncWorker> _logger;
    private readonly LiquidityWorkerSettings _workerSetting;

    public LiquidityEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider graphQlProvider, ILiquidityAppService liquidityService,
        ILogger<LiquidityEventSyncWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory, workerSettings.Value.LiquidityEvent, graphQlProvider, chainAppService)
    {
        _liquidityService = liquidityService;
        _logger = logger;
        _workerSetting = workerSettings.Value.LiquidityEvent;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        PreDoWork(workerContext, _workerSetting.ResetBlockHeightFlag, QueryType.Liquidity);
        
        _logger.LogInformation($"LiquidityEventSyncWorker.DoWorkAsync: Start with config: " +
                               $"TimePeriod: {_workerSetting.TimePeriod}, " +
                               $"ResetBlockHeightFlag: {_workerSetting.ResetBlockHeightFlag}, " +
                               $"ResetBlockHeight:{_workerSetting.ResetBlockHeight}");
        
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Liquidity);

            var queryList = await _graphQlProvider.GetLiquidRecordsAsync(chain.Name, lastEndHeight + _workerSetting.QueryStartBlockHeightOffset, 0);
            _logger.LogInformation(
                "liquidity event sync, queryList count: {count}, lastEndHeight: {lastEndHeight}",
                queryList.Count, lastEndHeight);
            try
            {
                long blockHeight = -1;
                foreach (var queryDto in queryList)
                {
                    await _liquidityService.CreateAsync(queryDto);
                    blockHeight = Math.Max(blockHeight, queryDto.BlockHeight);
                }

                if (blockHeight > 0)
                {
                    await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Liquidity, blockHeight);
                    _logger.LogInformation("liquidity lastEndHeight: {BlockHeight}", blockHeight);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "liquidity event fail.");
            }
        }
    }
}