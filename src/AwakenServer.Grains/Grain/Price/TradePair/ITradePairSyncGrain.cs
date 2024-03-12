using AwakenServer.Trade;

namespace AwakenServer.Grains.Grain.Trade;

using Orleans;

public interface ITradePairSyncGrain : IGrainWithStringKey
{
    public Task AddOrUpdateAsync(AwakenServer.Trade.Index.TradePair tradePair);
    
    public Task AddOrUpdateAsync(TradePairInfoIndex infoIndex);

    public Task<AwakenServer.Trade.Index.TradePair> GetAsync();
}