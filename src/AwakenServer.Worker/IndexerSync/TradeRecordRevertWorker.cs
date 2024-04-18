using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Common;
using AwakenServer.Provider;
using AwakenServer.Trade;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class TradeRecordRevertWorker : AwakenServerWorkerBase
{
    protected override WorkerBusinessType BusinessType => WorkerBusinessType.TradeRecordRevert;
    
    protected readonly IChainAppService _chainAppService;
    protected readonly IGraphQLProvider _graphQlProvider;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly TradeRecordRevertWorkerSettings _workerSetting;

    public TradeRecordRevertWorker(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory,
        ITradeRecordAppService tradeRecordAppService, 
        ILogger<AwakenServerWorkerBase> logger,
        IOptionsMonitor<WorkerOptions> optionsMonitor,
        IGraphQLProvider graphQlProvider,
        IChainAppService chainAppService)
        : base(timer, serviceScopeFactory, optionsMonitor, graphQlProvider, chainAppService, logger)
    {
        _chainAppService = chainAppService;
        _graphQlProvider = graphQlProvider;
        _tradeRecordAppService = tradeRecordAppService;
    }

    public override Task<long> SyncDataAsync(ChainDto chain, long startHeight, long newIndexHeight)
    {
        throw new System.NotImplementedException();
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            _logger.LogInformation("revert start, {chainName}", chain.Name);
            await _tradeRecordAppService.RevertTradeRecordAsync(chain.Name);
        }
    }
}