using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Shouldly;
using Xunit;

namespace AwakenServer.Grains.Tests.Price;

[Collection(ClusterCollection.Name)]
public class TradePairGrainTests : AwakenServerGrainTestBase
{
    [Fact]
    public async Task TradePairSyncGrainTest()
    {
        var tradePairId = Guid.Parse("feb4d613-1f3b-451e-9961-5043760ba295");
        var tradePair = new TradePairGrainDto
        {
            Id = tradePairId
        };
        
        var grain = Cluster.Client.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(tradePairId));
        
        //get
        var result = await grain.GetAsync();
        result.Success.ShouldBe(false);
        
        //add
        await grain.AddOrUpdateAsync(tradePair);
        
        //get
        result = await grain.GetAsync();
        Assert.NotNull(result);
        result.Data.Id.ShouldBe(tradePairId);
        result.Data.Price.ShouldBe(0.0);
        result.Data.TotalSupply.ShouldBe(null);
        
        //update
        tradePair.Price = 1.1;
        tradePair.TotalSupply = "22.2";
        await grain.AddOrUpdateAsync(tradePair);
        result = await grain.GetAsync();
        result.Data.Price.ShouldBe(1.1);
        result.Data.TotalSupply.ShouldBe("22.2");
    }
}