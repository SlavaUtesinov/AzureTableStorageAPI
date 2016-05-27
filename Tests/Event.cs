using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    public class Event : TableEntity
    {
        //RowKey  - GUID
        //PartitionKey - Type of event        

        public Guid Code { get; set; }
        public int NumberOfParticipants { get; set; }
        public string Description { get; set; }        
        public DateTime DateTime { get; set; }
        public bool Positive { get; set; }
        public double Сost { get; set; }
    }
}
