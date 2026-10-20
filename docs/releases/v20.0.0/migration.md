# Migrating from CsvHelper 19 to 20

CsvHelper 20 rewrites the parser and the writer for speed, and makes the configuration read-only so that one
configuration can be shared safely between threads. `CsvConfiguration` is now a `record` with `init` properties: you
set everything when you create it. What changes while you read or write, such as class maps and type converters, moves
to the new `CsvContext`, which also replaces `ReadingContext` and `WritingContext`.

What you need: C# 9 or later to set `init` properties. Projects that target .NET Framework, .NET Standard or .NET
Core 3.1 default to an older version, so set `<LangVersion>9</LangVersion>` or later in them. For Visual Basic, 21.0.5
adds a constructor that takes every setting as an optional argument. .NET 5 is added as a target framework; the others
stay.

## Creating a reader or writer

The constructors that took a configuration and a `leaveOpen` flag are gone: `LeaveOpen` is a setting now. The
constructors that take a culture keep `leaveOpen` as an optional argument. This is the same for `CsvReader`,
`CsvParser` and `CsvWriter`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
using var csv = new CsvReader(reader, config, true);
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    LeaveOpen = true,
};
using var csv = new CsvReader(reader, config);
```

`new CsvReader(IParser)` stays. `new CsvParser(IFieldReader)` and `new CsvWriter(ISerializer)` are gone, with the
field reader and the serializer: see [The parser](#the-parser) and [Writing](#writing).

## Configuration

The properties of `CsvConfiguration` are `init`-only, and `csv.Configuration` no longer has setters. Create the
configuration with all its settings first, then the reader or writer.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Configuration.Delimiter = ";";
csv.Configuration.MissingFieldFound = null;
csv.Configuration.PrepareHeaderForMatch = (header, index) => header.ToLower();
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    MissingFieldFound = null,
    PrepareHeaderForMatch = (header, index) => header.ToLower(),
};
using var csv = new CsvReader(reader, config);
```

- **Variants:** a record can be copied with changes, so `config with { Delimiter = "\t" }` is the same configuration
  with another delimiter.
- **Culture:** `CultureInfo` is set by the constructor only.
- **Class maps and type converters** are no longer on the configuration: see
  [Class maps and converters](#class-maps-and-converters).

### Your own configuration class

Only a record can derive from a record, so a configuration class becomes a `record`. Its constructor can still set
the properties.

```csharp before
public class SemicolonConfiguration : CsvConfiguration
{
    public SemicolonConfiguration() : base(CultureInfo.InvariantCulture)
    {
        Delimiter = ";";
    }
}
```

```csharp after
public record SemicolonConfiguration : CsvConfiguration
{
    public SemicolonConfiguration() : base(CultureInfo.InvariantCulture)
    {
        Delimiter = ";";
    }
}
```

### Callbacks

Each callback has its own delegate type in the `CsvHelper` namespace (`BadDataFound`, `PrepareHeaderForMatch`,
`ShouldQuote` and so on) instead of a `Func` or an `Action`. A lambda compiles as before. A variable, field or
parameter declared as a `Func` or an `Action` doesn't: declare it with the delegate type.

```csharp before
Func<string, int, string> lowerCase = (header, index) => header.ToLower();
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = lowerCase,
};
```

```csharp after
PrepareHeaderForMatch lowerCase = (header, index) => header.ToLower();
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = lowerCase,
};
```

The callbacks that got a `ReadingContext` get a `CsvContext` now (see [Reading state](#reading-state)), and
`ShouldQuote` gets the writer instead of a `WritingContext` (see [Writing](#writing)).

### Line endings

`NewLine` is a `char?` instead of the `NewLine` enum, and `NewLineString` is removed. Leave `NewLine` `null` to write
`Environment.NewLine`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = NewLine.LF,
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = '\n',
};
```

| CsvHelper 19 | CsvHelper 20 | Notes |
|---|---|---|
| `NewLine.LF` | `'\n'` | |
| `NewLine.CR` | `'\r'` | |
| `NewLine.Environment` | `null` | The default. |
| `NewLine.CRLF` | `null` | Writes `Environment.NewLine`, which is `\r\n` on Windows only. |
| `NewLineString` | none | 20 writes `config.NewLine?.ToString() ?? Environment.NewLine`. |

There is no `char` for `\r\n`, so 20 can't write it where it isn't `Environment.NewLine`. CsvHelper 21 makes
`NewLine` a string: see [Migrating from 20 to 21](../v21.0.0/migration.md).

When reading, `NewLine` is used only by the new `ParserMode.Escape`. The default mode, `ParserMode.RFC4180`, still
ends a record at `\r\n`, `\r` or `\n`.

## Class maps and converters

Class maps, the type converter cache and the type converter options cache move from the configuration to
`csv.Context`. They can change while you read or write, so they belong to one reader or writer, not to a
configuration that is shared.

```csharp before
public class Order
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
}

public sealed class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id).Name("OrderId");
        Map(m => m.Date);
    }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Configuration.RegisterClassMap<OrderMap>();
csv.Configuration.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd" };
var orders = csv.GetRecords<Order>().ToList();
```

```csharp after
public class Order
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
}

