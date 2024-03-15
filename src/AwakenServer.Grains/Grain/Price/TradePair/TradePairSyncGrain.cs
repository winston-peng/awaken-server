using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade;
using Orleans;
using TradePair = AwakenServer.Trade.Index.TradePair;

public class TradePairSyncGrain : Grain<TradePairSyncState>, ITradePairSyncGrain
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
    
    public async Task AddOrUpdateAsync(TradePair tradePair)
    {
        State.TradePair = tradePair;
        await WriteStateAsync();
    }

    public async Task AddOrUpdateInfoAsync(TradePairInfoIndex infoIndex)
    {
        State.InfoIndex = infoIndex;
        await WriteStateAsync();
    }


    public async Task<TradePair> GetAsync()
    {
        if (State.TradePair == null)
        {
            return null;
        }
        return State.TradePair;
    }
    
}