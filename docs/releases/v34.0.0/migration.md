# Migrating from CsvHelper 33 to 34

CsvHelper 34 is a rewrite. The reader parses UTF-8 bytes instead of characters (with SIMD on .NET 8 and later), records are
mapped by code that a source generator writes at compile time instead of by reflection, and the settings are immutable
option records. Almost every public type changed or was replaced, so this guide goes concept by concept: how each thing
you did in 33 is done now, with the old and new code side by side. Coming from an older version, read the guides of
the versions in between for the old side ([32](../v32.0.0/migration.md), for example, changed dynamic records); the new
side is the same.

What you need: .NET 8 or later, .NET Standard 2.0 or 2.1 (.NET 5 to 7, Mono, Unity), or .NET Framework 4.7 or later (4.6.2 is
no longer supported); C# 9 or later, since the options have `init` properties; and a Roslyn 4.3 compiler or later
(the .NET 6.0.400 SDK, Visual Studio 2022 17.3) for the source generator, which comes in the package.

## At a glance

| CsvHelper 33 | CsvHelper 34 |
|---|---|
| `new CsvReader(textReader, CultureInfo.InvariantCulture)` | `CsvReader.FromFile(path)`, `CsvReader.Create(stream or textReader)` |
| `CsvConfiguration` | `CsvReaderOptions` and `CsvWriterOptions`, immutable records |
| Any class, mapped by reflection | `[CsvRecord] partial` types, mapped by generated code |
| `ClassMap<T>` and `RegisterClassMap` | Attributes, or `[CsvRecordMap(typeof(T))]` for types you can't change |
| `ITypeConverter`, `DefaultTypeConverter` | `ICsvConverter<T>` |
| `GetField<T>(name)` | `Parse<T>(index or name)`, `GetString(name)` |
| `NextRecord()` | `EndRecord()` |
| `GetRecords<dynamic>()` gives an `IDictionary<string, object>` | `GetRecords<dynamic>()` gives a `CsvDynamicRecord` |
| `BadDataException`, `TypeConverterException`, ... | `CsvFormatException` and standard exceptions |
| `CsvHelper.Configuration`, `.Configuration.Attributes`, `.TypeConversion` | One namespace: `CsvHelper` |

## Namespaces

Everything is in the `CsvHelper` namespace now. Remove `using CsvHelper.Configuration;`,
`using CsvHelper.Configuration.Attributes;`, `using CsvHelper.TypeConversion;` and `using CsvHelper.Expressions;`:
they no longer exist, so they don't compile.

## Creating a reader

A reader opens its input with a factory method instead of a constructor, and no longer needs a culture. Give it a file
name, a stream or bytes, and it reads UTF-8 directly; a `TextReader` still works, for input that is already text.

```csharp before
using var streamReader = new StreamReader("orders.csv");
using var csv = new CsvReader(streamReader, CultureInfo.InvariantCulture);
```

```csharp after
using var csv = CsvReader.FromFile("orders.csv");
```

| Input | CsvHelper 34 |
|---|---|
| A file | `CsvReader.FromFile(path, options)` |
| A `Stream` | `CsvReader.Create(stream, options, leaveOpen)` |
| Bytes in memory | `CsvReader.Create(byte[] or ReadOnlyMemory<byte>, options)`, without copying |
| A string | `CsvReader.FromText(text, options)` |
| A `TextReader` | `CsvReader.Create(textReader, options, leaveOpen)` |

- **Encodings:** files, streams and bytes are read as UTF-8, and a byte order mark is skipped. A UTF-16 or UTF-32
  byte order mark is detected and the input is converted. For another encoding, such as Windows-1252, set
  `CsvReaderOptions.Encoding`; a `TextReader` decodes the input itself, as before.
