using System;
using AutoMapper;
using AwakenServer.Asset;
using AwakenServer.Chains;
using AwakenServer.Favorite;
using AwakenServer.Grains.Grain.Chain;
using AwakenServer.Grains.Grain.Tokens;
using AwakenServer.Grains.Grain.Favorite;
using AwakenServer.Grains.Grain.Price.TradePair;
using AwakenServer.Grains.Grain.Price.TradeRecord;
using AwakenServer.Grains.Grain.Price.UserTradeSummary;
using AwakenServer.Grains.Grain.Trade;
using AwakenServer.Grains.State.Tokens;
using AwakenServer.Tokens;
using AwakenServer.Trade;
using AwakenServer.Trade.Dtos;
using AwakenServer.Trade.Etos;
using AwakenServer.Trade.Index;
using Volo.Abp.AutoMapper;
using KLine = AwakenServer.Trade.Index.KLine;
using Token = AwakenServer.Tokens.Token;
using TradePairMarketDataSnapshot = AwakenServer.Trade.Index.TradePairMarketDataSnapshot;
using TradeRecord = AwakenServer.Trade.TradeRecord;
using UserLiquidity = AwakenServer.Trade.Index.UserLiquidity;

namespace AwakenServer
{
    public class AwakenServerApplicationAutoMapperProfile : Profile
    {
        public AwakenServerApplicationAutoMapperProfile()
        {
            /* You can configure your AutoMapper mapping configuration here.
             * Alternatively, you can split your mapping configurations
             * into multiple profile classes for a better organization. */

            CreateMap<Chain, ChainDto>();
            CreateMap<ChainDto, Chain>();
            CreateMap<ChainCreateDto, Chain>();
            CreateMap<Chain, NewChainEvent>();
            CreateMap<NewChainEvent, Chain>();
            CreateMap<ChainDto, ChainResponseDto>();
            CreateMap<ChainDto, ChainCreateDto>();

            CreateMap<Chain, ChainGrainDto>();
            CreateMap<ChainGrainDto, ChainDto>();

            CreateMap<UserTokenDto, UserTokenInfo>();

            CreateMap<TokenGrainDto, Tokens.TokenDto>();
            CreateMap<TokenGrainDto, Token>();
            CreateMap<TokenGrainDto, NewTokenEvent>();
            CreateMap<TokenCreateDto, TokenState>();
            CreateMap<NewTokenEvent, Token>();
            CreateMap<Tokens.TokenDto, Token>();
            CreateMap<TokenCreateDto, Token>();
            CreateMap<Token, TokenCreateDto>();
            CreateMap<Token, Tokens.TokenDto>();

            CreateMap<TokenCreateDto, Token>().Ignore(x => x.Id);

            CreateMap<TradePairCreateDto, Trade.TradePair>().Ignore(x => x.Id);
            CreateMap<TradeRecordCreateDto, Trade.TradeRecord>().Ignore(x => x.Id).ForMember(
                destination => destination.Timestamp,
                opt => opt.MapFrom(source => DateTimeHelper.FromUnixTimeMilliseconds(source.Timestamp)));
            CreateMap<TradeRecord, TradeRecordGrainDto>();
            CreateMap<TradeRecord, TradeRecordEto>();
            CreateMap<UserTradeSummaryGrainDto, UserTradeSummaryEto>();
            CreateMap<UserTradeSummaryEto, Trade.Index.UserTradeSummary>();
            CreateMap<Trade.TradePair, TradePairDto>();
            CreateMap<Trade.Index.TradePair, TradePairIndexDto>();
            CreateMap<Trade.Index.TradePair, TradePairDto>()
                .ForMember(dest => dest.Token0Symbol, opt => opt.MapFrom(src => src.Token0.Symbol))
                .ForMember(dest => dest.Token1Symbol, opt => opt.MapFrom(src => src.Token1.Symbol))
                .ForMember(dest => dest.Token0Id, opt => opt.MapFrom(src => src.Token0.Id))
                .ForMember(dest => dest.Token1Id, opt => opt.MapFrom(src => src.Token1.Id));
            CreateMap<TradePairEto, Trade.Index.TradePair>().ReverseMap();

            CreateMap<Trade.TradePair, TradePairWithToken>();
            CreateMap<TradePairWithToken, TradePairWithTokenDto>();

            CreateMap<TradePairMarketDataSnapshotEto, TradePairMarketDataSnapshot>();


            CreateMap<TradePairInfoDto, TradePairDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.Parse(src.Id)));
            CreateMap<TradePairInfoDto, Trade.Index.TradePair>().Ignore(x => x.ChainId)
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.Parse(src.Id)));
            
            CreateMap<TradePairInfoIndex, TradePairInfoDto>();
            CreateMap<TradePairInfoIndex, TradePairDto>();
            CreateMap<TradePairInfoIndex, TradePairDto>();
            CreateMap<TradePairInfoIndex, Trade.TradePair>();
            CreateMap<TradePairCreateDto, TradePairInfoIndex>();
            CreateMap<TradePairCreateDto, Trade.Index.TradePair>();
            CreateMap<TradePairDto, Trade.TradePair>();
            CreateMap<GetTradePairByIdsInput, GetTradePairsInput>();

            CreateMap<LiquidityRecordCreateDto, Trade.LiquidityRecord>().Ignore(x => x.Id).ForMember(
                destination => destination.Timestamp,
                opt => opt.MapFrom(source => DateTimeHelper.FromUnixTimeMilliseconds(source.Timestamp)));
            CreateMap<Trade.LiquidityRecord, NewLiquidityRecordEvent>();
            CreateMap<LiquidityRecordEto, Trade.Index.LiquidityRecord>();
            CreateMap<Trade.Index.LiquidityRecord, LiquidityRecordIndexDto>().ForMember(
                destination => destination.Timestamp,
                opt => opt.MapFrom(source => DateTimeHelper.ToUnixTimeMilliseconds(source.Timestamp)));
            CreateMap<GetLiquidityRecordsInput, GetLiquidityRecordIndexInput>();
            CreateMap<UserLiquidityEto, UserLiquidity>();
            CreateMap<LiquidityRecordDto, LiquidityRecordIndexDto>().Ignore(x => x.ChainId);
            CreateMap<UserLiquidityDto, UserLiquidityIndexDto>();
            
            CreateMap<GetUserAssertInput, GetUserLiquidityInput>();
            CreateMap<Trade.Index.UserLiquidity, UserLiquidityIndexDto>();

            CreateMap<Trade.TradeRecord, NewTradeRecordEvent>();
            CreateMap<TradeRecordEto, Trade.Index.TradeRecord>();
            CreateMap<Trade.Index.TradeRecord, TradeRecordIndexDto>().ForMember(
                destination => destination.Timestamp,
                opt => opt.MapFrom(source => DateTimeHelper.ToUnixTimeMilliseconds(source.Timestamp)));
            CreateMap<KLineEto, KLine>();
            CreateMap<Trade.Index.KLine, KLineDto>();
            CreateMap<KLineGrainDto, KLineEto>();
            
            CreateMap<TradePairMarketDataSnapshot, AwakenServer.Trade.TradePairMarketDataSnapshot>();
            CreateMap<AwakenServer.Trade.TradePairMarketDataSnapshot, TradePairMarketDataSnapshotGrainDto>();
            CreateMap<TradePairMarketDataSnapshotGrainDto, TradePairMarketDataSnapshotEto>();

            //Favorite
            CreateMapForFavorite();
        }

        private void CreateMapForFavorite()
        {
            CreateMap<FavoriteCreateDto, FavoriteGrainDto>();
            CreateMap<FavoriteGrainDto, FavoriteDto>();
        }
    }
}