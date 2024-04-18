using System;
using AwakenServer.Trade;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public class UnconfirmedTradeRecordGrainDto
{
    public string Address { get; set; }
    public Guid TradePairId { get; set; }
    public long BlockHeight { get; set; }
    public string TransactionHash { get; set; }
    public int Retry { get; set; }
}