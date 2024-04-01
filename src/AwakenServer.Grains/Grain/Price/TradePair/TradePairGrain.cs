using AwakenServer.Grains.Grain;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Tokens.TokenPrice;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Trade;
using AwakenServer.Price;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using IdentityServer4.Extensions;
using Microsoft.Extensions.Logging;
using Nest;
using Nethereum.Util;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace AwakenServer.Grains.Grain.Price.TradePair;

public class TradePairGrain : Grain<TradePairState>, ITradePairGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TradePairGrain> _logger;
    private readonly ITokenPriceProvider _tokenPriceProvider;
    private readonly IClusterClient _clusterClient;
    private readonly List<ITradePairMarketDataSnapshotGrain> _previous7DaysMarketDataSnapshots;
    
    public TradePairGrain(IObjectMapper objectMapper,
        ITokenPriceProvider tokenPriceProvider,
        IClusterClient clusterClient,
        ILogger<TradePairGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _tokenPriceProvider = tokenPriceProvider;
        _clusterClient = clusterClient;
        _previous7DaysMarketDataSnapshots = new List<ITradePairMarketDataSnapshotGrain>();
    }

    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await LoadPastWeekSnapshots();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }
    
    private async Task LoadPastWeekSnapshots()
    {
        DateTime now = DateTime.Now;
        DateTime pastWeek = now.AddDays(-7);

        Dictionary<DateTime, ITradePairMarketDataSnapshotGrain> pastWeekSnapshots = new Dictionary<DateTime, ITradePairMarketDataSnapshotGrain>();

        foreach (var snapshotGuid in State.MarketDataSnapshots)
        {
            ITradePairMarketDataSnapshotGrain snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(snapshotGuid);
            var snapshotDataResult = await snapshotGrain.GetAsync();
            if (snapshotDataResult.Success)
            {
                var snapshotData = snapshotDataResult.Data;
                if (snapshotData.Timestamp >= pastWeek && snapshotData.Timestamp <= now)
                {
                    _previous7DaysMarketDataSnapshots.Add(snapshotGrain);
                }
            }
        }
    }
    
    public async Task<GrainResultDto<TradePairGrainDto>> GetAsync()
    {
        if (State.Id == Guid.Empty)
        {
            return new GrainResultDto<TradePairGrainDto>()
            {
                Success = false
            };
        }

        return new GrainResultDto<TradePairGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<TradePairState, TradePairGrainDto>(State)
        };
    }

    public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestBeforeSnapshotAsync(DateTime maxTime)
    {
        
        List<TradePairMarketDataSnapshotGrainDto> sortedSnapshots = new List<TradePairMarketDataSnapshotGrainDto>();
        foreach(var grain in _previous7DaysMarketDataSnapshots)
        {
            var result = await grain.GetAsync();
            if (result.Success)
            {
                sortedSnapshots.Add(result.Data);
            }
        }

        sortedSnapshots.Sort((dto1, dto2) => dto2.Timestamp.CompareTo(dto1.Timestamp));
        
        foreach (var snapshot in sortedSnapshots)
        {
            if (maxTime > snapshot.Timestamp)
            {
                return snapshot;
            }
        }

        return null;
    }
    
    public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestSnapshotAsync()
    {
        
        if (_previous7DaysMarketDataSnapshots.IsNullOrEmpty())
        {
            return null;
        }
        
        
        List<TradePairMarketDataSnapshotGrainDto> sortedSnapshots = new List<TradePairMarketDataSnapshotGrainDto>();
        foreach(var grain in _previous7DaysMarketDataSnapshots)
        {
            var result = await grain.GetAsync();
            if (result.Success)
            {
                sortedSnapshots.Add(result.Data);
            }
        }

        sortedSnapshots.Sort((dto1, dto2) => dto2.Timestamp.CompareTo(dto1.Timestamp));

        return sortedSnapshots.FirstOrDefault();
    }

    public async Task<List<TradePairMarketDataSnapshotGrainDto>> GetPrevious7DaysSnapshotsDtoAsync()
    {
        
        List<TradePairMarketDataSnapshotGrainDto> sortedSnapshots = new List<TradePairMarketDataSnapshotGrainDto>();
        foreach(var grain in _previous7DaysMarketDataSnapshots)
        {
            var result = await grain.GetAsync();
            if (result.Success)
            {
                sortedSnapshots.Add(result.Data);
            }
        }

        sortedSnapshots.Sort((dto1, dto2) => dto2.Timestamp.CompareTo(dto1.Timestamp));
        
        return sortedSnapshots;
    }
    
    
    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> AddSnapshotAsync(
        TradePairMarketDataSnapshotGrainDto snapshotDto)
    {
        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        // add snapshot
        if (!State.MarketDataSnapshots.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(snapshotGrain);
            State.MarketDataSnapshots.Add(snapshotGrain.GetPrimaryKeyString());
        }
        
        var updateSnapshotResult = await snapshotGrain.AddOrUpdateAsync(snapshotDto);
        
        // update trade pair
        var updateTradePairResult = await UpdateFromSnapshotAsync(updateSnapshotResult.Data);
        
        return new GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>
        {
            Success = true,
            Data = new Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>(
                updateTradePairResult.Data,
                updateSnapshotResult.Data)
        };
    }


    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateTotalSupplyWithLiquidityEventAsync(
        TradePairMarketDataSnapshotGrainDto snapshotDto,
        string lpTokenCurrentSupply,
        int userTradeAddressCount,
        BigDecimal lpTokenAmount)
    {
        _logger.LogInformation($"AddSnapshot lp token {snapshotDto.TradePairId}, totalSupply: {snapshotDto.TotalSupply}");
        
        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        // add snapshot
        if (!State.MarketDataSnapshots.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(snapshotGrain);
            State.MarketDataSnapshots.Add(snapshotGrain.GetPrimaryKeyString());
        }
        
        // update snapshot grain
        var latestBeforeDto = await GetLatestBeforeSnapshotAsync(snapshotDto.Timestamp);
        
        var updateSnapshotResult = await snapshotGrain.UpdateTotalSupplyWithLiquidityAsync(snapshotDto,
            latestBeforeDto,
            lpTokenAmount, 
            userTradeAddressCount, 
            lpTokenCurrentSupply);
        
        // nie:The current snapshot is not up-to-date. The latest snapshot needs to update TotalSupply 
        var latestSnapshot = await GetLatestSnapshotAsync();
        if (latestSnapshot != null && snapshotDto.Timestamp < latestSnapshot.Timestamp)
        {
            return await UpdateTotalSupplyWithLiquidityEventAsync(latestSnapshot,
                lpTokenCurrentSupply,
                userTradeAddressCount,
                lpTokenAmount);
        }
        
        // update trade pair
        var updateTradePairResult = await UpdateFromSnapshotAsync(updateSnapshotResult.Data);
        
        return new GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>
        {
            Success = true,
            Data = new Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>(
                updateTradePairResult.Data,
                updateSnapshotResult.Data)
        };
        
    }

    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateTradeRecordAsync(
        TradePairMarketDataSnapshotGrainDto snapshotDto)
    {
        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        // add snapshot
        if (!State.MarketDataSnapshots.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(snapshotGrain);
            State.MarketDataSnapshots.Add(snapshotGrain.GetPrimaryKeyString());
        }
        
        var latestBeforeDto = await GetLatestBeforeSnapshotAsync(snapshotDto.Timestamp);

        var updateSnapshotResult = await snapshotGrain.UpdateTradeRecord(snapshotDto, latestBeforeDto);
        
        var updateTradePairResult = await UpdateFromSnapshotAsync(updateSnapshotResult.Data);
        
        return new GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>
        {
            Success = true,
            Data = new Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>(
                updateTradePairResult.Data,
                updateSnapshotResult.Data)
        };
    }


    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> UpdateLiquidityWithSyncEventAsync(
        TradePairMarketDataSnapshotGrainDto snapshotDto,
        int userTradeAddressCount)
    {
        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        // add snapshot
        if (!State.MarketDataSnapshots.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(snapshotGrain);
            State.MarketDataSnapshots.Add(snapshotGrain.GetPrimaryKeyString());
        }
        
        var lastMarketData = await GetLatestBeforeSnapshotAsync(snapshotDto.Timestamp);

        var updateSnapshotResult = await snapshotGrain.UpdateLiquidityWithSyncEvent(snapshotDto, lastMarketData, userTradeAddressCount);
        
        var updateTradePairResult = await UpdateFromSnapshotAsync(updateSnapshotResult.Data);
        
        return new GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>
        {
            Success = true,
            Data = new Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>(
                updateTradePairResult.Data,
                updateSnapshotResult.Data)
        };
    }
    
    public async Task<GrainResultDto<TradePairGrainDto>> UpdateAsync(DateTime timestamp,
        int userTradeAddressCount)
    {
        var previous7DaysSnapshotDtos = await GetPrevious7DaysSnapshotsDtoAsync();
        
        var volume24h = 0d;
        var tradeValue24h = 0d;
        var tradeCount24h = 0;
        var priceHigh24h = State.Price;
        var priceLow24h = State.Price;
        var priceHigh24hUSD = State.PriceUSD;
        var priceLow24hUSD = State.PriceUSD;
        var daySnapshot = previous7DaysSnapshotDtos.Where(s => s.Timestamp >= timestamp.AddDays(-1)).ToList();
        foreach (var snapshot in daySnapshot)
        {
            volume24h += snapshot.Volume;
            tradeValue24h += snapshot.TradeValue;
            tradeCount24h += snapshot.TradeCount;
            priceHigh24h = Math.Max(priceHigh24h, snapshot.PriceHigh);
            priceLow24h = Math.Min(priceLow24h, snapshot.PriceLow);
            priceHigh24hUSD = Math.Max(priceHigh24hUSD, snapshot.PriceHighUSD);
            priceLow24hUSD = Math.Min(priceLow24hUSD, snapshot.PriceLowUSD);
        }

        var lastDaySnapshot = previous7DaysSnapshotDtos.Where(s => s.Timestamp >= timestamp.AddDays(-2) && s.Timestamp < timestamp.AddDays(-1))
            .OrderByDescending(s => s.Timestamp).ToList();
        var lastDayVolume24h = lastDaySnapshot.Sum(snapshot => snapshot.Volume);
        var lastDayTvl = 0d;
        var lastDayPriceUSD = 0d;
        if (lastDaySnapshot.Count > 0)
        {
            var snapshot = lastDaySnapshot.First();
            lastDayTvl = snapshot.TVL;
            lastDayPriceUSD = snapshot.PriceUSD;
        }
        else
        {
            var sortDaySnapshot = daySnapshot.OrderBy(s => s.Timestamp).ToList();
            if (sortDaySnapshot.Count > 0)
            {
                var snapshot = sortDaySnapshot.First();
                lastDayTvl = snapshot.TVL;
                lastDayPriceUSD = snapshot.PriceUSD;
            }
        }
        
        var priceUSD0 = await _tokenPriceProvider.GetPriceAsync(State.Token0Symbol);
        var priceUSD1 = await _tokenPriceProvider.GetPriceAsync(State.Token1Symbol);
        
        State.PriceUSD = priceUSD1 != 0 ? State.Price * (double)priceUSD1 : (double)priceUSD0;
        State.PricePercentChange24h = lastDayPriceUSD == 0
            ? 0
            : (State.PriceUSD - lastDayPriceUSD) * 100 / lastDayPriceUSD;
        State.PriceChange24h = lastDayPriceUSD == 0
            ? 0
            : State.PriceUSD - lastDayPriceUSD;
        State.TVL = (double)priceUSD0 * State.ValueLocked0 + (double)priceUSD1 * State.ValueLocked1;
        State.TVLPercentChange24h = lastDayTvl == 0
            ? 0
            : (State.TVL - lastDayTvl) * 100 / lastDayTvl;
        State.PriceHigh24h = priceHigh24h;
        State.PriceHigh24hUSD = priceHigh24hUSD;
        State.PriceLow24hUSD = priceLow24hUSD;
        State.PriceLow24h = priceLow24h;
        State.Volume24h = volume24h;
        State.VolumePercentChange24h = lastDayVolume24h == 0
            ? 0
            : (State.Volume24h - lastDayVolume24h) * 100 / lastDayVolume24h;
        State.TradeValue24h = tradeValue24h;
        State.TradeCount24h = tradeCount24h;

        var volume7d = previous7DaysSnapshotDtos.Sum(k =>
            k.Volume);
        State.FeePercent7d =
            State.TVL == 0 ? 0 : (volume7d * State.PriceUSD * State.FeeRate * 365 * 100) / (State.TVL * 7);
        State.TradeAddressCount24h = userTradeAddressCount;

        _logger.LogInformation(
            "updatePairTimer token:{token0Symbol}-{token1Symbol},fee:{fee}-price:{price}-priceUSD:{priceUSD},token1:{token1}-priceUSD1:{priceUSD1}",
            State.Token0Symbol, State.Token1Symbol, State.FeeRate, State.Price, State.PriceUSD, State.Token1Symbol,
            priceUSD1);

        await WriteStateAsync();

        return new GrainResultDto<TradePairGrainDto>
        {
            Success = true,
            Data = _objectMapper.Map<TradePairState, TradePairGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<TradePairGrainDto>> UpdateFromSnapshotAsync(TradePairMarketDataSnapshotGrainDto dto)
    {
        
        var latestSnapshot = await GetLatestSnapshotAsync();
        if (latestSnapshot != null && dto.Timestamp < latestSnapshot.Timestamp)
        {
            return new GrainResultDto<TradePairGrainDto>
            {
                Success = true,
                Data = _objectMapper.Map<TradePairState, TradePairGrainDto>(State)
            };
        }
        
        var previous7DaysSnapshotDtos = await GetPrevious7DaysSnapshotsDtoAsync();
        var tokenAValue24 = 0d;
        var tokenBValue24 = 0d;
        var tradeCount24h = 0;
        var priceHigh24h = dto.PriceHigh;
        var priceLow24h = dto.PriceLow;
        var priceHigh24hUSD = dto.PriceHighUSD;
        var priceLow24hUSD = dto.PriceLowUSD;

        var daySnapshot = previous7DaysSnapshotDtos.Where(s => s.Timestamp >= dto.Timestamp.AddDays(-1)).ToList();
        foreach (var snapshot in daySnapshot)
        {
            tokenAValue24 += snapshot.Volume;
            tokenBValue24 += snapshot.TradeValue;
            tradeCount24h += snapshot.TradeCount;

            if (priceLow24h == 0)
            {
                priceLow24h = snapshot.PriceLow;
            }

            if (snapshot.PriceLow != 0)
            {
                priceLow24h = Math.Min(priceLow24h, snapshot.PriceLow);
            }

            if (priceLow24hUSD == 0)
            {
                priceLow24hUSD = snapshot.PriceLowUSD;
            }

            if (snapshot.PriceLowUSD != 0)
            {
                priceLow24hUSD = Math.Min(priceLow24hUSD, snapshot.PriceLowUSD);
            }

            priceHigh24hUSD = Math.Max(priceHigh24hUSD, snapshot.PriceHighUSD);
            priceHigh24h = Math.Max(priceHigh24h, snapshot.PriceHigh);
        }

        var lastDaySnapshot = previous7DaysSnapshotDtos.Where(s => s.Timestamp < dto.Timestamp.AddDays(-1))
            .OrderByDescending(s => s.Timestamp).ToList();
        var lastDayVolume24h = lastDaySnapshot.Sum(snapshot => snapshot.Volume);
        var lastDayTvl = 0d;
        var lastDayPriceUSD = 0d;

        if (lastDaySnapshot.Count > 0)
        {
            var snapshot = lastDaySnapshot.First();
            lastDayTvl = snapshot.TVL;
            lastDayPriceUSD = snapshot.PriceUSD;
        }
        else
        {
            var latestBeforeThisSnapshotDto = await GetLatestBeforeSnapshotAsync(dto.Timestamp);
            if (latestBeforeThisSnapshotDto != null)
            {
                lastDayTvl = latestBeforeThisSnapshotDto.TVL;
                lastDayPriceUSD = latestBeforeThisSnapshotDto.PriceUSD;
            }
        }

        State.TotalSupply = dto.TotalSupply;
        State.Price = dto.Price;
        State.PriceUSD = dto.PriceUSD;
        State.TVL = dto.TVL;
        State.ValueLocked0 = dto.ValueLocked0;
        State.ValueLocked1 = dto.ValueLocked1;
        State.Volume24h = tokenAValue24;
        State.TradeValue24h = tokenBValue24;
        State.TradeCount24h = tradeCount24h;
        State.TradeAddressCount24h = dto.TradeAddressCount24h;
        State.PriceHigh24h = priceHigh24h;
        State.PriceLow24h = priceLow24h;
        State.PriceHigh24hUSD = priceHigh24hUSD;
        State.PriceLow24hUSD = priceLow24hUSD;
        State.PriceChange24h = lastDayPriceUSD == 0
            ? 0
            : State.PriceUSD - lastDayPriceUSD;
        State.PricePercentChange24h = lastDayPriceUSD == 0
            ? 0
            : (State.PriceUSD - lastDayPriceUSD) * 100 / lastDayPriceUSD;
        State.VolumePercentChange24h = lastDayVolume24h == 0
            ? 0
            : (State.Volume24h - lastDayVolume24h) * 100 / lastDayVolume24h;
        State.TVLPercentChange24h = lastDayTvl == 0
            ? 0
            : (State.TVL - lastDayTvl) * 100 / lastDayTvl;

        if (dto.TVL != 0)
        {
            var volume7d = previous7DaysSnapshotDtos
                .Sum(k => k.Volume);
            volume7d += dto.Volume;
            State.FeePercent7d = (volume7d * dto.PriceUSD * State.FeeRate * 365 * 100) /
                                 (dto.TVL * 7);
        }

        await WriteStateAsync();

        return new GrainResultDto<TradePairGrainDto>
        {
            Success = true,
            Data = _objectMapper.Map<TradePairState, TradePairGrainDto>(State)
        };
    }

    public async Task<GrainResultDto<TradePairGrainDto>> AddAsync(TradePairGrainDto dto)
    {
        State = _objectMapper.Map<TradePairGrainDto, TradePairState>(dto);
        await WriteStateAsync();
        return new GrainResultDto<TradePairGrainDto>
        {
            Success = true,
            Data = dto
        };
    }
}