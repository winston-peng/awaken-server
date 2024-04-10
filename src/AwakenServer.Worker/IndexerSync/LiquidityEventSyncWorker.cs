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
public class LiquidityEventSyncWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ILiquidityAppService _liquidityService;
    private readonly ILogger<LiquidityEventSyncWorker> _logger;
    private readonly LiquiditySettings _setting;

    public LiquidityEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider iGraphQlProvider, ILiquidityAppService liquidityService,
        ILogger<LiquidityEventSyncWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _liquidityService = liquidityService;
        _logger = logger;
        _setting = workerSettings.Value.LiquidityEvent;
        timer.Period = _setting.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            if (_setting.ResetBlockHeightFlag)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.Liquidity,
                    _setting.ResetBlockHeight);
                _logger.LogInformation($"liquidity event sync, reset block height: {_setting.ResetBlockHeight}");
            }

            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.Liquidity);

            var queryList = await _graphQlProvider.GetLiquidRecordsAsync(chain.Name, lastEndHeight + 1, 0);
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