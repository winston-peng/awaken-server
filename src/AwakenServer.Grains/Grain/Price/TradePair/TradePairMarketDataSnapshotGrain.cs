using AwakenServer.Grains.State.Price;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp.ObjectMapping;
using System.Numerics;
using Nethereum.Util;

namespace AwakenServer.Grains.Grain.Price.TradePair;

public class TradePairMarketDataSnapshotGrain : Grain<TradePairMarketDataSnapshotState>,
    ITradePairMarketDataSnapshotGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TradePairMarketDataSnapshotGrain> _logger;


    public TradePairMarketDataSnapshotGrain(IObjectMapper objectMapper,
        ILogger<TradePairMarketDataSnapshotGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }

    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> GetAsync()
    {
        if (State.Id == Guid.Empty)
        {
            return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
            {
                Success = false
            };
        }

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairMarketDataSnapshotState, TradePairMarketDataSnapshotGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> AddOrUpdateAsync(
        TradePairMarketDataSnapshotGrainDto dto)
    {
        if (dto.Id == Guid.Empty)
        {
            dto.Id = Guid.NewGuid();
        }

        State = _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(dto);
        await WriteStateAsync();

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = dto
        };
    }


    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> UpdateTotalSupplyWithLiquidityAsync(
        TradePairMarketDataSnapshotGrainDto dto,
        TradePairMarketDataSnapshotGrainDto latestBeforeDto,
        BigDecimal lpTokenAmount,
        int userTradeAddressCount,
        string lpTokenCurrentSupply)
    {
        var totalSupply = lpTokenAmount;

        if (State.Id == Guid.Empty)
        {
            State = _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(dto);

            if (latestBeforeDto != null)
            {
                totalSupply += BigDecimal.Parse(latestBeforeDto.TotalSupply);
                State.Price = latestBeforeDto.Price;
                State.PriceUSD = latestBeforeDto.PriceUSD;
                State.TVL = latestBeforeDto.TVL;
                State.ValueLocked0 = latestBeforeDto.ValueLocked0;
                State.ValueLocked1 = latestBeforeDto.ValueLocked1;
            }

            State.TotalSupply = totalSupply.ToNormalizeString();
            State.TradeAddressCount24h = userTradeAddressCount;
        }
        else
        {
            totalSupply += BigDecimal.Parse(State.TotalSupply);
            State.TotalSupply = totalSupply.ToNormalizeString();
        }

        if (!string.IsNullOrWhiteSpace(lpTokenCurrentSupply))
        {
            State.TotalSupply = lpTokenCurrentSupply;
        }

        _logger.LogInformation("UpdateTotalSupplyAsync: totalSupply:{supply}", State.TotalSupply);

        await WriteStateAsync();

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairMarketDataSnapshotState, TradePairMarketDataSnapshotGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> UpdateLiquidityWithSyncEvent(
        TradePairMarketDataSnapshotGrainDto dto,
        TradePairMarketDataSnapshotGrainDto latestBeforeDto,
        int userTradeAddressCount)
    {
        if (State.Id == Guid.Empty)
        {
            State = _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(dto);

            if (latestBeforeDto != null)
            {
                State.TotalSupply = latestBeforeDto.TotalSupply;
            }

            State.TradeAddressCount24h = userTradeAddressCount;
            _logger.LogInformation("UpdateLiquidityAsync, supply:{supply}", State.TotalSupply);
        }
        else
        {
            State.Price = dto.Price;
            State.PriceHigh = Math.Max(State.PriceHigh, dto.Price);
            State.PriceHighUSD = Math.Max(State.PriceHighUSD, dto.PriceUSD);
            State.PriceLow = State.PriceLow == 0 ? dto.Price : Math.Min(State.PriceLow, dto.Price);
            State.PriceLowUSD =
                State.PriceLowUSD == 0 ? dto.Price : Math.Min(State.PriceLowUSD, dto.PriceUSD);
            State.PriceUSD = dto.PriceUSD;
            State.TVL = dto.TVL;
            State.ValueLocked0 = dto.ValueLocked0;
            State.ValueLocked1 = dto.ValueLocked1;
            _logger.LogInformation("UpdateLiquidityAsync, supply:{supply}", State.TotalSupply);
        }

        await WriteStateAsync();

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairMarketDataSnapshotState, TradePairMarketDataSnapshotGrainDto>(State)
        };
    }


    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> UpdateTradeRecord(
        TradePairMarketDataSnapshotGrainDto dto,
        TradePairMarketDataSnapshotGrainDto latestBeforeDto)
    {
        if (State.Id == Guid.Empty)
        {
            State = _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(dto);

            if (latestBeforeDto != null)
            {
                State.TotalSupply = latestBeforeDto.TotalSupply;
                State.Price = latestBeforeDto.Price;
                State.PriceUSD = latestBeforeDto.PriceUSD;
                State.TVL = latestBeforeDto.TVL;
                State.ValueLocked0 = latestBeforeDto.ValueLocked0;
                State.ValueLocked1 = latestBeforeDto.ValueLocked1;
            }
        }
        else
        {
            State.Volume += dto.Volume;
            State.TradeValue += dto.TradeValue;
            State.TradeCount += dto.TradeCount;
            State.TradeAddressCount24h = dto.TradeAddressCount24h;
        }

        await WriteStateAsync();

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairMarketDataSnapshotState, TradePairMarketDataSnapshotGrainDto>(State)
        };
    }

    
}