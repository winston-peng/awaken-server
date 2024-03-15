using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade.Index;
using Nest;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public interface ITradePairSnapshotGrain : IGrainWithStringKey
{
    public Task AddOrUpdateAsync(TradePairMarketDataSnapshot snapshot);

    public Task<TradePairMarketDataSnapshot> GetAsync();
}