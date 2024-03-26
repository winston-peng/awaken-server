using System.Collections.Generic;

namespace AwakenServer.Trade;

public class TradePairEventSyncOption
{
    public string ChainName { get; set; }
    public long LastEndHeight { get; set; } = -1;
}

public class TradePairEventSyncOptions
{
    public List<TradePairEventSyncOption> Chains { get; set; }
}