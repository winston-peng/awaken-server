using System;
using System.Collections.Generic;

namespace AwakenServer.Grains.State.Trade;

public class SyncRecordsState
{
    public string ChainId { get; set; }
    public string PairAddress { get; set; }
    public string SymbolA { get; set; }
    public string SymbolB { get; set; }
    public long ReserveA { get; set; }
    public long ReserveB { get; set; }
    public long Timestamp { get; set; }
    public long BlockHeight { get; set; }
    public string TransactionHash { get; set; }
}