- **No parser:** `new CsvReader(IParser)` and `CsvParser` are gone; the reader is also the parser. The reader is
  `sealed` and has no interfaces to mock (`IReader`, `IReaderRow` and `IParser` are gone): test code that reads with
  [real input in memory](../../pages/testing.md#testing-code-that-reads).

## Configuration

`CsvConfiguration` was one mutable record for reading and writing, created from a culture. It is replaced by two
immutable records, `CsvReaderOptions` and `CsvWriterOptions`, that need no culture: numbers and dates are read and
written with the invariant culture unless you set `FormatProvider`. Pass them when you create the reader or writer.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    PrepareHeaderForMatch = args => args.Header.ToLower(),
    TrimOptions = TrimOptions.Trim,
    AllowComments = true,
    Comment = '#',
    BadDataFound = null,
};
using var csv = new CsvReader(reader, config);
```

```csharp after
var options = new CsvReaderOptions
{
    Separator = ';',
    TrimWhitespace = true,
    CommentPrefix = '#',
};
using var csv = CsvReader.Create(reader, options);
```

The new code has fewer settings because the defaults changed: header names are matched ignoring case and surrounding
whitespace when there is no exact match, so most `PrepareHeaderForMatch` functions aren't needed, and bad quoting is
recovered from instead of thrown, which is what `BadDataFound = null` asked for. Options are records, so a variant is
`options with { Separator = '\t' }`, and `CsvReaderOptions.Default` and `CsvWriterOptions.Default` are the defaults.

### Reader options

| `CsvConfiguration` | `CsvReaderOptions` | Notes |
|---|---|---|
| `CultureInfo` (constructor) | `FormatProvider` | Defaults to the invariant culture. |
| `Delimiter` | `Separator` | One character. `null` (the default) detects a comma, semicolon, tab or vertical bar from the start of the input. |
| `DetectDelimiter`, `DetectDelimiterValues` | `Separator = null` | Detection is the default. `DetectSeparatorHint` reads Excel's `sep=` line. |
| `HasHeaderRecord` | `HasHeader` | |
| `Mode` (`CsvMode.RFC4180`) | `Mode` (`CsvMode.Rfc4180`) | The member is spelled `Rfc4180` now. `FixedWidth` is new, with `FieldWidths`. |
| `Quote`, `Escape` | `Quote`, `Escape` | `Escape` is for `CsvMode.Escape` only; Rfc4180 always doubles quotes. |
| `AllowComments`, `Comment` | `CommentPrefix` | `null` means no comments. |
| `IgnoreBlankLines` | `SkipEmptyLines` | |
| `TrimOptions.Trim` | `TrimWhitespace` | |
| `TrimOptions.InsideQuotes` | `TrimInsideQuotes` | |
| `WhiteSpaceChars` | `WhitespaceCharacters` | A string; defaults to space and tab. |
| `PrepareHeaderForMatch` | `NormalizeHeaderName` | A `Func<string, string>`; usually not needed (see above). |
| `ShouldSkipRecord` | `ShouldSkipRecord` | Gets the record as a `CsvRow`. |
| `BadDataFound` | `MalformedData`, `OnMalformedRecord` | See [Bad data and errors](#bad-data-and-errors). |
| `LineBreakInQuotedFieldIsBadData` | `AllowLineBreaksInQuotes = false` | |
| `MissingFieldFound` | `MissingField` | |
| `DetectColumnCountChanges` | `FieldCountMismatch` | |
| `ReadingExceptionOccurred` | `OnRecordError` | Returns `true` to **skip** the record; the old one returned `true` to throw. |
| `HeaderValidated` | `[CsvColumn(Optional = true)]`, `TryValidateHeader<T>` | |
| `MaxFieldSize` | `MaxRecordSize` | The size of a whole record, in bytes. |
| `ExceptionMessagesContainRawData` | `IncludeValuesInExceptions` | |
| `CountBytes` | none | `ByteOffset` is always available. |
| `BufferSize` | `BufferSize` | In bytes, 64 KiB by default. |
| `CacheFields` | `PoolStrings` | Reuses the strings of repeated values. |

The settings that chose the members and constructors to map (`MemberTypes`, `IncludePrivateMembers`,
`IgnoreReferences`, `ReferenceHeaderPrefix`, `GetConstructor`, `ShouldUseConstructorParameters`) are gone: the
generator decides, and attributes such as `[CsvColumn]`, `[CsvIgnore]` and `[CsvConstructor]` change it (see
[Records](#records)).

### Writer options

| `CsvConfiguration` | `CsvWriterOptions` | Notes |
|---|---|---|
| `CultureInfo` (constructor) | `FormatProvider` | Defaults to the invariant culture. |
| `Delimiter` | `Separator` | One character, `','` by default (not the culture's list separator). |
| `HasHeaderRecord` | `HasHeader` | |
| `NewLine` | `NewLine` | `"\r\n"` (the default), `"\n"` or `"\r"`. |
| `ShouldQuote` | `Quoting`, `ShouldQuote` | `Quoting = CsvQuoting.Always` quotes every field. |
| `InjectionOptions` | `Injection` | `CsvInjection.None`, `Escape`, `Strip` or `Throw` (was `Exception`). |
| `InjectionCharacters`, `InjectionEscapeCharacter` | `InjectionCharacters`, `InjectionEscape` | |
| `Comment` | `CommentPrefix` | `null` by default: `WriteComment` throws until it is set. |
| `Quote` | none | The writer always quotes with `"`. |

