using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class FilterTests : BaseTests
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

        protected void Template(Func<List<Event>> azureLoader, DateTime date)
        {            
            var azure = azureLoader();
            var local = initialData.Where(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)).ToList();

            Assert.AreNotEqual(0, local.Count);
            Assert.AreEqual(azure.Count, local.Count);
            Assert.AreEqual(azure.Count, azure.Join(local, x => x.RowKey, x => x.RowKey, (a, b) => a).Count());
        }

        [TestMethod]
        public void GetEntitiesWithFilter()
        {
            var date = DateTime.Now.AddDays(-500);
            Template(() => service.GetEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)), date);            
        }

        [TestMethod]
        public void GetBigDataEntitiesWithFilter()
        {
            var date = DateTime.Now.AddDays(-500);
            Template(() => service.GetBigDataEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)), date);
        }
    }
}
