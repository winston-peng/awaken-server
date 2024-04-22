using System;
using AwakenServer.Common;
using AwakenServer.Trade;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public class UnconfirmedTransactionsGrainDto
{
    public long BlockHeight { get; set; }
    public string TransactionHash { get; set; }
}