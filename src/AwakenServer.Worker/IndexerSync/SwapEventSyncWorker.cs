using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Chains;
using AwakenServer.Provider;
using AwakenServer.Trade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace AwakenServer.Worker;

public class TradeRecordEventSwapWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly IChainAppService _chainAppService;
    private readonly IGraphQLProvider _graphQlProvider;
    private readonly ITradeRecordAppService _tradeRecordAppService;
    private readonly SwapSettings _setting;
    
    private readonly ILogger<TradeRecordEventSwapWorker> _logger;
    private bool executed = false;

    public TradeRecordEventSwapWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory,
        IChainAppService chainAppService, IGraphQLProvider iGraphQlProvider,
        ITradeRecordAppService tradeRecordAppService, ILogger<TradeRecordEventSwapWorker> logger,
        IOptionsSnapshot<WorkerSettings> workerSettings)
        : base(timer, serviceScopeFactory)
    {
        _graphQlProvider = iGraphQlProvider;
        _chainAppService = chainAppService;
        _tradeRecordAppService = tradeRecordAppService;
        _logger = logger;
        _setting = workerSettings.Value.SwapEvent;
        timer.Period = _setting.TimePeriod;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        if (!executed)
        {
            _logger.LogInformation("FixTrade start");
            await UpdateTransaction();
            executed = true;
        }


        _logger.LogInformation("swap TradeRecordEventSwapWorker start");
        var chains = await _chainAppService.GetListAsync(new GetChainInput());
        foreach (var chain in chains.Items)
        {
            if (_setting.ResetBlockHeightFlag)
            {
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.TradeRecord, _setting.ResetBlockHeight);
                _logger.LogInformation($"swap reset block height: {_setting.ResetBlockHeight}");
            }
            
            var lastEndHeight = await _graphQlProvider.GetLastEndHeightAsync(chain.Name, QueryType.TradeRecord);
            _logger.LogInformation("swap first lastEndHeight: {lastEndHeight}", lastEndHeight);
            
            if (lastEndHeight < 0) continue;
            
            var queryList = await _graphQlProvider.GetSwapRecordsAsync(chain.Name, lastEndHeight + 1, 0);
            _logger.LogInformation("swap queryList count: {count}", queryList.Count);
            
            foreach (var queryDto in queryList)
            {
                if (!await _tradeRecordAppService.CreateAsync(queryDto))
                {
                    continue;
                }
                
                await _graphQlProvider.SetLastEndHeightAsync(chain.Name, QueryType.TradeRecord, queryDto.BlockHeight);
                _logger.LogInformation("swap success lastEndHeight: {BlockHeight}", queryDto.BlockHeight);
            }
        }
    }

    protected async Task UpdateTransaction()
    {
        var endHeight = await _graphQlProvider.GetLastEndHeightAsync("tDVV", QueryType.TradeRecord);
        long curHeight = 189607411;

        for (long i = curHeight; i <= endHeight;)
        {
            var queryList = await _graphQlProvider.GetSwapRecordsAsync("tDVV", i, 0);
            if (queryList.IsNullOrEmpty())
            {
                i++;
            }

            foreach (var queryDto in queryList)
            {
                await _tradeRecordAppService.FillRecord(queryDto);
                i = queryDto.BlockHeight;
            }
        }
    }
}