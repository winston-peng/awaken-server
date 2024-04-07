using AwakenServer.Trade;

namespace AwakenServer.Grains.State.Price;

public class TradeRecordRevertState
{
    public long MinUnconfirmedBlockHeight { get; set; }
    public Dictionary<long,List<ToBeConfirmRecord>> ToBeConfirmRecords { get; set; }
}

public class ToBeConfirmRecord
{
    public string Address { get; set; }
    public Guid TradePairId { get; set; }
    public long BlockHeight { get; set; }
    public string TransactionHash { get; set; }
    public int Retry { get; set; }
}