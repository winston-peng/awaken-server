using System;
using System.Collections.Generic;
using AwakenServer.Common;
using AwakenServer.Trade;

namespace AwakenServer.Grains.State.Price;

public class UnconfirmedTransactionsState
{
    public long MinUnconfirmedBlockHeight { get; set; }
    public Dictionary<long, List<ToBeConfirmRecord>> UnconfirmedTransactions { get; set; }  = new();
}

public class ToBeConfirmRecord
{
    public string TransactionHash { get; set; }
}