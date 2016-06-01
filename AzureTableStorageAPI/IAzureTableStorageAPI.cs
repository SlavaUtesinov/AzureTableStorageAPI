using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AzureTableStorage
{
    public interface IAzureTableStorageAPI
    {
        string TableName { get; set; }
        CancellationToken CancellationToken { get; set; }

        bool DeleteTable(string name);

        void AddEntity(TableEntity entity);
        void AddEntitiesSequentially<T>(List<T> entities) where T : TableEntity;
        bool AddEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4)
            where T : TableEntity;

        void RemoveEntity(TableEntity entity);
        void RemoveEntitiesSequentially<T>(List<T> entities) where T : TableEntity;
        bool RemoveEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) 
            where T : TableEntity;

        void UpdateEntity(TableEntity entity);
        void UpdateEntitiesSequentially<T>(List<T> entities) where T : TableEntity;
        bool UpdateEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4)
            where T : TableEntity;

        void DoOperation(TableEntity entity, Func<TableEntity, TableOperation> operation);
        void DoOperationsSequentially<T>(List<T> entities, Func<TableEntity, TableOperation> operation) where T : TableEntity;
        bool DoOperationsParallel<T>(List<T> entities, Func<TableEntity, TableOperation> operation, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4)
            where T : TableEntity;

        T GetEntity<T>(string PartitionKey, string RowKey) where T : TableEntity;
        List<T> GetEntities<T>(Expression<Func<T, bool>> predicate = null) where T : TableEntity, new();
        List<T> GetBigDataEntities<T>(Expression<Func<T, bool>> predicate = null) where T : TableEntity, new();
    }
}