New writer options: `NullValue`, `TrueValue` and `FalseValue` for how those are written, `ValidateFieldCount` (on by
default) and `WriteByteOrderMark`.

### Options on the record type

The class attributes that `CsvConfiguration.FromAttributes<T>()` read (`[Delimiter]`, `[HasHeaderRecord]`,
`[CultureInfo]` and so on) are one attribute now, `[CsvOptions]`, which `CsvReaderOptions.For<T>()` and
`CsvWriterOptions.For<T>()` read:

```csharp before
[Delimiter(";")]
[HasHeaderRecord(true)]
public class Order
{
    public int Id { get; set; }
}

var config = CsvConfiguration.FromAttributes<Order>(CultureInfo.InvariantCulture);
using var csv = new CsvReader(reader, config);
```

```csharp after
[CsvRecord]
[CsvOptions(Separator = ';', HasHeader = true)]
public partial class Order
{
    public int Id { get; set; }
}

using var csv = CsvReader.Create(reader, CsvReaderOptions.For<Order>());
```

## Reading by hand

The read loop is the same, but the header is read by the first `Read` (or by the first lookup of a column by name), so
`ReadHeader` is no longer called first. Field getters are typed methods instead of `GetField<T>`.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Read();
csv.ReadHeader();
while (csv.Read())
{
    var id = csv.GetField<int>("Id");
    var name = csv.GetField("Name");
    var price = csv.GetField<decimal>(2);
}
```

```csharp after
using var csv = CsvReader.Create(reader);
var idColumn = csv.GetFieldIndex("Id");    // reads the header
while (csv.Read())
{
    var id = csv.Parse<int>(idColumn);
    var name = csv.GetString("Name");
    var price = csv.Parse<decimal>(2);
}
```

**Don't port `csv.Read(); csv.ReadHeader();` as it is.** `ReadHeader()` reads the *current* record as the header, so
after a `Read` it takes the first data record for the header. Call it only to read a second header in the middle of a
file.

| CsvHelper 33 | CsvHelper 34 |
|---|---|
| `GetField(index)`, `GetField(name)` | `GetString(index)`, `GetString(name)`: `""` for an empty field, never `null` |
| `GetField<int>(index)` and the other built-in types, `TryGetField<int>` | `Parse<int>(index or name)` and `TryParse<int>(index, out value)`, for `int`, `long`, `short`, `byte`, `uint`, `ulong`, `double`, `float`, `decimal`, `bool`, `DateTime`, `DateTimeOffset`, `Guid` and `string` on every target; `ParseExact<DateTime>(index, format)` for an exact format |
| `GetField<T>(...)` for other types, `TryGetField<T>` | `Parse<T>(index or name)` and `TryParse<T>(index, out value)` for any `ISpanParsable<T>` on .NET 8 and later (`TimeSpan`, `BigInteger`, `char`, `IPAddress` and enums everywhere); `Parse(index, (text, provider) => ...)` and `TryParse(index, parser, out value)` everywhere |
| `GetField(name, index)` | `GetString(GetFieldIndex(name, index))` |
| `csv[index]`, `csv[name]` | `csv[index]` and `csv[name]` are a `CsvField`: `.ToString()` or `.Parse<T>()` |
| `HeaderRecord` | `Headers`, empty when there is no header |
| `ColumnCount`, `Parser.Count` | `FieldCount` |
| `Parser.Row`, `Parser.RawRow` | `RowNumber` (data records only), `LineNumber` (with `TrackLineNumbers`) |
| `Parser.RawRecord` | `Current.ToString()`; `RawRecord` is the UTF-8 bytes |
| `Context`, `Configuration` | `Options` |

`GetString` never returns `null`; for files with `NULL` values, list them in `NullValues` and use `GetStringOrNull`
and `IsNull`. See [Getting field values](../../pages/reading.md#getting-field-values).

## Records

In 33, `GetRecords<T>` mapped any class with reflection when it was first used. Now a source generator writes the
mapping code of each record type at compile time, so a record type is marked `[CsvRecord]` and declared `partial`
(the generator adds to it). That's the main change you make to your types.

```csharp before
public class Order
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var orders = csv.GetRecords<Order>().ToList();
```

```csharp after
[CsvRecord]
public partial class Order
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

