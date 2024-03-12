using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade.Index;
using Nest;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public interface ISnapshotIndexGrain : IGrainWithStringKey
{
    public Task AddAsync(TradePairMarketDataSnapshot snapshot);
    
    public Task UpdateAsync(TradePairMarketDataSnapshot snapshot);

    public Task<TradePairMarketDataSnapshot> getAsync();
}