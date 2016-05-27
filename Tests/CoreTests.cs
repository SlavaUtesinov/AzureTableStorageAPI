using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AzureTableStorageService;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Tests
{
    [TestClass]
    public class CoreTests : BaseTests
    {
        [TestInitialize]
        public override void Initialize()
        {
            base.Initialize();            
        }

        [TestCleanup]
        public override void Cleanup()
        {
            base.Cleanup();
        }

        [TestMethod]
        public void AddEntity()
        {
            var guid = Guid.NewGuid();
            service.AddEntity(new Event { RowKey = guid.ToString(), PartitionKey = "Political", DateTime = DateTime.Now });
            Assert.IsNotNull(service.GetEntity<Event>("Political", guid.ToString()));
        }

        [TestMethod]
        public void AddEntitiesSequentially()
        {
            Assert.AreEqual(initialData.Count, service.GetEntities<Event>().Count);
        }

        [TestMethod]
        public void AddEntitiesParallel()
        {
            service.AddEntitiesParallel(GenerateData(initialData.Count * 10));
            Assert.AreEqual(initialData.Count * 11, service.GetEntities<Event>().Count);
        }

        [TestMethod]
        public void GetBigDataEntities()
        {            
            Assert.AreEqual(initialData.Count, service.GetBigDataEntities<Event>().Count);
        }

        [TestMethod]
        public void RemoveEntity()
        {
            service.RemoveEntity(initialData.First());
            Assert.IsNull(service.GetEntity<Event>(initialData.First().PartitionKey, initialData.First().RowKey));
        }

        [TestMethod]
        public void RemoveEntitiesSequentially()
        {
            service.RemoveEntitiesSequentially(initialData.Where(x => x.PartitionKey == "Political").ToList());
            Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
        }

        [TestMethod]        
        public void RemoveEntitiesParallel()
        {
            service.RemoveEntitiesParallel(initialData.Where(x => x.PartitionKey == "Political").ToList());
            Assert.AreEqual(0, service.GetEntities<Event>(x => x.PartitionKey == "Political").Count);
        }
    }
}
