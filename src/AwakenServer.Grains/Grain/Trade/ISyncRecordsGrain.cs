using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public interface ISyncRecordsGrain : IGrainWithStringKey
{
    Task AddSyncRecordAsync(string syncRecord);
    
    Task<bool> ExistAsync(string syncRecord);
}

