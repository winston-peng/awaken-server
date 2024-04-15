using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Provider;


namespace AwakenServer.Worker;

public abstract class AwakenServerWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    protected readonly IChainAppService _chainAppService;
    protected readonly IGraphQLProvider _graphQlProvider;
    private readonly WorkerSettingBase _workerSetting;
    private bool _hasInitialized { get; set; }
    
    protected AwakenServerWorkerBase(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory, 
        WorkerSettingBase workerSetting,
        IGraphQLProvider graphQlProvider,
        IChainAppService chainAppService) : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = graphQlProvider;
        _chainAppService = chainAppService;
        timer.Period = workerSetting.TimePeriod;
        _workerSetting = workerSetting;
    }
    
    protected async Task PreDoWork(PeriodicBackgroundWorkerContext workerContext, bool resetBlockHeightFlag, string queryType)
    {
        if (!_workerSetting.Open)
        {
            await StopAsync();
        }

        if (!_hasInitialized)
        {
            _hasInitialized = true;
            if (resetBlockHeightFlag)
            {
                var chains = AsyncHelper.RunSync(async () => await _chainAppService.GetListAsync(new GetChainInput()));
                foreach (var chain in chains.Items)
                {
                    AsyncHelper.RunSync(async () =>
                        await _graphQlProvider.SetLastEndHeightAsync(chain.Name, queryType, _workerSetting.ResetBlockHeight));
                }
            }
        }
    }
    
    protected async Task PreDoWork(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_workerSetting.Open)
        {
            await StopAsync();
        }
    }
}