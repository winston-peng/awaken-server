using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade.Index;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public class TradePairSnapshotGrain : Grain<TradePairSnapshotState>, ITradePairSnapshotGrain
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
    
    public async Task AddOrUpdateAsync(TradePairMarketDataSnapshot snapshot)
    {
        State.Snapshot = snapshot;
        await WriteStateAsync();
    }

    public async Task<TradePairMarketDataSnapshot> GetAsync()
    {
        if (State.Snapshot == null)
        {
            return null;
        }
        
        return State.Snapshot;
    }
    
}