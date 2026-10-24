---
title: Writing
icon: pen
description: Write strict RFC 4180 CSV (or escaped, unescaped and fixed-width files) field by field or from [CsvRecord] objects, with values formatted straight into a UTF-8 buffer, comments, other line endings and protection against spreadsheet formulas.
---

# Writing

`CsvWriter` writes records field by field into a UTF-8 buffer, or whole objects of [`[CsvRecord]`](records.html) types.
By default the output is strict RFC 4180, and numbers, dates and other values are formatted straight into the buffer,
without intermediate strings.

```csharp
using CsvHelper;

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

## Creating a writer

| Destination | Method | Notes |
|---|---|---|
| A file | `CsvWriter.ToFile(path)` | Created or overwritten; other processes can read it while it is written. Closed when the writer is disposed. |
| A `Stream` | `CsvWriter.Create(stream)` | Written from its current position. Disposed with the writer unless `leaveOpen: true`. |
| A `TextWriter` | `CsvWriter.Create(textWriter)` | Gets UTF-16 text, which it encodes itself. Disposed with the writer unless `leaveOpen: true`. |

```csharp
// A file.
using var toFile = CsvWriter.ToFile("orders.csv");

// A stream you keep using afterwards, such as a MemoryStream or an HTTP response body.
var stream = new MemoryStream();
using (var writer = CsvWriter.Create(stream, leaveOpen: true))
{
    writer.WriteRecord("Id", "Name");
}

byte[] csv = stream.ToArray();

// A string.
var text = new StringWriter();
using (var writer = CsvWriter.Create(text))
{
    writer.WriteRecord("Id", "Name");
}

string result = text.ToString();     // "Id,Name\r\n"
```

Dispose the writer when you are done: disposing ends an unfinished record, writes what is still in the buffer, and
flushes and closes the destination (unless it was left open). The output is not complete until then, or until
[`Flush`](#flushing-and-async-writing).

## Writing records

A record is a series of `WriteField` calls ended by `EndRecord`. `WriteRecord` writes a whole record of strings at
once, and `WriteHeader` writes the header, which is an ordinary record:

```csharp
using var writer = CsvWriter.ToFile("orders.csv");
writer.WriteHeader("Id", "Name", "Price", "Quantity");

writer.WriteField("A-1");
writer.WriteField("Widget");
writer.WriteField(9.99m);
writer.WriteField(3);
writer.EndRecord();

writer.WriteRecord("A-2", "Gadget, large", "24.50", "1");
writer.WriteRecord("A-3", "12\" ruler", null, "");     // null and "" are empty fields
```

```csv
Id,Name,Price,Quantity
A-1,Widget,9.99,3
A-2,"Gadget, large",24.50,1
A-3,"12"" ruler",,
```

`RecordCount` is the number of records written so far (the header included), and `FieldCount` the number of fields of
the current record so far. Fields already written to the current record come before those of `WriteRecord`.

## Writing [CsvRecord] types

The source generator writes a record writer for every [`[CsvRecord]`](records.html) type, next to its mapper. It writes
the columns that the mapper reads, with the same names, formats, cultures and converters, so a reader reads the records
back:

```csharp
[CsvRecord]
public partial class Order
{
    public string Id { get; set; } = "";
    [CsvColumn("Unit Price")]
    public decimal Price { get; set; }
    public int? Quantity { get; set; }
}

using var writer = CsvWriter.ToFile("orders.csv");
writer.WriteRecords(orders);                  // the header, then a record per order

