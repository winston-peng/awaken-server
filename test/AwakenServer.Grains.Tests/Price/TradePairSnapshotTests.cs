using AwakenServer.Grains.Grain.Price.TradePair;
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
        var data = new TradePairMarketDataSnapshotGrainDto
        {
            ChainId = Guid.Parse("5ccf55f5-4d36-404d-8921-9c3fcd07a281").ToString(),
            TradePairId = Guid.Parse("feb4d613-1f3b-451e-9961-5043760ba295"),
            Timestamp = timestamp,
            TotalSupply = "10.1"
        };

        var grain = Cluster.Client.GetGrain<ITradePairMarketDataSnapshotGrain>($"{data.ChainId}-{data.TradePairId}-{(long)(timestamp - DateTime.UnixEpoch).TotalMilliseconds}");
        
        //get
        var result = await grain.GetAsync();
        result.Success.ShouldBe(false);
        
        //add
        await grain.AddOrUpdateAsync(data);
        
        //get
        result = await grain.GetAsync();
        result.Data.ChainId.ShouldBe(data.ChainId);
        result.Data.TradePairId.ShouldBe(data.TradePairId);
        result.Data.Timestamp.ShouldBe(timestamp);
        result.Data.TotalSupply.ShouldBe("10.1");
        
        //update
        data = result.Data;
        data.TotalSupply = "22.2";
        await grain.AddOrUpdateAsync(data);
        result = await grain.GetAsync();
        result.Data.Id.ShouldBe(data.Id);
        result.Data.TotalSupply.ShouldBe("22.2");
    }
}