using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace AwakenServer.Asset;

public class UserAssetInfoDto:PagedResultRequestDto
{
    public List<UserTokenInfo> Iterms { get; set; }
    public int TotalCount { get; set; }
}

public class UserTokenInfo : UserTokenDto
{
    public string Amount { get; set; }
    public string PriceInUsd { get; set; }
}