using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Nethereum.Util;
using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradePair;

public interface ITradePairGrain : IGrainWithStringKey
{
    
    public Task<GrainResultDto<TradePairGrainDto>> GetAsync();
    
    public Task<GrainResultDto<TradePairGrainDto>> AddAsync(TradePairGrainDto dto);
    
    public Task<GrainResultDto<TradePairGrainDto>> UpdateAsync(DateTime timestamp, int userTradeAddressCount);
    
    public Task<GrainResultDto<TradePairGrainDto>> UpdateFromSnapshotAsync(TradePairMarketDataSnapshotGrainDto dto);
    
    public Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateTotalSupplyWithLiquidityEventAsync(TradePairMarketDataSnapshotGrainDto snapshotDto,
        string lpTokenCurrentSupply,
        int userTradeAddressCount,
        BigDecimal lpTokenAmount);
    
    public Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateTradeRecordAsync(TradePairMarketDataSnapshotGrainDto snapshotDto);
    
    public Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateLiquidityWithSyncEventAsync(TradePairMarketDataSnapshotGrainDto snapshotDto,
        int userTradeAddressCount);

    public Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> AddSnapshotAsync(TradePairMarketDataSnapshotGrainDto snapshotDto);
    
    public Task<TradePairMarketDataSnapshotGrainDto> GetLatestSnapshotAsync();


}