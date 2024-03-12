using System.Collections;
using AwakenServer.Trade;
using TradePair = AwakenServer.Trade.Index.TradePair;

namespace AwakenServer.Grains.State.Trade;

public class TradePairSyncState
{
    public TradePair TradePair { get; set; }
    
    public TradePairInfoIndex InfoIndex { get; set; }
}