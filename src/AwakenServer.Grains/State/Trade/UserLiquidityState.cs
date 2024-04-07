using AwakenServer.Trade.Dtos;

namespace AwakenServer.Grains.State.Trade;

public class UserLiquidityState
{
    public Dictionary<string, Liquidity> TradePairLiquidities { get; set; }
}

public class Liquidity
{
    public TradePairWithTokenDto TradePair { get; set; }
    public string Address { get; set; }
    public long LpTokenAmount { get; set; }
    
    public string ChainId { get; set; }

}