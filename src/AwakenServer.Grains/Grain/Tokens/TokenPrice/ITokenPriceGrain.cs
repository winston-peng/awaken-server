using Orleans;

namespace AwakenServer.Grains.Grain.Tokens.TokenPrice;

public interface ITokenPriceGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<TokenPriceGrainDto>> GetCurrentPriceAsync(string symbol);
}