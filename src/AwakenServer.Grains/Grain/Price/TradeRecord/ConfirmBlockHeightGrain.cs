using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public class ConfirmBlockHeightGrain : Grain<long>, IConfirmBlockHeightGrain
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


    public async Task<GrainResultDto<long>> InsertAsync(long blockHeight)
    {
        State = blockHeight;

        await WriteStateAsync();

        return new GrainResultDto<long>()
        {
            Success = true,
            Data = blockHeight
        };
    }

    public async Task<GrainResultDto<long>> GetAsync()
    {
        return new GrainResultDto<long>()
        {
            Success = true,
            Data = State
        };
    }
}