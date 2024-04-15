using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Provider;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker
{
    public class TradePairUpdateWorker : AwakenServerWorkerBase
    {
        private readonly ITradePairAppService _tradePairAppService;
        private readonly TradePairUpdateWorkerSettings _workerSetting;
        private readonly ILogger<TradePairUpdateWorker> _logger;
        
        public TradePairUpdateWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
            ITradePairAppService tradePairAppService, IChainAppService chainAppService,
            IGraphQLProvider graphQlProvider,
            IOptionsSnapshot<WorkerSettings> workerSettings,
            ILogger<TradePairUpdateWorker> logger)
            : base(timer, serviceScopeFactory, workerSettings.Value.TradePairUpdate, graphQlProvider, chainAppService)
        {
            _tradePairAppService = tradePairAppService;
            _logger = logger;
            _workerSetting = workerSettings.Value.TradePairUpdate;
            timer.Period = _workerSetting.TimePeriod;
        }

        protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
        {
            PreDoWork(workerContext);
            
            _logger.LogInformation($"TradePairUpdateWorker.DoWorkAsync Start with config: " +
                                   $"TimePeriod: {_workerSetting.TimePeriod}");
            
            var chains = await _chainAppService.GetListAsync(new GetChainInput());
            foreach (var chain in chains.Items)
            {
                var pairs = await _tradePairAppService.GetListAsync(new GetTradePairsInput
                {
                    ChainId = chain.Name,
                    MaxResultCount = 1000
                });
                foreach (var pair in pairs.Items)
                {
                    await _tradePairAppService.UpdateTradePairAsync(pair.Id);
                }
            }
        }
    }
}
