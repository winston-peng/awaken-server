using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade.Index;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public class SnapshotIndexGrain : Grain<SnapshotIndexState>, ISnapshotIndexGrain
{
    
    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }
    
    public async Task AddAsync(TradePairMarketDataSnapshot snapshot)
    {
        State.Snapshot = snapshot;
        await WriteStateAsync();
    }

    public async Task UpdateAsync(TradePairMarketDataSnapshot snapshot)
    {
        State.Snapshot = snapshot;
        await WriteStateAsync();
    }

    public async Task<TradePairMarketDataSnapshot> getAsync()
    {
        return State.Snapshot;
    }
    
}