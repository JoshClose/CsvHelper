---
title: LINQ
icon: magnifying-glass
description: Query CSV files with any LINQ operator through CsvHelper.Linq, which reads only the columns and rows a query uses, joins with Entity Framework Core and other providers, and writes query results as CSV.
---

# LINQ

The CsvHelper.Linq package is a LINQ provider for CSV. A query over a CSV file can use any LINQ operator, and the
reader runs as much of it as it can itself:

- it reads only the members of the records that the query uses, so the other columns are never parsed;
- it filters records (`Where`) on the members the filter reads, before it reads the rest;
- it skips rows without parsing them, counts them without reading any field, and stops at the first record that
  `First` or `Take` needs.

LINQ to Objects runs the rest of the query on the records the reader returns. Queries of other LINQ providers in a CSV
query, such as tables of Entity Framework Core, run on their own provider, and joins with them ask only for the rows
with the keys of the CSV records.

```csharp
using CsvHelper.Linq;

var orders = CsvQueryable.FromFile<Order>("orders.csv");

var big = orders
    .Where(o => o.Total > 100)                  // reads Total of every record
    .Select(o => new { o.Id, o.Customer })      // reads Id and Customer of the records that pass
    .ToList();

var count = orders.Count();                     // counts the records, reads no field
```

## Install

<!-- if:released -->
```shell
dotnet add package CsvHelper.Linq
```

The package depends on CsvHelper, whose source generator writes the code that reads only some members of a
`[CsvRecord]` type. It writes that code for the record types of projects that reference CsvHelper.Linq. For record
types in a library that does not, add `[assembly: CsvRecordProjections]` to the library.
<!-- endif -->
<!-- if:unreleased -->
CsvHelper.Linq is not on NuGet yet. Build CsvHelper from source and reference the project, with the source generator
as for CsvHelper itself (see [Getting started](getting-started.html)):

