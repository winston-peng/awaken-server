using System;
using System.Threading.Tasks;
using AwakenServer.Grains.Grain.Tokens.TokenPrice;
using Nethereum.Util;
using Xunit.Sdk;

namespace AwakenServer.Applications.GameOfTrust
{
    public class MockTokenPriceProvider : ITokenPriceProvider
    {
        public Task<decimal> GetPriceAsync(string symbol)
        {
            return Task.FromResult(1m);
        }

        public Task<decimal> GetHistoryPriceAsync(string symbol, string dateTime)
        {
            return Task.FromResult(1m);
        }
    }
}

