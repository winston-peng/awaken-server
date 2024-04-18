using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace AwakenServer.Grains.Grain.Trade;

public interface IUserLiquidityGrain : IGrainWithStringKey
{
    public Task<GrainResultDto<UserLiquidityGrainDto>> AddOrUpdateAsync(UserLiquidityGrainDto dto);
    
    public Task<GrainResultDto<List<UserLiquidityGrainDto>>> GetAsync();

    public Task<GrainResultDto<UserAssetGrainDto>> GetAssetAsync();
}

public class UserAssetGrainDto
{
    public double AssetUSD { get; set; }
    public double AssetBTC { get; set; }
}