```xml
<ItemGroup>
  <ProjectReference Include="..\CsvHelper\src\CsvHelper.Linq\CsvHelper.Linq.csproj" />
  <ProjectReference Include="..\CsvHelper\src\CsvHelper.Generators\CsvHelper.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generator writes the code that reads only some members of a `[CsvRecord]` type for the record types of projects
that reference CsvHelper.Linq. For record types in a library that does not, add `[assembly: CsvRecordProjections]` to
the library.
<!-- endif -->

CsvHelper.Linq is a separate package because a LINQ provider compiles lambdas and uses reflection, which CsvHelper
itself does not, so that it stays safe for trimming and native AOT.

## Sources

| Source | Method | Notes |
|---|---|---|
| A file | `CsvQueryable.FromFile<T>(path)` | A `CsvTable<T>`: opened each time a query runs; records can also be [added, removed and changed](#changing-files). |
| Text | `CsvQueryable.FromText<T>(text)` | |
| UTF-8 bytes | `CsvQueryable.FromBytes<T>(bytes)` | Read in place. |
| Streams | `CsvQueryable.FromStream<T>(() => stream)` | The factory opens a stream each time a query runs, which is disposed after it. |
| Text readers | `CsvQueryable.FromTextReader<T>(() => reader)` | As streams. |
| A reader | `reader.Query<T>()` | Reads the rest of the reader once, and leaves it open. |

`T` is a [`[CsvRecord]`](records.html) type, or `CsvDynamicRecord` for [rows without a type](#dynamic-rows): every
method also has a version without `<T>` that returns rows. Each method takes `CsvQueryOptions`, whose `ReaderOptions`
are the options of the reader (by default those of the type's `[CsvOptions]`).

A query runs each time it is enumerated, and reads its source again. Any LINQ operator works, including the newest ones
of .NET 9 and 10 (`CountBy`, `AggregateBy`, `Index`, `LeftJoin`, `RightJoin`), and query syntax with `let`, `join`,
`join ... into` and `group ... by`.

## What the reader does itself

### Reading only the members a query uses

The provider follows the records of each source through the query (through `Where`, `Select`, `GroupBy`, joins,
`let` and the anonymous types that query syntax makes) and collects the members the query reads. The reader creates a
mapper for those members alone: the other columns are neither looked up in the header nor parsed. A record whose other
members are missing keeps their defaults, and a constructor parameter its default value (`""` for a string that is not
nullable).

A query needs every member when it uses whole records:

- it returns them (`orders.Where(o => o.Total > 100).ToList()`), or `First` and the other operators that return one;
- it passes them to a method (`Where(o => IsBig(o))`) or stores them (`new { o, Total = o.Total * 2 }` is fine, as the
  anonymous type is followed; `new Holder(o)` is not);
- it compares them: `Distinct()`, `Contains(order)`, `GroupBy(o => o)`, `Order()`;
- it reads a member that is not a column, such as a computed property (`o.Discounted => Total * 0.9m`).

### Filtering before reading

A `Where` right on the source (or several, and a `Skip` before them) runs in the reader: the filter reads only its own
members of each record, and the rest of the query's members are read only for the records that pass.

```csharp
var names = orders.Where(o => o.Status == Status.Open).Select(o => o.Customer);
// For every record: Status. For the open ones: Customer.
```

A filter that uses a variable of an enclosing lambda (`customers.SelectMany(c => orders.Where(o => o.Customer == c.Name))`)
runs in LINQ to Objects instead, on the records read once.

### Skipping, counting and stopping early

- A `Skip(n)` right on the source skips `n` rows without parsing them.
- `Count()`, `LongCount()` and `Any()` read no field unless a filter before them needs one.
- `First`, `Take`, `Any` and `ElementAt` stop reading at the record they need.

### Explaining a query

`CsvQueryable.Explain(query)` (or `query.Explain()` on a `CsvQuery<T>`) tells what a query pushes into the reader, without
running it. `CsvQueryOptions.OnExecute` gets the same plan each time a query runs.

```csharp
Console.WriteLine(CsvQueryable.Explain(big));
// text of 2048 characters (Order): Where; filter reads Total; reads Customer, Id
// In memory: Select
```

The plan has, for each source, the operators the reader runs (`PushedOperators`), the members its filters read
(`FilterMembers`) and the members read for the records that pass (`Members`, `null` for all), with notes that say why a
query needs every member; the queries of other providers it runs (`ForeignQueries`); and the operators that LINQ to
Objects runs (`InMemoryOperators`).

### What pruning changes

Reading only the members a query uses is what makes queries fast, and it changes what a query can notice:

- a field that cannot be parsed, a `[CsvValidate]` check that fails, or a missing column in a member the query does not
  read is not an error;
- a record that fails only in such a member is not given to `CsvReaderOptions.OnRecordError`, and is not skipped;
- rows that a `Skip` right on the source leaves out are not read at all.

`CsvQueryOptions.PruneColumns = false` reads every member of every record the query reads, as `GetRecords` does.

## Dynamic rows

Files without a record type are queried as `CsvDynamicRecord` rows. When a file has a header, the reader runs the
filters and the projection on its records by column, without making rows: `row["Name"]` reads the field as a string,
`row.Parse<decimal>("Total")` (or any other type of `Parse<T>`) parses it, and
`row.ContainsKey("Name")` and `row.FieldCount` work too. A column name that the header does not have throws
`KeyNotFoundException` at the first row, as the row would.

```csharp
var rows = CsvQueryable.FromFile("orders.csv");
var totals = rows
    .Where(r => r.Parse<decimal>("Total") > 100)
    .Select(r => new { Customer = r["Customer"], Total = r.Parse<decimal>("Total") })
    .ToList();
