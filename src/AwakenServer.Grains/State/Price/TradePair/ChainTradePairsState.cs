namespace AwakenServer.Grains.State.Price;

public class ChainTradePairsState
{
    public Dictionary<string, string> TradePairs { get; set; } = new Dictionary<string, string>();
}