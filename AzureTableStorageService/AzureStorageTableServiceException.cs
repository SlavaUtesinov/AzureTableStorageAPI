using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureTableStorageService
{    
    public class AzureStorageTableServiceException : Exception
    {
        public AzureStorageTableServiceException(string message)
            : base(message)
        {
        }
    }
}
