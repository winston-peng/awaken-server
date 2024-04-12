using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using System;
using System.Threading.Tasks;


namespace AwakenServer.Worker;

public abstract class AwakenServerWorkerBase : AsyncPeriodicBackgroundWorkerBase
{
    
    private readonly WorkerSettingBase _workerSetting;
    
    protected AwakenServerWorkerBase(AbpAsyncTimer timer, 
        IServiceScopeFactory serviceScopeFactory, 
        WorkerSettingBase workerSetting) : base(timer, serviceScopeFactory)
    {
        timer.Period = workerSetting.TimePeriod;
        _workerSetting = workerSetting;
    }
    
    protected async Task PreDoWork(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!_workerSetting.Open)
        {
            await StopAsync();
        }
    }
}