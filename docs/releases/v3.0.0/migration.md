# Migrating from CsvHelper 2 to 3

CsvHelper 3 is a large rewrite: a faster parser, configuration flags that became callbacks you can replace, and the
state of a reader or writer moved into a context object. Most class names lose their `Csv` prefix (`CsvConfiguration`
is `Configuration`, `CsvClassMap<T>` is `ClassMap<T>`), and "property" became "member" because fields can be mapped
too. The change that every hand-written read loop over a file with a header meets: `Read` no longer reads the
header. CsvHelper 3 is built for .NET Framework 4.5 and .NET Standard 2.0 only; the .NET Framework 2.0, 3.5 and 4.0,
portable and .NET Standard 1.3 builds are gone.

## At a glance

| CsvHelper 2 | CsvHelper 3 |
|---|---|
| `CsvConfiguration` | `Configuration`, in `CsvHelper.Configuration` |
| `Read()` reads the header first | `Read()`, then `ReadHeader()` |
| `FieldHeaders`, `CurrentRecord`, `Row` | `Context.HeaderRecord`, `Context.Record`, `Context.Row` |
| `CsvClassMap<T>`, `CsvPropertyMap` | `ClassMap<T>`, `MemberMap<TClass, TMember>` |
| `Map(...).TypeConverterOption("format")` | `Map(...).TypeConverterOption.Format("format")` |
| `ConvertFromString(options, text)` | `ConvertFromString(text, row, memberMapData)` |
| Static `TypeConverterFactory`, `TypeConverterOptionsFactory` | `TypeConverterCache`, `TypeConverterOptionsCache` on the configuration |
| `WriteHeader<T>()` and `WriteRecord(record)` end the record | Call `NextRecord()` after them |
| `ThrowOnBadData`, `WillThrowOnMissingField`, `IgnoreReadingExceptions` | `BadDataFound`, `MissingFieldFound`, `HeaderValidated`, `ReadingExceptionOccurred` |
| `CsvBadDataException`, `CsvMissingFieldException`, ... | `BadDataException`, `MissingFieldException`, ... |
| `ICsvReader`, `ICsvReaderRow`, `ICsvWriter`, `ICsvParser` | `IReader`, `IReaderRow`, `IWriter`, `IParser` |

## Renamed types

The `Csv` prefix is gone from the class names, except for `CsvReader`, `CsvParser`, `CsvWriter`, `CsvSerializer` and
`CsvHelperException`, and the types named after properties are named after members, since a map can now point at a
field. The namespaces are the same, so most of the port is a rename.

| CsvHelper 2 | CsvHelper 3 |
|---|---|
| `CsvConfiguration` | `Configuration` |
| `CsvClassMap`, `CsvClassMap<T>`, `DefaultCsvClassMap<T>` | `ClassMap`, `ClassMap<T>`, `DefaultClassMap<T>` |
| `CsvClassMapCollection` | `ClassMapCollection` |
| `CsvPropertyMap` | `MemberMap`, and `MemberMap<TClass, TMember>` from `Map` |
| `CsvPropertyMapData`, `CsvPropertyMapCollection` | `MemberMapData`, `MemberMapCollection` |
| `CsvPropertyReferenceMap`, `CsvPropertyNameCollection` | `MemberReferenceMap`, `MemberNameCollection` |
| `TypeConverterFactory`, `TypeConverterOptionsFactory` | `TypeConverterCache`, `TypeConverterOptionsCache`, instances on the configuration |
| `CsvFactory`, `ICsvFactory` | `Factory`, `IFactory` |
| `ICsvParser`, `ICsvReader`, `ICsvReaderRow` | `IParser`, `IReader`, `IReaderRow` |
| `ICsvSerializer`, `ICsvWriter` | `ISerializer`, `IWriter` (with `IWriterRow`) |

