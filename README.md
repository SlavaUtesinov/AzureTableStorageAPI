# AzureTableStorageAPI

## 1. Summary

This project deals with Azure Storage Tables. With the help of it, one can perform almost any permissible operation: add, remove, update, delete and other. Most of these operations can be performed parallel and by batches with implementing of caching strategy, that provides significant reduction of execution time. Also when you want to get data, you can specify enough complicated condition in **Entity Framework .Where** style.

## 2. Usage example

    var service = new AzureTableStorageAPI();
    var person = new Person{ RowKey = "1", PartitionKey = "Man", Age = 27, Name = "Tom", Address = "London", PostalCode = 121343 };
            
    service.AddEntity(person);
    //table's name will be "PersonTable", see convention section below
    
    person = service.GetEntity("Man", "1");
    person = service.GetEntities<Person>(x => (x.PartitionKey == "Man" && x.Age > 25) || x.Address == "Moscow" || x.PostalCode == 223545).First();
    
    service.RemoveEntity(person);

Table of contents
-------------------

1. [Summary](#1-summary)
2. [Usage example](#2-usage-example)
3. [Conventions](#3-conventions)
4. [Main operations](#4-main-operations)
    * [Add](#add)		     	                    
       * [AddEntity](#addentity)
       * [AddEntitiesSequentially](#addentitiessequentially)
       * [AddEntitiesParallel](#addentitiesparallel)
    * [Get](#get)    
       * [GetEntity](#getentity)
       * [GetEntities](#getentities)
       * [GetBigDataEntities](#getbigdataentities)
       * [GetDataWithConditions](#getdatawithconditions)
    * [Remove](#remove)
       * [RemoveEntity](#removeentity)
       * [RemoveEntitiesSequentially](#removeentitiessequentially)
       * [RemoveEntitiesParallel](#removeentitiesparallel)
    * [Update](#update)
       * [UpdateEntity](#updateentity)
       * [UpdateEntitiesSequentially](#updateentitiessequentially)
       * [UpdateEntitiesParallel](#updateentitiesparallel)
    * [Generic solution](#generic-solution)
    * [Delete table](#delete-table)   
5. [Tests](#5-tests)
6. [Notes](#6-notes)
7. [How to install](#7-how-to-install)
8. [License](#8-license)  
  
[back to top](#table-of-contents)
## 3. Conventions

There is the only one convention - when you perform any operation, default name of current Azure Storage Table will be concatenation of type(class) name and *"Table"* word; for example, if we have class with name `Person` as shown at [usage example](#2-usage-example) Azure table's name will be *"PersonTable"*. If you want to specify table name by your own, it is very easy to do: just wrap your code into `using` statement:

    using (service.SetTableName("MyCustomTableName"))
    {
         //your code...
        using (service.SetTableName("MyCustomTableName2"))
        {
             //your code...
        }
    }

> **Note:**
> All the code inside `using` block will use specified table's name for all operations(except [DeleteTable](#delete-table) method), after this block convention will be actual again. But one can use nested `using`s(as shown above): after each `using` table name will be returned to previous state and finally to convention state.

    

[back to top](#table-of-contents)
## 4. Main operations
Let's consider some class, inherited from `TableEntity` type, which we will use at all examples below:

    public class Event : TableEntity
    {
        //RowKey  - GUID
        //PartitionKey - Type of event        
    
        public Guid Code { get; set; }
        public int NumberOfParticipants { get; set; }
        public string Description { get; set; }        
        public DateTime DateTime { get; set; }
        public bool Positive { get; set; }
        public double Cost { get; set; }
    }
And instance of `AzureTableStorageAPI` class:

    var service = new AzureTableStorageAPI();

[back to top](#table-of-contents)
### Add
####AddEntity

    service.AddEntity(new Event { RowKey = guid.ToString(), PartitionKey = "Political", DateTime = DateTime.Now });

####AddEntitiesSequentially

    var data = new List<Event>();
    // data initialization not shown
    service.AddEntitiesSequentially(data);

####AddEntitiesParallel
This method sends data to remote server parallel, with the help of max 4(default value) Tasks.

    service.AddEntitiesParallel(data);

Also, one can pass to this method timeout(milliseconds) and/or cancellation token, when timeout expired or you cancel corresponding `CancellationTokenSource`, method will return control back:

    var source = new CancellationTokenSource();
    service.AddEntitiesParallel(data, timeout: 5000, token: source.Token);
One can change the max number of tasks, which may be used:

    service.AddEntitiesParallel(data, timeout: 5000, token: source.Token, maxNumberOfTasks: 3);

If you want to cancel execution of this operation, you should wrap your code into `using` statement:

    var source = new CancellationTokenSource();
    using (service.SetCancellationToken(source.Token))
    {
         service.AddEntitiesParallel(data, timeout: 5000);
         source.Cancel();
    }
This means, that all code inside this block will share one token, so `using`s content should be as small as it possible and consists of only code that you may attempt to cancel. Also you can use nested `using`s, so after each `using`, cancellation token will be returned to previous state. logic is the same as with table name's changing:(see [convention section](#3-conventions)).

[back to top](#table-of-contents)
###Get
####GetEntity

    var item = service.GetEntity<Event>("Political", "1");
where first argument is `PartitionKey` and second one is `RowKey`, generic type argument is `Event`.

####GetEntities

    var items = service.GetEntities<Event>();    
 this query will return all entities from table.

####GetBigDataEntities

    var  items = service.GetBigDataEntities<Event>();
Difference between this method and previous one, is that this method gets data from Azure server by portions with the help of `TableContinuationToken`, whereas `GetEntities` method get all items on one time. So, if you expect that, number of items will be great, the recommendation is to use `GetBigDataEntities` method.

####GetDataWithConditions
If you want to get not only the one entity by specifying `PartitionKey` and `RowKey`, or all entities from table, you can specify condition in **Entity Framework .Where** style:

    var date = DateTime.Now.AddDays(-10);
    var items = service.GetEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Cost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date));

> **Attention:**
> 
>  Unfortunately, one can not create any condition, only the combinations of simple conditions are permissible. You can't write somethig like this: 
> 
>  - `x.PartitionKey.Contains("Pol")`
>  - `x.DateTime >= DateTime.Now.AddDays(-10)`
>  - `x.Cost <= 123.23 / 34.2`
>  - `x.Positive == 2 > 3`
>  - and so on
> 
> If you want to use complicated query like, shown above, you firstly should create and assign variables before query execution and then use this variables inside predicate, see usage of date: `var date = DateTime.Now.AddDays(-10)`, i.e. each operand must be atomic.

GetBigDataEntities method also supports predicate usage.

[back to top](#table-of-contents)
###Remove
####RemoveEntity

    service.RemoveEntity(item);
    
####  RemoveEntitiesSequentially

    service.RemoveEntitiesSequentially(items);
####RemoveEntitiesParallel

    var source1 = new CancellationTokenSource();
    var source2 = new CancellationTokenSource();
        
    using(service.SetCancellationToken(source1.Token))
    {
        service.RemoveEntitiesParallel(items, timeout: 5000, token: source2.Token, maxNumberOfTasks: 3);
        source1.Cancel();
    }

Anything was said about [AddEntitiesParallel](#addentitiesparallel): timeout, token, maxNumberOfTasks, CancellationToken is fully applicable to RemoveEntitiesParallel method.

> **Note:** 
> If you want to delete entities, that were no initially received from Azure server, you will take a exception, because of concurrency checking on server side. But `AzureTableStorageAPI` will check all entities, that you intent to remove and, if it is needed,  will reload some of them before execution of remove operation, so you shouldn't worry about this situation.

[back to top](#table-of-contents)
###Update
Anything was said about [Remove](#remove) **is fully applicable** to [Update](#update) section, sure, except of meaning of operation.
####UpdateEntity

    service.UpdateEntity(item);

####UpdateEntitiesSequentially    

    service.UpdateEntitiesSequentially(items);
####UpdateEntitiesParallel
    service.UpdateEntitiesParallel(items, timeout: 5000, token: source.Token, maxNumberOfTasks: 3);
[back to top](#table-of-contents)
###Generic solution
As you noticed, [Remove](#remove) and [Update](#update) sections are the same. It is due to common programming algorithms, which are implemented at this methods. So, there is one shared entry point for this methods and it's name starts with *"DoOperation"* prefix. Let's consider some code equivalents:

    service.UpdateEntitiesParallel(items);
    service.DoOperationsParallel(items, TableOperation.Replace);
    
    service.RemoveEntitiesParallel(items);
    service.DoOperationsParallel(items, TableOperation.Delete);
    
    service.UpdateEntitiesSequentially(items);
    service.DoOperationsSequentially(items, TableOperation.Replace);
    
    service.UpdateEntity(item);
    service.DoOperation(item, TableOperation.Replace);
So, one can write any method and result will be the same. Moreover, you can perform custom operations, such as `TableOperation.InsertOrReplace`, `TableOperation.InsertOrMerge` and so on:

    service.DoOperationsSequentially(items, TableOperation.InsertOrReplace);
    service.DoOperation(item, TableOperation.InsertOrReplace);

> **Note:**
> For performance reason, the recommendation is to use *"Add"* prefixed methods instead of combination of corresponding *"DoOperation"* method with  `TableOperation.Insert` argument, passed into it.

[back to top](#table-of-contents)
###Delete table

    service.DeleteTable("EventTable");    

> **Attention:** It is not guaranteed, that after this command, table will be immediately deleted(it is caused by WindowsAzure.Storage API or other independent from this project reasons). So, if you intent to delete table and soon do some operation on this table, it will be a good practice to wait some time after deleting (use `Thread.Sleep`, for example) to ensure that table is completely deleted. Time of actual deleting depends on (as I can assume) table's size: you can think about this as: `deletingTime = Nrecords*k`, where `k` is `const` value.

[back to top](#table-of-contents)
##5. Tests

This repository consists of two projects: main project and tests project. Tests one contains a lot of examples, they show how to use main project assembly, most of them are shown at this documentation. I used [Azure Storage Emulator](https://azure.microsoft.com/en-us/documentation/articles/storage-use-emulator/). It redirects all queries to your local database. Connection string at this case, as you can read, is constant for any users and presented at App.config file. Sure, you can use your own  connection string.

[back to top](#table-of-contents)
##6. Notes
If you will build main project at Release mode, you will have after build error. It is caused by [Nuget publishing stuff](https://docs.nuget.org/create/creating-and-publishing-a-package). To fix this problem just edit AzureTableStorageAPI.csproj file: delete or comment section:

      <Target Name="AfterBuild" Condition=" '$(Configuration)' == 'Release'">
        <Exec Command="nuget pack AzureTableStorageAPI.csproj -Prop Configuration=Release">
        </Exec>
      </Target>


[back to top](#table-of-contents)
##7. How to install

With the help of [Nuget](https://www.nuget.org/packages/Azure.TableStorage.API/):

    PM> Install-Package Azure.TableStorage.API

[back to top](#table-of-contents)
##8. License
The MIT License (MIT)

Copyright (c) 2016 SlavaUtesinov

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE..