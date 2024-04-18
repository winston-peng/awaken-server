using System.Threading.Tasks;
using AwakenServer.Grains.State.Price;
using AwakenServer.Grains.State.Trade;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Grains.Grain.Trade;

public class LiquidityRecordGrain : Grain<LiquidityRecordState>, ILiquidityRecordGrain
{
    private readonly IObjectMapper _objectMapper;

    public LiquidityRecordGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

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

}