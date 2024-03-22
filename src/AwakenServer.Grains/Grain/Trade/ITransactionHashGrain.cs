using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public interface ITransactionHashGrain : IGrainWithStringKey
{
    public Task AddTransactionHashAsync(string transactionHash);

    public Task<bool> ExistTransactionHashAsync(string transactionHash);
}