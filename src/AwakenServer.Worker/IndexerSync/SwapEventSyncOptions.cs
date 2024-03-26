using System.Collections.Generic;
using AwakenServer.Chains;

namespace AwakenServer.Trade;

public class SwapEventSyncOption
{
    public string ChainName { get; set; }
    public long LastEndHeight { get; set; } = -1;
}

public class SwapEventSyncOptions
{
    public List<SwapEventSyncOption> Chains { get; set; }
}