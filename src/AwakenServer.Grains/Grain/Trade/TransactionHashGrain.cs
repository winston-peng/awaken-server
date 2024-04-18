using System.Collections.Generic;
using System.Threading.Tasks;
using AwakenServer.Grains.State.Trade;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public class TransactionHashGrain : Grain<TransactionHashState>, ITransactionHashGrain
{
    public async Task AddTransactionHashAsync(string transactionHash)
    {
        (State.SyncTransactionHashSet ?? (State.SyncTransactionHashSet = new HashSet<string>())).Add(transactionHash);
        await WriteStateAsync();
    }

    public async Task<bool> ExistTransactionHashAsync(string transactionHash)
    {
        await ReadStateAsync();
        if (State.SyncTransactionHashSet == null)
        {
            return false;
        }
        return State.SyncTransactionHashSet.Contains(transactionHash);
    }
}