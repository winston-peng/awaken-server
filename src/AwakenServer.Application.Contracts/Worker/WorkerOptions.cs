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
    public LiquidityWorkerSettings LiquidityEvent { get; set; }
    public SwapWorkerSettings SwapEvent { get; set; }
    public SyncWorkerSettings SyncEvent { get; set; }
    public TradePairWorkerSettings TradePairEvent { get; set; }
    public TradePairUpdateWorkerSettings TradePairUpdate { get; set; }
    public TradeRecordRevertWorkerSettings TradeRecordRevert { get; set; }
}

public class WorkerSettingBase
{
    public bool Open { get; set; } = true;
    public int TimePeriod { get; set; } = 3000;
    public bool ResetBlockHeightFlag { get; set; } = false;
    public long ResetBlockHeight { get; set; } = 0;
    public long QueryStartBlockHeightOffset { get; set; } = -1;
}

public class LiquidityWorkerSettings : WorkerSettingBase
{
}

public class SwapWorkerSettings : WorkerSettingBase
{
}

public class SyncWorkerSettings : WorkerSettingBase
{
}

public class TradePairWorkerSettings : WorkerSettingBase
{
}

public class TradeRecordRevertWorkerSettings : WorkerSettingBase
{
    public int TimePeriod { get; set; } = 75000;
    public int QueryOnceLimit { get; set; } = 1000;
    public int BlockHeightLimit { get; set; } = 100;
    public int RetryLimit { get; set; } = 3;
    public int TransactionHashExpirationTime { get; set; } = 360;

    public int BatchFlushTimePeriod { get; set; } = 3;

    public int BatchFlushCount { get; set; } = 10;

    public int StartBlockHeightGap { get; set; } = 10;
}

public class TradePairUpdateWorkerSettings : WorkerSettingBase
{
    public int TimePeriod { get; set; } = 1800000;
}