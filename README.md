# AzureTableStorageAPI

## Summary

This project dedicated to working with Azure Storage Tables. With it's help you can add, read and delete data from it. Most of this operations can be performed parallel and by batches with caching, that provides significant increase of performance. Also when you want to get data, you can specify complicated condition in **Entity Framework** *.Where* style.

## Usage example

    var api = new AzureTableStorageAPI();
    var person = new Person{ RowKey = "1", PartitionKey = "Man", Age = 27, Name = "Tom", Address = "London", PostalCode = 121343 };
            
    api.AddEntity(person);
    //table's name will be "PersonTable", see convention section below
    
    person = api.GetEntity("Man", "1");
    person = api.GetEntities<Person>(x => (x.PartitionKey == "Man" && x.Age > 25) || x.Address == "Moscow" || x.PostalCode == 223545).First();
    
    api.RemoveEntity(person);

Table of contents
-------------------

* [Summary](#summary)
* [Usage example](#usage-example)
* [Conventions](#conventions)
* [Core operations](#core-operations)
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
     * [Delete table](#delete-table)   
* [Tests](#tests)
* [How to install](#how-to-install)
* [License](#license)  
  

## Conventions

There is the only one convention - when you perform any operations, default name of current Azure Storage Table will be concatenation of type(class) name and *"Table"* word, for example, if we have class with name `Person` as shown at [usage example](#usage-example) Azure table's name will be *"PersonTable"*. If you want to specify table name by your own, it is very easy to do: before operation execution, set your custom table name to `TableName` property, but don't forget, that this table's name will be actual for all remaining time and for all operations, to return back to convention just set `null` value to this property.

## Core operations
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
        public double Ñost { get; set; }
    }
And instance of `AzureTableStorageAPI` class:

    var service = new new AzureTableStorageAPI();

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

Also, you can pass to this method timeout(milliseconds) and/or cancellation token, when timeout expired or you cancel corresponding `CancellationTokenSource`, method will return control back:

    var source = new CancellationTokenSource();
    service.AddEntitiesParallel(data, timeout: 5000, token: source.Token);
You can change number of tasks:

    service.AddEntitiesParallel(data, timeout: 5000, token: source.Token, maxNumberOfTasks: 3);

If you want to cancel execution of this operation, you can assign `CancellationToken` property:

    service.CancellationToken = source.Token;
    service.AddEntitiesParallel(data, timeout: 5000);
    source.Cancel();
But, then you should set to this property `null` value, to prevent it's accidentally usage in future operations, logic is the same as in case of [TableName](#conventions) property.    

###Get
####GetEntity

    var item = service.GetEntity<Event>("Political", "1");
where first argument is `PartitionKey` and second is `RowKey`, generic type argument is `Event`.

####GetEntities

    var items = service.GetEntities<Event>();    
 this query will return all entities from table.

####GetBigDataEntities

    var  items = service.GetBigDataEntities<Event>();
Difference between this method and previous one, is that this method gets data from Azure server by portions with the help of `TableContinuationToken`, whereas `GetEntities` method get all items on one time. So, if you expect that, number of items will be very big, recommendation is to use `GetBigDataEntities` method.

####GetDataWithConditions
If you want to get not only the one entity by specifying `PartitionKey` and `RowKey`, or all entities from table, you can specify condition in **Entity Framework** *.Where* style:

    var date = DateTime.Now.AddDays(-10);
    var items = service.GetEntities<Event>(x => (x.PartitionKey == "Political" && !(x.Ñost <= 50000.55) && 200000 < x.NumberOfParticipants) || (x.Positive == true && x.DateTime >= date));

> **Attention:**
> 
>  Unfortunately, you can not create any condition, all what is permissible is simple conditions. You can't write somethig like this: 
> 
>  - x.PartitionKey.Contains("Pol")
>  - x.DateTime >= DateTime.Now.AddDays(-10)
>  - x.Cost <= 123.23 / 34.2
>  - x.Positive == 2 > 3
>  - and so on
> 
> If you want to use complicated query like, shown above, you firstly should to create and assign variables before query execution and then use this variables inside predicate, see usage of date: `var date = DateTime.Now.AddDays(-10)`.

GetBigDataEntities method also supports predicate usage.

###Remove
####RemoveEntity

    service.RemoveEntity(item);
    
####  RemoveEntitiesSequentially

    service.RemoveEntitiesSequentially(items);
####RemoveEntitiesParallel

    var source1 = new CancellationTokenSource();
    var source2 = new CancellationTokenSource();
    
    service.CancellationToken = source1.Token;
    service.RemoveEntitiesParallel(items, timeout: 5000, token: source2.Token, maxNumberOfTasks: 3);

All what was said about [AddEntitiesParallel](#addentitiesparallel): timeout, token, maxNumberOfTasks, CancellationToken is fully applicable to RemoveEntitiesParallel method.

> **Note** 
> If you want to delete entities, that were no initially received from Azure server, you will take a exception,  because of concurrency checking on server side. But `AzureTableStorageAPI` will check all entities, that you intent to remove and, if it needed,  will reload some of them before execution of remove operation, so you shouldn't worry about this situation.

###Delete table

    service.DeleteTable("EventTable");

##Tests

This repository consists of two projects: core project and Tests project. Tests contains a lot of examples, that show how to use core project assembly, most of them are shown at this documentation. I used [Azure Storage Emulator](https://azure.microsoft.com/en-us/documentation/articles/storage-use-emulator/). It redirects all queries to your local database. Connection string at this case, as you can read, is constant and presented at App.config file. Sure you can use your own  connection string.

##How to install

With the help of [Nuget](https://www.nuget.org/):

    PM> Install-Package Azure.TableStorage.API

##License
The MIT License (MIT)

Copyright (c) 2016 Slava Utesinov

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.