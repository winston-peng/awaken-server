using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Common;
using AwakenServer.Provider;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker;

public class MarketSnapshotWorker : AwakenServerWorkerBase
{
    protected override WorkerBusinessType _businessType => WorkerBusinessType.MarketSnapshot;
    
    private readonly IChainAppService _chainAppService;
    private readonly ITradePairAppService _tradePairAppService;
    private readonly IFlushCacheService _flushCacheService;
    private readonly ILogger<MarketSnapshotWorker> _logger;

    public MarketSnapshotWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, ITradePairAppService tradePairAppService,
        IFlushCacheService flushCacheService, ILogger<AwakenServerWorkerBase> logger,
        IOptionsMonitor<WorkerOptions> optionsMonitor,
        IGraphQLProvider graphQlProvider,
        IOptions<ChainsInitOptions> chainsOption) : base(timer, serviceScopeFactory, optionsMonitor, graphQlProvider, chainAppService, logger, chainsOption)
    {
        _chainAppService = chainAppService;
        _tradePairAppService = tradePairAppService;
        _flushCacheService = flushCacheService;
    }
    
    public override Task<long> SyncDataAsync(ChainDto chain, long startHeight, long newIndexHeight)
    {
        throw new System.NotImplementedException();
    }


    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        try
        {
            var chains = await _chainAppService.GetListAsync(new GetChainInput());
            var cacheKeys = new List<String>();
            var nowHourTime = DateTime.UtcNow;
            var frontHourTime = DateTime.UtcNow.AddHours(-1);

            var nowHour = nowHourTime.Date.AddHours(nowHourTime.Hour);
            var frontHour = frontHourTime.Date.AddHours(frontHourTime.Hour);

            foreach (var chain in chains.Items)
            {
                var list = await _tradePairAppService.GetListAsync(new GetTradePairsInput()
                {
                    ChainId = chain.Name,
                    MaxResultCount = 1000
                });
                foreach (var pair in list.Items)
                {
                    cacheKeys.Add(string.Format("{0}-{1}-{2}", chain.Name,
                        pair.Id, nowHour));
                    cacheKeys.Add(string.Format("{0}-{1}-{2}", chain.Name,
                        pair.Id, frontHour));
                }
            }

            await _flushCacheService.FlushCacheAsync(cacheKeys);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "flush cache error");
        }
    }
}