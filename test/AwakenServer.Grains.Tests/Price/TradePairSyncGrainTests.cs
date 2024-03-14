using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade.Index;
using Shouldly;
using Xunit;

namespace AwakenServer.Grains.Tests.Price;

[Collection(ClusterCollection.Name)]
public class TradePairSyncGrainTests : AwakenServerGrainTestBase
{
    [Fact]
    public async Task TradePairSyncGrainTest()
    {
        var tradePairId = Guid.Parse("feb4d613-1f3b-451e-9961-5043760ba295");
        var tradePair = new TradePair(tradePairId);
        
        var grain = Cluster.Client.GetGrain<ITradePairSyncGrain>(GrainIdHelper.GenerateGrainId(tradePairId));
        
        //get
        var result = await grain.GetAsync();
        Assert.Null(result);
        
        //add
        await grain.AddOrUpdateAsync(tradePair);
        
        //get
        result = await grain.GetAsync();
        Assert.NotNull(result);
        result.Id.ShouldBe(tradePairId);
        result.Price.ShouldBe(0.0);
        result.TotalSupply.ShouldBe(null);
        
        //update
        tradePair.Price = 1.1;
        tradePair.TotalSupply = "22.2";
        await grain.AddOrUpdateAsync(tradePair);
        result = await grain.GetAsync();
        result.Price.ShouldBe(1.1);
        result.TotalSupply.ShouldBe("22.2");
    }
}