```

A lambda that uses rows otherwise (`r.Names`, `r.ToDictionary()`, or the row itself) gets rows.

## Joining with other providers

A CSV query can use queries of other LINQ providers, such as the tables of an Entity Framework Core context. Each runs
on its own provider, so its filters and projections become SQL, and the provider is asked for as little as the query
needs.

### Joins send the keys

A `Join`, `GroupJoin` or `LeftJoin` of CSV records with another provider's query reads the CSV records first, for their
keys, and asks the provider only for the rows with those keys: `Where(row => keys.Contains(row.Key))`, which Entity
Framework Core sends as one parameter (an `IN` list, or a JSON array). The keys go in batches of
`CsvQueryOptions.ForeignKeyBatchSize` (2000 by default).

```csharp
var report = orders
    .Join(db.Customers.Where(c => c.Active), o => o.CustomerId, c => c.Id, (o, c) => new { o.Id, c.Name, o.Total })
    .ToList();
// SELECT ... FROM Customers WHERE Active AND Id IN (the customer ids of orders.csv)
```

- Composite keys (`new { o.Region, o.Code }`) send the values of each member; the join keeps only the exact matches.
- With more distinct keys than `CsvQueryOptions.MaxForeignKeys` (100,000 by default), or when the provider cannot
  translate the filter, the provider's query is read whole, once. `MaxForeignKeys = 0` always does that.
- The CSV records are read twice, once for the keys and once for the join, unless the source is a caller's reader,
  which is read once into memory.
- The results come in the order of the CSV records; the provider's rows for one record come in the provider's order.

### Subqueries run once

A query of another provider inside a lambda runs once per run of the CSV query, not once per record:

- `provider.Select(c => c.Id).Contains(o.CustomerId)` and `provider.Any(c => c.Id == o.CustomerId && c.Active)` ask
  the provider for the keys alone (with the conditions on its rows alone), into a hash set;
- a value such as `db.Customers.Average(c => c.Discount)` is read once;
- any other query is read once into memory, and LINQ to Objects runs the lambda on it.

CSV queries inside lambdas are read once too. A query held in a variable is part of the query, so all of this applies
to it.

### When the other provider runs the query

A provider such as Entity Framework Core cannot translate a CSV query inside its own query. Read the CSV side first,
and use the list, which it sends as a parameter:

```csharp
var ids = orders.Where(o => o.Total > 100).Select(o => o.CustomerId).ToHashSet();
var customers = db.Customers.Where(c => ids.Contains(c.Id)).ToList();
```

## Asynchronous queries

`await foreach` over a `CsvQuery<T>` reads the CSV asynchronously. `AsCsvAsync()` does the same for the queries that
LINQ operators return, and has asynchronous versions of the operators that run a query:

```csharp
var open = await orders.Where(o => o.Status == Status.Open).AsCsvAsync().ToListAsync(cancellationToken);
var count = await orders.AsCsvAsync().CountAsync(o => o.Total > 100);
var latest = await orders.AsCsvAsync().ExecuteAsync(q => q.Max(o => o.Placed));
```

When the reader runs the whole query (filters, skips, and the `Select`, `Where`, `Skip` and `Take` after them), the
records stream; otherwise the records the reader returns are read into memory first, and LINQ to Objects runs the rest
of the query on them. The operators of `AsCsvAsync()` are instance methods, so they are chosen over the extension
methods of the same names that Entity Framework Core and .NET 10's `System.Linq.AsyncEnumerable` have.

The queries that LINQ operators return are `IAsyncEnumerable<T>` too, so the `ToListAsync()` and `AsAsyncEnumerable()`
of Entity Framework Core work on them. `CsvQuery<T>` itself is not, so that calling a LINQ operator on it is not
ambiguous between `Queryable` and `AsyncEnumerable`. Entity Framework Core's operators that return one value, such as
`CountAsync()`, need its own provider: use `AsCsvAsync()`.

## Reading in parallel

`CsvQueryOptions.MaxDegreeOfParallelism` reads, filters and maps the records on several threads, with
`CsvReader.ParallelEnumerate`. The records come out in the order of the file, and the rest of the query runs on them
as usual. The filters that the reader runs must be safe to call from several threads.

```csharp
var options = new CsvQueryOptions { MaxDegreeOfParallelism = -1 };   // one thread per processor
var count = CsvQueryable.FromFile<Order>("big.csv", options).Count(o => o.Total > 100);
```

Asynchronous queries read on one thread.

## Writing query results

`WriteCsv` writes the results of any query as CSV: of a CSV query, of Entity Framework Core, or of a list.

```csharp
orders.Where(o => o.Total > 100).Select(o => new { o.Id, o.Customer }).WriteCsv("big.csv");
await db.Customers.Where(c => c.Active).WriteCsvAsync("active.csv");   // read asynchronously
```

- `[CsvRecord]` types are written by their generated writers, and dictionaries and dynamic records by their keys, as
  [`CsvWriter.WriteRecords`](writing.html) writes them.
- Other types (anonymous types, tuples, records and classes, such as entities) are written from their public
  properties and fields of simple types (numbers, strings, dates, Booleans, enums and other formattable values): the
  members that the widest public constructor sets first, in its order, then the others, those of base classes first.
  References to other objects and collections, such as navigation properties, are left out.
  `[CsvIgnore]` leaves a member out, and the name of a `[CsvColumn]` names its column.
- Values are written as `CsvWriter.WriteField(object)` writes them.

`WriteCsv` takes a path, a stream, a text writer or a `CsvWriter`, and the writer options (by default those of the
type's `[CsvOptions]`). It returns the number of records written. `WriteCsvAsync` reads the records asynchronously when
they can be.

`AppendCsv` adds records to the end of a file: a new or empty file gets a header; a file with records keeps its
separator and line break, gets a line break at its end first when it has none, and must have the header that the records
are written with (ignoring case and surrounding spaces).

## Changing files

The `CsvTable<T>` of `CsvQueryable.FromFile` adds records to its file, and removes and changes the records that match
a condition:

```csharp
var orders = CsvQueryable.FromFile<Order>("orders.csv");
orders.AppendRecords(newOrders);
var removed = orders.Delete(o => o.Status == Status.Cancelled);
var changed = orders.Update(o => o.Status == Status.Pending, o => o with { Status = Status.Open });
var rows = CsvQueryable.FromFile("orders.csv");
rows.Update(r => r["Status"] == "Pending", r => r["Status"] = "Open");
```

`Delete` and `Update` (and their `Async` versions) rewrite the file next to itself and replace it when the new one is
complete, so a failure, or a canceled token, leaves the file as it was:

- the records that do not change, the header, comments, blank lines and anything else are copied byte for byte;
- a removed record goes with its line break;
- in a changed record, only the fields whose values changed are written again, so the formatting of the others, and
  the columns that the record type does not map, stay as they are. An update that changes a member that the file has no
  column for throws `InvalidOperationException`.

The condition reads only the members it uses. A record that cannot be read and that `OnRecordError` skips is kept.
Files must be UTF-8. `Update` needs a file that is not fixed-width and quotes with `"`.

## Trimming and native AOT

The query provider compiles lambdas and finds LINQ operators with reflection, so `CsvQueryable`, `CsvQuery<T>` and
`CsvTable<T>` carry `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`: applications that trim or compile ahead
of time get warnings where they use them. `WriteCsv` and `AppendCsv` are safe for trimming and native AOT.

## Options

`CsvQueryOptions` is a record with `init` properties.

| Option | Default | Meaning |
|---|---|---|
| `ReaderOptions` | `null` | The options of the reader; `null` for those of the type's `[CsvOptions]`. |
| `WriterOptions` | `null` | The options of the writer of a `CsvTable<T>`; `null` for those of the type, with the file's separator. |
| `PruneColumns` | `true` | Read only the members a query uses. |
| `MaxDegreeOfParallelism` | `1` | The threads that read, filter and map records; `-1` for one per processor. |
| `ForeignKeyBatchSize` | `2000` | The keys a join sends to another provider at once. |
| `MaxForeignKeys` | `100000` | The most keys a join sends; with more, the other provider's query is read whole. `0` never sends keys. |
| `OnExecute` | `null` | Gets the plan of each query before it runs. |