using var csv = CsvReader.Create(reader);
var orders = csv.GetRecords<Order>().ToList();
```

- **A type without a mapper throws.** `GetRecords<T>` on a type that is not `[CsvRecord]` (or mapped by
  `[CsvRecordMap]`, or a [hand-written mapper](../../pages/records.md#hand-written-mappers)) throws
  `InvalidOperationException`. It also throws when the project does not run the generator, such as with a compiler
  older than Roslyn 4.3.
- **More members are read:** public and internal properties and fields that can be set, those of base classes too,
  and the parameters of the constructor. 33 read only public properties by default, so a public field or an internal
  property is now a required column too: mark it `[CsvIgnore]` if the file has no column for it. A private member, or
  a property with a `private set`, is read only with `[CsvColumn]`. Records with a primary constructor work as they
  are; `[CsvConstructor]` picks a constructor when there are several.
- **Members of other classes:** 33 read the properties of a member whose type is a class as columns of the same
  record (a reference map). Now that type must be `[CsvRecord]` too, to be a
  [nested record](../../pages/records.md#nested-records); without it, a static `Parse` method or a converter, the
  member is a compile error (CSVH002).
- **Missing columns are found up front:** when the header lacks a column that a member needs, `GetRecords<T>` throws a
  `KeyNotFoundException` naming every missing column, before the first record is read.
- **`EnumerateRecords(record)`, `GetRecord<T>()` and `GetRecordsAsync<T>()`** work as before, for `[CsvRecord]` types.
- **Anonymous types and `Type` objects** can't be record types: `GetRecords(anonymousTypeDefinition)` and
  `GetRecords(Type)` are gone. Use a `[CsvRecord] partial record` with a primary constructor, which is about as short,
  or [dynamic records](#dynamic-records).

## Mapping attributes

The attributes that chose and converted columns are named after what they do, with a `Csv` prefix, and several were
merged into `[CsvColumn]`.

```csharp before
public class Order
{
    [Name("Order Id")]
    public int Id { get; set; }

    [Index(3)]
    public string Customer { get; set; }

    [Optional]
    public int? Quantity { get; set; }

    [Format("yyyyMMdd")]
    public DateTime Ordered { get; set; }

    [BooleanTrueValues("yes")]
    [BooleanFalseValues("no")]
    public bool Shipped { get; set; }

    [Ignore]
    public decimal Total { get; set; }
}
```

```csharp after
[CsvRecord]
public partial class Order
{
    [CsvColumn("Order Id")]
    public int Id { get; set; }

    [CsvColumn(Index = 3)]
    public string Customer { get; set; } = "";

    [CsvColumn(Optional = true)]
    public int? Quantity { get; set; }

    [CsvFormat("yyyyMMdd")]
    public DateTime Ordered { get; set; }

    [CsvBooleanValues("yes", "no")]
    public bool Shipped { get; set; }

    [CsvIgnore]
    public decimal Total { get; set; }
}
```

| CsvHelper 33 | CsvHelper 34 |
|---|---|
| `[Name("a", "b")]` | `[CsvColumn("a", "b")]`: the other names are tried in order |
| `[Index(i)]`, `[Index(i, end)]` | `[CsvColumn(Index = i)]`, `[CsvColumn(Index = i, EndIndex = end)]` |
| `[NameIndex(n)]` | `[CsvColumn("a", NameIndex = n)]` |
| `[Optional]` | `[CsvColumn(Optional = true)]`; constructor parameters with a default value are always optional |
| `[Ignore]`, `[IgnoreBase]` | `[CsvIgnore]`, `[CsvIgnoreBase]` |
| `[Default(value)]` | `[CsvDefault(value)]`, for an empty field |
| `[Constant(value)]` | `[CsvConstant(value)]` |
| `[Format("f")]` | `[CsvFormat("f", "other")]` |
| `[CultureInfo("de-DE")]` | `[CsvCulture("de-DE")]` |
| `[BooleanTrueValues]`, `[BooleanFalseValues]` | `[CsvBooleanValues(true, false)]` |
| `[NullValues("NULL")]` | `[CsvNullValues("NULL")]` |
| `[TypeConverter(typeof(C))]` | `[CsvConverter(typeof(C))]`, with an `ICsvConverter<T>` (see [Type converters](#type-converters)) |
| `[HeaderPrefix("Ship ")]` | `[CsvColumn(Prefix = "Ship ")]` on a member whose type is a record type |
| `[Name]` on an enum member | `[CsvEnumName("on hold")]` |
| `[DateTimeStyles]`, `[NumberStyles]`, `[EnumIgnoreCase]` | Removed: see [Behavior changes](#behavior-changes) |

`[CsvDefault]` is used for an empty field only, not for a missing column. The `useOnConversionFailure` of a class map's
`Default(value, true)` has no replacement: values that don't parse fail the record, which `OnRecordError` can skip.

## Class maps

A `ClassMap<T>` configured a type you couldn't or didn't want to put attributes on. Its replacement is a map class:
`[CsvRecordMap(typeof(T))]` on a `partial` class whose members have the names and types of `T`'s members and carry the
attributes for them. The generator writes the mapper of `T` from it, and there is nothing to register.

```csharp before
public class Supplier
{
    public string Code { get; set; }
    public decimal Rating { get; set; }
    public DateTime Since { get; set; }
    public string Notes { get; set; }
}

