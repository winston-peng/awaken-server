using System;
using System.Text;
using System.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Volo.Abp.Caching;
using IDatabase = StackExchange.Redis.IDatabase;

namespace AwakenServer.Trade;

public interface IDistributedLockService
{
    void  Lock(string key, string value, TimeSpan expirationTime);
    void  TryLock(string key, string value, TimeSpan expirationTime);
    IDatabase GetDatabase();
}

public class DistributedLockService : IDistributedLockService
{
    private readonly IDatabase _database;
    public DistributedLockService(IDatabase database)
    {
        _database = database;
    }
   
    public IDatabase GetDatabase()
    {
        return _database;
    }
        
    public void Lock(string key, string value, TimeSpan expirationTime)
    {
        var redisDistributedLock = new RedisDistributedLock(key, _database); 
        using (redisDistributedLock.Acquire())
        {
            //持有锁
        } 
    }


    public void TryLock(string key, string value, TimeSpan expirationTime)
    {
        var redisDistributedLock = new RedisDistributedLock(key, _database); 
        using (var handle = redisDistributedLock.TryAcquire())
        {
            if (handle != null)
            {
                Console.WriteLine("get lock--------------");

                Thread.Sleep(2000);
            }
            else
            {
                Console.WriteLine("get lock fail--------------");
                Thread.Sleep(1000);
            }
        }
    }
    // public IDisposable AcquireLock(string lockName, TimeSpan expirationTime)
    // {
    //     var acquired = _distributedCache.Get(lockName) == null;
    //     if (!acquired)
    //     {
    //         throw new Exception("Failed to acquire lock.");
    //     }
    //
    //     var lockValue = Encoding.UTF8.GetBytes("locked");
    //     _distributedCache.Set(lockName, lockValue, new DistributedCacheEntryOptions
    //     {
    //         AbsoluteExpirationRelativeToNow = expirationTime
    //     });
    //
    //     return new DistributedLockRelease(_distributedCache, lockName);
    // }
    //
    // private class DistributedLockRelease : IDisposable
    // {
    //     private readonly IDistributedCache<RedisCacheOptions> _distributedCache;
    //     private readonly string _lockName;
    //
    //     public DistributedLockRelease(IDistributedCache<RedisCacheOptions> distributedCache, string lockName)
    //     {
    //         _distributedCache = distributedCache;
    //         _lockName = lockName;
    //     }
    //
    //     public void Dispose()
    //     {
    //         _distributedCache.Remove(_lockName);
    //     }
    // }
}