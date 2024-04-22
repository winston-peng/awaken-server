using System;
using System.Collections.Generic;
using AwakenServer.Trade;

namespace AwakenServer.Grains.State.Trade;

public class LiquidityRecordState
{
    public bool IsDeleted { get; set; }
    public string TransactionHash { get; set; }
    public string ChainId { get; set; }
    public string PairAddress { get; set; }
    public string LpTokenAmount { get; set; }
    public DateTime Timestamp { get; set; }
    public LiquidityType Type { get; set; }
    public long BlockHeight { get; set; }
}