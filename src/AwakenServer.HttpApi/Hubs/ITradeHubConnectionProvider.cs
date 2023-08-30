using System;
using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace AwakenServer.Hubs
{
    public interface ITradeHubConnectionProvider
    {
        void AddUserConnection(string chainId, Guid tradePairId, string address, long timestamp, string connectionId);
        void AddRemovedUserConnection(string chainId, Guid tradePairId, string address, string connectionId);
        string GetUserConnection(string chainId, Guid tradePairId, string address, long timestamp);
        string GetRemovedUserConnection(string chainId, Guid tradePairId, string address);
        void ClearUserConnection(string chainId, Guid tradePairId, string address, long timestamp, string connectionId);
        void ClearRemovedUserConnection(string chainId, Guid tradePairId, string address, string connectionId);
        void ClearUserConnection(string connectionId);
        void ClearRemovedUserConnection(string connectionId);
    }
    
    public class TradeHubConnectionProvider : ITradeHubConnectionProvider, ISingletonDependency
    {
        private readonly ConcurrentDictionary<string, string> _userConnections = new();
        private readonly ConcurrentDictionary<string, string> _userConnectionIds = new();

        public void AddUserConnection(string chainId, Guid tradePairId, string address, long timestamp, string connectionId)
        {
            var key = GetKey(chainId, tradePairId, address, timestamp);
            if (_userConnectionIds.TryAdd($"{connectionId}-0", key))
            {
                _userConnections.TryAdd(key, $"{connectionId}-0");
            }
        }
        
        public void AddRemovedUserConnection(string chainId, Guid tradePairId, string address, string connectionId)
        {
            var key = GetKey(chainId, tradePairId, address);
            if (_userConnectionIds.TryAdd(connectionId, key))
            {
                _userConnections.TryAdd(key, connectionId);
            }
        }

        public string GetUserConnection(string chainId, Guid tradePairId, string address, long timestamp)
        {
            var key = GetKey(chainId, tradePairId, address, timestamp);
            _userConnections.TryGetValue(key, out var value);
            return value?.Substring(0, value.Length - 2);
        }
        
        public string GetRemovedUserConnection(string chainId, Guid tradePairId, string address)
        {
            var key = GetKey(chainId, tradePairId, address);
            _userConnections.TryGetValue(key, out var value);
            return value;
        }

        public void ClearUserConnection(string chainId, Guid tradePairId, string address, long timestamp, string connectionId)
        {
            var key = GetKey(chainId, tradePairId, address, timestamp);
            _userConnections.TryRemove(key, out _);
            _userConnectionIds.TryRemove($"{connectionId}-0", out _);
        }
        
        public void ClearRemovedUserConnection(string chainId, Guid tradePairId, string address, string connectionId)
        {
            var key = GetKey(chainId, tradePairId, address);
            _userConnections.TryRemove(key, out _);
            _userConnectionIds.TryRemove(connectionId, out _);
        }
        
        public void ClearUserConnection(string connectionId)
        {
            if (_userConnectionIds.TryRemove($"{connectionId}-0", out var key))
            {
                _userConnections.TryRemove(key, out _);
            }
        }
        
        public void ClearRemovedUserConnection(string connectionId)
        {
            if (_userConnectionIds.TryRemove(connectionId, out var key))
            {
                _userConnections.TryRemove(key, out _);
            }
        }

        private string GetKey(string chainId, Guid tradePairId, string address, long timestamp)
        {
            return $"{chainId}{tradePairId}{address}-{timestamp}";
        }
        
        private string GetKey(string chainId, Guid tradePairId, string address)
        {
            return $"{chainId}{tradePairId}{address}";
        }
    }
}