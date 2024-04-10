namespace AwakenServer.Worker;

public class WorkerOptions
{
    public const int TimePeriod = 3000;
    public const int RevertTimePeriod = 75000;
    public const int PairUpdatePeriod = 1800 * 1000;
    public const int QueryBlockHeightLimit = 1000;
}

public class WorkerSettings
{
    public LiquiditySettings LiquidityEvent { get; set; }
    public SwapSettings SwapEvent { get; set; }
    public SyncSettings SyncEvent { get; set; }
    public TradePairSettings TradePairEvent { get; set; }
    public TradePairUpdateSettings TradePairUpdate { get; set; }
    public TradeRecordRevertSettings TradeRecordRevert { get; set; }
}

public class LiquiditySettings
{
    public int TimePeriod { get; set; }
    public bool ResetBlockHeightFlag { get; set; }
    public long ResetBlockHeight { get; set; }
}

public class SwapSettings
{
    public int TimePeriod { get; set; }
    public bool ResetBlockHeightFlag { get; set; }
    public long ResetBlockHeight { get; set; }
}

public class SyncSettings
{
    public int TimePeriod { get; set; }
    public bool ResetBlockHeightFlag { get; set; }
    public long ResetBlockHeight { get; set; }
}
public class TradePairSettings
{
    public int TimePeriod { get; set; }
    public bool ResetBlockHeightFlag { get; set; }
    public long ResetBlockHeight { get; set; }
}
public class TradeRecordRevertSettings
{
    public int TimePeriod { get; set; }
    public int QueryOnceLimit { get; set; }
    public int BlockHeightLimit { get; set; }
    public int RetryLimit { get; set; }
    public int TransactionHashExpirationTime { get; set; }
    public int BatchFlushTimePeriod { get; set; }
    public int BatchFlushCount { get; set; }
}

public class TradePairUpdateSettings
{
    public int TimePeriod { get; set; }
}