public class SupplierMap : ClassMap<Supplier>
{
    public SupplierMap()
    {
        Map(m => m.Code).Name("Supplier Code");
        Map(m => m.Rating);
        Map(m => m.Since).TypeConverterOption.Format("yyyyMMdd");
        Map(m => m.Notes).Ignore();
    }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.RegisterClassMap<SupplierMap>();
var suppliers = csv.GetRecords<Supplier>().ToList();
```

```csharp after
public class Supplier
{
    public string Code { get; set; } = "";
    public decimal Rating { get; set; }
    public DateTime Since { get; set; }
    public string Notes { get; set; } = "";
}

[CsvRecordMap(typeof(Supplier))]
internal partial class SupplierMap
{
    [CsvColumn("Supplier Code")]
    public string Code { get; set; } = "";
    [CsvFormat("yyyyMMdd")]
    public DateTime Since { get; set; }
    [CsvIgnore]
    public string Notes { get; set; } = "";
}

using var csv = CsvReader.Create(reader);
var suppliers = csv.GetRecords<Supplier>().ToList();
```

Members without a stand-in in the map class (`Rating` here) are mapped as usual. The rest of `ClassMap`:

| `ClassMap` | CsvHelper 34 |
|---|---|
| `AutoMap()` | Automatic: every member is mapped unless it is `[CsvIgnore]`. |
| `Map(m => m.X).Name(...).Index(...).Optional()` | `[CsvColumn(...)]` on the stand-in member |
| `.Default(...)`, `.Constant(...)` | `[CsvDefault]`, `[CsvConstant]` |
| `.TypeConverter<C>()`, `.TypeConverterOption.Format(...)` | `[CsvConverter(typeof(C))]`, `[CsvFormat]` |
| `.Validate(args => ...)` | `[CsvValidate(nameof(Method))]`, a static method that returns whether the value is valid |
| `.Convert(args => ...)` | An `ICsvConverter<T>`, or a [hand-written mapper](../../pages/records.md#hand-written-mappers) for logic that spans columns |
| `References<TMap>(m => m.Address)` | A member whose type is a `[CsvRecord]` type, with `[CsvColumn(Prefix = "...")]` if its columns have a prefix; a type mapped by `[CsvRecordMap]` can't be nested yet |
| `Parameter("name")` | `[CsvColumn]` on the constructor parameter, `[CsvConstructor]` to choose the constructor |
| `RegisterClassMap<TMap>()`, `UnregisterClassMap` | Not needed: the generated code registers itself |

## Type converters

`ITypeConverter` converted between an `object` and a string with access to the reader row and member map. Its
replacement, `ICsvConverter<T>`, is typed: it reads a field from the reader (`GetString(index)`, or the UTF-8 bytes
with `GetSpan(index)`) and writes one with `WriteField`.

```csharp before
public class PercentConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
        double.Parse(text.TrimEnd('%'), CultureInfo.InvariantCulture) / 100;

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) =>
        ((double)value * 100).ToString(CultureInfo.InvariantCulture) + "%";
}

public class Link
{
    [TypeConverter(typeof(PercentConverter))]
    public double Share { get; set; }
}
```

```csharp after
public sealed class PercentConverter : ICsvConverter<double>
{
    public double Read(CsvReader reader, int index) =>
        double.Parse(reader.GetString(index).TrimEnd('%'), CultureInfo.InvariantCulture) / 100;

