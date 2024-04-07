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
    private readonly SortedSet<Tuple<DateTime,string>> _previous7DaysMarketDataSnapshots;
    
    public TradePairGrain(IObjectMapper objectMapper,
        ITokenPriceProvider tokenPriceProvider,
        IClusterClient clusterClient,
        ILogger<TradePairGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _tokenPriceProvider = tokenPriceProvider;
        _clusterClient = clusterClient;
        _previous7DaysMarketDataSnapshots = new SortedSet<Tuple<DateTime,string>>(
            Comparer<Tuple<DateTime,string>>.Create((grain1, grain2) =>
            {
                return grain2.Item1.CompareTo(grain1.Item1);
            })
        );
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

        Dictionary<DateTime, ITradePairMarketDataSnapshotGrain> pastWeekSnapshots =
            new Dictionary<DateTime, ITradePairMarketDataSnapshotGrain>();

        foreach (var snapshotId in State.MarketDataSnapshotGrainIds)
        {
            ITradePairMarketDataSnapshotGrain snapshotGrain =
                _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(snapshotId);
            var snapshotDataResult = await snapshotGrain.GetAsync();
            if (snapshotDataResult.Success)
            {
                var snapshotData = snapshotDataResult.Data;
                if (snapshotData.Timestamp >= pastWeek && snapshotData.Timestamp <= now)
                {
                    _previous7DaysMarketDataSnapshots.Add(new Tuple<DateTime, string>(snapshotData.Timestamp, snapshotGrain.GetPrimaryKeyString()));
                }
            }
        }
    }

    public async Task RemoveHistorySnapshotCacheAsync()
    {
        DateTime currentDate = DateTime.Now;
        DateTime oneWeekAgo = currentDate.AddDays(-7);
        
        _previous7DaysMarketDataSnapshots.RemoveWhere(item => item.Item1 < oneWeekAgo);
    }
    
    public async Task<GrainResultDto<TradePairGrainDto>> GetAsync()
    {
        if (State.Id == Guid.Empty)
        {
            return new GrainResultDto<TradePairGrainDto>
            {
                Success = false
            };
        }

        return new GrainResultDto<TradePairGrainDto>
        {
            Success = true,
            Data = _objectMapper.Map<TradePairState, TradePairGrainDto>(State)
        };
    }

    public async Task<TradePairMarketDataSnapshotGrainDto> GetLatestBeforeSnapshotAsync(DateTime maxTime)
    {
        foreach (var snapshot in _previous7DaysMarketDataSnapshots)
        {
            if (maxTime >= snapshot.Item1)
            {
                var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(snapshot.Item2);
                var result = await grain.GetAsync();
                if (result.Success)
                {
                    return result.Data;
                }
                else
                {
                    _logger.LogError($"trade pair {State.Id} contains empty snapshot grain");
                }
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
        
        foreach (var snapshot in _previous7DaysMarketDataSnapshots)
        {
            var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(snapshot.Item2);
            var result = await grain.GetAsync();
            if (result.Success)
            {
                return result.Data;
            }
        }

        return null;
    }

    public async Task<List<TradePairMarketDataSnapshotGrainDto>> GetPrevious7DaysSnapshotsDtoAsync()
    {
        List<TradePairMarketDataSnapshotGrainDto> sortedSnapshots = new List<TradePairMarketDataSnapshotGrainDto>();
        foreach (var snapshot in _previous7DaysMarketDataSnapshots)
        {
            var grain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(snapshot.Item2);
            var result = await grain.GetAsync();
            if (result.Success)
            {
                sortedSnapshots.Add(result.Data);
            }
        }
        
        return sortedSnapshots;
    }


    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>> AddSnapshotAsync(
        TradePairMarketDataSnapshotGrainDto snapshotDto)
    {
        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        var updateSnapshotResult = await snapshotGrain.AddAsync(snapshotDto);

        // add snapshot
        if (!State.MarketDataSnapshotGrainIds.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(new Tuple<DateTime, string>(snapshotDto.Timestamp, snapshotGrain.GetPrimaryKeyString()));
            State.MarketDataSnapshotGrainIds.Add(snapshotGrain.GetPrimaryKeyString());
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


    public async Task<GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>>
        AddOrUpdateSnapshotAsync(TradePairMarketDataSnapshotGrainDto snapshotDto)
    {
        _logger.LogInformation(
            $"AddSnapshot lp token {snapshotDto.TradePairId}, totalSupply: {snapshotDto.TotalSupply}");

        var snapshotGrain = _clusterClient.GetGrain<ITradePairMarketDataSnapshotGrain>(
            GrainIdHelper.GenerateGrainId(snapshotDto.ChainId, snapshotDto.TradePairId, snapshotDto.Timestamp));
        
        // update snapshot grain
        var latestBeforeDto = await GetLatestBeforeSnapshotAsync(snapshotDto.Timestamp);
        var updateSnapshotResult = await snapshotGrain.UpdateAsync(snapshotDto, latestBeforeDto);

        // add snapshot
        if (!State.MarketDataSnapshotGrainIds.Contains(snapshotGrain.GetPrimaryKeyString()))
        {
            _previous7DaysMarketDataSnapshots.Add(new Tuple<DateTime, string>(snapshotDto.Timestamp, snapshotGrain.GetPrimaryKeyString()));
            State.MarketDataSnapshotGrainIds.Add(snapshotGrain.GetPrimaryKeyString());
        }
        
        // nie:The current snapshot is not up-to-date. The latest snapshot needs to update TotalSupply 
        var latestSnapshot = await GetLatestSnapshotAsync();
        if (latestSnapshot != null && snapshotDto.Timestamp < latestSnapshot.Timestamp)
        {
            var updateLatestResult =  await AddOrUpdateSnapshotAsync(latestSnapshot);
            // update the latest snapshot BUT return current snapshot result and the latest trade pair
            return new GrainResultDto<Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>>
            {
                Success = true,
                Data = new Tuple<TradePairGrainDto, TradePairMarketDataSnapshotGrainDto>(
                    updateLatestResult.Data.Item1,
                    updateSnapshotResult.Data)
            };
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
        
        var priceUSD0 = await _tokenPriceProvider.GetPriceAsync(State.Token0.Symbol);
        var priceUSD1 = await _tokenPriceProvider.GetPriceAsync(State.Token1.Symbol);
        
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
            State.Token0.Symbol, State.Token1.Symbol, State.FeeRate, State.Price, State.PriceUSD, State.Token1.Symbol,
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

    public async Task<GrainResultDto<TradePairGrainDto>> AddOrUpdateAsync(TradePairGrainDto dto)
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