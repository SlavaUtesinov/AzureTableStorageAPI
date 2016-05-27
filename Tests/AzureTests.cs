using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AzureTableStorageService;
using System.Collections.Generic;

namespace Tests
{
    [TestClass]
    public class AzureTests
    {
        static IAzureStorageTableService service { get; set; } = new AzureStorageTableService();
        static List<Event> data { get; set; } = new List<Event>();

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {                        
            var types = new List<string> { "Political", "Social", "Nature" };            
            for (var i = 0; i < 1000; i++)
            {
                var guid = Guid.NewGuid();
                var type = types[i % 3];
                data.Add(new Event { RowKey = guid.ToString(), PartitionKey = type, Code = guid, DateTime = DateTime.Now.AddDays(-i), NumberOfParticipants = i * 1000, Positive = i % 3 == 0, Сost = (i * 10000.0) / 3, Description = type + i });
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            service.DeleteTable("EventTable");
        }

        [TestMethod]
        public void AddEntities()
        {
            service.AddEntitiesSequentially(data);
        }
    }
}