// Id,Unit Price,Quantity
// A-1,9.99,3
// A-2,24.50,
```

| Method | Writes |
|---|---|
| `WriteRecords<T>(records)` | The header of `T` if nothing has been written yet and `HasHeader` is on (the default), then a record per item. |
| `WriteRecordsAsync<T>(records, cancellationToken)` | The same for an `IEnumerable<T>` or an `IAsyncEnumerable<T>`, flushing asynchronously. |
| `WriteHeader<T>()` | The header of `T`: the names of its columns. |
| `WriteRecord<T>(record)`, `WriteRecordAsync<T>(record)` | One record, without a header. |
| `WriteRecords<T>(records, recordWriter)` | The same as `WriteRecords<T>(records)` with a `CsvRecordWriter<T>` of your own, which is not registered for the type. |

To write records without a header, set `HasHeader = false` in the options, or call `WriteRecord<T>` for each one.
`WriteRecords` also writes [dynamic records](records.html#writing-dynamic-records), `ExpandoObject`s and dictionaries
keyed by column name, in the columns of the first record.
`CsvWriterOptions.For<T>()` returns the options that the type's
[`[CsvOptions]`](records.html#options-on-the-record-type-csvoptions) gives. The generated code calls the `WriteField`
overloads directly, so it is as fast as writing the members by hand, and like the mappers it uses no reflection.
[Records](records.html#writing-records) describes how each member is written and which types cannot be written.

Anonymous types, tuples and classes without `[CsvRecord]`, such as the entities of an ORM, can be written with the
`WriteCsv` extension of [CsvHelper.Linq](linq.html#writing-query-results), which writes them by their public members.

## Writing values

`WriteField` has overloads for every common type. Each formats its value straight into the buffer, then quotes,
escapes or pads it as the [mode](#modes) says.

### Text

```csharp
writer.WriteField("Gadget, large");           // a string; null is an empty field
writer.WriteField(name.AsSpan(0, 3));         // a ReadOnlySpan<char>
writer.WriteField("already UTF-8"u8);         // a ReadOnlySpan<byte>: copied as is, not validated
```

Invalid UTF-16 (a lone surrogate) is written as U+FFFD. UTF-8 spans are the fastest way to copy fields from a
`CsvReader` (see [Copying and transforming CSV](#copying-and-transforming-csv)).

### Numbers

Numbers are written in the invariant culture by default: `.` as the decimal separator, no thousands separators.
`double` and `float` are written as the shortest text that reads back as the same value.

```csharp
writer.WriteField(42);                 // 42
writer.WriteField(-42L);               // -42
writer.WriteField(9.99m);              // 9.99
writer.WriteField(0.1 + 0.2);          // 0.30000000000000004
writer.WriteField(1.5f);               // 1.5
writer.WriteField(1e21);               // 1E+21
writer.WriteField(double.NaN);         // NaN
```

A format string works as in `ToString`:

```csharp
writer.WriteField(9.5m, "F2");          // 9.50
writer.WriteField(255, "X4");           // 00FF
writer.WriteField(0.256, "P1");         // 25.6 %
writer.WriteField(1234567.891, "N2");   // "1,234,567.89"  (quoted: it contains the separator)
```

### Dates and times

`DateTime` and `DateTimeOffset` are written in ISO 8601 round-trip format (`"O"`) unless you give a format, and so are
`DateOnly` (`yyyy-MM-dd`) and `TimeOnly` (`HH:mm:ss.fffffff`) on .NET, also when they are nullable:

```csharp
writer.WriteField(new DateTime(2024, 3, 1, 10, 30, 0, DateTimeKind.Utc));               // 2024-03-01T10:30:00.0000000Z
writer.WriteField(new DateTime(2024, 3, 1, 10, 30, 0));                                 // 2024-03-01T10:30:00.0000000
writer.WriteField(new DateTimeOffset(2024, 3, 1, 10, 30, 0, TimeSpan.FromHours(2)));    // 2024-03-01T10:30:00.0000000+02:00
writer.WriteField(new DateOnly(2024, 3, 1));                                            // 2024-03-01
writer.WriteField(new TimeOnly(10, 30, 15));                                            // 10:30:15.0000000
writer.WriteField(new DateTime(2024, 3, 1, 10, 30, 0), "yyyy-MM-dd HH:mm");             // 2024-03-01 10:30
writer.WriteField(TimeSpan.FromMinutes(90));                                            // 01:30:00
```

A format provider can also be given for one value, such as a culture for one column:

```csharp
var german = CultureInfo.GetCultureInfo("de-DE");
writer.WriteField(1234.5m, default, german);                                            // "1234,5"
writer.WriteField(new DateTime(2024, 3, 1), "d", german);                               // 01.03.2024
```

### Booleans, nulls and other values

```csharp
writer.WriteField(true);               // True
writer.WriteField((int?)null);         // an empty field: every nullable overload writes null as NullValue
writer.WriteField(order.Key);          // a Guid: 0f8fad5b-d9cb-469f-a165-70867728950e
writer.WriteField(order.Quantity);     // an int?: the number, or an empty field
```

`NullValue` sets the text of null values (strings, nullable values, `null` objects and the null members of
[records](records.html#writing-records)), such as `NULL` or `\N` for a database import; `TrueValue` and `FalseValue` set
the text of booleans. A null value that needs quotes is quoted, and per-field quoting applies to it:

```csharp
var options = new CsvWriterOptions { NullValue = "NULL", TrueValue = "yes", FalseValue = "no" };
writer.WriteRecord("a", null, "");     // a,NULL,
writer.WriteField(true);               // yes
writer.WriteField((double?)null);      // NULL
```

An empty string stays an empty field, so a reader with [`NullValues = ["NULL"]`](reading.html#null-values) tells the
two apart.

`WriteField(object)` writes values whose type you don't know at compile time: strings and the types above as their
overloads do, other `IFormattable` values with the format provider, anything else with `ToString()`. Enums are written
by name:

```csharp
object?[] values = ["text", 42, 3.5, null, true, Status.OnHold, new Uri("https://example.com/a,b")];
foreach (var value in values) writer.WriteField(value);
writer.EndRecord();