    public void Write(CsvWriter writer, double value) =>
        writer.WriteField((value * 100).ToString(CultureInfo.InvariantCulture) + "%");
}

[CsvRecord]
public partial class Link
{
    [CsvConverter(typeof(PercentConverter))]
    public double Share { get; set; }
}
```

- **Converters are created with their parameterless constructor**: the attribute takes no constructor arguments for
  them, as `[TypeConverter(typeof(C), args)]` did. The threads of `ParallelEnumerate` and `ParallelWriteRecords` share
  them, so keep them stateless.
- **A converter for every member of a type:** 33 registered one with `Context.TypeConverterCache.AddConverter<T>`. Now
  it's an assembly attribute, `[assembly: CsvConverter(typeof(MoneyConverter))]`, read at compile time.
- **Converter options per type** (`Context.TypeConverterOptionsCache`) are gone: use the reader options
  (`FormatProvider`, `NullValues`, `TrueValues`, `FalseValues`) or the member attributes.
- **Built-in types:** `string`, the numeric types, `bool`, `DateTime`, `DateTimeOffset`, `Guid`, enums, `Uri`,
  nullable types, any type with a static `Parse` method (`DateOnly`, `TimeSpan` and your own types), collections and
  nested record types. Any other member type is a compile error (CSVH002) until it gets a `[CsvConverter]` or
  `[CsvIgnore]`, where 33 mapped it as a reference or skipped it.

## Writing

A writer is created like a reader. `NextRecord` is `EndRecord`, and it throws when the record has no fields.

```csharp before
using var streamWriter = new StreamWriter("orders.csv");
using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);
csv.WriteField("Id");
csv.WriteField("Name");
csv.NextRecord();
csv.WriteField(1);
csv.WriteField("Widget");
csv.NextRecord();
```

```csharp after
using var csv = CsvWriter.ToFile("orders.csv");
csv.WriteField("Id");
csv.WriteField("Name");
csv.EndRecord();
csv.WriteField(1);
csv.WriteField("Widget");
csv.EndRecord();
```

Files and streams are written as UTF-8 (`WriteByteOrderMark` adds a byte order mark); `CsvWriter.Create(textWriter)`
writes to a `TextWriter`, which encodes the text itself.

### Writing records

`WriteRecords` writes `[CsvRecord]` types, with the header first. `WriteHeader<T>()` and `WriteRecord<T>(record)` now
end the record themselves:

```csharp before
public class Order
{
    public int Id { get; set; }
}

var orders = new List<Order> { new Order { Id = 1 } };
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
csv.WriteHeader<Order>();
csv.NextRecord();
foreach (var order in orders)
{
    csv.WriteRecord(order);
    csv.NextRecord();
}
```

```csharp after
[CsvRecord]
public partial class Order
{
    public int Id { get; set; }
}

var orders = new List<Order> { new Order { Id = 1 } };
using var csv = CsvWriter.Create(writer);
csv.WriteHeader<Order>();
foreach (var order in orders)
{
    csv.WriteRecord(order);
}
```

**A literal port that keeps `EndRecord()` after `WriteHeader<T>()` or `WriteRecord(record)` throws**, since it ends a
record with no fields. Remove those calls, or write the whole list with `csv.WriteRecords(orders)`.

- **Only record types:** `WriteRecords` and `WriteRecord` write `[CsvRecord]` types (and dynamic records, dictionaries
  and `ExpandoObject`). Anonymous types and other classes, which 33 wrote by reflection, throw
  `InvalidOperationException`; make them `[CsvRecord] partial`, or write them with the `WriteCsv` extension of
  [CsvHelper.Linq](../../pages/linq.md#writing-query-results), which writes them by their public members.
- **Only the columns that are read:** a record type writes the columns its mapper reads, so a property without a
  setter, such as a computed one, is no longer written, where 33 wrote every public property with a getter. Write such
  values from a `[CsvRecord]` type of their own whose members can be set, or field by field with `WriteField`.
- **Values:** `WriteField` has overloads for strings, numbers, booleans, dates and spans, and formats numbers straight
  into the UTF-8 buffer. `WriteField(value, ITypeConverter)` and `WriteConvertedField` are gone: call your
  `ICsvConverter<T>.Write(csv, value)`.
- **Quoting a field:** `WriteField(text, shouldQuote: true)` is `WriteField(text, CsvQuoting.Always)`.
- **Comments:** `WriteComment` needs `CommentPrefix` in the writer options, and ends its line itself, so remove the
  `NextRecord()` that followed it.
- **Counts:** `Index` is `FieldCount`, and `Row` is `RecordCount + 1`: `RecordCount` counts the records written, where
  `Row` numbered the current one from 1.

## Dynamic records

`GetRecords<dynamic>()` gave each record as a dynamic object that was an `IDictionary<string, object>`. It gives a
`CsvDynamicRecord` now: the same `record.Name` syntax, much faster, but read-only, and its values are strings.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
foreach (IDictionary<string, object> record in csv.GetRecords<dynamic>())
{
    record["Total"] = 0;
    Console.WriteLine(record["Name"]);
}
```

