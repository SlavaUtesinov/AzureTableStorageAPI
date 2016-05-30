using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AzureTableStorage;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Diagnostics;

namespace Tests
{
    [TestClass]
    public class CoreTests : BaseTests
    {
        protected string testTableName { get; } = "TestTable";
        
        public override void Initialize()
        {
            base.Initialize();
        }
        
        public override void Cleanup()
        {            
            service.DeleteTable(testTableName);
            base.Cleanup();
        }

        [TestMethod]
        public void AddEntity()
        {
            LockWrapper(() => 
            {
                var guid = Guid.NewGuid();
                service.AddEntity(new Event { RowKey = guid.ToString(), PartitionKey = "Political", DateTime = DateTime.Now });
                Assert.IsNotNull(service.GetEntity<Event>("Political", guid.ToString()));
            });            
        }

        [TestMethod]
        public void AddEntitiesSequentially()
        {
            LockWrapper(() =>
            {
                Assert.AreEqual(initialData.Count, service.GetEntities<Event>().Count);
            });
        }

        [TestMethod]
        public void AddEntitiesParallel()
        {
            LockWrapper(() =>
            {
                service.AddEntitiesParallel(GenerateData(initialData.Count * 10));
                Assert.AreEqual(initialData.Count * 11, service.GetEntities<Event>().Count);
            });
        }

        [TestMethod]        
        public void AddEntitiesParallelWithCancellationToken()
        {
            LockWrapper(() =>
            {
                var source = new CancellationTokenSource();
                service.CancellationToken = source.Token;

                var sw = Stopwatch.StartNew();
                service.AddEntitiesParallel(GenerateData(initialData.Count * 20), timeout: 5000);
                sw.Stop();
                source.Cancel();

                var remote = service.GetEntities<Event>();

                Assert.IsTrue(sw.ElapsedMilliseconds < 5500);
                Assert.IsTrue(remote.Count > initialData.Count && remote.Count < initialData.Count * 21);
            });
        }

        [TestMethod]
        public void GetBigDataEntities()
        {
            LockWrapper(() =>
            {                
                Assert.AreEqual(initialData.Count, service.GetBigDataEntities<Event>().Count);
            });
        }

        [TestMethod]
        public void RemoveEntity()
        {
            LockWrapper(() =>
            {
                service.RemoveEntity(initialData.First());
                Assert.IsNull(service.GetEntity<Event>(initialData.First().PartitionKey, initialData.First().RowKey));
            });
        }

        [TestMethod]
        public void RemoveEntitiesSequentially()
        {
            LockWrapper(() =>
            {
                service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Political").ToList());
                Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
            });
        }

        [TestMethod]
        public void RemoveEntitiesParallel()
        {
            LockWrapper(() =>
            {
                service.RemoveEntitiesParallel(initialData.Where(x => x.PartitionKey == "Political").ToList());
                Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
            });
        }

        [TestMethod]
        public void RemoveEntitiesParallelWithCancellationToken()
        {
            LockWrapper(() =>
            {
                var source = new CancellationTokenSource();
                service.CancellationToken = source.Token;

                var sw = Stopwatch.StartNew();
                service.RemoveEntitiesParallel(initialData.Where(x => x.PartitionKey == "Political").ToList(), 1000);
                sw.Stop();
                source.Cancel();

                var remote = service.GetEntities<Event>();

                Assert.IsTrue(sw.ElapsedMilliseconds < 1500);
                Assert.IsTrue(remote.Count < initialData.Count && remote.Count > 0);
            });
        }

        [TestMethod]
        public void ChangeConventionTableName()
        {
            LockWrapper(() =>
            {
                service.TableName = testTableName;            

                service.AddEntitiesSequentially(initialData);
                service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Political").ToList());

                var remote = service.GetEntities<Event>();
                Assert.AreEqual(initialData.Where(x => x.PartitionKey != "Political").Count(), remote.Count);

                service.TableName = null;
                service.AddEntitiesSequentially(GenerateData());
                remote = service.GetEntities<Event>();
                Assert.AreEqual(initialData.Count * 2, remote.Count);
            });
        }
    }
}
