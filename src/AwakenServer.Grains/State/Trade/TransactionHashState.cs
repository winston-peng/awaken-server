using System;
using System.Collections.Generic;

namespace AwakenServer.Grains.State.Trade;

public class TransactionHashState
{
    public HashSet<String> SyncTransactionHashSet { get; set; }
}