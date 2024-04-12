using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class TradeRecordRevertWorker : AwakenServerWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly ILogger<TradeRecordRevertWorker> _logger;
    private readonly TradeRecordRevertWorkerSettings _workerSetting;

    public TradeRecordRevertWorker(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService,
        ITradeRecordAppService tradeRecordAppService, 
        ILogger<TradeRecordRevertWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory, workerSettings.Value.TradeRecordRevert)
    {
        _chainAppService = chainAppService;
        _tradeRecordAppService = tradeRecordAppService;
        _logger = logger;
        _workerSetting = workerSettings.Value.TradeRecordRevert;
        timer.Period = _workerSetting.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        PreDoWork(workerContext);
        
        _logger.LogInformation($"TradeRecordRevertWorker.DoWorkAsync Start with config: " +
                               $"TimePeriod: {_workerSetting.TimePeriod}, " +
                               $"RetryLimit: {_workerSetting.RetryLimit}, " +
                               $"QueryOnceLimit: {_workerSetting.QueryOnceLimit}, " +
                               $"BlockHeightLimit: {_workerSetting.BlockHeightLimit}");
        
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            _logger.LogInformation("revert start, {chainName}", chain.Name);
            await _tradeRecordAppService.RevertAsync(chain.Name);
        }
    }
}