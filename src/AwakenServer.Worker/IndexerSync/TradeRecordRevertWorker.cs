using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class TradeRecordRevertWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly ILogger<TradeRecordRevertWorker> _logger;
    private readonly TradeRecordRevertSettings _setting;

    public TradeRecordRevertWorker(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService,
        ITradeRecordAppService tradeRecordAppService, 
        ILogger<TradeRecordRevertWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory)
    {
        _chainAppService = chainAppService;
        _tradeRecordAppService = tradeRecordAppService;
        _logger = logger;
        _setting = workerSettings.Value.TradeRecordRevert;
        timer.Period = _setting.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // var chains = await _chainAppService.GetListAsync(new GetChainInput());
        // foreach (var chain in chains.Items)
        // {
        //     _logger.LogInformation("revert start, {chainName}", chain.Name);
        //     await _tradeRecordAppService.RevertAsync(chain.Name);
        // }
    }
}