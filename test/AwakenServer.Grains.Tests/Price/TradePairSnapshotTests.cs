using AwakenServer.Grains.Grain.Price.TradePair;
using TradePairMarketDataSnapshot = AwakenServer.Trade.Index.TradePairMarketDataSnapshot;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade;
using Shouldly;
using Xunit;

namespace AwakenServer.Grains.Tests.Price;

[Collection(ClusterCollection.Name)]
public class TradePairSnapshotTests : AwakenServerGrainTestBase
{
    [Fact]
    public async Task TradePairSnapshotTest()
    {
        var timestamp = DateTime.Now;
        var data = new TradePairMarketDataSnapshot
        {
            ChainId = Guid.Parse("5ccf55f5-4d36-404d-8921-9c3fcd07a281").ToString(),
            TradePairId = Guid.Parse("feb4d613-1f3b-451e-9961-5043760ba295"),
            Timestamp = timestamp,
            TotalSupply = "10.1"
        };

        var grain = Cluster.Client.GetGrain<ITradePairSnapshotGrain>($"{data.ChainId}-{data.TradePairId}-{(long)(timestamp - DateTime.UnixEpoch).TotalMilliseconds}");
        
        //get
        var result = await grain.GetAsync();
        Assert.Null(result);
        
        //add
        await grain.AddAsync(data);
        
        //get
        result = await grain.GetAsync();
        result.ChainId.ShouldBe(data.ChainId);
        result.TradePairId.ShouldBe(data.TradePairId);
        result.Timestamp.ShouldBe(timestamp);
        result.TotalSupply.ShouldBe("10.1");
        
        //update
        data = result;
        data.TotalSupply = "22.2";
        await grain.UpdateAsync(data);
        result = await grain.GetAsync();
        result.Id.ShouldBe(data.Id);
        result.TotalSupply.ShouldBe("22.2");
    }
}