The exceptions are renamed the same way: see [Exceptions](#exceptions).

`Configuration` has the name of its namespace. If a file also has `using System.Configuration;`, whose namespace has a
`Configuration` class too, write `CsvHelper.Configuration.Configuration`.

## Creating a reader or writer

The constructors take a `TextReader` or `TextWriter` and an optional configuration, as before. Only the configuration
class is renamed.

```csharp before
var config = new CsvConfiguration { Delimiter = ";" };
using var csv = new CsvReader(reader, config);
csv.Configuration.HasHeaderRecord = false;
```

```csharp after
var config = new Configuration { Delimiter = ";" };
using var csv = new CsvReader(reader, config);
csv.Configuration.HasHeaderRecord = false;
```

- **`csv.Configuration` is an interface now:** `IReaderConfiguration` on a reader, `IWriterConfiguration` on a writer.
  Each shows only the settings its side uses, so a reader has no `QuoteAllFields`. Code that took `csv.Configuration`
  as a `CsvConfiguration` takes the interface, or the `Configuration` object you created.
- **Leaving the text reader open:** disposing a reader or writer still disposes the `TextReader` or `TextWriter`.
  `new CsvReader(reader, leaveOpen: true)`, `new CsvReader(reader, config, leaveOpen: true)` and
  `new CsvWriter(writer, leaveOpen: true)` leave it open. For a writer with a configuration, pass
  `new CsvSerializer(writer, config, leaveOpen: true)` to `new CsvWriter(serializer)`.

## Configuration

Most settings keep their names. Three groups changed: the flags that chose between throwing and ignoring became
callbacks (see [Bad data and errors](#bad-data-and-errors)), the header matching flags became one function, and
trimming moved into the parser.

```csharp before
var config = new CsvConfiguration
{
    Delimiter = ";",
    IsHeaderCaseSensitive = false,
    TrimFields = true,
    SkipEmptyRecords = true,
    IgnorePrivateAccessor = true,
};
using var csv = new CsvReader(reader, config);
```

```csharp after
var config = new Configuration
{
    Delimiter = ";",
    PrepareHeaderForMatch = header => header.ToLower(),
    TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
    ShouldSkipRecord = record => record != null && record.All(string.IsNullOrEmpty),
    IncludePrivateMembers = true,
};
using var csv = new CsvReader(reader, config);
```

| `CsvConfiguration` | `Configuration` | Notes |
|---|---|---|
| `IsHeaderCaseSensitive = false` | `PrepareHeaderForMatch = header => header.ToLower()` | See [Header matching](#header-matching). |
| `TrimHeaders = true` | `PrepareHeaderForMatch = header => header.Trim()` | |
| `IgnoreHeaderWhiteSpace = true` | `PrepareHeaderForMatch = header => Regex.Replace(header, @"\s", string.Empty)` | |
| `TrimFields = true` | `TrimOptions` | `TrimOptions.Trim` trims the spaces around a field, outside its quotes; `TrimOptions.InsideQuotes` trims the spaces inside them. 2 trimmed the value, quoted or not, so set both. |
| `SkipEmptyRecords = true` | `ShouldSkipRecord = record => record != null && record.All(string.IsNullOrEmpty)` | 3.0.0 also calls it with `null` at the end of the file; 3.1.0 doesn't. |
| `WillThrowOnMissingField = false` | `MissingFieldFound = null`, `HeaderValidated = null` | See [Reading records](#reading-records). |
| `ThrowOnBadData`, `BadDataCallback` | `BadDataFound` | Throws by default now. |
| `IgnoreReadingExceptions`, `ReadingExceptionCallback` | `ReadingExceptionOccurred` | |
| `IgnorePrivateAccessor` | `IncludePrivateMembers` | |
| `UseNewObjectForNullReferenceProperties` | `UseNewObjectForNullReferenceMembers` | |
| `PropertyBindingFlags` | `MemberTypes`, `IncludePrivateMembers` | 2.16.3 never read `PropertyBindingFlags`. `MemberTypes.Fields` maps fields too. |
| `HasExcelSeparator`, `UseExcelLeadingZerosFormatForNumerics` | none | See [Removed with no replacement](#removed-with-no-replacement). |

These keep their names: `Delimiter`, `Quote`, `Comment`, `AllowComments`, `HasHeaderRecord`, `IgnoreBlankLines`,
`IgnoreQuotes`, `QuoteAllFields`, `QuoteNoFields`, `CultureInfo`, `Encoding`, `CountBytes`, `BufferSize`,
`DetectColumnCountChanges`, `IgnoreReferences`, `PrefixReferenceHeaders`, `ShouldSkipRecord` and the class map
methods (`RegisterClassMap`, `UnregisterClassMap`, `AutoMap`, `Maps`).

A `ShouldSkipRecord` function brought over from 2 must accept `null` on 3.0.0: 2 never passed it the end of the file,
but 3.0.0 calls it with `null` there, so `record.All(...)` throws an `ArgumentNullException`. 3.1.0 no longer does.

### Header matching

`IsHeaderCaseSensitive`, `TrimHeaders` and `IgnoreHeaderWhiteSpace` are replaced by `PrepareHeaderForMatch`, a function
that both the header name and the member name go through before they are compared. One function covers any
combination, and anything else you need, such as removing underscores.

```csharp before
var config = new CsvConfiguration
{
    IsHeaderCaseSensitive = false,
    IgnoreHeaderWhiteSpace = true,
};
```

```csharp after
using System.Text.RegularExpressions;

var config = new Configuration
{
    PrepareHeaderForMatch = header => Regex.Replace(header, @"\s", string.Empty).ToLower(),
};
```

After the function, names are compared exactly, case included.

## Reading by hand

`Read` no longer reads the header. Call `Read` and then `ReadHeader`, which takes the current record as the header, so
the header doesn't have to be the first row. The state of the current record moved to `Context`.

```csharp before
using var csv = new CsvReader(reader);
csv.ReadHeader();
var headers = csv.FieldHeaders;
while (csv.Read())
{
    var id = csv.GetField<int>("Id");
    var name = csv.GetField("Name");
    Console.WriteLine($"Row {csv.Row}: {id} {name}");
}
```

```csharp after
using var csv = new CsvReader(reader);
csv.Read();
csv.ReadHeader();
var headers = csv.Context.HeaderRecord;
while (csv.Read())
{
    var id = csv.GetField<int>("Id");
    var name = csv.GetField("Name");
    Console.WriteLine($"Row {csv.Context.Row}: {id} {name}");
}
```

- **Without `ReadHeader`,** the header row comes back as the first record, and getting a field by name throws a
  `MissingFieldException`, since there are no names to find.
- **`ReadHeader` before the first `Read`,** which 2 allowed, throws a `ReaderException`: there is no current record to
  take the header from.
- **`ReadAsync()`** is new.

| CsvHelper 2 | CsvHelper 3 |
|---|---|
| `FieldHeaders` | `Context.HeaderRecord` |
| `CurrentRecord` | `Context.Record` |
| `Row` | `Context.Row` |
| `Parser.RawRecord` | `Context.RawRecord` |
| `Parser.CharPosition`, `Parser.BytePosition` | `Context.CharPosition`, `Context.BytePosition` |
| `IsRecordEmpty()` | `Context.Record.All(string.IsNullOrEmpty)` |
| `GetField(index, converter)` and the other obsolete overloads that returned `object` | `GetField(type, index, converter)` or `GetField<T>(index, converter)` |
| `GetField<T>`, `TryGetField<T>`, `GetField(name, index)`, `csv[name]` | Unchanged |

## Reading records

`GetRecords<T>()` is called the same way and still reads the header itself. What's new is that it validates the
header before the first record: every mapped member must have a column, or `HeaderValidated` throws a
`ValidationException`. In 2 a missing column threw `CsvMissingFieldException`, unless `WillThrowOnMissingField` was
`false`; that switch is now two callbacks.

```csharp before
public class Order
{
    public int Id { get; set; }
    public string Note { get; set; }
}

var config = new CsvConfiguration { WillThrowOnMissingField = false };
using var csv = new CsvReader(reader, config);
var orders = csv.GetRecords<Order>().ToList();
```

```csharp after
public class Order
{
    public int Id { get; set; }
    public string Note { get; set; }
}

var config = new Configuration
{
    HeaderValidated = null,
    MissingFieldFound = null,
};
using var csv = new CsvReader(reader, config);
var orders = csv.GetRecords<Order>().ToList();
```

Setting only `HeaderValidated = null` isn't enough: the member's column is then looked up when the first record is
created, and `MissingFieldFound` throws a `MissingFieldException`. With both `null`, members without a column are left
unset, as in 2.

- **`GetRecord<T>()` in a `Read()` loop** works as before: it reads the header when it hasn't been read yet.
- **More classes can be read.** Auto mapping still maps public properties; `MemberTypes` adds fields and
  `IncludePrivateMembers` adds private members. A class without a parameterless constructor is created through its
  constructor (the one with the most parameters, unless you set `GetConstructor`), whose parameters are matched to
  columns.
  Anonymous types work with `GetRecords(new { Id = 0, Name = "" })`, and `EnumerateRecords(record)` fills one object
  again and again.
- **`ClearRecordCache()`** is `csv.Context.ClearCache(Caches.ReadRecord)`, which clears the cache of every type.

## Class maps

`CsvClassMap<T>` is `ClassMap<T>`, and `Map` returns a `MemberMap<TClass, TMember>` that knows the member's type, so
`Default` and `Constant` are type checked. The overloads of `TypeConverterOption(...)` became methods of a
`TypeConverterOption` property, named after what they set.

```csharp before
public class Customer
{
    public string Name { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public DateTime Ordered { get; set; }
    public bool Shipped { get; set; }
    public decimal Total { get; set; }
    public Customer Customer { get; set; }
}

public sealed class CustomerMap : CsvClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name).Name("Customer");
    }
}

public sealed class OrderMap : CsvClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id).Name("Order Id");
        Map(m => m.Ordered).Name("Date").TypeConverterOption("yyyyMMdd");
        Map(m => m.Shipped).TypeConverterOption(true, "Yes").TypeConverterOption(false, "No");
        Map(m => m.Total).ConvertUsing(row => row.GetField<decimal>("Net") + row.GetField<decimal>("Tax"));
        References<CustomerMap>(m => m.Customer);
    }
}

using var csv = new CsvReader(reader);
csv.Configuration.RegisterClassMap<OrderMap>();
var orders = csv.GetRecords<Order>().ToList();
```

```csharp after
public class Customer
{
    public string Name { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public DateTime Ordered { get; set; }
    public bool Shipped { get; set; }
    public decimal Total { get; set; }
    public Customer Customer { get; set; }
}

public sealed class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name).Name("Customer");
    }
}

public sealed class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id).Name("Order Id");
        Map(m => m.Ordered).Name("Date").TypeConverterOption.Format("yyyyMMdd");
        Map(m => m.Shipped)
            .TypeConverterOption.BooleanValues(true, true, "Yes")
            .TypeConverterOption.BooleanValues(false, true, "No");
        Map(m => m.Total).ConvertUsing(row => row.GetField<decimal>("Net") + row.GetField<decimal>("Tax"));
        References<CustomerMap>(m => m.Customer);
    }
}

using var csv = new CsvReader(reader);
csv.Configuration.RegisterClassMap<OrderMap>();
var orders = csv.GetRecords<Order>().ToList();
```

- **Call `TypeConverterOption` last.** Its methods return a `MemberMap`, not the typed map, so
  `Map(m => m.Ordered).TypeConverterOption.Format("yyyyMMdd").Name("Date")` doesn't compile: put `Name`, `Index` and
  the rest first.
- **Members mapped with `ConvertUsing` need a column in 3.0.0.** The header check also looks for a column named after
  them, `Total` here, and throws a `ValidationException` when there is none. 3.1.0 skips members mapped with
  `ConvertUsing` or `Constant`; on 3.0.0, set `HeaderValidated = null`.

| `CsvClassMap<T>` | `ClassMap<T>` | Notes |
|---|---|---|
| `Map(m => m.X)` | `Map(m => m.X)` | Returns `MemberMap<TClass, TMember>`. |
| `.Default(object)` | `.Default(TMember)`, `.Default(string)` | A `string` default for a member of another type is converted with the member's converter. |
| `.TypeConverterOption(format)` | `.TypeConverterOption.Format(formats)` | Takes several formats: dates and times are read with any of them, values are written with the first. |
| `.TypeConverterOption(cultureInfo)` | `.TypeConverterOption.CultureInfo(cultureInfo)` | |
| `.TypeConverterOption(dateTimeStyle)` | `.TypeConverterOption.DateTimeStyles(dateTimeStyle)` | |
| `.TypeConverterOption(numberStyle)` | `.TypeConverterOption.NumberStyles(numberStyle)` | |
| `.TypeConverterOption(isTrue, values)` | `.TypeConverterOption.BooleanValues(isTrue, clearValues, values)` | `clearValues` must be passed when you pass values. |
| `.ConvertUsing(row => ...)` | `.ConvertUsing(row => ...)` | The row is an `IReaderRow`. |
| `AutoMap(ignoreReferences, prefixReferenceHeaders)` | `AutoMap()`, `AutoMap(configuration)` | The two options come from the configuration's `IgnoreReferences` and `PrefixReferenceHeaders`. |
| `PropertyMaps` | `MemberMaps` | |
| `PropertyMap<T>(expression)` | `MemberMaps.Find<T>(expression)` | |
| `CreateMap()` | none | It was obsolete in 2: map in the constructor. |

New in the maps: `Map(m => m.Customer.Name)` maps a member of a child object without a second map,
`.ConvertUsing(order => ...)` with the record returns the string to write, `.Constant(value)` uses one value for every
record, `.Validate(field => ...)` checks a field when reading, `.TypeConverterOption.NullValues("NULL")` lists strings
read as `null`, and `Map()` with no member writes a field that isn't on the class.

## Type converters

`ITypeConverter` gets the reader or writer row and the member's map data instead of the options alone, so a
converter can look at other fields. `CanConvertFrom` and `CanConvertTo` are gone. The converter registry and the
per-type options are no longer static: each configuration has its own.

```csharp before
public class PercentConverter : DefaultTypeConverter
{
    public override bool CanConvertFrom(Type type) => type == typeof(string);

    public override object ConvertFromString(TypeConverterOptions options, string text) =>
        decimal.Parse(text.TrimEnd('%'), options.CultureInfo) / 100;

    public override string ConvertToString(TypeConverterOptions options, object value) =>
        ((decimal)value * 100).ToString(options.CultureInfo) + "%";
}

TypeConverterFactory.AddConverter<decimal>(new PercentConverter());
using var csv = new CsvReader(reader);
```

```csharp after
public class PercentConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
        decimal.Parse(text.TrimEnd('%'), memberMapData.TypeConverterOptions.CultureInfo) / 100;

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) =>
        ((decimal)value * 100).ToString(memberMapData.TypeConverterOptions.CultureInfo) + "%";
}

using var csv = new CsvReader(reader);
csv.Configuration.TypeConverterCache.AddConverter<decimal>(new PercentConverter());
```

- **A converter is used for reading whenever it's mapped.** In 2 the reader skipped a member whose converter's
  `CanConvertFrom` returned `false`, which `DefaultTypeConverter` did by default. 3 calls `ConvertFromString` for every
  mapped member, and `DefaultTypeConverter`'s throws a `TypeConverterException`: a converter derived from
  `DefaultTypeConverter` that only overrode `ConvertToString` needs `ConvertFromString` too.
- **Return `string.Empty`, not `null`,** from `ConvertToString` for an empty field. `null` now means the converter
  wrote the field itself, so nothing is written and the columns shift.
- **A converter registered once is no longer global.** `TypeConverterFactory` was static, so one `AddConverter` call
  applied to every reader and writer. Add it to each configuration, or share one `Configuration` object.
- **`TypeConverter(...)`, `GetField<T, TConverter>`, `WriteField<T, TConverter>`** work as before, with converters
  written for the new signature.

### Options for a type

`TypeConverterOptionsFactory` is the configuration's `TypeConverterOptionsCache`, and `Format` is `Formats`, an array.

```csharp before
TypeConverterOptionsFactory.AddOptions<DateTime>(new TypeConverterOptions { Format = "yyyyMMdd" });
using var csv = new CsvReader(reader);
```

```csharp after
using var csv = new CsvReader(reader);
var options = new TypeConverterOptions { Formats = new[] { "yyyyMMdd" } };
csv.Configuration.TypeConverterOptionsCache.AddOptions<DateTime>(options);
```

Dates and times are read with any of the formats, and values are written with the first. `TypeConverterOptions` also
has `NullValues`, the strings that a `string` or nullable member reads as `null`. There are none by default.

## Writing

`WriteHeader<T>()` and `WriteRecord(record)` no longer end the record: call `NextRecord()` after them. That lets you
add fields after a record, or write several records on one line, and matches the reader, where `GetRecord` doesn't
call `Read`.

```csharp before
public class Order
{
    public int Id { get; set; }
}

var orders = new List<Order> { new Order { Id = 1 } };
using var csv = new CsvWriter(writer);
csv.WriteHeader<Order>();
foreach (var order in orders)
{
    csv.WriteRecord(order);
}
```

```csharp after
public class Order
{
    public int Id { get; set; }
}

var orders = new List<Order> { new Order { Id = 1 } };
using var csv = new CsvWriter(writer);
csv.WriteHeader<Order>();
csv.NextRecord();
foreach (var order in orders)
{
    csv.WriteRecord(order);
    csv.NextRecord();
}
```

**A literal port doesn't write the file you expect, and nothing fails.** On 3.0.0 it writes nothing: the fields wait
for a `NextRecord()` that never comes. From 3.1.0, which writes the waiting fields when the writer is disposed, the
header and the records run together on one line. Add the `NextRecord()` calls, or write the list with
`csv.WriteRecords(orders)`, which writes the header and ends each record itself, as before.

- **`WriteRecords` is generic in 3.0.0 and 3.1.0:** it takes an `IEnumerable<T>`, so a non-generic collection, such
  as an `ArrayList`, doesn't compile; pass `list.Cast<object>()`, and each record is written by its own type. 3.1.1
  takes any `IEnumerable` again.
- **`null` fields:** `WriteField<T>(null)` writes an empty field. 2 threw a `NullReferenceException`.
- **`Flush()` is new** and isn't the `TextWriter`'s: it writes the fields of the current record without a line
  ending. `NextRecord()` is `Flush()` and a line ending.
- **New:** `WriteComment(text)`, writing `ExpandoObject` and other dynamic objects with `WriteRecords`,
  `NextRecordAsync()` and `FlushAsync()`.
- **`ClearRecordCache()`** is `csv.Context.ClearCache(Caches.WriteRecord)`.

## Bad data and errors

2 had flags that chose whether to throw (`ThrowOnBadData`, `WillThrowOnMissingField`, `IgnoreReadingExceptions`) and
separate callbacks to be told. 3 has one callback for each case, set to a function that throws by default: replace it
to log, or set it to `null` to ignore.

```csharp before
var config = new CsvConfiguration
{
    BadDataCallback = field => Console.WriteLine($"Bad data: {field}"),
    IgnoreReadingExceptions = true,
    ReadingExceptionCallback = (ex, row) => Console.WriteLine($"Skipped row {row.Row}: {ex.Message}"),
};
```

```csharp after
var config = new Configuration
{
    BadDataFound = context => Console.WriteLine($"Bad data: {context.Field}"),
    ReadingExceptionOccurred = ex => Console.WriteLine($"Skipped row {ex.ReadingContext.Row}: {ex.Message}"),
};
```

| Callback | Called when | By default |
|---|---|---|
| `BadDataFound` | A field contains a quote but isn't quoted. Gets the `IReadingContext`; `Field` is the field. | Throws `BadDataException`. 2 ignored bad data unless `ThrowOnBadData` was set. |
| `MissingFieldFound` | A field is asked for by a name the header lacks, or by an index past the end of the record. | Throws `MissingFieldException`. |
| `HeaderValidated` | `GetRecords`, `GetRecord` and `ValidateHeader<T>()` check that the header has a column for each mapped member. | Throws `ValidationException`. |
| `ReadingExceptionOccurred` | `GetRecords` fails to create a record, or `DetectColumnCountChanges` finds a record of another length. Gets a `CsvHelperException`. | Rethrows. When it returns instead, `GetRecords` skips the record it couldn't create and goes on. |

### Exceptions

The exceptions lose the `Csv` prefix. Instead of a text in `Data["CsvHelper"]`, a `CsvHelperException` has the
`ReadingContext` or `WritingContext` it happened in. The reading context has the `Row`, `Record`, `RawRecord`,
`HeaderRecord` and `CurrentIndex`.

```csharp before
public class Order
{
    public int Id { get; set; }
}

using var csv = new CsvReader(reader);
try
{
    var orders = csv.GetRecords<Order>().ToList();
}
catch (CsvMissingFieldException ex)
{
    Console.WriteLine(ex.Data["CsvHelper"]);
}
```

```csharp after
public class Order
{
    public int Id { get; set; }
}

using var csv = new CsvReader(reader);
try
{
    var orders = csv.GetRecords<Order>().ToList();
}
catch (ValidationException ex)
{
    Console.WriteLine($"{ex.Message} Header: {string.Join(",", ex.ReadingContext.HeaderRecord)}");
}
```

A column missing from the header is now a `ValidationException` from the header check (see
[Reading records](#reading-records)), so a `catch` for the missing field exception no longer catches it.

| CsvHelper 2 | CsvHelper 3 | Notes |
|---|---|---|
| `CsvHelperException` | `CsvHelperException` | `ReadingContext`, `WritingContext` |
| `CsvBadDataException` | `BadDataException` | |
| `CsvMissingFieldException` | `MissingFieldException` | `System` has a `MissingFieldException` too: write `CsvHelper.MissingFieldException`. |
| `CsvParserException` | `ParserException` | |
| `CsvReaderException` | `ReaderException` | |
| `CsvWriterException` | `WriterException` | |
| `CsvTypeConverterException` | `TypeConverterException` | In `CsvHelper.TypeConversion`. |
| `CsvConfigurationException` | `ConfigurationException` | In `CsvHelper.Configuration`. |
| none | `ValidationException` | Header validation and `Validate` in a map. |

**Other exceptions are wrapped.** In 2 an exception from a converter, such as a `FormatException`, came out of
`GetRecords` as it was. Now the record methods throw it as the `InnerException` of a `ReaderException` (a
`WriterException` when writing, a `ParserException` from the parser), so `catch (FormatException)` around
`GetRecords` no longer catches it.

## Interfaces and the parser

The interfaces lose the `Csv` prefix and gain the context and async members. The record methods (`GetRecords`,
`GetRecords(Type)`) moved from `ICsvReaderRow` to `IReader`, since they read many rows; code that takes an
`IReaderRow` and calls them takes an `IReader` instead. The parser's position is in its `Context`.

```csharp before
using var parser = new CsvParser(reader);
string[] record;
while ((record = parser.Read()) != null)
{
    Console.WriteLine($"Row {parser.Row}: {parser.RawRecord}");
}
```

```csharp after
using var parser = new CsvParser(reader);
string[] record;
while ((record = parser.Read()) != null)
{
    Console.WriteLine($"Row {parser.Context.Row}: {parser.Context.RawRecord}");
}
```

| CsvHelper 2 | CsvHelper 3 |
|---|---|
| `ICsvReader` | `IReader`: `Read`, `ReadHeader`, `ReadAsync`, `GetRecords`, `EnumerateRecords` |
| `ICsvReaderRow` | `IReaderRow`: `Context`, `Configuration`, `GetField`, `TryGetField`, `GetRecord` |
| `ICsvWriter` | `IWriter`: `NextRecord`, `Flush`, `WriteRecords`, and the async methods |
| none | `IWriterRow`: `Context`, `Configuration`, `WriteField`, `WriteHeader`, `WriteRecord`, `WriteComment` |
| `parser.Row`, `RawRow`, `RawRecord`, `CharPosition`, `BytePosition` | `parser.Context.Row`, `RawRow`, `RawRecord`, `CharPosition`, `BytePosition` |
| `parser.Configuration`, `serializer.Configuration` (a `CsvConfiguration`) | An `IParserConfiguration`, an `ISerializerConfiguration` |
| `new CsvReader(parser)`, `new CsvWriter(serializer)` | Unchanged |

## Behavior changes

These compile once the code is ported, but behave differently:

| | CsvHelper 2 | CsvHelper 3 |
|---|---|---|
| `Read()` with a header | Reads the header, then the first record | Returns the header row as a record; call `ReadHeader()` |
| `ReadHeader()` | Reads the next row as the header | Takes the current record as the header |
| A quote in an unquoted field | Read as it is | `BadDataException`, unless `BadDataFound = null` |
| A mapped member without a column | `CsvMissingFieldException` when the first record is read | `ValidationException` before the first record |
| Booleans | `true`, `false`, `1`, `0`, `yes`, `y`, `no`, `n` | `true`, `false`, `1`, `0`; add others with `BooleanValues` |
| Empty or blank `DateTime` and `DateTimeOffset` | `MinValue` | Throws. For empty fields, use a nullable member or `Default` |
| `WriteHeader<T>()`, `WriteRecord(record)` | End the record | Don't; call `NextRecord()` |
| `WriteField<T>(null)` | `NullReferenceException` | An empty field |
| `ConvertToString` returns `null` | An empty field | No field |
| Exceptions from `GetRecords` and `GetRecord` | Thrown as they are, details in `Data["CsvHelper"]` | Wrapped in a `ReaderException` unless they are a `CsvHelperException`, details in `ReadingContext` |
| `DetectColumnCountChanges` | The parser throws `CsvBadDataException` | The reader calls `ReadingExceptionOccurred`, which throws `BadDataException` by default |

## Removed with no replacement

- **The Excel separator line (`HasExcelSeparator`, `WriteExcelSeparator()`):** the Excel-specific code left the
  library. To write the line, write it as the first record: `csv.WriteField("sep=;", false); csv.NextRecord();`. To
  read a file that starts with one, read that line from the `TextReader` (`reader.ReadLine()`) before you create the
  `CsvReader`.
- **`UseExcelLeadingZerosFormatForNumerics`:** write such a field yourself with
  `csv.WriteField("=\"" + value + "\"", false)`. When reading, the `="0123"` form is now an unquoted field with quotes
  in it: set `BadDataFound = null` and trim the `=` and quotes in a converter or `ConvertUsing`.
- **`CanConvertFrom` and `CanConvertTo`:** delete the overrides. See [Type converters](#type-converters) for the
  converters that relied on them.
- **The members that were obsolete in 2:** `GetField(index, converter)` and the other `object` overloads without a
  `Type`, `WriteField(type, field)`, `WriteRecord(type, record)`, `CsvClassMap.CreateMap()` and `PropertyMap<T>()`. Use
  the generic or `Type` overloads, and map in the class map's constructor.
- **`PropertyBindingFlags`:** it had no effect in 2.16.3. `MemberTypes` and `IncludePrivateMembers` choose what auto
  mapping uses.
- **The .NET Framework 2.0, 3.5 and 4.0, portable and .NET Standard 1.3 builds:** stay on CsvHelper 2 for those
  platforms, or move to .NET Framework 4.5 or a .NET Standard 2.0 platform.
