using AElf.Types;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Tokens.TokenPrice;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Trade;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.ObjectMapping;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public class UserLiquidityGrain : Grain<UserLiquidityState>, IUserLiquidityGrain
{
    private readonly IClusterClient _clusterClient;
    private readonly ITokenPriceProvider _tokenPriceProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UserLiquidityGrain> _logger;


    private const string BTCSymbol = "BTC";
    
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

    public UserLiquidityGrain(IClusterClient clusterClient,
        ITokenPriceProvider tokenPriceProvider,
        IObjectMapper objectMapper,
        ILogger<UserLiquidityGrain> logger)
    {
        _clusterClient = clusterClient;
        _tokenPriceProvider = tokenPriceProvider;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<GrainResultDto<UserLiquidityGrainDto>> AddOrUpdateAsync(UserLiquidityGrainDto dto)
    {
        State.TradePairLiquidities ??= new Dictionary<string, Liquidity>();
        if (!State.TradePairLiquidities.ContainsKey(dto.TradePair.Address))
        {
            State.TradePairLiquidities[dto.TradePair.Address] =
                _objectMapper.Map<UserLiquidityGrainDto, Liquidity>(dto);
        }
        else
        {
            if (dto.Type == LiquidityType.Mint)
            {
                State.TradePairLiquidities[dto.TradePair.Address].LpTokenAmount += dto.LpTokenAmount;
            }
            else
            {
                State.TradePairLiquidities[dto.TradePair.Address].LpTokenAmount -= dto.LpTokenAmount;
            }
        }

        return new GrainResultDto<UserLiquidityGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<Liquidity, UserLiquidityGrainDto>(State.TradePairLiquidities[dto.TradePair.Address])
        };
    }
    
    public async Task<GrainResultDto<List<UserLiquidityGrainDto>>> GetAsync()
    {
        var dataList = new List<UserLiquidityGrainDto>();
        foreach (var liquidity in State.TradePairLiquidities)
        {
            var grainDto = new UserLiquidityGrainDto
            {
                Address = liquidity.Value.Address,
                LpTokenAmount = liquidity.Value.LpTokenAmount,
                TradePair = liquidity.Value.TradePair
            };

            var tradePairGrain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(liquidity.Value.TradePair.Id));
            var pairResult = await tradePairGrain.GetAsync();
            var pair = pairResult.Data;
            if (pair == null || pair.TotalSupply == null || pair.TotalSupply == "0")
            {
                continue;
            }

            grainDto.LpTokenAmount = liquidity.Value.LpTokenAmount;

            var prop = pair.TotalSupply == null || pair.TotalSupply == "0"
                ? 0
                : liquidity.Value.LpTokenAmount / double.Parse(pair.TotalSupply);
            
            _logger.LogInformation(
                $"User liquidity GetAsync " +
                $"token0:{pair.Token0Symbol} decimal:{pair.Token0.Decimals}, " +
                $"token1:{pair.Token1Symbol} decimal:{pair.Token1.Decimals}, " +
                $"token0 amount:{pair.ValueLocked0}, token1 amount:{pair.ValueLocked1}");


            grainDto.Token0Amount = pair.Token0.Decimals == 0
                ? Math.Floor(prop / Math.Pow(10, 8) * pair.ValueLocked0).ToString()
                : ((long)(prop * pair.ValueLocked0)).ToDecimalsString(8);
            grainDto.Token1Amount = pair.Token1.Decimals == 0
                ? Math.Floor(prop / Math.Pow(10, 8) * pair.ValueLocked1).ToString()
                : ((long)(prop * pair.ValueLocked1)).ToDecimalsString(8);

            grainDto.AssetUSD = pair.TVL * prop / Math.Pow(10, 8);
            dataList.Add(grainDto);
        }

        return new GrainResultDto<List<UserLiquidityGrainDto>>
        {
            Success = true,
            Data = dataList
        };
    }
    
    public async Task<GrainResultDto<UserAssetGrainDto>> GetAssetAsync()
    {
        
        double asset = 0;
        foreach (var liquidity in State.TradePairLiquidities)
        {
            var tradePairGrain = _clusterClient.GetGrain<ITradePairGrain>(GrainIdHelper.GenerateGrainId(liquidity.Value.TradePair.Id));
            var pairResult = await tradePairGrain.GetAsync();
            var pair = pairResult.Data;
            if (pair == null || pair.TotalSupply == null || pair.TotalSupply == "0")
            {
                continue;
            }

            var totalSupply = double.Parse(pair.TotalSupply);
            asset += pair.TVL * double.Parse(liquidity.Value.LpTokenAmount.ToDecimalsString(8)) / totalSupply;
        }

        var btcPrice = (double)_tokenPriceProvider.GetPriceAsync(BTCSymbol).Result;
        
        _logger.LogInformation($"User liquidity GetAssetAsync asset:{asset}, btcPrice:{btcPrice}");
        
        return new GrainResultDto<UserAssetGrainDto>
        {
            Success = true,
            Data = new UserAssetGrainDto
            {
                AssetUSD = asset,
                AssetBTC = btcPrice == 0 ? 0 : asset / btcPrice
            }
        };
    }
}