```csharp after
using var csv = CsvReader.Create(reader);
foreach (var record in csv.GetRecords<CsvDynamicRecord>())
{
    Dictionary<string, string?> copy = record.ToDictionary();
    copy["Total"] = "0";
    Console.WriteLine(record["Name"]);
}
```

- **Casts to `IDictionary<string, object>` fail:** a `CsvDynamicRecord` is an `IReadOnlyDictionary<string, string?>`.
  `ToDictionary()` gives a copy you can change.
- **Typed values:** `record.Parse<int>("Age")` parses a column, as the reader's `Parse<T>` does.
- **Writing** dynamic records, `ExpandoObject` and dictionaries works with `WriteRecords`, as before.

## Bad data and errors

33 threw on bad quoting (`BadDataFound`) and on missing fields (`MissingFieldFound`) unless you set the callbacks to
`null`. 34 recovers by default, as most of those `null` settings asked, and has options to throw or skip instead:

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args => Console.WriteLine($"Bad data: {args.RawRecord}"),
    MissingFieldFound = null,
    ReadingExceptionOccurred = args => false,
};
```

```csharp after
var options = new CsvReaderOptions
{
    OnMalformedRecord = record => Console.WriteLine($"Bad data: {record.RawRecord}"),
    OnRecordError = error => true,
};
```

- **Bad quoting:** `MalformedData = CsvMalformedDataHandling.Recover` (the default) reads the record as well as it
  can, `Throw` throws a `CsvFormatException`, `Skip` skips the record. `OnMalformedRecord` reports each record that is
  recovered or skipped.
- **Missing fields:** `MissingField = CsvMissingFieldHandling.ReturnEmpty` (the default) gives `""` for a field past
  the end of a record, `Throw` throws `IndexOutOfRangeException`.
- **Records that can't be mapped:** `OnRecordError` returns **`true` to skip** the record and go on, `false` to throw.
  That is the opposite of `ReadingExceptionOccurred`, whose `true` meant throw, so check every port of it.
- **Header validation:** there is no `HeaderValidated` callback. A required column that is missing throws
  `KeyNotFoundException` from `GetRecords<T>`; mark members that may be missing `[CsvColumn(Optional = true)]`, or
  check first with `TryValidateHeader<T>(out var missing)`.
- **Field counts:** `FieldCountMismatch = CsvFieldCountHandling.Throw` or `Skip` replaces `DetectColumnCountChanges`.

See [Messy files](../../pages/messy-files.md) for the details.

### Exceptions

The `CsvHelperException` hierarchy is gone. Errors in the input are a `CsvFormatException` (a `FormatException`) with
the record number, byte offset and, for records, the member and field; mistakes in your code are the standard
exceptions.

| CsvHelper 33 | CsvHelper 34 |
|---|---|
| `CsvHelperException`, to catch them all | No common base: `CsvFormatException` for the input and for records that can't be read, the exceptions below for the rest |
| `BadDataException` | `CsvFormatException`, with `MalformedData = Throw` |
| `TypeConverterException`, `ReaderException` | `CsvFormatException` from the record methods, with the cause as `InnerException`; `FormatException` from `Parse<T>` |
| `FieldValidationException` | `CsvValidationException`, as the `InnerException` of a `CsvFormatException` |
| `HeaderValidationException` | `KeyNotFoundException` |
| `MissingFieldException` | `IndexOutOfRangeException` (with `MissingField = Throw`); `KeyNotFoundException` for a name the header lacks |
| `MaxFieldSizeException` | `CsvFormatException` (`MaxRecordSize`) |
| `WriterException` | `InvalidOperationException` for a type or record that can't be written; other exceptions, such as a converter's, are not wrapped |
| `ConfigurationException` | `ArgumentException` when the reader or writer is created |

The `Context` property of the old exceptions is replaced by the properties of `CsvFormatException`: `RecordNumber`,
`LineNumber` (with `TrackLineNumbers`), `ByteOffset`, `Member` and `FieldIndex`.

## DataTable and IDataReader

`CsvDataReader` takes the reader, and has no schema table parameter: every column is a string, and the reader's
`NullValues` become `DBNull`.

```csharp before
using System.Data;

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
using var dataReader = new CsvDataReader(csv);
var table = new DataTable();
table.Load(dataReader);
```

```csharp after
using System.Data;