public sealed class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id).Name("OrderId");
        Map(m => m.Date);
    }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.RegisterClassMap<OrderMap>();
csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { "yyyy-MM-dd" };
var orders = csv.GetRecords<Order>().ToList();
```

| CsvHelper 19 | CsvHelper 20 |
|---|---|
| `csv.Configuration.RegisterClassMap<TMap>()` | `csv.Context.RegisterClassMap<TMap>()` |
| `csv.Configuration.UnregisterClassMap<TMap>()` | `csv.Context.UnregisterClassMap<TMap>()` |
| `csv.Configuration.AutoMap<T>()` | `csv.Context.AutoMap<T>()` |
| `csv.Configuration.Maps` | `csv.Context.Maps` |
| `csv.Configuration.TypeConverterCache` | `csv.Context.TypeConverterCache` |
| `csv.Configuration.TypeConverterOptionsCache` | `csv.Context.TypeConverterOptionsCache` |

**Each reader and writer has its own context.** In 19, a class map or converter registered on a configuration applied
to every reader and writer created with it. In 20, register it on each reader or writer you create.

## Reading state

`ReadingContext` is replaced by members of the reader and of the parser. The reader has the header and the current
field; the parser has the current record, its raw text and the row numbers.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Read();
csv.ReadHeader();
var header = csv.Context.HeaderRecord;
while (csv.Read())
{
    var fieldCount = csv.Context.Record.Length;
    Console.WriteLine($"Row {csv.Context.Row}: {csv.Context.RawRecord}");
}
```

```csharp after
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Read();
csv.ReadHeader();
var header = csv.HeaderRecord;
while (csv.Read())
{
    var fieldCount = csv.Parser.Count;
    Console.WriteLine($"Row {csv.Parser.Row}: {csv.Parser.RawRecord}");
}
```

| `csv.Context` in 19 | CsvHelper 20 |
|---|---|
| `HeaderRecord` | `csv.HeaderRecord` |
| `CurrentIndex` | `csv.CurrentIndex` |
| `ColumnCount` | `csv.ColumnCount` |
| `Record` | `csv.Parser.Record` |
| `Record.Length` | `csv.Parser.Count` |
| `RawRecord` | `csv.Parser.RawRecord` |
| `Row`, `RawRow` | `csv.Parser.Row`, `csv.Parser.RawRow` |
| `CharPosition`, `BytePosition` | `csv.Parser.CharCount`, `csv.Parser.ByteCount` |
| `ReaderConfiguration`, `ParserConfiguration` | `csv.Configuration`, `csv.Parser.Configuration` |

The callbacks that got a `ReadingContext` (`BadDataFound`, `GetDynamicPropertyName`, `HeaderValidated` and
`MissingFieldFound`) get a `CsvContext`: its `Reader` and `Parser` have the members above. In a `Convert` function,
the `IReaderRow` has `HeaderRecord`, `CurrentIndex` and `Parser` too.

## The parser

`CsvParser.Read()` returns `bool` instead of the record, and you get the fields from the parser after it. A field is
processed only when you get it, so get only the fields you need.

```csharp before
using var parser = new CsvParser(reader, CultureInfo.InvariantCulture);
string[] record;
while ((record = parser.Read()) != null)
{
    Console.WriteLine(record[0]);
}
```

```csharp after
using var parser = new CsvParser(reader, CultureInfo.InvariantCulture);
while (parser.Read())
{
    Console.WriteLine(parser[0]);

    // All the fields, in a new array each time.
    var record = parser.Record;
}
```

- `ReadAsync()` returns `Task<bool>` too.
- `IFieldReader` and `CsvFieldReader` are removed: the parser reads the `TextReader` itself. The protected virtual
  methods of `CsvParser` (`ReadLine`, `ReadField`, `ReadQuotedField` and the others) are removed with them.

## Mapping

`ConvertUsing` is renamed `Convert`, for reading and for writing.

```csharp before
public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}

public sealed class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id);
        Map(m => m.Total)
            .ConvertUsing(row => row.GetField<decimal>("Net") + row.GetField<decimal>("Tax"))
            .ConvertUsing(order => order.Total.ToString("0.00"));
    }
}
```

```csharp after
public class Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
}

public sealed class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id);
        Map(m => m.Total)
            .Convert(row => row.GetField<decimal>("Net") + row.GetField<decimal>("Tax"))
            .Convert(order => order.Total.ToString("0.00"));
    }
}
```

The fluent builder that `Factory.CreateClassMapBuilder<T>()` returns keeps `ConvertUsing`, with the new delegate types.

## Writing

