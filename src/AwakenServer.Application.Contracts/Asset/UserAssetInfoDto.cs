using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace AwakenServer.Asset;

public class UserAssetInfoDto
{
    public List<UserTokenInfo> Items { get; set; }
}

public class UserTokenInfo : UserTokenDto
{
    public string Amount { get; set; }
    public string PriceInUsd { get; set; }
}