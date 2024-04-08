using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade.Etos;
using Orleans;
using Volo.Abp.EventBus.Local;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Grains.Grain.Trade;

public class KLineGrain : Grain<KLineState>, IKLineGrain
{
    private readonly IObjectMapper _objectMapper;
    public KLineGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public async Task<GrainResultDto<KLineGrainDto>> AddOrUpdateAsync(KLineGrainDto kLineGrainDto)
    {
        var result = new GrainResultDto<KLineGrainDto>();
        
        if (State.GrainId == null)
        {
            kLineGrainDto.GrainId = this.GetPrimaryKeyString();
            _objectMapper.Map(kLineGrainDto, State);
        }
        else
        {
            if (State.Timestamp == kLineGrainDto.Timestamp)
            {
                State.Close = kLineGrainDto.Close;
                if (State.High < kLineGrainDto.High)
                {
                    State.High = kLineGrainDto.High;
                }
                if (State.Low > kLineGrainDto.Low)
                {
                    State.Low = kLineGrainDto.Low;
                }
                State.Volume += kLineGrainDto.Volume;
            }
            else
            {
                State.Timestamp = kLineGrainDto.Timestamp;
                State.Open = kLineGrainDto.Open;
                State.Close = kLineGrainDto.Close;
                State.High = kLineGrainDto.High;
                State.Low = kLineGrainDto.Low;
                State.Volume = kLineGrainDto.Volume;
            }
        }
        
        await WriteStateAsync();
        result.Success = true;
        result.Data = _objectMapper.Map<KLineState, KLineGrainDto>(State);
        return result;
    }

    public async Task<GrainResultDto<KLineGrainDto>> GetAsync()
    {
        await ReadStateAsync();
        var result = new GrainResultDto<KLineGrainDto>();
        if (State.GrainId == null)
        {
            result.Success = false;
            return result;
        }
        result.Data = _objectMapper.Map<KLineState, KLineGrainDto>(State);
        result.Success = true;
        return result;
    }
}