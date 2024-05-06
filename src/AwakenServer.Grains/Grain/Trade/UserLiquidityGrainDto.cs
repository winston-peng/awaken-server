using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;

namespace AwakenServer.Grains.Grain.Trade;

public class UserLiquidityGrainDto
{
    public string ChainId { get; set; }
    public TradePairWithTokenDto TradePair { get; set; }
    public string Address { get; set; }
    public long LpTokenAmount { get; set; }
    public LiquidityType Type { get; set; }
    public double AssetUSD { get; set; }
    public string Token0Amount { get; set; }
    public string Token1Amount { get; set; }
    public bool IsRevert { get; set; }

}