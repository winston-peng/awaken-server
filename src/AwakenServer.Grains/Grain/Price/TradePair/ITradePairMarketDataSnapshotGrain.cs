using Nethereum.Util;
using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradePair;

public interface ITradePairMarketDataSnapshotGrain : IGrainWithStringKey
{
    Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> GetAsync();
    Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> AddAsync(TradePairMarketDataSnapshotGrainDto dto);
    
    Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> UpdateAsync(
        TradePairMarketDataSnapshotGrainDto dto,
        TradePairMarketDataSnapshotGrainDto latestBeforeDto);
    
}