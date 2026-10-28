---
title: Reading
icon: book-open
description: Read records and fields from files, streams, strings and memory, synchronously, asynchronously or on several threads, in RFC 4180, escaped, unescaped or fixed-width format.
---

# Reading

`CsvReader` reads forward, one record at a time. The input is parsed as UTF-8 bytes, and a field is only turned
into a string or a number when you ask for it, so you pay only for the fields you use.

```csharp
using CsvHelper;

using var reader = CsvReader.FromFile("orders.csv");    // or Create(stream), Create(byte[]), Create(TextReader), FromText(string)
var price = reader.GetFieldIndex("Unit Price");         // header lookup: exact, then ignoring case and whitespace
while (reader.Read())
{
    string id = reader.GetString(0);                    // by index
    string name = reader.GetString("Name");             // by header name
    decimal unitPrice = reader.Parse<decimal>(price);   // also int, long, double, bool, DateTime, Guid, ...
    ReadOnlySpan<byte> raw = reader.GetSpan(3);         // UTF-8, already unquoted, no allocation
    DateOnly ordered = reader.Parse<DateOnly>(4);       // any ISpanParsable<T> (.NET 8 and later)
}
```

The examples on this page read this file, `orders.csv`, unless they say otherwise:

```
Id,Name,Unit Price,Quantity,Ordered
A-1,Widget,9.99,3,2024-03-01
A-2,"Gadget, large",24.50,1,2024-03-02
A-3,"12"" ruler",3.25,,2024-03-02
```

## Opening a reader