// text,42,3.5,,True,OnHold,"https://example.com/a,b"
```

`WriteField<T>` takes any `IUtf8SpanFormattable` value on .NET (`Half`, `Int128`, `TimeSpan`, your own types) and any
`IFormattable` value on the other targets, with an optional format.

## Quoting

In RFC 4180 mode (the default), a field is enclosed in double quotes when it contains the separator, a quote, CR or
LF, and quotes inside it are doubled. Nothing else is quoted, not even leading or trailing spaces:

```csharp
writer.WriteRecord("plain", "a,b", "say \"hi\"", "two\nlines", " spaced ", "");
```

```csv
plain,"a,b","say ""hi""","two
lines", spaced ,
```

`Quoting = CsvQuoting.Always` quotes every field, numbers included, which some importers expect:

```csharp
var options = new CsvWriterOptions { Quoting = CsvQuoting.Always };
// "plain","a,b",""
// "42","1.5","True"
```

`Quoting = CsvQuoting.WhenNeededOrWhitespace` also quotes fields that start or end with a space or a tab, so that a
reader that [trims whitespace](reading.html#whitespace) keeps it:

```csharp
var options = new CsvWriterOptions { Quoting = CsvQuoting.WhenNeededOrWhitespace };
writer.WriteRecord(" a", "b ", "c d", "");

// " a","b ",c d,
```

To quote one field, pass the quoting with it:

```csharp
writer.WriteField("00123", CsvQuoting.Always);                   // "00123"
writer.WriteField("plain", CsvQuoting.WhenNeeded);               // plain
writer.WriteField(" padded", CsvQuoting.WhenNeededOrWhitespace); // " padded"
```

`ShouldQuote` decides for every field, from its text, the type of its value, its index and the index of its record.
This quotes text but not numbers, dates or booleans:

```csharp
var options = new CsvWriterOptions { ShouldQuote = (field, type, index, record) => type == typeof(string) };
writer.WriteHeader("Name", "Count");
writer.WriteField("Anna");
writer.WriteField(42);
writer.EndRecord();

