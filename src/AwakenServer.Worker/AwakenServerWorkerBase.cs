using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Common;
using AwakenServer.Provider;
using Microsoft.Extensions.Logging;


namespace AwakenServer.Worker;

public abstract class AwakenServerWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    protected abstract WorkerBusinessType BusinessType { get; }
    protected bool OpenSwitch { get; set; }
    protected bool ResetBlockHeightFlag { get; set; }
    protected long ResetBlockHeight { get; set; }
    protected long QueryStartBlockHeightOffset { get; set; }
    
    protected readonly ILogger<AwakenServerWorkerBase> _logger;
    
    protected readonly IChainAppService _chainAppService;
    protected readonly IGraphQLProvider _graphQlProvider;
    

    protected AwakenServerWorkerBase(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory, 
        IOptionsMonitor<WorkerOptions> optionsMonitor,
        IGraphQLProvider graphQlProvider,
        IChainAppService chainAppService,
        ILogger<AwakenServerWorkerBase> logger) : base(timer, serviceScopeFactory)
    {
        _logger = logger;
        _chainAppService = chainAppService;
        _graphQlProvider = graphQlProvider;
        
        timer.Period = optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType).TimePeriod;
        
        OpenSwitch = optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType) != null ?
            optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType).OpenSwitch : true;
        
        ResetBlockHeightFlag = optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType) != null ?
            optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType).ResetBlockHeightFlag : false;
        
        ResetBlockHeight = optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType) != null ?
            optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType).ResetBlockHeight : 0;
        
        QueryStartBlockHeightOffset = optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType) != null ?
            optionsMonitor.CurrentValue.GetWorkerSettings(BusinessType).QueryStartBlockHeightOffset : -1;
        
        _logger.LogInformation($"AwakenServerWorkerBase: BusinessType: {BusinessType.ToString()}," +
                               $"Start with config: " +
                               $"TimePeriod: {timer.Period}, " +
                               $"ResetBlockHeightFlag: {ResetBlockHeightFlag}, " +
                               $"ResetBlockHeight:{ResetBlockHeight}," +
                               $"OpenSwitch: {OpenSwitch}," +
                               $"QueryStartBlockHeightOffset: {QueryStartBlockHeightOffset}");
        
        if (!OpenSwitch)
        {
            timer.Stop();
        }
        
        //to change timer Period if the WorkerOptions has changed.
        optionsMonitor.OnChange((newOptions, _) =>
        {
            var workerSetting = newOptions.GetWorkerSettings(BusinessType);
            
            timer.Period = workerSetting.TimePeriod;
            ResetBlockHeightFlag = workerSetting.ResetBlockHeightFlag;
            ResetBlockHeight = workerSetting.ResetBlockHeight;
            QueryStartBlockHeightOffset = workerSetting.QueryStartBlockHeightOffset;
            OpenSwitch = workerSetting.OpenSwitch;
            
            if (OpenSwitch)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }

            _logger.LogInformation(
                "The workerSetting of Worker {BusinessType} has changed to Period = {Period} ms, OpenSwitch = {OpenSwitch}.",
                BusinessType, timer.Period, workerSetting.OpenSwitch);
        });
    }
    
    public abstract Task<long> SyncDataAsync(ChainDto chain, long startHeight, long newIndexHeight);
    
    public async Task DealDataAsync()
    {
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            try
            {
                if (ResetBlockHeightFlag)
                {
                    await _graphQlProvider.SetLastEndHeightAsync(chain.Name, BusinessType, ResetBlockHeight);
                }
                
                var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, BusinessType);
                var newIndexHeight = await _graphQlProvider.GetIndexBlockHeightAsync(chain.Name);
                
                _logger.LogInformation(
                    $"Start deal data for businessType: {BusinessType} " +
                    $"chainId: {chain.Name}, " +
                    $"lastEndHeight: {lastEndHeight}, " +
                    $"newIndexHeight: {newIndexHeight}, " +
                    $"ResetBlockHeightFlag: {ResetBlockHeightFlag}, " +
                    $"ResetBlockHeight: {ResetBlockHeight}, " +
                    $"QueryStartBlockHeightOffset: {QueryStartBlockHeightOffset}");
                
                var blockHeight = await SyncDataAsync(chain, lastEndHeight + QueryStartBlockHeightOffset, newIndexHeight);

                if (blockHeight > 0)
                {
                    await _graphQlProvider.SetLastEndHeightAsync(chain.Name, BusinessType, blockHeight);
                }
                
                _logger.LogInformation(
                    "End deal data for businessType: {businessType} chainId: {chainId} lastEndHeight: {BlockHeight}",
                    BusinessType, chain.Name, blockHeight);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "DealDataAsync error businessType:{businessType} chainId: {chainId}",
                    BusinessType.ToString(), chain.Name);
            }
        }
    }
}