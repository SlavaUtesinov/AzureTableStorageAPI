using AzureTableStorageService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{    
    public abstract class BaseTests
    {
        protected static IAzureStorageTableService service { get; set; } = new AzureStorageTableService();
        protected static List<Event> initialData { get; set; } = new List<Event>();

        protected List<Event> GenerateData(int count = 1000)
        {
            var data = new List<Event>();
            var types = new List<string> { "Political", "Social", "Nature" };
            for (var i = 0; i < count; i++)
            {
                var guid = Guid.NewGuid();
                var type = types[i % 3];
                data.Add(new Event { RowKey = guid.ToString(), PartitionKey = type, Code = guid, DateTime = DateTime.Now.AddDays(-i), NumberOfParticipants = i * 1000, Positive = i % 3 == 0, Сost = (i * 1000.0) / 3, Description = type + i });
            }
            return data;
        }
        
        public virtual void Initialize()
        {
            initialData = GenerateData();
            service.AddEntitiesSequentially(initialData);
        }
        
        public virtual void Cleanup()
        {
            service.DeleteTable("EventTable");
        }
    }
}