// "Name","Count"
// "Anna",42
```

`type` is `string` for text (strings and character or UTF-8 spans) and the type of the value otherwise, `index` is the
field's position in its record and `record` is `RecordCount` (0 for the header). A field that needs quotes is quoted
whatever the callback returns, so the output stays valid. The callback gets the text of every field, which makes
writing slower. Per-field quoting and `ShouldQuote` apply to RFC 4180 mode only.

A record whose only field is empty is written as `""`, so that a reader does not take it for an empty line. A record
with no fields at all is an error.

## Separators

`Separator` replaces the comma with another ASCII character. Quoting then follows the same rules for that separator:

```csharp
var semicolons = new CsvWriterOptions { Separator = ';' };
// Name;Note
// a,b;"c;d"

var tabs = new CsvWriterOptions { Separator = '\t' };
// a,b<TAB>"c<TAB>d"
```

The separator can be any ASCII character except CR, LF and, in RFC 4180 mode, the quote.

## Line endings

Records end with CRLF, as RFC 4180 specifies. `NewLine` chooses `"\n"` or `"\r"` instead:

```csharp
var unix = new CsvWriterOptions { NewLine = "\n" };
// Id,Name\n1,Anna\n
```

Fields that contain CR or LF are still quoted (or escaped), so a reader splits records at the right places whatever
the line ending. In Escape mode with `"\n"`, a record whose last value ends with a CR ends with CRLF: a reader takes an
LF right after an escaped CR as part of the value. Other line endings are not allowed.

## Comments

With `CommentPrefix`, `WriteComment` writes comment lines, which a reader with the same
[`CommentPrefix`](reading.html#comments-and-empty-lines) skips:

```csharp
var options = new CsvWriterOptions { CommentPrefix = '#' };
using var writer = CsvWriter.ToFile("export.csv", options);
writer.WriteComment("Exported 2024-03-01\nsource: orders");
writer.WriteHeader("Id", "Name");
writer.WriteRecord("#12", "Anna");

// #Exported 2024-03-01
// #source: orders
// Id,Name
// "#12",Anna
```

- The text is written as it is, without quotes or escapes, and each of its lines becomes a comment line.
- A comment goes between records: in the middle of a record it throws `InvalidOperationException`.
- Comments are not records: `RecordCount` and the field count of the records leave them out.
- A record whose first field starts with the prefix would read as a comment, so the writer quotes that field (`"#12"`
  above) or, in Escape mode, escapes the prefix (`\#12`). FixedWidth mode can do neither and throws
  `InvalidOperationException` for such a record, which is discarded; NoEscape mode writes it as it is.

## Spreadsheet formulas

Spreadsheets such as Excel run a cell that starts with `=`, `+`, `-` or `@` as a formula, which an attacker can use to
run commands on the computer of whoever opens a CSV file (CSV injection). `Injection` protects text fields that start
with one of `InjectionCharacters` (`= @ + -`, tab and CR by default, as OWASP recommends):

| `Injection` | `=1+2` is written as |
|---|---|
| `None` (default) | `=1+2` |
| `Escape` | `"'=1+2"`: `InjectionEscape` (`'`) in front, and in RFC 4180 mode quoted, so Excel shows it as text |
| `Strip` | `1+2`: the injection characters at its start are removed |
| `Throw` | `InvalidOperationException`; the record is discarded and the writer can go on |

```csharp
var options = new CsvWriterOptions { Injection = CsvInjection.Escape };
writer.WriteField("=HYPERLINK(\"http://evil\")");
writer.WriteField("-5");                       // text
writer.WriteField(-5);                         // a number
writer.EndRecord();

// "'=HYPERLINK(""http://evil"")","'-5",-5
```

It applies to text: strings, character and UTF-8 spans, and values written with `ToString()`. Numbers, dates, times,
`Guid`s and booleans that the writer formats itself cannot be formulas, so a negative number stays a number. Reading
an escaped field back gives the text with the escape character.

## Field counts

RFC 4180 requires every record to have the same number of fields. With `ValidateFieldCount` (on by default), a record
whose field count differs from the first record's throws `InvalidOperationException`, and the bad record is discarded:
nothing of it is written, and the writer can go on with the next one.

