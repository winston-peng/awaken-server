using AwakenServer.Trade;
using Orleans;
using TradePair = AwakenServer.Trade.Index.TradePair;

namespace AwakenServer.Grains.Grain.Trade;

public interface ITradePairSyncGrain : IGrainWithStringKey
{
    public Task AddOrUpdateAsync(TradePair tradePair);
    
    public Task AddOrUpdateInfoAsync(TradePairInfoIndex infoIndex);

    public Task<TradePair> GetAsync();
}