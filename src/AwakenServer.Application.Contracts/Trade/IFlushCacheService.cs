using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace AwakenServer.Trade;

public interface IFlushCacheService : IApplicationService
{
    Task FlushCache(List<string> cacheKeys);
}