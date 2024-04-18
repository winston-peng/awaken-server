using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Grains.State.Trade;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public class SyncRecordsGrain : Grain<SyncRecordsState>, ISyncRecordsGrain
{
    public async Task AddSyncRecordAsync(string syncRecord)
    {
        (State.SyncRecordSet ?? (State.SyncRecordSet = new HashSet<string>())).Add(syncRecord);
        await WriteStateAsync();
    }

    public async Task<bool> ExistAsync(string syncRecord)
    {
        await ReadStateAsync();
        if (State.SyncRecordSet == null)
        {
            return false;
        }
        return State.SyncRecordSet.Contains(syncRecord);
    }
}