using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AzureTableStorage;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;

namespace Tests
{
    [TestClass]
    public class CoreTests : BaseTests
    {
        protected string testTableName { get; } = "TestTable";
        protected string testTableName2 { get; } = "TestTable2";

        public override void Initialize()
        {
            base.Initialize();
        }
        
        public override void Cleanup()
        {            
            service.DeleteTable(testTableName);
            service.DeleteTable(testTableName2);
            base.Cleanup();
        }

        [TestMethod]
        [TestCategory("Add")]
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
        [TestCategory("Add")]
        public void AddEntitiesSequentially()
        {
            LockWrapper(() =>
            {
                Assert.AreEqual(initialData.Count, service.GetEntities<Event>().Count);
            });
        }

        [TestMethod]
        [TestCategory("Add")]
        public void AddEntitiesParallel()
        {
            LockWrapper(() =>
            {
                service.AddEntitiesParallel(GenerateData(initialData.Count * 10));
                Assert.AreEqual(initialData.Count * 11, service.GetEntities<Event>().Count);
            });
        }

        [TestMethod]
        [TestCategory("Add")]
        public void AddEntitiesParallelWithCancellationToken()
        {
            LockWrapper(() =>
            {
                var source = new CancellationTokenSource();
                var source2 = new CancellationTokenSource();
                var remote = new List<Event>();

                using (service.SetCancellationToken(source.Token))
                {
                    var sw = Stopwatch.StartNew();
                    service.AddEntitiesParallel(GenerateData(initialData.Count * 20), timeout: 5000);
                    sw.Stop();
                    source.Cancel();
                    Assert.IsTrue(sw.ElapsedMilliseconds < 5500);

                    using (service.SetCancellationToken(source2.Token))
                    {
                        using (service.SetTableName(testTableName))
                        {
                            service.AddEntitiesParallel(GenerateData(initialData.Count * 20), timeout: 5000);
                            remote = service.GetEntities<Event>();
                            Assert.IsTrue(remote.Count > 0 && remote.Count < initialData.Count * 20);
                        }
                    }
                }                 

                remote = service.GetEntities<Event>();                
                Assert.IsTrue(remote.Count > initialData.Count && remote.Count < initialData.Count * 21);
            });
        }

        [TestMethod]
        [TestCategory("Get")]
        public void GetBigDataEntities()
        {
            LockWrapper(() =>
            {                
                Assert.AreEqual(initialData.Count, service.GetBigDataEntities<Event>().Count);
            });
        }

        [TestMethod]
        [TestCategory("Remove")]
        public void RemoveEntity()
        {
            LockWrapper(() =>
            {
                service.RemoveEntity(initialData.First());
                Assert.IsNull(service.GetEntity<Event>(initialData.First().PartitionKey, initialData.First().RowKey));
            });
        }

        [TestMethod]
        [TestCategory("Remove")]
        public void RemoveEntitiesSequentially()
        {
            LockWrapper(() =>
            {
                service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Political").ToList());
                Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
            });
        }

        [TestMethod]
        [TestCategory("Remove")]
        public void RemoveEntitiesParallel()
        {
            LockWrapper(() =>
            {
                service.RemoveEntitiesParallel(initialData.Where(x => x.PartitionKey == "Political").ToList());
                Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
            });
        }

        [TestMethod]
        [TestCategory("Remove")]
        public void RemoveEntitiesParallelWithCancellationToken()
        {
            LockWrapper(() =>
            {
                var source = new CancellationTokenSource();                

                using (service.SetCancellationToken(source.Token))
                {
                    var sw = Stopwatch.StartNew();
                    service.RemoveEntitiesParallel(initialData.Where(x => x.PartitionKey == "Political").ToList(), 1000);
                    sw.Stop();
                    source.Cancel();
                    Assert.IsTrue(sw.ElapsedMilliseconds < 1500);
                }
                    
                var remote = service.GetEntities<Event>();                
                Assert.IsTrue(remote.Count < initialData.Count && remote.Count > 0);
            });
        }