using var csv = CsvReader.Create(reader);
using var dataReader = new CsvDataReader(csv);
var table = new DataTable();
table.Load(dataReader);
```

Code that needs typed values calls the data reader's typed getters (`GetInt32`, `GetDecimal`, `GetDateTime` and so
on), which parse the field as the CSV reader's `Parse<T>` does.

## Async

The async methods return `ValueTask` instead of `Task` and take a `CancellationToken`. `await` works as before; code
that stored the `Task` in a variable or passed it to `Task.WhenAll` needs `.AsTask()`.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
while (await csv.ReadAsync())
{
    Console.WriteLine(csv.GetField(0));
}
```

```csharp after
using var csv = CsvReader.Create(reader);
while (await csv.ReadAsync(cancellationToken))
{
    Console.WriteLine(csv.GetString(0));
}
```

`GetRecordsAsync<T>`, `EnumerateRecordsAsync`, `WriteRecordsAsync`, `FlushAsync` and `DisposeAsync` keep their names.
`NextRecordAsync` is `EndRecordAsync`.

## Behavior changes

These compile once the code is ported, but behave differently:

| | CsvHelper 33 | CsvHelper 34 |
|---|---|---|
| Culture | Required; it also chose the delimiter (`;` for many European cultures) | Invariant unless you set `FormatProvider`; the separator is detected when reading and `,` when writing |
| Header names | Matched exactly | Exactly first, then ignoring case and surrounding whitespace |
| Members read into a record | Public properties | Also public fields, and internal properties and fields |
| Bad quoting | Throws `BadDataException` | Recovers |
| A field past the end of a record | Throws `MissingFieldException` | `""` |
| Enums | Names matched with their case | Names matched ignoring case |
| Booleans | `true`, `false` (any case), `1`, `0` | Also `yes`, `no`, `y`, `n`, `t`, `f` |
| `DateTime` fields that end in `Z` | Converted to local time | Kept in UTC (`DateTimeKind.Utc`) |
| Quoting when writing | Also fields that start or end with a space | Only fields that need it; `CsvQuoting.WhenNeededOrWhitespace` keeps the old rule |
| Dates and times when writing | The culture's default format (`"G"` for `DateTime`) | ISO 8601 (`"O"`) unless you give a format |
| Columns of `WriteRecords` | Every public property with a getter | The columns that are read: no computed properties |
| Field counts when writing | Not checked | A record with another field count than the first throws (`ValidateFieldCount = false` allows it) |
| Dynamic records | `IDictionary<string, object>`, can be changed | `CsvDynamicRecord`, read-only |

## Removed with no replacement

- **`IReader`, `IReaderRow`, `IParser`, `IWriter`, `IWriterRow`, `CsvContext`:** the reader and writer are sealed
  classes with no interfaces. Code that took an `IReader` takes a `CsvReader`, and tests use
  [real input in memory](../../pages/testing.md).
- **Writing multi-character separators:** `Delimiter = "||"` worked for reading and writing. Reading one is done
  with a [`CsvSeparatorStream`](../../pages/reading.md#separators-of-several-characters); the writer's `Separator` is
  one character.
- **`[DateTimeStyles]`, `[NumberStyles]` and the class map's `TypeConverterOption` styles:** use `[CsvFormat]` or a
  converter for values these allowed.
- **`useOnConversionFailure` of a class map's `Default`:** values that don't parse fail the record; `OnRecordError`
  can skip it.
- **The `ClassMap.Convert` lambdas:** write an `ICsvConverter<T>` or a hand-written mapper.
- **Writing anonymous types and plain classes:** declare a `[CsvRecord]` type, or use `WriteCsv` of
  [CsvHelper.Linq](../../pages/linq.md#writing-query-results).
- **`GetRecords(Type)` and the other `Type`-based methods:** use the generic methods.
