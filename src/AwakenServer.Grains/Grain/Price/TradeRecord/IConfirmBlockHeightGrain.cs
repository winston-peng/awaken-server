using System.Threading.Tasks;
using Orleans;

namespace AwakenServer.Grains.Grain.Price.TradeRecord;

public interface IConfirmBlockHeightGrain : IGrainWithStringKey
{
    Task<GrainResultDto<long>> InsertAsync(long blockHeight);
    Task<GrainResultDto<long>> GetAsync();
}