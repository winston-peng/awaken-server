using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradePair;

public interface ITradePairGrain : IGrainWithStringKey
{
    public Task<GrainResultDto<TradePairGrainDto>> AddOrUpdateAsync(TradePairGrainDto dto);
    
    public Task<GrainResultDto<TradePairGrainDto>> AddOrUpdateFromTradeAsync(TradePairMarketDataSnapshotGrainDto dto, 
        List<TradePairMarketDataSnapshotGrainDto> previous7DaysSnapshotDtos,
        TradePairMarketDataSnapshotGrainDto latestBeforeThisSnapshotDto);
    
    public Task<GrainResultDto<TradePairGrainDto>> AddOrUpdateFromUpdateAsync(DateTime timestamp, 
        List<TradePairMarketDataSnapshotGrainDto> previous7DaysSnapshotDtos,
        int userTradeAddressCount,
        double priceUSD0,
        double priceUSD1);
    
    public Task<GrainResultDto<TradePairGrainDto>> GetAsync();
}