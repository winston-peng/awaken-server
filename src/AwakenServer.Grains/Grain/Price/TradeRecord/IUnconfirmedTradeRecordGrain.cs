using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public interface IUnconfirmedTradeRecordGrain : IGrainWithStringKey
{
    Task<GrainResultDto<UnconfirmedTradeRecordGrainDto>> AddAsync(UnconfirmedTradeRecordGrainDto dto);
    Task<GrainResultDto<List<UnconfirmedTradeRecordGrainDto>>> GetAsync(long confirmedHeight);
}