```csharp
using var writer = CsvWriter.Create(text);
writer.WriteHeader("A", "B");
writer.WriteRecord("1", "2");
try
{
    writer.WriteRecord("3");
}
catch (InvalidOperationException e)
{
    // Record 3 has 1 fields but the first record has 2. RFC 4180 requires the same number of fields in every
    // record (set ValidateFieldCount = false to allow it).
}

writer.WriteRecord("4", "5");

// A,B
// 1,2
// 4,5
```

`ValidateFieldCount = false` allows records of any length.

## Modes

`Mode` chooses the output format. The [reader](reading.html#parsing-modes) has the same modes, so a file written in a
mode reads back in that mode. Records end with CRLF (or [`NewLine`](#line-endings)) in every mode.

| Mode | Values are | Example |
|---|---|---|
| `Rfc4180` (default) | Quoted when needed, with quotes doubled. | `"a,b","say ""hi"""` |
| `Escape` | Never quoted: the escape character goes before every separator, escape character, CR and LF. | `a\,b,say "hi"` |
| `NoEscape` | Written as they are. | `a b,say "hi"` |
| `FixedWidth` | Padded with spaces to the width of their column, without separators. | `a b  say "hi"` |

### RFC 4180

The default, described in [Quoting](#quoting). `Quoting` applies only to this mode.

### Escape

No quotes. `Escape` (`\` by default) is written before every separator, escape character, CR and LF in a value:

```csharp
var options = new CsvWriterOptions { Mode = CsvMode.Escape };
using var writer = CsvWriter.ToFile("export.csv", options);
writer.WriteHeader("Path", "Note");
writer.WriteRecord(@"C:\Temp\out", "a, b");
writer.WriteRecord("multi", "line\none");
writer.WriteRecord("quotes", "say \"hi\"");
```

```text
Path,Note
C:\\Temp\\out,a\, b
multi,line\
one
quotes,say "hi"
```

Another escape character or separator works the same way. The escape character must be ASCII, and not a space, a tab
or the separator:

```csharp
var options = new CsvWriterOptions { Mode = CsvMode.Escape, Escape = '^', Separator = '|' };
writer.WriteRecord("a|b", "c^d", @"e\f");     // a^|b|c^^d|e\f
```

A record whose only field is empty is an empty line, which a reader skips unless `SkipEmptyLines = false`.

### NoEscape

Values are written exactly as they are, with nothing quoted or escaped. Use it for data that cannot contain the
separator or a line break, such as tab-separated exports of identifiers and numbers:

```csharp
var options = new CsvWriterOptions { Mode = CsvMode.NoEscape, Separator = '\t' };
using var writer = CsvWriter.ToFile("export.tsv", options);
writer.WriteHeader("Name", "Comment");
writer.WriteRecord("Anna", "said \"hi\", twice");   // Anna<TAB>said "hi", twice
```

Nothing is checked: a value that contains the separator or a line break changes the fields or records a reader sees.
A record whose only field is empty is an empty line.

### FixedWidth

No separators: each value is padded on the right with spaces to the width of its column. `FieldWidths` gives the
widths, in characters:

```csharp
var options = new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = [6, 12, 10] };
using var writer = CsvWriter.ToFile("accounts.txt", options);
writer.WriteHeader("Id", "Name", "Balance");
writer.WriteField(1);
writer.WriteField("Anna");
writer.WriteField(1250.00m, "F2");
writer.EndRecord();
writer.WriteField(2);
writer.WriteField("Benedikt");
writer.WriteField(-3.75m);
writer.EndRecord();
```

```text
Id    Name        Balance
1     Anna        1250.00
2     Benedikt    -3.75
```

Every line is as wide as all the columns together: the last column is padded too. A value that does not fit is an
error rather than being cut, so no data is lost silently. These throw `InvalidOperationException` and discard the
record:

| Problem | Message |
|---|---|
| A value longer than its column | `Field 0 of record 1 is 4 characters long, but its column is 3 wide. The record is discarded.` |
| A value with a line break | `Field 1 of record 1 contains a line break, which FixedWidth mode cannot write. The record is discarded.` |
| More fields than columns | `Record 1 has more fields than the 2 columns of FieldWidths. The record is discarded.` |
| Fewer fields than columns | `Record 1 has 1 fields but FieldWidths has 2 columns (set ValidateFieldCount = false to allow fewer).` |

With `ValidateFieldCount = false`, a record may have fewer fields than there are columns: its line ends after the last
field written, which a reader in FixedWidth mode reads as empty fields.

When a record type holds the layout (`[CsvColumn(Index = n, Width = w)]`, see
[Fixed-width records](records.html#fixed-width-records)), `FieldWidths = CsvRecordMapper.GetFieldWidths<T>()` writes
the same layout that the type reads, and `WriteRecord<T>` writes its records in it.

## Cultures

`FormatProvider` formats numbers and dates in another culture. Dates without a format stay ISO 8601 in every culture,
so pass a format (such as `"d"`, the culture's short date) for local dates. A value whose text contains the separator
is quoted, so in cultures that write `1,5`, use another separator (or expect quotes):

```csharp
var options = new CsvWriterOptions { Separator = ';', FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
using var writer = CsvWriter.ToFile("preise.csv", options);
writer.WriteHeader("Artikel", "Preis", "Datum");
writer.WriteField("Tee");
writer.WriteField(1234.5m);
writer.WriteField(new DateTime(2024, 3, 1), "d");
writer.EndRecord();

// Artikel;Preis;Datum
// Tee;1234,5;01.03.2024
// (with the default comma separator, the price would be written "1234,5", in quotes)
```

## Encoding and byte order mark

Streams and files get UTF-8. `WriteByteOrderMark = true` starts the output with the UTF-8 byte order mark, which some
versions of Excel need to open a UTF-8 file with accents correctly:

```csharp
var excel = new CsvWriterOptions { WriteByteOrderMark = true };
using var writer = CsvWriter.ToFile("for-excel.csv", excel);
```

A `TextWriter` gets UTF-16 text and encodes it as it was configured to; `WriteByteOrderMark` does not apply to it.

## Flushing and async writing

Fields are written into a buffer (`BufferSize`, 64 KiB by default). The writer only writes to its destination when a
record ends with the buffer mostly full, on `Flush`, and on `Dispose`. `Flush` writes everything buffered, including
the fields of an unfinished record, and flushes the destination.

The async methods write the buffer asynchronously, and every other call stays in memory, so an async writer never
blocks on I/O:

```csharp
await using var writer = CsvWriter.Create(stream);
writer.WriteHeader("Id", "Name");
foreach (var item in items)
{
    writer.WriteField(item.Id);
    writer.WriteField(item.Name);
    await writer.EndRecordAsync(cancellationToken);           // writes when the buffer is mostly full
}

await writer.WriteRecordAsync(["Total", "2"], cancellationToken);
await writer.FlushAsync(cancellationToken);                   // optional: DisposeAsync flushes too
```

This makes the writer suitable for streaming a response in ASP.NET Core, which does not allow synchronous writes to
the response body:

```csharp
app.MapGet("/orders.csv", async (HttpContext context, OrderStore store) =>
{
    context.Response.ContentType = "text/csv; charset=utf-8";
    await using var writer = CsvWriter.Create(context.Response.Body);
    writer.WriteHeader("Id", "Unit Price", "Quantity");
    await foreach (var order in store.GetOrdersAsync(context.RequestAborted))
    {
        writer.WriteField(order.Id);
        writer.WriteField(order.Price);
        writer.WriteField(order.Quantity);
        await writer.EndRecordAsync(context.RequestAborted);
    }
});
```

## Writing from several threads

`ParallelWriteRecords` writes records as `WriteRecords` does, but formats them on several threads: the output is the
same, byte for byte, in the same order. It suits large outputs, and records with many numbers and dates, which take
longer to format than to write:

```csharp
using var writer = CsvWriter.ToFile("orders.csv");
writer.ParallelWriteRecords(orders);                              // the header, then a record per order

// At most 4 threads (the default, -1, is one per processor):
writer.ParallelWriteRecords(orders, maxDegreeOfParallelism: 4);

// Asynchronously, from an IEnumerable<T> or an IAsyncEnumerable<T>:
await writer.ParallelWriteRecordsAsync(db.Orders.AsAsyncEnumerable(), cancellationToken: cancellationToken);
```

It writes what `WriteRecords` writes: [`[CsvRecord]` types](#writing-csvrecord-types), records with a record writer of
your own (`ParallelWriteRecords(records, recordWriter)`) and [dynamic records](records.html#writing-dynamic-records).
`ParallelWrite` calls a function of yours for each item instead, which writes the records of the item: as many as it
needs, or none.

```csharp
writer.WriteHeader("Id", "Customer", "Total");
writer.ParallelWrite(orders, (w, order) =>
{
    w.WriteField(order.Id);                       // w is the worker's writer, not writer
    w.WriteField(order.Customer);
    w.WriteField(order.Price * order.Quantity);
    w.EndRecord();
    if (order.Lines.Count > 1)
    {
        w.WriteRecord("", "Subtotal", order.Subtotal.ToString(CultureInfo.InvariantCulture));
    }
});
```

Your thread enumerates the items and writes the output to the destination, while workers format batches of about
128 KB, each with a writer of its own.

- The items are enumerated on your thread, one at a time, so the sequence need not be thread-safe: an Entity Framework
  query or a data reader is fine. The workers read an array or a `List<T>` in place, so don't change it until the call
  returns.
- Record writers, [converters](records.html#converters-csvconverter), `ShouldQuote` and the function of `ParallelWrite`
  run on several threads at once, so anything shared that they change must be thread-safe. Generated record writers
  are.
- The function writes whole records on the writer it gets: a record that it leaves unfinished throws
  `InvalidOperationException`. Don't flush or dispose that writer, or start a parallel write with it. Its
  `RecordCount`, and the record index that `ShouldQuote` gets, count as if every item wrote one record.
- The header, and the first record when it fixes the field count or the columns of dynamic records, are written on
  your thread before the workers start, and so is a sequence that fits in one batch. `ParallelWrite` writes no header:
  write it first.
- An exception, from the sequence, a record or the function, is thrown after the records before it are written, and
  none after it are: the output is what `WriteRecords` would have written.
- What the writer has buffered is written to the destination before the first batch, and each batch as soon as the
  ones before it are. The destination is not flushed.
- Don't use the writer for anything else until the call returns.

Writing the 50,000 records of the [benchmark](benchmarks.html#writing-on-several-threads) data sets on an AMD Ryzen 9
9950X3D (16 cores, 32 threads), with one worker per processor:

| Records | `WriteRecords` | `ParallelWriteRecords` | To a file: `WriteRecords` | `ParallelWriteRecords` |
|---|---:|---:|---:|---:|
| PackageAssets: 25 text fields, 16 MB | 8.23 ms | **1.32 ms** (6.2x) | 12.0 ms | **5.59 ms** (2.2x) |
| Floats: 3 text and 25 `float` fields, 14.7 MB | 23.6 ms | **1.62 ms** (14.6x) | 28.2 ms | **5.13 ms** (5.5x) |
| Orders: dates, a `decimal`, a `Guid`, numbers and text, 9.5 MB | 6.50 ms | **0.85 ms** (7.7x) | 8.78 ms | **3.54 ms** (2.5x) |

Times are BenchmarkDotNet means. To a file, writing the file becomes the limit, at 2.7 to 2.9 GB/s, so records that are
cheap to format gain least there; to memory, a network stream or a pipe, numbers and dates gain most.

To write records that other threads produce one at a time, send them to one thread that writes them (a `Channel<T>`
suits this), or take a lock around each whole record, from its first `WriteField` to `EndRecord`. Otherwise a writer
is not thread-safe: use it from one thread at a time, and finish each async call before starting the next. Nothing
checks this; fields from two threads end up mixed in the output. The [options](#options) and the
[record writers](records.html#writing-records) do not change, so they can be shared between writers on any thread.

## Copying and transforming CSV

A reader's `GetSpan` and the writer's UTF-8 `WriteField` copy fields without creating strings, so converting between
formats is cheap. This turns a German semicolon-separated file into an RFC 4180 file in the invariant culture:

```csharp
// Artikel;Preis;Notiz
// Tee;1,5;"lose; 100 g"
// Kaffee;2,25;"sagt ""hallo"""
var german = new CsvReaderOptions { Separator = ';', FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
using var reader = CsvReader.FromFile("preise.csv", german);
using var writer = CsvWriter.ToFile("prices.csv");
writer.WriteHeader("Item", "Price", "Note");
while (reader.Read())
{
    writer.WriteField(reader.GetSpan(0));          // copied as UTF-8
    writer.WriteField(reader.Parse<decimal>(1));   // parsed as de-DE, written as invariant
    writer.WriteField(reader.GetSpan(2));          // re-quoted for the new separator
    writer.EndRecord();
}

// Item,Price,Note
// Tee,1.5,lose; 100 g
// Kaffee,2.25,"sagt ""hallo"""
```

## Errors

| Exception | When |
|---|---|
| `InvalidOperationException` | A record with a different field count, a record with no fields, a [fixed-width](#fixedwidth) value that does not fit, a text field that starts like a formula with `Injection = Throw`, or in FixedWidth mode a record that starts with the comment prefix. The record is discarded; the writer can go on. Also `WriteComment` without `CommentPrefix` or in the middle of a record, and a type whose records [cannot be written](records.html#writing-records). |
| `ArgumentException` | Invalid options, when the writer is created: a separator that is not ASCII or is CR, LF or (in RFC 4180 mode) the quote, an unusable escape character, missing or non-positive `FieldWidths` in FixedWidth mode, a `NewLine` other than CRLF, LF and CR, an unusable `CommentPrefix` or injection character. Also a `null` record in `WriteRecords`. |
| `FormatException` | A format string that is not valid for the value. |
| `ObjectDisposedException` | Writing after `Dispose`. |

## Options

The options are an immutable record, like the reader's: create one with an object initializer or `with`, and share it
between writers, on any thread.

| Option | Default | Explained in |
|---|---|---|
| `Mode` | `Rfc4180` | [Modes](#modes) |
| `Separator` | `,` | [Separators](#separators) |
| `HasHeader` | `true` | [Writing [CsvRecord] types](#writing-csvrecord-types) |
| `Quoting` | `WhenNeeded` | [Quoting](#quoting) |
| `ShouldQuote` | `null` | [Quoting](#quoting) |
| `NullValue` | `null` (an empty field) | [Booleans, nulls and other values](#booleans-nulls-and-other-values) |
| `TrueValue`, `FalseValue` | `"True"`, `"False"` | [Booleans, nulls and other values](#booleans-nulls-and-other-values) |
| `NewLine` | `"\r\n"` | [Line endings](#line-endings) |
| `CommentPrefix` | `null` (no comments) | [Comments](#comments) |
| `Injection` | `None` | [Spreadsheet formulas](#spreadsheet-formulas) |
| `InjectionCharacters` | `"=@+-\t\r"` | [Spreadsheet formulas](#spreadsheet-formulas) |
| `InjectionEscape` | `'` | [Spreadsheet formulas](#spreadsheet-formulas) |
| `Escape` | `\` | [Escape](#escape) |
| `FieldWidths` | `null` (required in FixedWidth mode) | [FixedWidth](#fixedwidth) |
| `ValidateFieldCount` | `true` | [Field counts](#field-counts) |
| `FormatProvider` | `CultureInfo.InvariantCulture` | [Cultures](#cultures) |
| `WriteByteOrderMark` | `false` | [Encoding and byte order mark](#encoding-and-byte-order-mark) |
| `BufferSize` | 64 KiB (at least 256 bytes) | [Flushing and async writing](#flushing-and-async-writing) |
