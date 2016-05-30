using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureTableStorage
{    
    public class AzureTableStorageAPIException : Exception
    {
        public AzureTableStorageAPIException(string message)
            : base(message)
        {
        }
    }
}
