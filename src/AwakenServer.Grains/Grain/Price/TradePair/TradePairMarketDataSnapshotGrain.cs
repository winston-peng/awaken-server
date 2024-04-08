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

    public bool IsInitialSnapshotInTimeRange(TradePairMarketDataSnapshotGrainDto dto,
        TradePairMarketDataSnapshotGrainDto latestBeforeDto)
    {
        return latestBeforeDto.Timestamp != dto.Timestamp;
    }

    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> AccumulateTotalSupplyAsync(BigDecimal supply)
    {
        State.TotalSupply = (BigDecimal.Parse(State.TotalSupply) + supply).ToNormalizeString();
        
        await WriteStateAsync();

        return new GrainResultDto<TradePairMarketDataSnapshotGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairMarketDataSnapshotState, TradePairMarketDataSnapshotGrainDto>(State)
        };
    }
    public async Task<GrainResultDto<TradePairMarketDataSnapshotGrainDto>> AddOrUpdateAsync(
        TradePairMarketDataSnapshotGrainDto updateDto,
        TradePairMarketDataSnapshotGrainDto dto)
    {
        if (updateDto.Id == Guid.Empty)
        {
            updateDto.Id = Guid.NewGuid();
        }

        if (dto != null)
        {
            // latestBeforeDto.TradeAddressCount24h = await _tradeRecordAppService.GetUserTradeAddressCountAsync(chainId,
            //     eventData.TradePairId,
            //     eventData.Timestamp.AddDays(-1), 
            //     eventData.Timestamp),
            
            bool initialSnapshot = IsInitialSnapshotInTimeRange(updateDto, dto);
            
            dto.Timestamp = updateDto.Timestamp;
            
            if (updateDto.TotalSupply != "0")
            {
                dto.TotalSupply = (BigDecimal.Parse(dto.TotalSupply) + BigDecimal.Parse(updateDto.TotalSupply)).ToNormalizeString();
            }

            if (updateDto.Volume > 0)
            {
                if (initialSnapshot)
                {
                    dto.Volume = updateDto.Volume;
                }
                else
                {
                    dto.Volume += updateDto.Volume;
                }
            }

            if (updateDto.TradeValue > 0)
            {
                if (initialSnapshot)
                {
                    dto.TradeValue = updateDto.TradeValue;
                }
                else
                {
                    dto.TradeValue += updateDto.TradeValue;
                }
            }

            if (updateDto.TradeCount > 0)
            {
                if (initialSnapshot)
                {
                    dto.TradeCount = updateDto.TradeCount;
                }
                else
                {
                    dto.TradeCount += updateDto.TradeCount;
                }
            }

            if (updateDto.Price > 0)
            {
                dto.Price = updateDto.Price;
                dto.PriceHigh = Math.Max(dto.PriceHigh, updateDto.Price);
                dto.PriceLow = dto.PriceLow == 0
                    ? updateDto.Price
                    : Math.Min(dto.PriceLow, updateDto.Price);
            }

            if (updateDto.PriceUSD > 0)
            {
                dto.PriceUSD = updateDto.PriceUSD;
                dto.PriceHighUSD = Math.Max(dto.PriceHighUSD, updateDto.PriceUSD);
                dto.PriceLowUSD = dto.PriceLowUSD == 0
                    ? updateDto.Price
                    : Math.Min(dto.PriceLowUSD, updateDto.PriceUSD);
            }

            if (updateDto.TVL > 0)
            {
                dto.TVL = updateDto.TVL;
            }

            if (updateDto.ValueLocked0 > 0)
            {
                dto.ValueLocked0 = updateDto.ValueLocked0;
            }

            if (updateDto.ValueLocked1 > 0)
            {
                dto.ValueLocked1 = updateDto.ValueLocked1;
            }

            State =
                _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(dto);
        }
        else
        {
            State = _objectMapper.Map<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotState>(updateDto);
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