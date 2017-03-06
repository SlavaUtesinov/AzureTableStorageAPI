using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class FilterTests : BaseTests
    {
        protected void Template(Func<List<Event>> azureLoader, DateTime date)
        {
            LockWrapper(() => 
            {
                List<Event> azure = null;
                for (var i = 0;  i < 3; i++)
                {
                    try
                    {
                        azure = azureLoader();
                        break;
                    }
                    catch(StorageException)
                    {
                        if (i == 2)
                            throw;
                        else
                            Thread.Sleep(2000);
                    }
                }                
                var local = initialData.Where(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)).ToList();

                Assert.AreNotEqual(0, local.Count);
                Assert.AreEqual(azure.Count, local.Count);
                Assert.AreEqual(azure.Count, azure.Join(local, x => x.RowKey, x => x.RowKey, (a, b) => a).Count());
            });
        }

        [TestMethod]
        [TestCategory("Filter")]
        public void GetEntitiesWithFilter()
        {
            var date = DateTime.Now.AddDays(-500);
            Template(() => service.GetEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)), date);            
        }

        [TestMethod]
        [TestCategory("Filter")]
        public void GetBigDataEntitiesWithFilter()
        {
            var date = DateTime.Now.AddDays(-500);
            Template(() => service.GetBigDataEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Сost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date)), date);
        }
    }
}
