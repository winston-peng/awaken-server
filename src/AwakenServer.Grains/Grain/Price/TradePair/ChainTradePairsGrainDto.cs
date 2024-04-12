using Nest;

namespace AwakenServer.Grains.Grain.Price;

public class ChainTradePairsGrainDto
{
    public string TradePairAddress { get; set; }
    
    public string TradePairGrainId { get; set; }
}