        private void UpdateTemplate(Action<Event> testMethod)
        {
            LockWrapper(() =>
            {
                var entity = initialData.First();
                var guid = Guid.NewGuid();
                entity.Code = guid;
                testMethod(entity);

                service.GetEntities<Event>(x => x.Code == guid).First();
            });
        }

        [TestMethod]
        [TestCategory("Update")]
        public void UpdateEntity()
        {
            UpdateTemplate(service.UpdateEntity);            
        }

        private void UpdateEntitiesTemplate(Action<List<Event>> testMethod)
        {
            LockWrapper(() =>
            {
                var entities = initialData.Take(100).ToList();
                var guid = Guid.NewGuid();
                var date = DateTime.Now;
                entities.ForEach(x =>
                {
                    x.DateTime = date;
                    x.Code = guid;
                });

                testMethod(entities);

                var remote = service.GetEntities<Event>(x => x.Code == guid && x.DateTime == date);
                Assert.AreEqual(entities.Count, remote.Count);
            });
        }

        [TestMethod]
        [TestCategory("Update")]
        public void UpdateEntitiesSequentially()
        {
            UpdateEntitiesTemplate(service.UpdateEntitiesSequentially);               
        }

        [TestMethod]
        [TestCategory("Update")]
        public void UpdateEntitiesParallel()
        {
            UpdateEntitiesTemplate((x) => {
                service.UpdateEntitiesParallel(x);
            });
        }

        [TestMethod]
        [TestCategory("DoOperations")]
        public void DoOperations()
        {
            lock(lockObject)
            {
                UpdateTemplate((x) => service.DoOperation(x, TableOperation.Replace));

                UpdateEntitiesTemplate((x) =>
                {
                    service.DoOperationsSequentially(x, TableOperation.Replace);
                });

                UpdateEntitiesTemplate((x) =>
                {
                    service.DoOperationsParallel(x, TableOperation.Replace);
                });
            }                
        }

        [TestMethod]
        [TestCategory("DoOperations")]
        public void DoOperationsInsertOrReplace()
        {
            LockWrapper(() =>
            {                
                var existing = initialData.Where(x => x.PartitionKey == "Political").ToList();
                var guid = Guid.NewGuid();
                existing.ForEach(x => x.Code = guid);

                var newOnes = GenerateData(500);
                newOnes.ForEach(x => x.Code = guid);

                var all = existing.Union(newOnes).ToList();

                service.DoOperationsParallel(all, TableOperation.InsertOrReplace);

                var remote = service.GetEntities<Event>(x => x.Code == guid);
                Assert.AreEqual(all.Count, remote.Count);

                Assert.AreEqual(existing.Count, existing.Join(remote, x => x.RowKey, x => x.RowKey, (a, b) => a).Count());
            });
        }

        [TestMethod]
        [TestCategory("Convention")]
        public void ChangeConventionTableName()
        {
            LockWrapper(() =>
            {
                var remote = new List<Event>();

                using (service.SetTableName(testTableName))
                {
                    service.AddEntitiesSequentially(initialData);
                    service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Political").ToList());

                    using (service.SetTableName(testTableName2))
                    {
                        service.AddEntitiesSequentially(initialData);
                        service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Social").ToList());

                        remote = service.GetEntities<Event>();
                        Assert.AreEqual(initialData.Where(x => x.PartitionKey != "Social").Count(), remote.Count);
                    }

                    remote = service.GetEntities<Event>();
                    Assert.AreEqual(initialData.Where(x => x.PartitionKey != "Political").Count(), remote.Count);
                }                    
                                
                service.AddEntitiesSequentially(GenerateData());
                remote = service.GetEntities<Event>();
                Assert.AreEqual(initialData.Count * 2, remote.Count);
            });
        }
    }
}
