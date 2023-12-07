// // using System.Collections.Generic;
// //
// // namespace AwakenServer.Trade;
// //
// // public interface IEventConcurrenceManager<T>
// // {
// //     void Push(T item);
// //     void Lock();
// //     void UnLock();
// // }
// //
// // public class  EventConcurrenceManager<T> : IEventConcurrenceManager<T>
// // {
// //     private readonly List<T> _queue = new();
// //
// //     public void Push(T item)
// //     {
// //         
// //         _queue.Enqueue(item);
// //         _semaphore.Release();
// //     }
// //
// //     public void Lock()
// //     {
// //         _semaphore.Wait();
// //     }
// //
// //     public void UnLock()
// //     {
// //         _semaphore.Release();
// //     }
// // }
//
// using System;
// using System.Collections.Generic;
// using System.Threading;
//
// public interface IStorage<T>
// {
//     void AddItem(T item);
//     void ClearItems();
// }
//
// public class LocalStorage<T> : IStorage<T>
// {
//     private List<T> itemList;
//     private int maxCount;
//     private TimeSpan maxTime;
//
//     private readonly object lockObject = new object();
//
//     public LocalStorage(int maxCount, TimeSpan maxTime)
//     {
//         this.itemList = new List<T>();
//         this.maxCount = maxCount;
//         this.maxTime = maxTime;
//     }
//
//     public void AddItem(T item)
//     {
//         lock (lockObject)
//         {
//             itemList.Add(item);
//
//             if (itemList.Count >= maxCount || DateTime.Now - itemList[0].AddTime >= maxTime)
//             {
//                 ClearItems();
//             }
//         }
//     }
//
//     public void ClearItems()
//     {
//         // 清空List，处理逻辑根据实际需求编写
//         itemList.Clear();
//     }
// }
//
// public class RedisStorage<T> : IStorage<T>
// {
//     // Redis存储相关的实现代码
//     // ...
//
//     public void AddItem(T item)
//     {
//         // Redis存储相关的添加元素逻辑
//         // ...
//     }
//
//     public void ClearItems()
//     {
//         // Redis存储相关的清空元素逻辑
//         // ...
//     }
// }
//
// public class Manager<T>
// {
//     private IStorage<T> storage;
//
//     public Manager(IStorage<T> storage)
//     {
//         this.storage = storage;
//     }
//
//     public void AddItem(T item)
//     {
//         storage.AddItem(item);
//     }
// }
//
// public class Item<T>
// {
//     public T Value { get; set; }
//     public DateTime AddTime { get; set; }
//
//     public Item(T value)
//     {
//         Value = value;
//         AddTime = DateTime.Now;
//     }
// }
//
// public class Program
// {
//     public static void Main(string[] args)
//     {
//         IStorage<Item<int>> storage = new LocalStorage<Item<int>>(5, TimeSpan.FromSeconds(10));
//         Manager<Item<int>> manager = new Manager<Item<int>>(storage);
//
//         // 启动多个线程并发添加元素到管理器中
//         for (int i = 0; i < 10; i++)
//         {
//             int value = i;
//             Thread thread = new Thread(() =>
//             {
//                 manager.AddItem(new Item<int>(value));
//                 Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Added item {value}");
//             });
//             thread.Start();
//         }
//
//         // 等待所有线程执行完毕
//         Thread.Sleep(1000);
//
//         Console.ReadLine();
//     }
// }