The writer puts each field straight into its buffer. `ISerializer`, `CsvSerializer` and `RecordBuilder` are removed,
and `WritingContext` is replaced by members of the writer. `ShouldQuote` gets the writer, as an `IWriterRow`, instead
of the `WritingContext`, and so does the default `ConfigurationFunctions.ShouldQuote`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // Quote what CsvHelper quotes, and numbers with leading zeros.
    ShouldQuote = (field, context) => ConfigurationFunctions.ShouldQuote(field, context) || (field != null && field.StartsWith("0")),
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // Quote what CsvHelper quotes, and numbers with leading zeros.
    ShouldQuote = (field, row) => ConfigurationFunctions.ShouldQuote(field, row) || (field != null && field.StartsWith("0")),
};
```

| `WritingContext` in 19 | CsvHelper 20 |
|---|---|
| `WriterConfiguration`, `SerializerConfiguration` | `csv.Configuration` (`row.Configuration` in `ShouldQuote`) |
| `Row` | `csv.Row` |
| `Record` | none: the fields are already in the writer's buffer |

- **`Row` and `Index` in 20.0.0:** the writer's `Row` and `Index` stay at 1 while you write. 21.0.1 fixes this.
- **Changing how fields are written:** derive from `CsvWriter` and override `WriteField(string field, bool
  shouldQuote)`, which is still virtual, instead of replacing the serializer.
- **`await using` on .NET Framework:** the .NET Framework 4.7 build of `CsvWriter` no longer implements
  `IAsyncDisposable`, so `await using` doesn't compile in a project that targets .NET Framework 4.7 or later. Use
  `using` there. The .NET Standard 2.1 and .NET 5 builds implement it.

## Errors and bad data

Exceptions have one `Context` property, a `CsvContext`, instead of `ReadingContext` and `WritingContext`, and their
constructors take a `CsvContext`. `BadDataFound` gets the new context too.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = context => Console.WriteLine($"Bad data: {context.RawRecord}"),
    ReadingExceptionOccurred = exception =>
    {
        Console.WriteLine($"Skipped row {exception.ReadingContext.Row}: {exception.Message}");
        return false;
    },
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = context => Console.WriteLine($"Bad data: {context.Parser.RawRecord}"),
    ReadingExceptionOccurred = exception =>
    {
        Console.WriteLine($"Skipped row {exception.Context.Parser.Row}: {exception.Message}");
        return false;
    },
};
```

**In `BadDataFound`, don't get `context.Parser.Record` or `context.Parser[i]`.** Fields are processed when you get
them, so getting the bad field again calls `BadDataFound` again, without end. Use `context.Parser.RawRecord`.

## Custom implementations

If you implement the interfaces yourself, these members changed:

| Interface | Changes |
|---|---|
| `IParser` | `Read()` returns `bool` and `ReadAsync()` returns `Task<bool>`. `Context` is a `CsvContext`. Adds `Count`, `this[int]`, `Record`, `RawRecord`, `Row`, `RawRow`, `CharCount` and `ByteCount`. `FieldReader` is removed. |
| `IReader` | `Parser` moves to `IReaderRow`. |
| `IReaderRow` | `Context` is a `CsvContext`. Adds `ColumnCount`, `CurrentIndex`, `HeaderRecord` and `Parser`. |
| `IWriterRow` | `Context` is a `CsvContext`. Adds `HeaderRecord`, `Row` and `Index`. |
| `IParserConfiguration` | No setters. Adds `CacheFields`, `LeaveOpen`, `NewLine`, `Mode` and `WhiteSpaceChars`. `IgnoreQuotes` is removed. |
| `IReaderConfiguration` | No setters. The class map members (`Maps`, `RegisterClassMap`, `UnregisterClassMap`, `AutoMap`), `TypeConverterCache` and `TypeConverterOptionsCache` are removed. |
| `IWriterConfiguration` | No setters. Takes the members of `ISerializerConfiguration`, which is removed, except `NewLineString`; `NewLine` is a `char?`. Adds `LeaveOpen`. The class map members, `TypeConverterCache`, `TypeConverterOptionsCache`, `QuoteString` and `DoubleQuoteString` are removed. |

The callbacks of the configuration interfaces use the new delegate types (see [Callbacks](#callbacks)).

## Behavior changes

- **The default line ending:** 19 wrote `\r\n` with `CultureInfo.InvariantCulture` and `Environment.NewLine` with
  other cultures. 20 writes `Environment.NewLine` unless you set `NewLine`, so files written on Linux and macOS with
  the invariant culture end their lines with `\n` instead of `\r\n`. From 21.0.2, the default is `\r\n` with every
  culture.
- **Exception messages** end with the state of the reader, the parser or the writer, and with the raw record when
  reading. Keep that in mind if the data is sensitive and you log the messages.

## Removed with no replacement

- **`Caches` and `ClearCache`:** the caches are internal to each reader and writer. Create a new `CsvReader` or
  `CsvWriter` to start with empty caches.
- **`IgnoreQuotes`:** 20 has no setting that ignores quotes. 21.1.0 adds `ParserMode.NoEscape`, which ignores quote
  and escape characters.
