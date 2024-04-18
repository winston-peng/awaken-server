using System;

namespace AwakenServer.Grains.State.Price;

public class TradePairInfoState
{
    
    public Guid Id { get; set; }
    
    public string ChainId { get; set; }
    
    public string BlockHash { get; set; }

    public long BlockHeight { get; set; }

    
    public string PreviousBlockHash { get; set; }

    public bool IsDeleted { get; set; }
     
    public string Address { get; set; }
     
    public string Token0Symbol { get; set; }
     
    public string Token1Symbol { get; set; }
    
    public Guid Token0Id { get; set; }
    
    public Guid Token1Id { get; set; }
    
    public double FeeRate { get; set; }
    
    public bool IsTokenReversed { get; set; }
}