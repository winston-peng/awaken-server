using System;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;

namespace AwakenServer.Grains.Grain.Trade;

public class LiquidityRecordGrainDto
{
    public string ChainId { get; set; }
    public LiquidityType Type { get; set; }
    public string LpTokenAmount { get; set; }
    public DateTime Timestamp { get; set; }
}