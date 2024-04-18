using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Grains.State.Price;
using Microsoft.Extensions.Logging;
using Volo.Abp.Caching;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public class UnconfirmedTradeRecordGrain : Grain<TradeRecordRevertState>, IUnconfirmedTradeRecordGrain
{

    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UnconfirmedTradeRecordGrain> _logger;
    
    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        State.ToBeConfirmRecords ??= new Dictionary<long, List<ToBeConfirmRecord>>();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }

    UnconfirmedTradeRecordGrain(IObjectMapper objectMapper,
        ILogger<UnconfirmedTradeRecordGrain> logger)
    {
        _logger = logger;
        _objectMapper = objectMapper;
    }
    
    public async Task<GrainResultDto<UnconfirmedTradeRecordGrainDto>> AddAsync(UnconfirmedTradeRecordGrainDto dto)
    {
        if (dto.BlockHeight <= State.MinUnconfirmedBlockHeight)
        {
            _logger.LogError("TradeRecordRevertGrain: Adding tradeRecord before the confirmed block.");
            return new GrainResultDto<UnconfirmedTradeRecordGrainDto>
            {
                Success = false
            };
        }
        
        State.ToBeConfirmRecords[dto.BlockHeight].Add(_objectMapper.Map<UnconfirmedTradeRecordGrainDto, ToBeConfirmRecord>(dto));
        
        return new GrainResultDto<UnconfirmedTradeRecordGrainDto>
        {
            Success = true,
            Data = dto
        };
    }

    public async Task<GrainResultDto<List<UnconfirmedTradeRecordGrainDto>>> GetAsync(long confirmedHeight)
    {
        var list = new List<UnconfirmedTradeRecordGrainDto>();
        // [MinUnconfirmedBlockHeight, confirmedHeight]
        for (var i = State.MinUnconfirmedBlockHeight; i <= confirmedHeight; ++i)
        {
            var blockUnconfirmedList = State.ToBeConfirmRecords[i];
            
        }

        return new GrainResultDto<List<UnconfirmedTradeRecordGrainDto>>
        {
            Success = true,
            Data = list
        };
    }

}