Each source has a factory method. They all take an optional `CsvReaderOptions` (see [Options](#options)), and none
of them reads anything until the first record or the header is needed.

| Source | Method | Notes |
|---|---|---|
| A file | `CsvReader.FromFile(path)` | Opened for reading and shared with other readers; closed when the reader is disposed. |
| A `Stream` | `CsvReader.Create(stream)` | Read sequentially from its current position; it does not need to be seekable. Disposed with the reader unless `leaveOpen: true`. |
| UTF-8 bytes | `CsvReader.Create(byte[])`, `CsvReader.Create(ReadOnlyMemory<byte>)` | Read in place, without a copy. The bytes must not change while the reader is in use. |
| A `string` | `CsvReader.FromText(text)` | Transcoded to UTF-8 in chunks while reading. |
| A `TextReader` | `CsvReader.Create(textReader)` | For text that is already decoded, such as a `StreamReader` with a chosen encoding. Disposed with the reader unless `leaveOpen: true`. |

```csharp
// A file. FileOptions.SequentialScan is the default; pass FileOptions.Asynchronous too for overlapped async reads.
using var fromFile = CsvReader.FromFile("orders.csv");

// A stream you keep using afterwards, for example an upload or a network response.
using var fromStream = CsvReader.Create(uploadStream, leaveOpen: true);

// Bytes already in memory: the fastest source, read without copying.
byte[] bytes = File.ReadAllBytes("orders.csv");
using var fromBytes = CsvReader.Create(bytes);

// A string, for example from a test or a text box.
using var fromText = CsvReader.FromText("Id,Name\nA-1,Widget\n");

// A TextReader.
using var fromTextReader = CsvReader.Create(new StringReader("Id,Name\nA-1,Widget\n"));
```

Readers hold pooled buffers, so dispose them (`using`, or `await using` for async code). Disposing a reader disposes
its stream, file or text reader too, unless it was created with `leaveOpen: true`.

## The read loop

`Read` moves to the next record and returns `false` at the end of the input. The first call reads the header too.
Empty lines, [comments](#comments-and-empty-lines) and records that the options say to skip are passed over.

```csharp
using var reader = CsvReader.FromFile("orders.csv");
while (reader.Read())
{
    Console.WriteLine($"Record {reader.RowNumber} has {reader.FieldCount} fields");
}

// Record 1 has 5 fields
// Record 2 has 5 fields
// Record 3 has 5 fields
```

- `RowNumber` is the 1-based number of the current data record. The header is not counted, and neither are skipped
  lines, so it is not a line number. Exceptions and malformed-record reports use a record number that counts the
  header (see [Errors](#errors)).
- `FieldCount` is the number of fields of the current record. Records may have different counts; see
  [Empty and missing fields](#empty-and-missing-fields).
- `ByteOffset` is where the record starts, in bytes of UTF-8 input (UTF-16 input counts the bytes of its UTF-8 form),
  for progress reports and error messages. `LineNumber` is the line where it starts, counting every line (those inside
  quoted fields, empty lines and comments too); it needs `TrackLineNumbers = true`, which makes reading a little
  slower.

`ShouldSkipRecord` skips data records by a test of yours, such as totals or section titles in the middle of the data.
It gets every record but the header, as a [`CsvRow`](#rows-and-fields), before `Read` returns it, so the records it
skips never reach `Read`, `GetRecords` or `ParallelEnumerate`, and do not use up a `RowNumber`:

```csharp
var options = new CsvReaderOptions { ShouldSkipRecord = row => row[0].ToString() is "Subtotal" or "Total" };
```

**Spans and rows are valid until the next read.** `GetSpan`, `GetRawSpan`, `RawRecord`, `CsvRow` and `CsvField`
point into the reader's buffer, which the next `Read` or `ReadAsync` reuses. Strings and parsed values are yours to
keep:

```csharp
reader.Read();
string first = reader.GetString(0);             // a new string: still "A-1" after the next Read
ReadOnlySpan<byte> span = reader.GetSpan(0);    // a view of the buffer: don't use it after the next Read
```

## Headers

With `HasHeader = true` (the default), the first record is the header. `Headers` lists its names, and
`GetFieldIndex` finds the index of a column:

```csharp
using var reader = CsvReader.FromFile("orders.csv");
IReadOnlyList<string> names = reader.Headers;          // Id, Name, Unit Price, Quantity, Ordered
int price = reader.GetFieldIndex("Unit Price");        // 2
bool found = reader.TryGetFieldIndex("Discount", out int discount);  // false
```

Getting `Headers` or calling `GetFieldIndex` before the first `Read` reads the header then. `ReadHeader` (or
`ReadHeaderAsync`) does the same explicitly, which is useful to check the columns before reading any record:

```csharp
using var reader = CsvReader.FromFile("orders.csv");
reader.ReadHeader();
string[] required = ["Id", "Unit Price", "Quantity"];
var missing = required.Where(name => !reader.TryGetFieldIndex(name, out _)).ToList();
if (missing.Count > 0) throw new InvalidDataException($"Missing columns: {string.Join(", ", missing)}");
```

### How names are matched

An exact match wins. Otherwise the name is matched ignoring case and the whitespace around it. When several columns
have the same name, the first one wins:

```csharp
// Header: " Unit Price ,ID,id"
reader.GetFieldIndex("unit price");   // 0: case and surrounding whitespace ignored
reader.GetFieldIndex("ID");           // 1: exact
reader.GetFieldIndex("id");           // 2: exact
reader.GetFieldIndex("Id");           // 1: no exact match; the first column that matches ignoring case
reader.GetFieldIndex("Total");        // throws KeyNotFoundException: The CSV header has no column named 'Total'.
```

Every accessor that takes a name (`GetString("Name")`, `reader["Name"]`, `Parse<T>("Name")`) matches it the same
way. A lookup by name costs a dictionary lookup, so in a loop over many records, look the index up once with
`GetFieldIndex` and use the index.

`NormalizeHeaderName` changes names before they are matched, both those of the header and those you look up, for
names that differ in more than case. `Headers` keeps the names as they are:

```csharp
var options = new CsvReaderOptions { NormalizeHeaderName = name => name.Replace("_", "") };
// Header: unit_price,order_id
reader.GetFieldIndex("UnitPrice");    // 0
reader.GetFieldIndex("order_id");     // 1: "orderid" either way
```

### Columns with the same name

A name finds the first column that has it. Pass the position among the columns with the name for the others:

```csharp
// Header: Id,Amount,Tax,Amount
reader.GetFieldIndex("Amount");                // 1
reader.GetFieldIndex("Amount", 1);             // 3: the second column called Amount
reader.TryGetFieldIndex("Amount", 2, out _);   // false
```

Exact matches are counted first, while there are enough of them, then the matches that ignore case and whitespace.
Record types choose a column the same way with [`[CsvColumn("Amount", NameIndex = 1)]`](records.html#by-name).

### Several data sets in one file

Calling `ReadHeader` while the reader is on a record makes that record the header of the records after it, for files
with several tables one after the other:

```csharp
// name,age
// Ann,30
// id,city
// 7,Oslo
var people = new List<Person>();
var cities = new List<City>();
var inCities = false;
while (reader.Read())
{
    if (reader.GetString(0) == "id")
    {
        reader.ReadHeader();       // Headers is now id, city
        inCities = true;
        continue;
    }

    if (inCities) cities.Add(reader.GetRecord<City>());
    else people.Add(reader.GetRecord<Person>());
}
```

The new header is not a data record (`RowNumber` goes on from the record before it), and the by-name accessors and the
record mappers created afterwards use its names. The next header is read as a record first, so when the data sets have
different numbers of columns, `FieldCountMismatch` must be `Allow` (the default).

### Files without a header

Set `HasHeader = false`. `Headers` is then empty, every record is data, and fields are read by index:

```csharp
var options = new CsvReaderOptions { HasHeader = false };
using var reader = CsvReader.FromText("A-1,9.99\nA-2,24.50\n", options);
while (reader.Read())
{
    string id = reader.GetString(0);
    decimal price = reader.Parse<decimal>(1);
}
```

Looking a name up without a header finds nothing (`GetFieldIndex` throws `KeyNotFoundException`).

## Getting field values

Every accessor takes a 0-based field index, and many also take a header name. Values are already decoded as the
[mode](#parsing-modes) says: unquoted with `""` turned into `"` in RFC 4180 mode, without escape characters in
Escape mode, without padding in FixedWidth mode.

### Strings

`GetString` creates a string from the field (an empty field is `string.Empty`). `GetChars` copies the field into a
buffer of yours instead, which avoids the allocation:

```csharp
string name = reader.GetString(1);                // Gadget, large
string sameName = reader.GetString("Name");

Span<char> buffer = stackalloc char[64];
int length = reader.GetChars(1, buffer);          // throws ArgumentException if the buffer is too small
ReadOnlySpan<char> chars = buffer[..length];
```

With `PoolStrings = true`, a value seen before in the same column returns the same string instance, which saves
memory when you keep many records; see [Performance tips](#performance-tips).

### Parse and TryParse

`Parse<T>` parses a field as `T`, by index or by header name. On every target framework, the built-in types are parsed
from the UTF-8 bytes of the field directly, without creating a string first:

| Type | Accepts |
|---|---|
| `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `ulong` | Integers (`NumberStyles.Integer`: an optional sign and surrounding whitespace). |
| `double`, `float` | Decimal and exponent notation, `NaN` and infinities, thousands separators (`NumberStyles.Float` plus thousands). Exactly rounded, like `double.Parse`; a value beyond the range is an infinity. |
| `decimal` | Numbers with thousands separators and exponents (`NumberStyles.Number` plus exponents). |
| `bool` | `true`/`false`, `yes`/`no`, `y`/`n`, `t`/`f` and `1`/`0` (or `TrueValues` and `FalseValues`), in any case, with surrounding whitespace ignored. |
| `DateTime` | ISO 8601 or the format provider's formats. `Z` gives a `DateTimeKind.Utc` value, an offset a local time; no zone gives `DateTimeKind.Unspecified`. |
| `DateTimeOffset` | ISO 8601 or the format provider's formats; without an offset, the local time zone's. |
| `Guid` | Any format `Guid.Parse` accepts. |
| `string` | The field, as `GetString` gets it. |
| Enums | Member names, ignoring ASCII case, and numbers and lists of flags. See [Enums](#enums). |

```csharp
// Active,Score,Seen,Key
// yes,1e3,2024-03-01T10:30:00Z,0f8fad5b-d9cb-469f-a165-70867728950e
bool active = reader.Parse<bool>(0);              // true
double score = reader.Parse<double>(1);           // 1000
DateTime seen = reader.Parse<DateTime>(2);        // 2024-03-01 10:30:00, Kind = Utc
Guid key = reader.Parse<Guid>("Key");             // by header name
bool ok = reader.TryParse<int>(1, out int count); // false: TryParse returns false instead of throwing
```

A field that cannot be parsed throws `FormatException` (`The input string 'abc' was not in a correct format.`), and
one out of range of an integer type throws `OverflowException`. Numbers and dates use the invariant culture unless
you set a [format provider](#numbers-dates-and-cultures).

`TrueValues` and `FalseValues` replace the texts of booleans, for files in another language or with codes of their own.
They are matched ignoring ASCII case and surrounding spaces and tabs, and setting one list keeps the built-in values of
the other:

```csharp
var options = new CsvReaderOptions { TrueValues = ["Ja", "J"], FalseValues = ["Nein", "N"] };
// Aktiv
//  ja
reader.Parse<bool>(0);                            // true
```

`Parse<T>(index, provider)` takes a format provider for one call, and `ParseExact<T>` parses a `DateTime` or a
`DateTimeOffset` in an exact format, as their own `ParseExact` does:

```csharp
// Opened;Amount;Month
// 20240301;1.234,50;März 2024
var german = CultureInfo.GetCultureInfo("de-DE");
DateTime opened = reader.ParseExact<DateTime>(0, "yyyyMMdd");             // 2024-03-01
decimal amount = reader.Parse<decimal>(1, german);                         // 1234.50
DateTime month = reader.ParseExact<DateTime>(2, "MMMM yyyy", german);     // 2024-03-01: a format and a provider
```

A field in another format throws `FormatException`. For files that mix formats, `ParseExact<T>(index, formats)` takes
several, tried in order: `reader.ParseExact<DateTime>(0, ["yyyy-MM-dd", "dd/MM/yyyy"])`.

### Enums

`Parse<T>`, `TryParse<T>` and `ParseOrNull<T>` read enums as [`[CsvRecord]` members](records.html#enums) do: the
names of the members and the other names that [`[CsvEnumName]`](records.html#enums) gives them are matched in UTF-8,
ignoring ASCII case (unless two names of different members differ only in case), and anything else, such as a number
or a list of flags, goes to `Enum.TryParse`, ignoring case:

```csharp
public enum Status { Open, [CsvEnumName("on hold")] OnHold, Closed }

[Flags]
public enum Access { None = 0, Read = 1, Write = 2 }

// Status,Access
// On Hold,"Read, Write"
Status status = reader.Parse<Status>("Status");   // Status.OnHold
Access access = reader.Parse<Access>(1);          // Access.Read | Access.Write
bool known = reader.TryParse(0, out Status other); // false for a value that is not a Status
```

A field that is not a value throws `FormatException` (`'pending' is not a value of Status.`, with the full name of the
type). Enums are not `ISpanParsable<T>`, so on .NET 8 and later these overloads are extension methods, of
`CsvEnumExtensions` in the `CsvHelper` namespace; on .NET Framework and .NET Standard the reader's own `Parse<T>` reads
enums. The calls are the same everywhere.

### Any other type

On .NET 8 and later, `Parse<T>` and `TryParse<T>` also read any other `ISpanParsable<T>` type (`DateOnly`, `TimeSpan`,
`Half`, `BigInteger`, your own types) with its own parser. On .NET Framework and .NET Standard they parse `TimeSpan`,
`BigInteger`, `char` and `IPAddress` too, as .NET does (through a string), and throw `NotSupportedException` for the
other types. On every target framework, the overloads that take a parser get the field as a string and the reader's
format provider:

```csharp
// When,Duration,Code
// 2024-03-01,01:30:00,7
DateOnly when = reader.Parse<DateOnly>("When");                       // .NET 8 and later: ISpanParsable<T>
bool ok = reader.TryParse<TimeSpan>(1, out TimeSpan duration);       // every target

TimeSpan parsed = reader.Parse("Duration", TimeSpan.Parse);           // every target: Func<string, IFormatProvider, T>
bool valid = reader.TryParse(1, TimeSpan.TryParse, out TimeSpan d);   // every target: CsvTryParser<T>
bool small = reader.TryParse(2, (string s, IFormatProvider p, out sbyte b) =>
    sbyte.TryParse(s, NumberStyles.Integer, p, out b), out sbyte code); // any parsing code of yours
```

### Empty and missing fields

An empty field reads as `string.Empty` from `GetString` and as an empty span from `GetSpan`; `Parse<T>` throws
`FormatException` for it (except as a `string`). `IsEmpty` tells you whether a field is empty, which makes optional
values easy:

```csharp
// A-3,"12"" ruler",3.25,,2024-03-02  (no quantity)
int quantityIndex = reader.GetFieldIndex("Quantity");
int? quantity = reader.IsEmpty(quantityIndex) ? null : reader.Parse<int>(quantityIndex);
int? sameQuantity = reader[quantityIndex].ParseOrNull<int>();     // null when empty or not an int
```

Records do not need to have the same number of fields. A field past the end of a short record reads as empty, as if
it were there; `IsEmpty` returns `true` for it:

```csharp
// A,B,C
// 1,2
reader.Read();
int count = reader.FieldCount;      // 2
string c = reader.GetString(2);     // ""
```

To treat a short record as an error, set `MissingField = CsvMissingFieldHandling.Throw`: reading a field past the end
then throws `IndexOutOfRangeException` (`Record 2 has 2 fields; field 2 does not exist.`). To check the field count of
every record as it is read, see [field counts](messy-files.html#field-counts).

### Null values

`NullValues` lists texts that mean null, such as `NULL` or `\N` in a database export. `IsNull` is true for them and for
empty fields, and `GetStringOrNull` and `ParseOrNull` return `null` for them. `GetStringOrNull` reads an empty field as
`""` unless `""` is one of the values:

```csharp
var options = new CsvReaderOptions { NullValues = ["NULL", @"\N"] };
// Name,Note,Quantity
// A-1,NULL,\N
bool none = reader.IsNull(2);                      // true
string? note = reader.GetStringOrNull(1);          // null
int? quantity = reader[2].ParseOrNull<int>();      // null
```

The values are matched exactly, after [trimming](#whitespace); a quoted `"NULL"` is null too. Without `NullValues`,
`IsNull` is `IsEmpty` and `GetStringOrNull` is `GetString`. [Record types](records.html#null-values-csvnullvalues) read
their `Nullable<T>` members and nullable string members with these rules.

### Spans and raw text

`GetSpan` returns the decoded value as UTF-8 bytes, without allocating. `GetRawSpan` returns the field exactly as it
appears in the input, with its quotes, escape characters or padding, and `RawRecord` the whole record without its line
break:

```csharp
// Text,Other
// "say ""hi""", plain
reader.Read();
ReadOnlySpan<byte> text = reader.GetSpan(0);       // say "hi"           (UTF-8 bytes)
ReadOnlySpan<byte> raw = reader.GetRawSpan(0);     // "say ""hi"""
ReadOnlySpan<byte> other = reader.GetSpan(1);      //  plain             (whitespace is kept unless TrimWhitespace is set)
ReadOnlySpan<byte> record = reader.RawRecord;      // "say ""hi""", plain
```

Spans are the way to work with fields without creating strings: compare them with UTF-8 literals, search them, or
pass them to UTF-8 parsers and writers.

```csharp
int currency = reader.GetFieldIndex("Currency");
while (reader.Read())
{
    if (!reader.GetSpan(currency).SequenceEqual("EUR"u8)) continue;
    total += reader.Parse<decimal>(amount);
}
```

## Rows and fields

`foreach` over the reader gives a `CsvRow` for every record, and a row's indexer gives a `CsvField`:

```csharp
using var reader = CsvReader.FromFile("orders.csv");
foreach (CsvRow row in reader)
{
    string id = row[0].ToString();
    decimal price = row["Unit Price"].Parse<decimal>();
    int? quantity = row["Quantity"].ParseOrNull<int>();
    DateOnly ordered = row[4].Parse(DateOnly.Parse);             // a parser of strings
    string[] all = row.ToArray();                                // every field as a string
    Console.WriteLine($"{row.RowNumber}: {row.ToString()}");     // the raw record: A-1,Widget,9.99,3,2024-03-01
}
```

| Member | |
|---|---|
| `CsvRow.FieldCount`, `CsvRow.RowNumber` | As on the reader. |
| `row[index]`, `row["name"]` | The field, as a `CsvField`. |
| `CsvRow.GetSpan`, `CsvRow.GetString`, `CsvRow.Parse` | As on the reader. |
| `CsvRow.ToArray` | Every field as a new string array. |
| `CsvRow.ToDictionary` | Every field as a new dictionary keyed by header name (`Field1`, `Field2` and so on without a header). |
| `CsvRow.ToString`, `CsvRow.RawRecord` | The record as it appears in the input. |
| `CsvField.Span`, `CsvField.Length`, `CsvField.IsEmpty` | The decoded value as UTF-8, its length in bytes, whether it is empty. |
| `CsvField.ToString` | The value as a string. |
| `CsvField.Parse`, `CsvField.TryParse`, `CsvField.ParseOrNull` | Parse the value, as a built-in type (on .NET 8 and later, any `ISpanParsable<T>`) or with a parser. `ParseOrNull` returns `null` for an empty or invalid value. |

The reader's own indexer and `Current` work the same way, so `reader["Name"].ToString()` and `reader.Current` are
available in a `while (reader.Read())` loop too.

`CsvRow` and `CsvField` are `ref struct`s: they cannot be stored in fields or collections, captured by lambdas, or
used after the next read. Copy what you need into strings or values. To keep whole records without a record type, read them as
[dynamic records](records.html#dynamic-records) (`reader.GetRecords<dynamic>()`).

## Reading asynchronously

`ReadAsync` and `ReadHeaderAsync` read from the source asynchronously. They only await when the buffer has to be
refilled; otherwise they complete synchronously, without allocating. Field access is the same as in synchronous code.

```csharp
await using var reader = CsvReader.Create(stream);
await reader.ReadHeaderAsync(cancellationToken);              // optional: Headers is then known without blocking
while (await reader.ReadAsync(cancellationToken))
{
    string id = reader.GetString(0);
    decimal price = reader.Parse<decimal>(2);
}
```

`await foreach` gives rows, as `foreach` does (C# 13 or later, since `CsvRow` is a `ref struct`; a row cannot be used
after an `await` in the loop body):

```csharp
await using var reader = CsvReader.Create(stream);
await foreach (CsvRow row in reader)
{
    Console.WriteLine(row["Name"].ToString());
}
```

To pass a cancellation token, use the `ReadAsync` loop above. `GetRecordsAsync<T>` reads [records](records.html) as
an `IAsyncEnumerable<T>`. Accessing `Headers` or `GetFieldIndex` before the header has been read reads it
synchronously, so async code calls `ReadHeaderAsync` first.

## Reading on several threads

A reader is not thread-safe: use it from one thread at a time, and finish each async call before starting the next.
Nothing checks this; two threads reading one reader get wrong records, not an exception. What a reader is given or
returns for reuse does not change, so it can be shared between threads: the [options](#options), the
[record mappers](records.html#reading-records) and [dynamic records](records.html#dynamic-records).

`ParallelEnumerate` maps records to results on several threads and returns the results in input order. It suits large
inputs where turning a record into an object (parsing, validation, lookups) costs more than reading it:

```csharp
using var reader = CsvReader.FromFile("orders.csv");
List<Order> orders = reader
    .ParallelEnumerate(r => new Order(r.GetString(0), r.Parse<decimal>(2), r.Parse<int>(3)))
    .ToList();

// Records of a [CsvRecord] type:
List<Order> sameOrders = reader2.ParallelEnumerate<Order>().ToList();

// At most 4 threads (the default, -1, is one per processor):
decimal total = reader3.ParallelEnumerate(r => r.Parse<decimal>(2) * r.Parse<int>(3), maxDegreeOfParallelism: 4).Sum();
```

Workers take turns reading and scanning the next batch of records, then map that batch themselves while its bytes are
still in their cache. Rules for the selector:

- It gets a reader of its own (per thread), positioned on one record. Use its field accessors and return a value. Do
  not call `Read` on it (that throws `InvalidOperationException`), and do not keep its spans after returning.
- Calls run concurrently, so anything shared that the selector changes must be thread-safe.
- The input is read and checked by one thread at a time, in order: skipped lines, `ShouldSkipRecord`,
  `OnMalformedRecord` callbacks and field count checks happen exactly as in a `Read` loop, never concurrently.
- `ParallelEnumerate<T>` shares one mapper between the workers, so [converters](records.html#converters-csvconverter)
  must be thread-safe. It also calls `OnRecordError` on the workers, so that callback can run on several threads at
  once.
- An exception, from the reader or the selector, is thrown by the enumeration after the results of the records before
  it.
- With `PoolStrings`, the workers share one string pool.
- Don't use the reader for anything else until the enumeration ends.

## DataTable and IDataReader

`CsvDataReader` wraps a reader as a `DbDataReader` (an `IDataReader`), for APIs that take one: `DataTable.Load`,
`SqlBulkCopy.WriteToServer` and other bulk importers.

```csharp
using var reader = CsvReader.FromFile("orders.csv", new CsvReaderOptions { NullValues = ["NULL"] });
using var data = new CsvDataReader(reader);
var table = new DataTable();
table.Load(data);      // columns Id, Name, Unit Price, ...; NULL fields are DBNull
```

Every column is a string column (`GetFieldType` is `string`), named after the header, or `Column1`, `Column2`, ...
without one (the first record is read to count the columns). `GetValue` returns the field as a string, or `DBNull` for
a [null value](#null-values). The typed getters (`GetInt32`, `GetDecimal`, `GetDateTime`, ...) parse the field as the
reader's `Parse<T>` does, and `GetOrdinal` matches names as [`GetFieldIndex`](#how-names-are-matched) does. Disposing the
data reader disposes the CSV reader, unless it was created with `leaveOpen: true`.

## Parsing modes

`Mode` chooses how fields are delimited and escaped. The writer has the same modes (see
[Writing](writing.html#modes)), so what one writes the other reads.

| Mode | Fields | Example |
|---|---|---|
| `Rfc4180` (default) | Quoted fields can contain separators and line breaks; `""` is a quote. | `"a,b","say ""hi"""` |
| `Escape` | No quoting: the escape character (`Escape`, `\` by default) makes the next character text. | `a\,b,say "hi"` |
| `NoEscape` | No quoting or escaping: everything but the separator and line breaks is text. | `a b,say "hi"` |
| `FixedWidth` | No separators: columns of fixed widths (`FieldWidths`), padded with spaces. | `a b  say "hi"` |

### RFC 4180

The default. A field that starts with a quote is a quoted field: separators and line breaks inside it are text, and a
doubled quote is one quote. The quotes are removed from the value.

```
Name,Address,Note
Anna,"12 Main St, Springfield","said ""hi"""
Ben,"Line one
Line two",
```

```csharp
using var reader = CsvReader.FromFile("contacts.csv");
while (reader.Read())
{
    Console.WriteLine($"[{reader.GetString(0)}] [{reader.GetString(1)}] [{reader.GetString(2)}]");
}

// [Anna] [12 Main St, Springfield] [said "hi"]
// [Ben] [Line one
// Line two] []
```

A quote anywhere else is an ordinary character: `5'10"` reads as `5'10"`. How the reader handles quotes that break the
rules is described in [Messy files](messy-files.html).

`Quote` changes the quote character, for files that quote with apostrophes:

```csharp
// Name,Note
// Anna,'a, b'
// Ben,'it''s'
var options = new CsvReaderOptions { Quote = '\'' };
using var reader = CsvReader.FromFile("notes.csv", options);    // Anna: "a, b", Ben: "it's"
```

### Escape

No quoting. The escape character makes the character after it text, whatever it is. This is the format of database
exports such as MySQL's `SELECT ... INTO OUTFILE`, and of many logs.

```
Path,Note
C:\\Temp\\out,a\, b
multi,line\
one
literal,\n and "quotes"
```

```csharp
var options = new CsvReaderOptions { Mode = CsvMode.Escape };
using var reader = CsvReader.FromFile("export.csv", options);
while (reader.Read())
{
    Console.WriteLine($"[{reader.GetString(0)}] [{reader.GetString(1)}]");
}

// [C:\Temp\out] [a, b]
// [multi] [line
// one]
// [literal] [n and "quotes"]
```

- `\,` is a comma, `\\` a backslash, and an escape before a line break keeps the line break in the field (before CR LF,
  the pair).
- Any other escaped character is itself: `\n` is the letter `n`, not a line break. Quotes are ordinary text.
- `Escape` sets another escape character: with `Escape = '^'`, `a^,b,c` reads as `a,b` and `c`. It must be ASCII, and
  not a space, a tab or the separator.
- An escape character at the very end of the input is kept as text; it is reported as `TrailingEscape` when you
  [watch for malformed records](messy-files.html#reporting-malformed-records).
- `GetRawSpan` returns a field with its escape characters, for formats that give an escape sequence a meaning of its
  own (MySQL writes `NULL` as `\N`, which reads as `N`):

```csharp
bool isNull = reader.GetRawSpan(3).SequenceEqual(@"\N"u8);
```

### NoEscape

No quoting and no escaping: everything except the separator and line breaks is text, read as it is. Use it for
tab-separated exports, logs and other files where quotes are data:

```
Name,Comment
Anna,said "hi"
"Ben",5'10"
```

```csharp
var options = new CsvReaderOptions { Mode = CsvMode.NoEscape };
using var reader = CsvReader.FromFile("comments.csv", options);
// Anna: [said "hi"]
// "Ben": ["Ben"] [5'10"]   (in Rfc4180 mode: [Ben] [5'10"], the quotes of "Ben" are removed)
```

A value can contain neither the separator nor a line break, since nothing could mark them as text.

### FixedWidth

No separators: each line is a record, cut into columns of fixed widths. `FieldWidths` gives the width of every column,
in characters, from the start of the line.

```
Id    Name        Balance
1     Anna         1250.00
2     Benedikt       -3.75   extra
3     Cara
```

```csharp
var options = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [6, 12, 10] };
using var reader = CsvReader.FromFile("accounts.txt", options);
while (reader.Read())
{
    int id = reader.Parse<int>(0);
    string name = reader.GetString("Name");
    string balance = reader.GetString(2);
}

// 1, Anna, 1250.00
// 2, Benedikt, -3.75      (text after the last column is ignored)
// 3, Cara, ""             (a short line has empty fields at its end)
```

- Values are trimmed of spaces and tabs. `GetRawSpan` returns a column with its padding (`"   -3.75  "`), and
  `TrimWhitespace = false` keeps the padding in every value.
- Each line has a field for every column (`FieldCount` is the number of widths). A short line has empty fields at its
  end, and text after the last column is not part of any field (`RawRecord` still has it).
- A header line is split into columns too, so `GetFieldIndex("Name")` and [records](records.html) by column name work.
  Files without a header line need `HasHeader = false`.
- A record type can describe the layout itself: `[CsvColumn(Index = 0, Width = 6)]` on its members, and
  `FieldWidths = CsvRecordMapper.GetFieldWidths<T>()` (see [Fixed-width records](records.html#fixed-width-records)).
- Widths count characters as .NET counts them (`string.Length`, UTF-16 code units), not bytes, so `José` is 4
  characters wide. Files in a single-byte code page work too, read through [`Encoding`](#encodings).
- `Separator`, `Quote` and the `sep=` line don't apply (`Separator` is `'\0'`).

### What works in every mode

Empty lines, comments (`CommentPrefix`), `TrimWhitespace`, `MissingField`, field count checks, every input source,
async reading, `ParallelEnumerate` and [records](records.html) work the same way in every mode.

## Separators and dialects

### Separator detection

By default the separator is detected from the first lines of the input: `,`, `;`, tab or `|`, whichever appears most
consistently per line. `Separator` tells you which one was found:

```csharp
using var reader = CsvReader.FromText("Name;City\nAnna;Paris\nBen;Rome\n");
char separator = reader.Separator;       // ';'
```

Excel writes a first line `sep=;` to name the separator. The reader honors it and skips the line
(`DetectSeparatorHint = false` turns that off). To use one separator without detection, set it:

```csharp
var options = new CsvReaderOptions { Separator = ';' };   // any ASCII character, such as '\t' or ':'
```

Getting `Separator` before the first `Read` reads the start of the input, synchronously.

### Separators of several characters

`Separator` is one ASCII character. For a longer separator, such as `||`, or one that is not ASCII, such as `¦`, read
through a `CsvSeparatorStream`. It puts one byte in place of each separator, and the reader splits fields on that byte:

```csharp
// Id||Name||Note
// 1||Tea||"loose || 100 g"
using var reader = CsvReader.Create(new CsvSeparatorStream(File.OpenRead("items.txt"), "||"));
while (reader.Read())
{
    string note = reader.GetString("Note");     // loose || 100 g
}
```

The reader gives the stream its `Mode`, `Quote`, `Escape`, `CommentPrefix`, `TrimWhitespace` and
`AllowLineBreaksInQuotes`, so a separator that is text stays text: in a quoted field, after an escape character (Escape
mode) and in a comment line. Separators that overlap are taken from the left: `a|||b` is `a` and `|b`, and `a||||b` has
an empty field in the middle. Leave `Separator` unset, or set it to the stream's `Replacement`. FixedWidth mode has no
separator to replace.

- **The replacement** is U+001F, the ASCII unit separator, unless you choose another:
  `new CsvSeparatorStream(stream, "||", replacement: '\t')`. `RawRecord` and `GetRawSpan` show it where the separator
  was. Input that contains it outside quoted fields would split fields that are not there, so reading such input throws
  `CsvFormatException`.
- **Encodings.** The input is UTF-8, or a single-byte encoding set in `Encoding` (the separator is encoded with it).
  UTF-16 and UTF-32 input throws `NotSupportedException`.
- **Speed.** The stream replaces separators in the reader's own buffer, with the same 64-byte SIMD blocks as the reader,
  so the reader keeps its UTF-8 fast path. On the benchmark machine it replaces 7 to 10 GB/s with AVX-512 and 4.5 to
  6 GB/s with AVX2. Finding the records of a `||` file takes 1.65 to 1.85 times as long as for the same file with commas.

### Comments and empty lines

`CommentPrefix` skips lines that start with that character. Empty lines are skipped by default; with
`SkipEmptyLines = false`, an empty line is a record with one empty field.

```csharp
// # exported 2024-03-01
// Name,Qty
//
// Apple,3
// # a note
// Pear,5
var options = new CsvReaderOptions { CommentPrefix = '#' };
using var reader = CsvReader.FromFile("stock.csv", options);     // Name,Qty header; records Apple,3 and Pear,5
```

A comment prefix must start the line; inside a record it is ordinary text. In Escape mode, `\#` at the start of a line
is text, not a comment.

### Whitespace

Spaces and tabs around values are kept by default. `TrimWhitespace = true` removes them, from header names too.
Whitespace inside quotes is kept, a quote after leading whitespace still opens a quoted field, and escaped whitespace
(Escape mode) is kept:

```csharp
// Name , Qty
//   Apple ,  3
//  "  Pear " ,5
var options = new CsvReaderOptions { TrimWhitespace = true };
// Headers: [Name] [Qty]. Records: [Apple] [3] and [  Pear ] [5].
```

The typed number getters ignore surrounding whitespace even without trimming. In FixedWidth mode, values are trimmed
unless `TrimWhitespace = false`.

`TrimInsideQuotes = true` trims the values of quoted fields too, on its own or with `TrimWhitespace`:

```csharp
//  "  Pear " ,5
var options = new CsvReaderOptions { TrimWhitespace = true, TrimInsideQuotes = true };
// [Pear] [5]
```

`WhitespaceCharacters` (space and tab by default) says what trimming removes, for files padded with other characters.
They must be ASCII, and neither CR, LF, the quote nor the escape character:

```csharp
var options = new CsvReaderOptions { TrimWhitespace = true, WhitespaceCharacters = " \t_" };
// __a_,_b  →  [a] [b]
```

## Encodings

Input is UTF-8 unless it says otherwise. A UTF-8 byte order mark is skipped, and UTF-16 and UTF-32 byte order marks are
detected, in which case the input is transcoded to UTF-8 while reading. For files in another encoding, set `Encoding`:

```csharp
// A Latin-1 file: without Encoding, "José" would read as "Jos\uFFFD" (invalid UTF-8).
var options = new CsvReaderOptions { Encoding = Encoding.Latin1 };
using var reader = CsvReader.FromFile("legacy.csv", options);

// Windows code pages on .NET (Core) need the code pages provider first:
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var windows1252 = new CsvReaderOptions { Encoding = Encoding.GetEncoding(1252) };
```

`Encoding` applies to streams, files and bytes. Strings and `TextReader`s are already decoded.

## Numbers, dates and cultures

Numbers, dates and times are parsed with the invariant culture: `.` is the decimal separator and dates are ISO 8601
(`2024-03-01`, `2024-03-01T10:30:00Z`). For files written in another culture, set `FormatProvider`:

```csharp
// Artikel;Preis;Datum
// Tee;1.234,50;01.03.2024
var options = new CsvReaderOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
using var reader = CsvReader.FromFile("preise.csv", options);
reader.Read();
decimal price = reader.Parse<decimal>(1);     // 1234.50
DateTime date = reader.Parse<DateTime>(2);    // 2024-03-01
```

The format provider is used by `Parse`, `TryParse` and `ParseExact`, the parsers you pass them, and records.
Plain integers, and floats when the decimal separator is `.`, take fast paths that parse the UTF-8 directly; anything
else goes through the .NET parsers. Booleans accept the same values in every culture.

For a file whose columns use different cultures, pass a provider to a single call (`reader.Parse<decimal>(1, culture)`),
or give a record member [`[CsvCulture("de-DE")]`](records.html#cultures-csvculture).

## Errors

| Exception | When |
|---|---|
| `CsvFormatException` | A malformed record, when `MalformedData` or `FieldCountMismatch` say to throw (see [Messy files](messy-files.html)), or a record too large to buffer or longer than `MaxRecordSize`. `RecordNumber`, `ByteOffset` and (with `TrackLineNumbers`) `LineNumber` say where. Also a record that cannot be read as a [record type](records.html#errors), with the member and field in `Member` and `FieldIndex`, and the cause in `InnerException`. |
| `FormatException`, `OverflowException` | `Parse`, `ParseExact` or a parser got a field that is not a valid value, or is out of range. |
| `KeyNotFoundException` | No header column has the name you asked for. |
| `IndexOutOfRangeException` | A field past the end of the record, with `MissingField = CsvMissingFieldHandling.Throw`. |
| `ArgumentException` | Invalid options, when the reader is created: a separator that is not ASCII, the same character for two purposes, missing `FieldWidths` in FixedWidth mode. |
| `InvalidOperationException` | `Read` was called on the reader that a `ParallelEnumerate` selector got. |

`CsvFormatException.RecordNumber` counts the header and the records returned before (not skipped records, empty lines
or comments). Records before the bad one are still read normally, so you can report where the input went wrong:

```csharp
var options = new CsvReaderOptions { MalformedData = CsvMalformedDataHandling.Throw };
using var reader = CsvReader.FromFile("orders.csv", options);
try
{
    while (reader.Read()) Import(reader);
}
catch (CsvFormatException e)
{
    // Malformed CSV in record 3: text after the closing quote of a quoted field (byte offset 26).
    Console.WriteLine($"{e.Message} Record {e.RecordNumber}, byte {e.ByteOffset}.");
}
catch (FormatException e)
{
    Console.WriteLine($"Bad value in record {reader.RowNumber}: {e.Message} ({Encoding.UTF8.GetString(reader.RawRecord)})");
}
```

With `TrackLineNumbers = true`, messages name the line too (`Malformed CSV in record 3 (line 4): ...`).

### Values in exception messages

The messages of `Parse` show the value that did not parse (`'abc' is not a valid boolean.`). When fields may
hold personal data that must stay out of logs, set `IncludeValuesInExceptions = false`: `Parse`, `ParseExact` and the
generated record mappers then name the type but not the value (`The field is not a valid Int32
(IncludeValuesInExceptions is off, so its value is not shown).`). The exceptions of your own parsers and converters are
not changed.

### Records too large to buffer

A record grows the read buffer until it fits, up to about 2 GiB, so input such as an unclosed quote at the start of a
large file makes the reader buffer the rest of it. `MaxRecordSize` sets a limit, in bytes:

```csharp
var options = new CsvReaderOptions { MaxRecordSize = 1024 * 1024 };
// CsvFormatException: Record 2 is longer than MaxRecordSize (1048576 bytes).
```

The limit is checked only when the reader needs more input, so it costs nothing per record: a longer record that is
already in the buffer is read, and the buffer never grows past about twice the limit. Bytes in memory (`byte[]` and
`ReadOnlyMemory<byte>`) are read in place, not buffered, so the limit does not apply to them; strings are transcoded in
chunks, so it does.

## Performance tips

- **Look indexes up once.** Call `GetFieldIndex` before the loop and use the index inside it.
- **Parse straight from the field.** `Parse<int>`, `Parse<double>`, `Parse<decimal>` and the other built-in types
  parse the UTF-8 bytes; `int.Parse(reader.GetString(i))` creates a string first.
- **Use spans for comparisons and copies.** `reader.GetSpan(i).SequenceEqual("EUR"u8)` allocates nothing, and
  `CsvWriter.WriteField(reader.GetSpan(i))` copies a field to a writer as UTF-8.
- **Keep strings with `PoolStrings`.** When you keep many records in memory, `PoolStrings = true` makes repeated values
  of a column share one string instance, which saves memory and garbage collection work. Columns of mostly unique
  values switch pooling off by themselves. For strings that are used and dropped, the default (no pooling) is faster.
- **Prefer bytes to text.** Files, streams and `byte[]` are read as UTF-8 directly; strings and `TextReader`s are
  transcoded from UTF-16 first.
- **Spread expensive mapping over threads** with [`ParallelEnumerate`](#reading-on-several-threads).
- **The buffer grows by itself.** `BufferSize` (64 KiB by default, at least 256 bytes) is the initial size of the read
  buffer; it grows for records longer than that, so it rarely needs changing.

## Options

The options are an immutable record. Create one with an object initializer, or derive one from another with `with`,
and pass it to any factory method. One instance can be shared by any number of readers, on any thread.

```csharp
var semicolons = new CsvReaderOptions { Separator = ';', FormatProvider = CultureInfo.GetCultureInfo("fr-FR") };
var semicolonsNoHeader = semicolons with { HasHeader = false };
using var reader = CsvReader.FromFile("data.csv", semicolons);
```

Options are checked when a reader is created; invalid ones throw `ArgumentException` (for example
`Separator and Quote must differ.`). `CsvReaderOptions.For<T>()` returns the options that a record type's
[`[CsvOptions]`](records.html#options-on-the-record-type-csvoptions) gives, such as its separator and culture.

| Option | Default | Explained in |
|---|---|---|
| `Mode` | `Rfc4180` | [Parsing modes](#parsing-modes) |
| `Separator` | `null`: detected | [Separator detection](#separator-detection), [several characters](#separators-of-several-characters) |
| `DetectSeparatorHint` | `true` | [Separator detection](#separator-detection) |
| `HasHeader` | `true` | [Headers](#headers) |
| `NormalizeHeaderName` | `null` | [How names are matched](#how-names-are-matched) |
| `Quote` | `"` | [RFC 4180](#rfc-4180) |
| `Escape` | `\` | [Escape](#escape) |
| `FieldWidths` | `null` (required in FixedWidth mode) | [FixedWidth](#fixedwidth) |
| `CommentPrefix` | `null`: no comments | [Comments and empty lines](#comments-and-empty-lines) |
| `SkipEmptyLines` | `true` | [Comments and empty lines](#comments-and-empty-lines) |
| `TrimWhitespace` | `null`: trim in FixedWidth mode only | [Whitespace](#whitespace) |
| `TrimInsideQuotes` | `false` | [Whitespace](#whitespace) |
| `WhitespaceCharacters` | `" \t"` | [Whitespace](#whitespace) |
| `ShouldSkipRecord` | `null` | [The read loop](#the-read-loop) |
| `TrackLineNumbers` | `false` | [The read loop](#the-read-loop) |
| `MissingField` | `ReturnEmpty` | [Empty and missing fields](#empty-and-missing-fields) |
| `NullValues` | `null` | [Null values](#null-values) |
| `TrueValues`, `FalseValues` | `null`: the built-in values | [Parse and TryParse](#parse-and-tryparse) |
| `MalformedData` | `Recover` | [Messy files](messy-files.html#options-for-malformed-files) |
| `OnMalformedRecord` | `null` | [Messy files](messy-files.html#reporting-malformed-records) |
| `AllowLineBreaksInQuotes` | `true` | [Messy files](messy-files.html#unbalanced-quotes) |
| `FieldCountMismatch` | `Allow` | [Messy files](messy-files.html#field-counts) |
| `OnRecordError` | `null` | [Records](records.html#errors) |
| `IncludeValuesInExceptions` | `true` | [Values in exception messages](#values-in-exception-messages) |
| `MaxRecordSize` | `null`: no limit | [Records too large to buffer](#records-too-large-to-buffer) |
| `Encoding` | `null`: byte order mark, else UTF-8 | [Encodings](#encodings) |
| `FormatProvider` | `CultureInfo.InvariantCulture` | [Numbers, dates and cultures](#numbers-dates-and-cultures) |
| `PoolStrings` | `false` | [Performance tips](#performance-tips) |
| `BufferSize` | 64 KiB | [Performance tips](#performance-tips) |
