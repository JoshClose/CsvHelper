# CsvHelper

<!-- docs:exclude -->
[![NuGet Version](https://img.shields.io/nuget/v/CsvHelper.svg)](https://www.nuget.org/packages/CsvHelper)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CsvHelper.svg)](https://www.nuget.org/packages/CsvHelper)

**[Documentation](https://joshclose.github.io/CsvHelper/)**: guides, the API reference, the changelog and migration guides.

**Upgrading from CsvHelper 33?** Version 34 is a rewrite: the [migration guide](docs/releases/v34.0.0/migration.md) goes
through each concept with before and after code.
<!-- /docs:exclude -->

A CSV reader and writer for .NET, built for speed. It targets .NET 10 and also runs on .NET Framework 4.7+ and
.NET Standard 2.0 and 2.1 (see [Target frameworks](#target-frameworks)):

- **Reader**: parses UTF-8 directly with SIMD (AVX-512, AVX2 or 128-bit vectors), finds quoted regions
  branch-free with a carry-less multiply, and exposes fields as zero-copy spans. Flexible: it adapts to
  most real-world files and has explicit options for malformed ones. Besides RFC 4180 it reads escaped
  (`a\,b`), unescaped and fixed-width files ([Parsing modes](#parsing-modes)).
- **Writer**: strict RFC 4180 output by default (CRLF, minimal quoting, doubled quotes, consistent field
  counts), formatting numbers straight into a UTF-8 buffer; also escaped, unescaped and fixed-width output.
- **LINQ**: the CsvHelper.Linq package queries CSV with any LINQ operator, reading only the columns and rows a
  query uses, joins with Entity Framework Core and other providers by sending them only the keys it needs, and
  writes query results as CSV.

Benchmarks against [Sep](https://github.com/nietras/Sep) and [Sylvan.Data.Csv](https://github.com/MarkPflug/Sylvan)
are in [`bench/`](bench/CsvHelper.Benchmarks); results are in [Benchmarks](#benchmarks) below.

<!-- docs:exclude -->
## Install

```shell
dotnet add package CsvHelper
```

Or in Visual Studio's Package Manager Console: `Install-Package CsvHelper`.

## Reading

A summary. The documentation has the full guides, with examples of every mode, attribute and option:
[Reading](https://joshclose.github.io/CsvHelper/reading.html), [Records](https://joshclose.github.io/CsvHelper/records.html),
[Messy files](https://joshclose.github.io/CsvHelper/messy-files.html) and
[Writing](https://joshclose.github.io/CsvHelper/writing.html).

```csharp
using CsvHelper;

using var reader = CsvReader.FromFile("data.csv");        // or Create(stream), Create(byte[]), Create(TextReader), FromText(string)
int price = reader.GetFieldIndex("Price");                // header lookup: exact, then case/whitespace-insensitive
while (reader.Read())
{
    string name = reader.GetString(0);
    double value = reader.GetDouble(price);              // also GetInt32/GetInt64/GetDecimal/GetBoolean/GetDateTime/GetGuid
    ReadOnlySpan<byte> raw = reader.GetSpan(2);          // UTF-8, already unquoted and unescaped, no allocation
    int qty = reader.Parse<int>("Quantity");             // any ISpanParsable<T>
    TimeSpan t = reader.Parse("Duration", TimeSpan.Parse); // any parser of strings, on every target
}
```

`foreach`/`await foreach` give a `CsvRow` with `CsvField` values (`row[0].ToString()`, `row["Price"].Parse<decimal>()`):

```csharp
await using var reader = CsvReader.Create(stream);
await foreach (CsvRow row in reader)
    Console.WriteLine(row["Name"].ToString());
```

Spans and rows are valid until the next `Read`/`ReadAsync`. Async reads (`ReadAsync`, `ReadHeaderAsync`) only
await when the buffer needs refilling; otherwise they complete synchronously without allocating.

- **Positions.** `ByteOffset` is where the current record starts in the UTF-8 input; with `TrackLineNumbers`,
  `LineNumber` is its line (counting the lines inside quoted fields), which error messages name too.
- **Null and boolean texts.** `NullValues = ["NULL"]` makes `IsNull`, `GetStringOrNull`, `ParseOrNull` and nullable
  record members read those values as `null`; `TrueValues` and `FalseValues` replace the texts of `GetBoolean`.
- **Headers.** `NormalizeHeaderName` changes names before they are matched (`unit_price` finds `UnitPrice`),
  `GetFieldIndex("Amount", 1)` finds the second column of a name, and `ReadHeader` on a record makes it the header
  of the next data set in the same file.
- **Skipping.** `ShouldSkipRecord` skips records by a test of yours, such as totals.
- **Limits and privacy.** `MaxRecordSize` stops a record that keeps growing (an unclosed quote) before it fills
  memory; `IncludeValuesInExceptions = false` keeps field values out of exception messages.
- **IDataReader.** `new CsvDataReader(reader)` is a `DbDataReader` (string columns, `DBNull` for null values) for
  `DataTable.Load` and bulk importers.

`ParallelEnumerate` maps records to objects on several threads and returns them in input order:

```csharp
using var reader = CsvReader.FromFile("data.csv");
List<Order> orders = reader.ParallelEnumerate(r => new Order(r.GetString("Id"), r.GetDecimal(1))).ToList();
```

Workers take turns reading and scanning the next batch of records, then map that batch themselves while its
bytes are still in their cache. Each worker gets a reader of its own, positioned on one record per call (don't
call `Read` on it), and with `PoolStrings` the workers share one string pool. The input is read and checked one
batch at a time in order, so malformed-record callbacks run one at a time in input order, and an exception is
thrown after the results of the records before it.

### Parsing modes

`Mode` (on `CsvReaderOptions` and `CsvWriterOptions`) chooses how fields are delimited and escaped:

| Mode | Fields | Example |
|---|---|---|
| `Rfc4180` (default) | Quoted fields can contain separators and line breaks; `""` is a quote. | `"a,b","say ""hi"""` |
| `Escape` | No quoting: the escape character (`Escape`, `\` by default) makes the next character text. | `a\,b,say "hi"` |
| `NoEscape` | No quoting or escaping: everything but the separator and line breaks is text. | `a b,say "hi"` |
| `FixedWidth` | No separators: columns of fixed widths (`FieldWidths`), padded with spaces. | `a b  say "hi"` |

```csharp
// MySQL/PostgreSQL style dumps: \, is a comma, \\ a backslash, \ before a line break keeps it in the field.
using var escaped = CsvReader.FromFile("dump.csv", new CsvReaderOptions { Mode = CsvMode.Escape });

// Fixed-width columns: 10, 30 and 8 characters wide. Values are trimmed of their padding.
var options = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [10, 30, 8], HasHeader = false };
using var reader = CsvReader.FromFile("accounts.txt", options);
while (reader.Read())
{
    string id = reader.GetString(0);
    decimal balance = reader.GetDecimal(2);
}
```

- **Escape**: an escaped character is always text, whatever it is: `\n` is the letter `n`, and `\` before a
  line break keeps the line break in the field (before CR LF, the pair). Quotes are ordinary text. An escape
  character at the very end of the input is kept, and reported as `TrailingEscape` when malformed data is
  detected (`MalformedData`, `OnMalformedRecord`). Escapes work in the SIMD scanner as quotes do: the escaped
  bytes of each 64-byte block are found branch-free (records of the escaped benchmark file are found at 7.4 GB/s,
  against 11 GB/s for the same data with nothing to escape).
- **FixedWidth**: widths count characters as .NET counts them (`string.Length`), so text with accents, and
  single-byte files read through `Encoding`, work as well as ASCII. Each line is a record with a field per column:
  a short line has empty fields at its end, and text after the last column is ignored (`RawRecord` still has it).
  Values are trimmed of spaces and tabs (`TrimWhitespace` is `null` by default, which means trim in this mode
  only); `GetRawSpan` returns a column with its padding. The separator, quote and `sep=` line don't apply.
  A header line is split into columns too.
- Empty lines, comments (`CommentPrefix`), `MissingField`, records, `ParallelEnumerate` and every input source work
  in every mode.

### Separators of several characters

`Separator` is one ASCII character. For a separator such as `||`, or one that is not ASCII, read through a
`CsvSeparatorStream`: it puts one byte (U+001F unless you choose another) in place of each separator, and the reader
splits on it.

```csharp
using var reader = CsvReader.Create(new CsvSeparatorStream(File.OpenRead("items.txt"), "||"));
```

The reader gives the stream its quoting, escape and comment rules, so separators in quoted fields, after an escape
character and in comment lines stay text, and overlapping ones are taken from the left (`a|||b` is `a` and `|b`). The
stream works on the bytes in the reader's buffer with the reader's 64-byte SIMD blocks (one VPCOMPRESSB per block with
AVX-512 VBMI2), so `||` files keep the UTF-8 fast path ([benchmarks](#separators-of-several-characters-1)). Input that
contains the replacement outside quoted fields throws `CsvFormatException`; UTF-16 and UTF-32 input is not supported.

### Reading records into objects

Mark a `partial` class, struct or record `[CsvRecord]` and the CsvHelper source generator, which ships in the
package, writes its mapping code at compile time. The columns are looked up once per reader, then each record is
read with the same typed getter calls you would write by hand. There is no reflection, so it works with trimming
and native AOT, and it runs as fast as hand-written code (`CsvHelper_RecordsGenerated` in the benchmarks).

```csharp
[CsvRecord]
public partial class Order
{
    public string Id { get; set; } = "";
    [CsvColumn("Unit Price")]
    public decimal Price { get; set; }
    [CsvColumn(Optional = true)]
    public int? Quantity { get; set; }
    [CsvIgnore]
    public decimal Total { get; set; }
}

[CsvRecord]
public partial record Line(string Sku, [CsvColumn(Index = 3)] double Weight, int Count = 1);

using var reader = CsvReader.FromFile("orders.csv");
foreach (Order order in reader.GetRecords<Order>()) { }          // also GetRecordsAsync<T>() and ParallelEnumerate<T>()
while (reader.Read()) { Order order = reader.GetRecord<Order>(); } // the current record
foreach (Order order in reader.EnumerateRecords(new Order())) { }  // one instance, refilled for every record
```

- **Members.** These are read:
  - public and internal properties and fields that can be set (`set`, `init` or `required`), including those of base
    classes;
  - other members only with `[CsvColumn]`.

  `[CsvIgnore]` skips a member, and `[CsvIgnoreBase]` on a class the members of its base classes. The parameters of
  the constructor are read too. That constructor is the one with `[CsvConstructor]`, else the parameterless one, else
  the one with the most parameters (positional records). Members the constructor sets are not set again.
- **Columns.** A member reads the header column with its name, or with the name in `[CsvColumn("name")]`. As with
  `GetFieldIndex`, an exact match wins, then case and surrounding whitespace are ignored. `[CsvColumn("Unit Price",
  "Price")]` also accepts other names, tried in order. `[CsvColumn(Index = n)]` reads a column by position, which is
  needed when `HasHeader = false`, and `[CsvColumn("Amount", NameIndex = 1)]` the second of several columns with a
  name. `GetRecords` looks up the columns before it returns. If any are missing, it throws one `KeyNotFoundException`
  that names them all (`ValidateHeader<T>()` and `TryValidateHeader<T>(out missing)` check without reading). Members
  that are `Optional`, and constructor parameters with a default value, may be missing; they keep their default.
- **Fixed width.** `[CsvColumn(Index = n, Width = w)]` on every column makes the type's fixed-width layout:
  `FieldWidths = CsvRecordMapper.GetFieldWidths<T>()` reads (and writes) it.
- **Types.** These can be read:
  - `string`;
  - the types of the typed getters (`int`, `long`, `double`, `float`, `decimal`, `bool`, `DateTime`, `DateTimeOffset`,
    `Guid`);
  - enums: names (and the other names of `[CsvEnumName("on hold")]`) are matched in UTF-8 ignoring ASCII case, and
    numbers and flag lists go through `Enum.TryParse`;
  - `Uri`, absolute or relative;
  - `Nullable<T>` of any of these, where an empty field reads as `null`;
  - any type with a static `Parse`: `ISpanParsable<T>` on .NET 10, `Parse(string, IFormatProvider)` or `Parse(string)`
    on the other targets;
  - any type at all with `[CsvConverter(typeof(MyConverter))]`, an `ICsvConverter<T>` that the mapper creates with
    `new` and calls directly; `[assembly: CsvConverter(typeof(MyConverter))]` gives every member of its type the
    converter;
  - arrays, lists and the collection interfaces, read from every column with the member's name, or from
    `[CsvColumn(Index = 1, EndIndex = 4)]` (to the end of the record without `EndIndex`); dictionaries with string keys
    read the columns from `Index` on, keyed by their header names;
  - other record types, read from the same row as nested records; `[CsvColumn(Prefix = "Ship ")]` puts a prefix in
    front of their header names.

  Parsing uses the reader's `FormatProvider`. A member of any other type is a compile error until it gets
  `[CsvIgnore]`.
- **Values.** `[CsvFormat("yyyyMMdd")]` reads a date or time in an exact format (`[CsvFormat("yyyy-MM-dd",
  "dd/MM/yyyy")]` in either), `[CsvCulture("de-DE")]` with another culture, `[CsvBooleanValues("Ja", "Nein")]` with other
  true and false values. `[CsvNullValues("NULL")]` reads those values as `null` (the reader's `NullValues` apply to every
  `Nullable<T>` and `string?` member), `[CsvDefault(0)]` gives the value of an empty field, and `[CsvConstant("v2")]`
  gives a member a value without reading a column. `[CsvValidate(nameof(IsValidAge))]` checks the value with a static
  method of the type.
- **Errors.** A record that cannot be read throws a `CsvFormatException` that names the record, line, member and field
  (`Cannot read Order.Price from field 1 'Unit Price' of record 3: ...`), with the cause as its `InnerException`.
  `OnRecordError` skips such records (it gets the row number, byte offset, exception and raw record) instead of ending
  the enumeration.
- **Types you cannot change.** `[CsvRecordMap(typeof(Supplier))]` on a `partial` class maps a type of another library:
  the class's members stand in for the type's and carry its attributes. A hand-written `CsvRecordMapper<T>` subclass
  reads anything else; `[CsvConverter(typeof(MyMapper))]` on the type registers it, or `CsvRecordMapper.Register<T>` at
  startup.
- **Options.** `[CsvOptions(Separator = ';', HasHeader = false, Culture = "de-DE")]` keeps a type's dialect with it;
  `CsvReaderOptions.For<T>()` and `CsvWriterOptions.For<T>()` return those options.
- **Dynamic records.** Without a record type, `GetRecords<dynamic>()` reads each record as a `CsvDynamicRecord` whose
  members are its columns (`record.Name`; values are strings, and `record.GetInt32("Age")` parses one). A record holds
  its bytes, not a dictionary of strings, and `record.Name` is bound once to its column for every record with that
  header, so reading them is 16-22x faster than `ExpandoObject`s (see [Benchmarks](#dynamic-records)). `WriteRecords` writes them back, and `ExpandoObject`s and dictionaries keyed by
  column name too.
- **Writing.** `writer.WriteRecords(orders)` writes the header and then the records, in the columns the mapper reads
  and with the same names, formats, cultures, boolean and null values, enum names, constants and converters (an
  `ICsvConverter<T>` has a `Write` method too), so the file reads back. The generated record writer calls `WriteField`
  for each member, as fast as writing them by hand. `WriteHeader<T>()`, `WriteRecord<T>(record)` and
  `WriteRecordsAsync<T>` (for `IEnumerable<T>` and `IAsyncEnumerable<T>`) write parts of it, and `HasHeader = false`
  in the writer options leaves out the header; a hand-written `CsvRecordWriter<T>` writes types with a hand-written
  mapper.

The generator reports problems when you compile:

| Id | Problem |
|---|---|
| CSVH001 | The type, or a type that contains it, is not `partial`. |
| CSVH002 | A member's type cannot be read from a field. |
| CSVH003 | No constructor to use: the type is abstract or static, has no public or internal constructor, or several constructors have the most parameters. |
| CSVH004 | The type is generic, a `ref struct`, file-local or not accessible from its whole assembly, or it contains itself through nested records. |
| CSVH005 | Warning: `[CsvColumn]` is on a member that cannot be set. |
| CSVH006 | A `required` member has `[CsvIgnore]`. |
| CSVH007 | The project uses C# 8 or earlier. |
| CSVH008 | The fixed-width layout is not valid: a column has no width or two, a width has no index, or the type has nested records. |
| CSVH009 | A `[CsvConverter]` cannot be used: the wrong interface or base class, no suitable constructor, two on one member, or two assembly converters of one type. |
| CSVH010 | Warning: an attribute does not apply to its member, such as `[CsvFormat]` on an `int`, and is ignored. |
| CSVH011 | An attribute argument is not valid, such as a `[CsvDefault]` or `[CsvConstant]` that does not convert to the member's type, or a `[CsvValidate]` without a suitable method. |
| CSVH012 | A `[CsvRecordMap]` cannot map its type, or a member of the map class stands in for no member of the type. |

The generator needs C# 9 and a Roslyn 4.3 compiler or later (the .NET 6.0.400 SDK, Visual Studio 2022 17.3). A
generated module initializer registers each mapper, so
`GetRecords<T>()` finds it with no reflection. A project that references CsvHelper as a project instead of a package
adds the generator itself:
`<ProjectReference Include="...\CsvHelper.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`.

### What the reader adapts to by default

| Situation | Behavior |
|---|---|
| Separator | Detected from the first lines (`,` `;` tab `\|`), scored by how consistently each appears per line. Excel's `sep=;` first line is honored and skipped. |
| Encoding | UTF-8 (BOM skipped). UTF-16/UTF-32 byte order marks are detected and transcoded. `Encoding` option for other code pages. |
| Line endings | `\n`, `\r\n` and lone `\r`, mixed freely. |
| Quotes | RFC 4180: a quote at the start of a field opens a quoted field (separators/line breaks inside are text, `""` is a quote). |
| Stray quotes (`5'10"`, `ab"c`) | Kept as text; they do not open a quoted region. |
| Text after a closing quote (`"ab"c`) | Appended: `abc`. |
| Unterminated quote | The field runs to the end of the input. |
| Empty lines | Skipped (`SkipEmptyLines`). |
| Ragged records | Allowed; missing fields read as empty (`MissingField`). |
| Header names | Exact match first, then case-insensitive and trimmed. Duplicates allowed (first wins; `GetFieldIndex(name, n)` finds the others). |
| Numbers | Invariant culture, fast UTF-8 paths for integers and floats; `FormatProvider` for other cultures. Booleans accept true/false, yes/no, y/n, t/f, 1/0. |

### Options for malformed files

| Option | Values | Use it when |
|---|---|---|
| `MalformedData` | `Recover` (default), `Throw`, `Skip` | Decide what happens to records with bad quoting. `Throw` raises `CsvFormatException` at the bad record (earlier records are still readable). |
| `OnMalformedRecord` | callback | Log every malformed record (record number, reason, byte offset, raw text) whether it was recovered or skipped. |
| `AllowLineBreaksInQuotes` | `true` (default) / `false` | Set `false` for files with unbalanced quotes: every line break then ends the record, so one bad quote cannot swallow the rest of the file. |
| `FieldCountMismatch` | `Allow` (default), `Throw`, `Skip` | Enforce that records have the header's field count. |
| `MissingField` | `ReturnEmpty` (default), `Throw` | Accessing a field past the end of a short record. |
| `TrimWhitespace` | `null` (default: trim in `FixedWidth` mode only), `true`, `false` | Trim spaces/tabs around values; `  "quoted"  ` still unquotes, and escaped whitespace is kept. `TrimInsideQuotes` trims inside the quotes too, and `WhitespaceCharacters` says what is trimmed. |
| `ShouldSkipRecord`, `OnRecordError` | callbacks | Skip records such as totals; skip records of a record type whose fields do not parse or fail their checks. |
| `MaxRecordSize` | `null` (default: no limit) | Stop a record that keeps growing, such as one after an unclosed quote. |
| `TrackLineNumbers` | `false` (default) / `true` | Report the line of each record and of errors (`LineNumber`). |
| `IncludeValuesInExceptions` | `true` (default) / `false` | Keep field values, which may be personal data, out of exception messages. |
| `Mode` | `Rfc4180` (default), `Escape`, `NoEscape`, `FixedWidth` | `NoEscape` treats quotes as ordinary text (TSV exports, logs); see [Parsing modes](#parsing-modes). |
| `CommentPrefix` | `null` (default) | Skip lines starting with e.g. `#`. |
| `Separator`, `Quote`, `HasHeader`, `Encoding`, `FormatProvider` | | Override detection and defaults. |
| `PoolStrings` | `false` (default) / `true` | Set `true` when you keep many records in memory: repeated values in a column then share one string instance, which saves memory and GC work (columns of mostly unique values switch this off by themselves). For strings that are used and dropped, the default is faster. |

## Writing

```csharp
using var writer = CsvWriter.ToFile("out.csv");           // or Create(stream), Create(TextWriter)
writer.WriteHeader("Id", "Name", "Price", "When");
foreach (var item in items)
{
    writer.WriteField(item.Id);                           // numbers format straight to UTF-8
    writer.WriteField(item.Name);                         // quoted only if it contains , " CR or LF
    writer.WriteField(item.Price);
    writer.WriteField(item.Timestamp);                    // DateTime/DateTimeOffset: ISO 8601 ("O") by default
    writer.EndRecord();                                   // or await writer.EndRecordAsync()
}
```

`writer.WriteRecords(orders)` writes the records of a [`[CsvRecord]`](#reading-records-into-objects) type with its
header, and `writer.ParallelWriteRecords(orders)` formats them on several threads, with the same output (see
[Writing from several threads](https://joshclose.github.io/CsvHelper/writing.html#writing-from-several-threads)). By
default the output follows RFC 4180:

- records end with CRLF (`NewLine = "\n"` or `"\r"` changes that);
- a field is enclosed in double quotes when it contains the separator, a quote, CR or LF, and inner quotes
  are doubled (`CsvQuoting.Always` quotes every field, `CsvQuoting.WhenNeededOrWhitespace` also fields with
  whitespace at either end, `WriteField(value, CsvQuoting.Always)` one field, and the `ShouldQuote` callback decides
  per field, such as text but not numbers);
- `null` is an empty field and booleans are `True` and `False`; `NullValue = "NULL"`, `TrueValue` and `FalseValue`
  change that;
- every record must have the same number of fields as the first (`ValidateFieldCount`, on by default): a
  mismatch throws and the bad record is discarded;
- a record that consists of a single empty field is written as `""` so it is not mistaken for an empty line;
  a record with no fields is rejected.

`Separator` can be changed (e.g. `;` or tab); quoting then follows the same rules for that separator.

With `CommentPrefix = '#'`, `WriteComment` writes comment lines, and a record whose first field starts with the prefix
has that field quoted so it does not read as a comment. `Injection = CsvInjection.Escape` (or `Strip`, `Throw`) protects
text fields that a spreadsheet would run as a formula (`=1+2`, `@SUM(...)`), but not numbers.

The other [parsing modes](#parsing-modes) write what the reader reads in those modes (records still end with CRLF or
`NewLine`):

- `Escape`: no quotes; `Escape` (`\`) goes before every separator, escape character, CR and LF in a value.
- `NoEscape`: values are written as they are. A value that contains the separator or a line break changes the
  fields or records a reader sees, so use it only for data that cannot contain them.
- `FixedWidth`: no separators; each value is padded with spaces to the width of its column (`FieldWidths`, in
  characters). A value that is too long or contains a line break, or a field past the last column, throws and the
  record is discarded; `ValidateFieldCount` requires a field for every column.

In `Escape` and `NoEscape` mode a record whose only field is empty is an empty line.
Output is UTF-8 (optional BOM). I/O happens only when a record ends with the buffer mostly full, or on
`Flush`/`FlushAsync`, so async writers never block.

## LINQ

The [CsvHelper.Linq](https://joshclose.github.io/CsvHelper/linq.html) package (`dotnet add package CsvHelper.Linq`)
is a LINQ provider for CSV. The reader runs what it can of a query itself: it reads only the members of the records
that the query uses, filters records before it reads the rest, skips rows without parsing them and stops early.

```csharp
using CsvHelper.Linq;

var orders = CsvQueryable.FromFile<Order>("orders.csv");
var big = orders.Where(o => o.Total > 100).Select(o => new { o.Id, o.Customer }).ToList();   // reads 3 columns

// Entity Framework Core is asked only for the customers that orders.csv has.
var report = orders.Join(db.Customers, o => o.CustomerId, c => c.Id, (o, c) => new { o.Id, c.Name }).ToList();

report.WriteCsv("report.csv");                                     // anonymous types too
orders.Delete(o => o.Status == Status.Cancelled);                  // the rest of the file is kept byte for byte
```

<!-- /docs:exclude -->

## Target frameworks

| Target | Used by | Scanner |
|---|---|---|
| `net10.0` | .NET 10 and later | SIMD: AVX-512, AVX2 or 128-bit vectors (SSE, NEON), chosen at startup |
| `net47` | .NET Framework 4.7 and later | 64-bit integers: the masks of each 64-byte block from eight `ulong`s, then the same pipeline |
| `netstandard2.1` | .NET Core 3.0 to .NET 9, Mono, Unity 2021.2 and later | same as `net47` |
| `netstandard2.0` | everything else: .NET Core 2.x, Unity before 2021.2 | same as `net47` |

`net47` and `netstandard2.0` depend on two Microsoft packages, `System.Memory` and `Microsoft.Bcl.AsyncInterfaces`;
`netstandard2.1`, which has spans and async streams built in, only on `System.Runtime.CompilerServices.Unsafe`. Every
build reads and writes exactly the same CSV: the tests run on .NET 10, on .NET 8 (the `netstandard2.1` build) and on
.NET Framework 4.8, and on .NET 10 the 64-bit scanner is also checked against the SIMD ones. The differences:

- **Parsing to a type.** `Parse<T>`, `TryParse<T>` and `ParseOrNull<T>` for `ISpanParsable<T>` types need .NET 7
  generic math, so they are .NET only. On every target, `Parse(index, parser)`, `TryParse(index, parser, out value)`
  and `CsvField.ParseOrNull(parser)` take a parser that gets the field as a string and the reader's format provider:
  `reader.Parse(3, TimeSpan.Parse)`. The typed getters (`GetInt32`, `GetDouble`, `GetDateTime`, ...) are on every
  target. `[CsvRecord]` members follow the same rule. .NET 8 and 9 projects use the `netstandard2.1` build, so there
  they are parsed through `Parse(string, IFormatProvider)` too.
- **Writing values.** `WriteField<T>` takes `IUtf8SpanFormattable` values on .NET and `IFormattable` values elsewhere
  (numbers in the invariant culture are still formatted straight to UTF-8). The `DateOnly`, `Half` and `Int128`
  overloads are .NET only, but on .NET 8 and 9 the `netstandard2.1` build still writes `DateOnly` and `TimeOnly`
  values in ISO 8601 round-trip format ("O"). On the older targets `WriteRecord` and `WriteHeader` also take arrays, for compilers before C# 13.
- **Floating point.** Doubles and floats are written in the shortest form that round-trips, as .NET Core 3.0 and
  later write them, not as .NET Framework's 15-digit `ToString()`. Parsing is exactly rounded, except for inputs the
  fast parser leaves to the BCL (thousands separators, other cultures, more than 19 significant digits right on a
  rounding boundary): .NET Framework's `double.Parse` is not always exactly rounded.
- **C# version.** The options are records with `init` properties: setting them needs C# 9 or later in the calling
  project (`<LangVersion>latest</LangVersion>` works for .NET Framework projects too).
- **Speed.** Without SIMD, and with the slower spans of the `System.Memory` package, expect about a quarter of the
  .NET 10 speed. On an Intel Core i7-12800H, reading 52 MB of the PackageAssets data as strings took 89 ms on .NET
  Framework 4.8 x64 (23 ms on .NET 10). Scanning alone ran at 1.5 GB/s (7.2 GB/s on .NET 10), `ParallelEnumerate` at
  1.2 GB/s.

## How it is fast

1. **UTF-8 throughout.** Files are UTF-8, so the reader scans bytes, not chars: no transcoding pass, half the
   memory traffic of a UTF-16 parser, and numbers parse straight from UTF-8. Strings are created only when
   asked for, with an ASCII fast path that widens straight into the new string.
2. **64-byte blocks, 64-bit masks.** Each block yields bit masks of separators, quotes, CR and LF (two AVX2
   loads, or one AVX-512 load).
3. **Branch-free quotes.** A carry-less multiply of the quote mask (prefix XOR) marks every byte inside quotes,
   which removes quoted separators and line breaks without per-character state. For well-formed data that
   rule is exactly RFC 4180. A quote that opens anywhere but at the start of a field (the one case where the
   parity rule and RFC 4180 disagree) is detected with a few mask operations, and only that block goes
   through an exact scalar state machine, which treats the quote as text.
4. **Positions in bulk.** Terminator positions are written in groups of eight with `tzcnt`/`blsr` and no
   per-field branches. The index stores *position + 1* with spare high bits that mark "this field needs
   unquoting" (computed for all fields at once with an add-carry trick) and "this record ends in CRLF".
   Field access and record iteration therefore never touch the data bytes, so a plain field is two integer
   loads and a mask. A quoted field of up to 64 bytes is checked with one 64-byte vector comparison (no loop,
   no length-dependent branch) and returned in place without its quotes; only fields with escaped quotes are
   copied.
5. **No per-row restarts.** Unlike parsers that restart their vector loop at each record start, the scan
   runs continuously over 16 KiB chunks; records are cut from the index afterwards.
6. **Exact, fast number parsing.** Integers parse straight from UTF-8. Floats use a port of
   [fast_float](https://github.com/fastfloat/fast_float) (Clinger's fast path, then Eisel-Lemire with a 128-bit
   power-of-five table, 8 digits at a time with SWAR): bit-identical to `double.Parse`/`float.Parse` (checked on
   the shortest and 9-digit strings of all 2^32 floats and on 200 million random doubles and long decimal strings),
   3.4x (`float`) to 4.5x (`double`) faster on typical values, and `float` is rounded once, never parsed as
   `double` and cast. Anything unusual (thousands separators, other cultures, >19 digits on a rounding boundary)
   falls back to the BCL.
7. **Strings on demand, optionally pooled per column.** Strings are only created when asked for, widening ASCII
   straight into the new string. With `PoolStrings`, repeated values (up to 128 bytes) in a column share one
   instance, which cuts allocations and GC work when many records are kept in memory. A lookup costs about as much
   as a plain allocation: the first and last 16 bytes of the field, loaded with masks instead of length-dependent
   branches, are an exact key up to 32 bytes; the slot that matched last is checked before hashing, since
   neighbouring records often repeat a value; and a column of (nearly) unique values switches pooling off, after
   512 values when nearly every record brought a new one, or when its table fills up while most lookups still miss.
   Pooling is off by default: for strings that are used and dropped, allocating them is cheaper.
8. **A writer that formats in place.** Every field goes straight into one UTF-8 buffer. Short strings are narrowed
   from UTF-16 with vector instructions, and the same pass detects the characters that need quoting. Quoting
   expands the field in place, from the back. Numbers skip the check, since invariant number output cannot contain
   the usual separators. `float` and `double` use a port of [Dragonbox](https://github.com/jk-jeon/dragonbox)
   (shortest round-trip digits). Its output is byte-identical to `double.TryFormat` / `float.TryFormat` (checked on
   all 2^32 floats and a billion doubles) and about 3.5x faster on typical values.

Correctness is checked by fuzzing the reader against an independent character-by-character reference parser
for every vector width (512/256/128/scalar only), every input source (memory, chunked streams, strings,
text readers, async) and tiny buffer sizes, plus writer → reader round trips.

## Benchmarks

CsvHelper against [Sep](https://github.com/nietras/Sep) 0.17.1 and [Sylvan.Data.Csv](https://github.com/MarkPflug/Sylvan) 1.4.4,
measured with BenchmarkDotNet 0.15.8 on .NET 10.0.12, Windows 11, AMD Ryzen 9 9950X3D (one core, AVX-512). Times are means per operation; lower is better. Sep is the
baseline. CsvHelper runs with its default options, except where a row says otherwise.

Data sets (deterministic, generated by [`DataGenerator`](bench/CsvHelper.Benchmarks/DataGenerator.cs), 50,000 records each):

- **PackageAssets** (16 MB, LF): 25 short text columns modelled on the NuGet package assets data set; one column is quoted.
- **Floats** (14.7 MB, LF): 3 text columns and 25 `float` columns, like a machine learning feature file.
- **Quoted** (7.1 MB, CRLF): 7 columns; the 4 text columns are quoted and contain separators, escaped quotes and line breaks.

Scenarios: **Rows** finds the records only. **Cols** touches every field as a span, without creating strings.
**Records** builds an object with 25 string properties per record and keeps them all. **Strings** creates a string
for every field. **Floats** parses the 25 floats of each record and sums them. **Write** writes the same records, and
every library produces byte-identical RFC 4180 output. Reads come from a `MemoryStream` unless the data column says
otherwise. The `Async` variants use each library's async API.

### Reading

| Data | Scenario | Sep | **CsvHelper** | Sylvan | CsvHelper vs Sep |
|---|---|---:|---:|---:|---:|
| PackageAssets | Rows | 2.80 ms | **2.01 ms** | 4.79 ms | **1.40x faster** |
| PackageAssets | RowsAsync | 3.04 ms | **2.02 ms** | 5.23 ms | **1.51x faster** |
| PackageAssets | Cols | 3.75 ms | **3.02 ms** | 6.73 ms | **1.24x faster** |
| PackageAssets | Records | 28.7 ms | **26.9 ms** | 30.6 ms | **1.07x faster** |
| PackageAssets | RecordsAsync | 28.4 ms | **26.5 ms** | 31.2 ms | **1.07x faster** |
| PackageAssets | Records, both pooling strings ¹ | 27.1 ms | **19.3 ms** | | **1.40x faster** |
| PackageAssets, from a `string` | Cols | 3.41 ms | **3.05 ms** | 6.24 ms | **1.12x faster** |
| PackageAssets, from a `string` | Records | 28.0 ms | **27.4 ms** | 30.2 ms | **1.02x faster** |
| PackageAssets, from a file | Cols | 9.12 ms | **4.05 ms** | 8.95 ms | **2.25x faster** |
| PackageAssets, from a file | ColsAsync | 22.4 ms | **5.15 ms** | 13.4 ms | **4.35x faster** |
| Floats | Floats | 19.9 ms | **12.2 ms** | 48.3 ms | **1.62x faster** |
| Floats | FloatsAsync | 19.7 ms | **13.6 ms** | 48.4 ms | **1.45x faster** |
| Quoted | Rows | 2.63 ms | **1.46 ms** | 6.90 ms | **1.80x faster** |
| Quoted | Strings | 6.17 ms | **5.60 ms** | 11.4 ms | **1.10x faster** |
| Quoted | StringsAsync | 6.43 ms | **4.79 ms** | 11.9 ms | **1.34x faster** |

### Writing

| Data | Scenario | Sep | **CsvHelper** | Sylvan | CsvHelper vs Sep |
|---|---|---:|---:|---:|---:|
| PackageAssets | Write | 27.5 ms | **8.64 ms** | 12.7 ms | **3.18x faster** |
| PackageAssets | WriteAsync | 44.7 ms | **6.77 ms** | 15.1 ms | **6.60x faster** |
| PackageAssets | WriteRecords ² | | **8.66 ms** | | |
| PackageAssets | WriteRecordsAsync ² | | **9.18 ms** | | |
| Floats | Write | 86.3 ms | **23.7 ms** | 62.5 ms | **3.64x faster** |
| Quoted | Write | 11.0 ms | **5.28 ms** | 5.80 ms | **2.08x faster** |

¹ Sep with its per-column string pool (`SepToString.PoolPerCol()`), CsvHelper with `PoolStrings = true`.

² Objects of a `[CsvRecord]` type with 25 string fields: CsvHelper's `WriteRecords` with its generated record writer,
which is 2% slower than writing the fields by hand (8.48 ms). Sep and Sylvan do not write objects.

Throughput: CsvHelper finds records at 8.0 GB/s (Sep 5.7 GB/s) and reads every field of PackageAssets at
5.3 GB/s (Sep 4.3 GB/s). It writes 1.9 GB/s of text records (Sep 0.58 GB/s) and 0.62 GB/s of floats (Sep 0.17 GB/s).

**Records** keeps 1.25 million strings in memory (62 MB allocated per run, the same for every library), so garbage
collection takes most of its time, and the libraries end up close. Pooling strings (`PoolStrings = true`) cuts that to
18.4 MB (Sep's pool: 28.4 MB). It is off by default because for strings that are used and dropped (**Strings**) a pool
lookup costs more than it saves. The writer allocates under 400 bytes per run (Sep 7–10 KB).

How the comparison is kept fair:

- All three libraries return the same data: the same record count and content hash, the same sum of floats,
  byte-identical written files (in the PackageAssets Cols scenarios Sep's total is larger because its spans keep the
  quotes, see below). `dotnet run -c Release -- verify` runs each benchmark once and prints these values.
- Sep reads with `Unescape = true` in the Records and Strings scenarios and on the Quoted data, so every library
  returns the same unquoted values. In the PackageAssets Rows and Cols scenarios it uses its defaults, so in Cols
  its spans of the one quoted column still include the quotes, which is slightly less work. Sep writes with
  `Escape = true`, Sylvan with `\r\n` line endings.
- File scenarios use each library's own file API (`Sep.Reader().FromFile`/`FromFileAsync`, Sylvan's
  `Create`/`CreateAsync` with a path, `CsvReader.FromFile`).
- The job pins the process to one performance core. It sets `DOTNET_TC_CallCountingDelayMs=0` so that benchmarks
  taking a long time per call reach optimized (Tier-1) code before measurement, which applies to every library alike.

### Other parsing modes

The [parsing modes](#parsing-modes) other than RFC 4180, on the PackageAssets records (50,000, 25 columns), on the
same machine. Sep has no escape mode, and none of the other libraries reads or writes fixed widths, so each row names
its own baseline.

- **Escape** (16.1 MB): a backslash before every comma and backslash (framework monikers, Windows paths), nothing
  quoted. Sylvan with `CsvStyle.Escaped`.
- **NoEscape** (16.0 MB): tab separated, nothing quoted or escaped. Sep with `DisableQuotesParsing`; Sylvan reads it
  in its standard mode.
- **FixedWidth** (29.0 MB): every column padded with spaces to its longest value. The baseline is the usual
  hand-written loop: `StreamReader.ReadLine`, then a trimmed slice per column (`Substring` for records), and
  `PadRight` per value for writing.

| Mode | Scenario | Baseline | **CsvHelper** | Others | CsvHelper vs baseline |
|---|---|---:|---:|---|---:|
| Escape | Rows | Sylvan 15.1 ms | **2.18 ms** | | **6.92x faster** |
| Escape | Cols | Sylvan 19.5 ms | **4.95 ms** | | **3.94x faster** |
| Escape | Records | Sylvan 42.3 ms | **22.1 ms** | | **1.92x faster** |
| Escape | Write | Sylvan 14.9 ms | **12.5 ms** | | **1.19x faster** |
| NoEscape | Rows | Sep 2.56 ms | **1.45 ms** | Sylvan 2.97 ms | **1.77x faster** |
| NoEscape | Cols | Sep 3.53 ms | **2.27 ms** | Sylvan 4.93 ms | **1.55x faster** |
| NoEscape | Records | Sep 27.7 ms | **19.2 ms** | Sylvan 28.8 ms | **1.45x faster** |
| NoEscape | Write | Sep 15.9 ms | **8.48 ms** | | **1.87x faster** |
| FixedWidth | Rows | ReadLine 3.39 ms | **1.91 ms** | | **1.77x faster** |
| FixedWidth | Cols | ReadLine 13.6 ms | **11.7 ms** | | **1.17x faster** |
| FixedWidth | Records | ReadLine 70.0 ms | **35.3 ms** | | **1.99x faster** |
| FixedWidth | Write | PadRight 16.7 ms | **11.8 ms** | | **1.42x faster** |

Every library returns the same results here too, and every writer produces byte-identical output.

### Separators of several characters

The PackageAssets and Quoted data with `||` in place of every comma (in the Quoted data, the commas inside quoted fields
become `||` too), read through `CsvSeparatorStream`. Sep and Sylvan do not read such separators. The baseline is
CsvHelper on the same data with commas, so the difference is the cost of the stream.

| Data | Scenario | CsvHelper, `,` | **CsvHelper, `\|\|`** |
|---|---|---:|---:|
| PackageAssets | Rows | 2.06 ms | **3.82 ms** |
| PackageAssets | Strings | 7.65 ms | **9.29 ms** |
| Quoted | Rows | 1.52 ms | **2.51 ms** |
| Quoted | Strings | 4.97 ms | **6.08 ms** |

The stream alone replaces separators at 9.6 GB/s on PackageAssets and 6.9 GB/s on the Quoted data with AVX-512 VBMI2
(one VPCOMPRESSB per 64-byte block), and at 5.8 and 4.6 GB/s with the code for AVX2 and SSSE3 on the same machine (a
byte shuffle per 8 bytes, which for a separator of two bytes also writes the replacement). It allocates about 300 bytes
per stream.

### Dynamic records

The PackageAssets data read as `dynamic` records, without a record type (see
[Dynamic records](https://joshclose.github.io/CsvHelper/records.html#dynamic-records)). **Dynamic** reads 3 of the 25 fields of every record by name
(`record.Id`, `record.Version`, `record.FileName`); **DynamicToList** keeps every record in a list. They are compared
with the usual way to get dynamic records: an `ExpandoObject` per record, with a string per field added by header name
(filled by CsvHelper's reader, so the difference is the record).

| Scenario | **CsvHelper** | `ExpandoObject` | CsvHelper vs `ExpandoObject` |
|---|---:|---:|---:|
| Dynamic | **5.05 ms** | 82.1 ms | **16.3x faster** |
| Dynamic, from a `byte[]` | **3.87 ms** | | |
| DynamicToList | **5.41 ms** | 119 ms | **22.0x faster** |

CsvHelper reads dynamic records at 3.2 GB/s (4.1 GB/s from a `byte[]`). It allocates 29.7 MB per run in **Dynamic**
(14.3 MB from a `byte[]`, whose records reference the input in place; most of the rest are the 150,000 strings of the
fields read), against 136 MB for the `ExpandoObject`s. Kept in a list, its 50,000 records take 23.6 MB and the
`ExpandoObject`s 137 MB. The `ExpandoObject` times vary more than CsvHelper's (a standard deviation of 9 ms in
**Dynamic**).

### Writing on several threads

`ParallelWriteRecords` against `WriteRecords` on the same `[CsvRecord]` objects (see
[Writing from several threads](https://joshclose.github.io/CsvHelper/writing.html#writing-from-several-threads)), with
byte-identical output. Unlike the other benchmarks, these run on all 32 threads of the same machine (16 cores). The
data are the PackageAssets records, the Floats records (objects with 25 `float` fields) and **Orders** (9.5 MB): 12
fields of dates, a `decimal`, a `Guid`, numbers and text. 50,000 records each. The 4, 8 and 16 thread columns set
`maxDegreeOfParallelism`; 32 threads is the default, one per processor.

| Data | `WriteRecords` | 4 threads | 8 threads | 16 threads | **32 threads** |
|---|---:|---:|---:|---:|---:|
| PackageAssets | 8.23 ms | 2.84 ms (2.9x) | 1.71 ms (4.8x) | 1.84 ms (4.5x) | **1.32 ms (6.2x)** |
| Floats | 23.6 ms | 7.16 ms (3.3x) | 3.97 ms (5.9x) | 2.31 ms (10.2x) | **1.62 ms (14.6x)** |
| Orders | 6.50 ms | 2.29 ms (2.8x) | 1.29 ms (5.1x) | 0.90 ms (7.2x) | **0.85 ms (7.7x)** |

To a file (`CsvWriter.ToFile`, on a WD Blue SN5000 NVMe drive), with 32 threads:

| Data | `WriteRecords` | **`ParallelWriteRecords`** |
|---|---:|---:|
| PackageAssets | 12.0 ms | **5.59 ms (2.2x)** |
| Floats | 28.2 ms | **5.13 ms (5.5x)** |
| Orders | 8.78 ms | **3.54 ms (2.5x)** |

To memory, the parallel writer reaches 12.1 GB/s on PackageAssets, 9.1 GB/s on Floats and 11.3 GB/s on Orders. To a
file, all three end up at 2.7 to 2.9 GB/s: writing the file is then the limit, so the records that cost the most to
format gain the most. A call allocates about 42 KB with 32 workers (their writers and output buffers), against 416
bytes for `WriteRecords`.

Run them with:

```shell
cd bench/CsvHelper.Benchmarks
dotnet run -c Release -- --filter *PackageAssetsRead.* *PackageAssetsReadFromString.* *PackageAssetsReadFile.* *FloatsRead.* *QuotedRead.* *PackageAssetsWrite.* *FloatsWrite.* *QuotedWrite.*
dotnet run -c Release -- --filter *.EscapeRead.* *.NoEscapeRead.* *.FixedWidthRead.* *.EscapeWrite.* *.NoEscapeWrite.* *.FixedWidthWrite.*
dotnet run -c Release -- --filter *SeparatorPackageAssetsRead.* *SeparatorQuotedRead.* --anyCategories Rows Strings
dotnet run -c Release -- --filter *ParallelWrite.*            # on every core
dotnet run -c Release -- verify                # check that all libraries return the same results
dotnet run -c Release -- quick QuotedRead      # quick Stopwatch runner for tuning (not for published numbers)
```

## Building and testing

```shell
dotnet build -c Release
dotnet test tests/CsvHelper.Tests -c Release              # .NET 10, .NET 8 and .NET Framework 4.8 (Windows)
dotnet test tests/CsvHelper.Tests -c Release -f net10.0  # .NET 10 only
pwsh docs/build.ps1                                       # the documentation site, in docs/_site
pwsh docs/build.ps1 -Preview 34.0.0                       # the site as it will be once v34.0.0 is tagged
pwsh docs/build.ps1 -h                                    # all of its options
```

<!-- docs:exclude -->
## License

Completely free for commercial use. Dual licensed under the
[Microsoft Public License (MS-PL)](https://opensource.org/licenses/MS-PL) and the
[Apache License, Version 2.0](https://opensource.org/licenses/Apache-2.0): choose whichever suits your needs. See
[LICENSE.txt](LICENSE.txt).

## Contributing

Want to contribute? Great! Here are a few guidelines.

1. If you want to do a feature, post an issue about the feature first. Some features are intentionally left out, some
   features may already be in the works, or I may have some advice on how I think it should be done. I would feel bad
   if time was spent on some code that won't be used.
2. If you want to do a bug fix, it might not be a bad idea to post about it too. I've had the same bug fixed by multiple
   people at the same time before.
3. All code should have a unit test. If you make a feature, there should be significant tests around the feature. If
   you do a bug fix, there should be a test specific to that bug so it doesn't happen again.
4. Pull requests should have a single commit. If you have multiple commits, squash them into a single commit before
   requesting a pull.
5. When in Rome: Try and follow the code styling already in place. I use default Visual Studio Text Editor settings but
   change tabs to Keep tabs.

## Credits

### Contributors

This project exists thanks to all the people who contribute.

<a href="https://github.com/JoshClose/CsvHelper/graphs/contributors"><img src="https://opencollective.com/csvhelper/contributors.svg?width=890&button=false" alt="CsvHelper's contributors" /></a>

### Sponsors

You can make one-time or recurring donations through [GitHub Sponsors](https://github.com/sponsors/JoshClose).

A huge thanks to the [.NET on AWS Open Source Software Fund](https://github.com/aws/dotnet-foss) for sponsoring
CsvHelper!

<a href="https://github.com/aws/dotnet-foss"><img src="docs/assets/images/aws.png" alt=".NET on AWS Open Source Software Fund" /></a>

Thanks to [Microsoft](https://github.com/microsoft) for being a sponsor!

<a href="https://github.com/microsoft"><img src="docs/assets/images/microsoft.png" alt="Microsoft" /></a>
<!-- /docs:exclude -->
