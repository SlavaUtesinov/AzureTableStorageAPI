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
        class AzureTableStorageAPIDisposable : IDisposable
        {
            private Action dispose { get; set; }

            public AzureTableStorageAPIDisposable(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                dispose();
            }
        }

        private string TableName { get; set; }
        private CancellationToken CancellationToken { get; set; }

        public IDisposable SetTableName(string tableName)
        {
            var temp = TableName;
            TableName = tableName;
            return new AzureTableStorageAPIDisposable(() => TableName = temp);
        }

        public IDisposable SetCancellationToken(CancellationToken token)
        {
            var temp = CancellationToken;
            CancellationToken = token;
            return new AzureTableStorageAPIDisposable(() => CancellationToken = temp);
        }                

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
                throw new AzureTableStorageAPIException(string.Format("It is not possible to create and delete table {0} simultaneously.", tableName));

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
                    throw new AzureTableStorageAPIException(string.Format("Table {0} was not created in {1} attempts, likely it is not deleted yet or nuget package WindowsAzure.Storage is not installed.", tableName, attempts));
            }

            var exist = table.Exists();
            if (!exist && !attempt2delete)
                throw new AzureTableStorageAPIException(string.Format("Table {0} should be created at first!", tableName));

            if (exist)
                tables.AddOrUpdate(tableName, table, (key, value) => value);

            return table;
        }

        public bool DeleteTable(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            using (SetTableName(name))
            {
                var res = GetTable(null, attempt2delete: true).DeleteIfExists();
                if (res)
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
                        throw new AzureTableStorageAPIException(string.Format("Failed to delete reference from dictionary on table {0} in {1} attempts.", name, attempts));
                }
                return res;
            }                            
        }


        #region DoOperation
        public void DoOperation(TableEntity entity, Func<TableEntity, TableOperation> operation)
        {
            if (entity == null)
                return;

            var originType = entity.GetType();            

            if (!string.IsNullOrEmpty(entity.ETag))
                GetTable(originType).Execute(operation(entity));
            else
            {
                _GetEntity<TableEntity>(entity.PartitionKey, entity.RowKey, originType);
                GetTable(originType).Execute(operation(entity));
            }
        }

        public void DoOperationsSequentially<T>(List<T> entities, Func<TableEntity, TableOperation> operation) where T : TableEntity
        {            
            if(!(new Func<TableEntity, TableOperation>[] { TableOperation.Insert, TableOperation.InsertOrMerge, TableOperation.InsertOrReplace }.Contains(operation)))
                foreach (var item in entities.Where(x => x.ETag == null))
                    item.ETag = _GetEntity<T>(item.PartitionKey, item.RowKey).ETag;

            BatchOperationPortion(entities, operation);
        }

        public bool DoOperationsParallel<T>(List<T> entities, Func<TableEntity, TableOperation> operation, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {
            return ProcessEntitiesParallel(entities, (x) => DoOperationsSequentially(x, operation), timeout, token, maxNumberOfTasks);
        }
        #endregion

        #region Get
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
        #endregion

        #region Private
        private TableQuery<T> PrepareTableQuery<T>(Expression<Func<T, bool>> predicate)
            where T : TableEntity
        {            
            return predicate != null ? new TableQuery<T>().Where(predicate.GetAzureCondition()) : new TableQuery<T>();
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
            var pages = desiredPages > maxNumberOfTasks ? maxNumberOfTasks : desiredPages;

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
        #endregion

        #region Remove
        public void RemoveEntity(TableEntity entity)
        {
            DoOperation(entity, TableOperation.Delete);
        }

        public void RemoveEntitiesSequentially<T>(List<T> entities) where T : TableEntity
        {
            DoOperationsSequentially(entities, TableOperation.Delete);
        }

        public bool RemoveEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {            
            return ProcessEntitiesParallel(entities, RemoveEntitiesSequentially, timeout, token, maxNumberOfTasks);
        }
        #endregion

        #region Add
        public void AddEntity(TableEntity entity)
        {
            if (entity == null)
                return;

            var insertOperation = TableOperation.Insert(entity);
            GetTable(entity.GetType(), true).Execute(insertOperation);
        }

        public void AddEntitiesSequentially<T>(List<T> entities) where T : TableEntity
        {
            BatchOperationPortion(entities, TableOperation.Insert);
        }

        public bool AddEntitiesParallel<T>(List<T> entities, int? timeout = null, CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {
            return ProcessEntitiesParallel(entities, AddEntitiesSequentially, timeout, token, maxNumberOfTasks);
        }
        #endregion

        #region Update
        public void UpdateEntity(TableEntity entity)
        {
            DoOperation(entity, TableOperation.Replace);
        }

        public void UpdateEntitiesSequentially<T>(List<T> entities) where T : TableEntity
        {
            DoOperationsSequentially(entities, TableOperation.Replace);
        }

        public bool UpdateEntitiesParallel<T>(List<T> entities, int? timeout = default(int?), CancellationToken token = default(CancellationToken), int maxNumberOfTasks = 4) where T : TableEntity
        {
            return ProcessEntitiesParallel(entities, UpdateEntitiesSequentially, timeout, token, maxNumberOfTasks);
        }
        #endregion
    }
}
