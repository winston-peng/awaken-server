using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AwakenServer.Grains;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using AwakenServer.Trade.Etos;
using MassTransit;
using Orleans;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EventBus.Distributed;
using TradePair = AwakenServer.Trade.Index.TradePair;

namespace AwakenServer.EntityHandler.Trade
{
    public class TradePairIndexHandler : TradeIndexHandlerBase,
        IDistributedEventHandler<EntityCreatedEto<TradePairEto>>
    {
        private readonly INESTRepository<TradePairInfoIndex, Guid> _tradePairInfoIndex;
        private readonly INESTRepository<TradePair, Guid> _tradePairIndexRepository;
        private readonly IBus _bus;
        private readonly IClusterClient _clusterClient;

        public TradePairIndexHandler(INESTRepository<TradePair, Guid> tradePairIndexRepository,
            IBus bus,
            IClusterClient clusterClient)
        {
            _tradePairIndexRepository = tradePairIndexRepository;
            _bus = bus;
            _clusterClient = clusterClient;
        }

        public async Task HandleEventAsync(EntityCreatedEto<TradePairEto> eventData)
        {
            var index = ObjectMapper.Map<TradePairEto, TradePair>(eventData.Entity);
            index.Token0 = await GetTokenAsync(eventData.Entity.Token0Id);
            index.Token1 = await GetTokenAsync(eventData.Entity.Token1Id);

            await _tradePairIndexRepository.AddOrUpdateAsync(index);
            await _bus.Publish<NewIndexEvent<TradePairIndexDto>>(new NewIndexEvent<TradePairIndexDto>
            {
                Data = ObjectMapper.Map<TradePair, TradePairIndexDto>(index)
            });
        }
    }
}