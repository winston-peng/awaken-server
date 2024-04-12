using System;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.CMS;
using AwakenServer.Provider;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker.IndexerSync;

public class TradePairEventSyncWorker : AwakenServerWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ILogger<TradePairEventSyncWorker> _logger;
    private readonly ITradePairAppService _tradePairAppService;
    private readonly TradePairWorkerSettings _workerSetting;
    
    public TradePairEventSyncWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IGraphQLProvider iGraphQlProvider, IChainAppService chainAppService,
        ITradePairAppService tradePairAppService, ILogger<TradePairEventSyncWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory, workerSettings.Value.TradePairEvent)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _logger = logger;
        _tradePairAppService = tradePairAppService;
        _workerSetting = workerSettings.Value.TradePairEvent;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        PreDoWork(workerContext);
        
        _logger.LogInformation($"TradePairEventSyncWorker.DoWorkAsync Start with config: " +
                               $"TimePeriod: {_workerSetting.TimePeriod}, " +
                               $"ResetBlockHeightFlag: {_workerSetting.ResetBlockHeightFlag}, " +
                               $"ResetBlockHeight:{_workerSetting.ResetBlockHeight}");
        
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            if (_workerSetting.ResetBlockHeightFlag)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.TradePair, _workerSetting.ResetBlockHeight);
                _logger.LogInformation($"trade reset block height: {_workerSetting.ResetBlockHeight}");
            }
            
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.TradePair);
            _logger.LogInformation("trade first lastEndHeight: {lastEndHeight}", lastEndHeight);
            
            var result = await _graphQlProvider.GetTradePairInfoListAsync(new GetTradePairsInfoInput
            {
                ChainId = chain.Name,
                StartBlockHeight = lastEndHeight + _workerSetting.QueryStartBlockHeightOffset,
                EndBlockHeight = 0
            });

            long blockHeight = -1;
            foreach (var pair in result.TradePairInfoDtoList.Data)
            {
                blockHeight = Math.Max(blockHeight, pair.BlockHeight);

                _logger.LogInformation("Syncing {pairId} on {chainName}, {Token0Symbol}/{Token1Symbol}",
                    pair.Id, chain.Name, pair.Token0Symbol, pair.Token1Symbol);

                await _tradePairAppService.SyncTokenAsync(pair.ChainId, pair.Token0Symbol, chain);
                await _tradePairAppService.SyncTokenAsync(pair.ChainId, pair.Token1Symbol, chain);
                await _tradePairAppService.SyncPairAsync(pair, chain);
                
                _logger.LogInformation("Syncing {pairId} on {chainName}, {Token0Symbol}/{Token1Symbol} done",
                    pair.Id, chain.Name, pair.Token0Symbol, pair.Token1Symbol);
            }

            if (blockHeight > 0)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.TradePair, blockHeight);
            }
            _logger.LogInformation($"TradePair lastEndHeight: {blockHeight},:chainId:{chain.Name}");
        }
    }
}