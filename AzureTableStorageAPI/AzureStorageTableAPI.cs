//#define SAVEPORTIONS

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AzureTableStorage
{    
    public class AzureTableStorageAPI : IAzureTableStorageAPI
    {
        public string TableName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        //https://msdn.microsoft.com/en-us/library/azure/dd894038.aspx
        private const int maxPackSize = 100;        
        private static ConcurrentDictionary<string, CloudTable> tables = new ConcurrentDictionary<string, CloudTable>();
        private static Random random = new Random();

        private CloudTable GetTable(Type target, bool CreateIfNotExists = false, bool attempt2delete = false)
        {            
            CloudTable table;
            CloudTable table2;
            var tableName = TableName ?? target.Name.Replace("_", "") + "Table";
            if (CreateIfNotExists && attempt2delete)
                throw new AzureTableStorageAPIException($"It is not possible to create and delete table {tableName} simultaneously.");

            if (tables.TryGetValue(tableName, out table))
                return table;

            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();            
            table = tableClient.GetTableReference(tableName);
            
            if (CreateIfNotExists)
            {
                const int attempts = 10;
                var counter = 0;
                do
                {
                    try
                    {
                        counter++;                        
                        if (!table.Exists())
                            table.CreateIfNotExists();
                        break;
                    }
                    catch (StorageException)
                    {
                        if (tables.TryGetValue(tableName, out table2))
                            return table2;
                        Thread.Sleep(3000 + random.Next(0, 3000));
                    }
                } while (counter < attempts);
                if (counter == attempts)
                    throw new AzureTableStorageAPIException($"Table {tableName} was not created {attempts} attempts, likely it is not deleted yet or nuget package WindowsAzure.Storage is not installed.");
            }

            var exist = table.Exists();
            if (!exist && !attempt2delete)
                throw new AzureTableStorageAPIException($"Table {tableName} should be created at first!");

            if (exist)
                tables.AddOrUpdate(tableName, table, (key, value) => value);

            return table;
        }

        public bool DeleteTable(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            TableName = name;
            var res = GetTable(null, attempt2delete: true).DeleteIfExists();
            if(res)
            {
                CloudTable table;
                var counter = 0;
                const int attempts = 10;
                do
                {
                    if (tables.TryRemove(name, out table))
                        break;
                    Thread.Sleep(2000 + random.Next(0, 3000));
                } while (counter < attempts);
                if (counter == attempts)
                    throw new AzureTableStorageAPIException($"Failed to delete reference from dictionary on table {name} {attempts} attempts.");
            }                       

            return res;
        }

        public void AddEntity(TableEntity entity)
        {
            if (entity == null)
                return;

            var insertOperation = TableOperation.Insert(entity);            
            GetTable(entity.GetType(), true).Execute(insertOperation);
        }

        public void RemoveEntity(TableEntity entity)
        {
            if (entity == null)
                return;

            var originType = entity.GetType();
            Action<TableEntity> func = (item) =>
            {
                var deleteOperation = TableOperation.Delete(item);
                GetTable(originType).Execute(deleteOperation);
            };

            if (!string.IsNullOrEmpty(entity.ETag))
                func(entity);
            else
            {
                var deleteEntity = _GetEntity<TableEntity>(entity.PartitionKey, entity.RowKey, originType);
                func(deleteEntity);
            }           
        }

        public T GetEntity<T>(string PartitionKey, string RowKey) where T : TableEntity
        {
            return _GetEntity<T>(PartitionKey, RowKey);
        }

        private T _GetEntity<T>(string PartitionKey, string RowKey, Type Type4Remove = null) where T : TableEntity
        {
            if (string.IsNullOrEmpty(PartitionKey) || string.IsNullOrEmpty(RowKey))
                return null;

            var retrieveOperation = TableOperation.Retrieve<T>(PartitionKey, RowKey);
            var retrievedResult = GetTable(Type4Remove == null ? typeof(T) : Type4Remove).Execute(retrieveOperation);
            return (T)retrievedResult.Result;            
        }        
        
        public List<T> GetEntities<T>(Expression<Func<T, bool>> predicate = null) where T : TableEntity, new()
        {
            return GetTable(typeof(T)).ExecuteQuery(PrepareTableQuery(predicate)).ToList();
        }

        private TableQuery<T> PrepareTableQuery<T>(Expression<Func<T, bool>> predicate)
            where T : class
        {
            var tableQuery = new TableQuery<T>();

            if (predicate != null)            
                tableQuery = tableQuery.Where(predicate.GetAzureCondition());            

            return tableQuery;
        }                

        private void BatchOperationPortion<T>(List<T> entities, Func<T, TableOperation> operation) where T : TableEntity
        {
            foreach (var items in entities.GroupBy(x => x.PartitionKey))
                BatchOperationPortionAtom(items.ToList(), operation);
        }

        private void BatchOperationPortionAtom<T>(List<T> entities, Func<T, TableOperation> operation) where T : TableEntity
        {
            if (entities == null || entities.Count == 0)
                return;

            var pages = entities.Count / maxPackSize + (entities.Count % maxPackSize == 0 ?  0 : 1);

#if (DEBUG && SAVEPORTIONS)
            var portions = new List<List<TableEntity>>();
#endif
            for (var i = 0; i < pages; i++)
            {
#if (DEBUG && SAVEPORTIONS)
                var portion = entities.OrderBy(x => x.RowKey).Skip(i * maxPackSize);
                portions.Add((i == pages - 1 ? portion : portion.Take(maxPackSize)).ToList());
                BatchOperationAtom(portions.Last(), operation);
#else
                var portion = entities.Skip(i * maxPackSize);
                BatchOperationAtom((i == pages - 1 ? portion : portion.Take(maxPackSize)).ToList(), operation);
#endif
            }
        }

        private void BatchOperationAtom<T>(List<T> entities, Func<T, TableOperation> operation) where T : TableEntity
        {
            if (CancellationToken != null)
                CancellationToken.ThrowIfCancellationRequested();

            var batchOperation = new TableBatchOperation();
            entities.ForEach(x => batchOperation.Add(operation(x)));
            GetTable(entities.First().GetType(), true).ExecuteBatch(batchOperation);
        }        

        private bool ProcessEntitiesParallel<T>(List<T> entities, Action<List<T>> action, int? timeout, CancellationToken token, int maxNumberOfTasks) where T : TableEntity
        {
            if (entities == null || entities.Count == 0)
                return false;

            var desiredPages = entities.Count / maxPackSize + (entities.Count % maxPackSize == 0 ? 0 : 1);
            var pages = desiredPages > 5 ? 5 : desiredPages;

            var size = entities.Count / pages;
            var tasks = new Task[pages];

#if (DEBUG && SAVEPORTIONS)
            var portions = new List<List<TableEntity>>();
#endif
            for (var i = 0; i < pages; i++)
            {
#if (DEBUG && SAVEPORTIONS)
                var portion = entities.OrderBy(x => x.RowKey).Skip(i * size);
                portions.Add((i == pages - 1 ? portion : portion.Take(size)).ToList());
                tasks[i] = Task.Factory.StartNew((items) =>
                {
                    action(items as List<TableEntity>);
                }, portions.Last());
#else
                var portion = entities.Skip(i * size);
                tasks[i] = Task.Factory.StartNew((items) => {
                    action(items as List<T>);
                }, (i == pages - 1 ? portion : portion.Take(size)).ToList());
#endif
            }

            return Task.WaitAll(tasks, timeout ?? int.MaxValue, token);
        }

        public bool RemoveEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {            
            return ProcessEntitiesParallel(entities, RemoveEntitiesSequentially, timeout, token, maxNumberOfTasks);
        }

        public bool AddEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {            
            return ProcessEntitiesParallel(entities, AddEntitiesSequentially, timeout, token, maxNumberOfTasks);
        }

        public void AddEntitiesSequentially<T>(List<T> entities) where T : TableEntity
        {
            BatchOperationPortion(entities, (x) => TableOperation.Insert(x));
        }

        public void RemoveEntitiesSequentially<T>(List<T> entities) where T : TableEntity
        {
            foreach (var item in entities.Where(x => x.ETag == null))
                item.ETag = _GetEntity<T>(item.PartitionKey, item.RowKey).ETag;

            BatchOperationPortion(entities, (x) => TableOperation.Delete(x));
        }

        public List<T> GetBigDataEntities<T>(Expression<Func<T, bool>> predicate = null) where T : TableEntity, new()
        {
            TableContinuationToken token = null;
            var result = new List<T>();
            var table = GetTable(typeof(T));
            var condition = PrepareTableQuery(predicate);
            do
            {
                var answer = table.ExecuteQuerySegmented(condition, token);
                result.AddRange(answer);
                token = answer.ContinuationToken;
                
            } while (token != null);
            return result;
        }
    }
}
