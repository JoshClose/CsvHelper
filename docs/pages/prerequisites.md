---
title: Prerequisites
icon: graduation-cap
description: The .NET basics that CsvHelper assumes you know, and that most questions turn out to be about: disposing, files, streams, encodings, cultures, lazy sequences, async, nullable values, spans and exceptions.
---

# Prerequisites

CsvHelper assumes you know the .NET basics on this page. Most questions about CsvHelper turn out to be about one of
them: a file that comes out empty, a stream that reads nothing, accents that turn into `Ã©`, prices that come out 100
times too large. When something doesn't behave as you expect, check the section it touches first.
[Microsoft's .NET documentation](https://learn.microsoft.com/dotnet/) goes deeper into each topic.

## Using and Dispose

Classes that hold resources the garbage collector doesn't manage, such as open files, implement `IDisposable`. Call
`Dispose` when you are done with them, which a `using` statement does for you, even when an exception is thrown.
`CsvReader` and `CsvWriter` are disposable: they hold an open file or stream, and buffers rented from a pool.

```csharp
// A using declaration: disposed at the end of the enclosing block, here the method.
using var reader = CsvReader.FromFile("orders.csv");

// A using statement: disposed at its closing brace.
using (var writer = CsvWriter.ToFile("totals.csv"))
{
    writer.WriteRecord("Total", "42");
}
```

Disposing matters most for writers. A writer collects records in a buffer and writes them to its destination when the
buffer is full, on `Flush` and on `Dispose`, so a writer that is never disposed leaves the file empty or without its
last records:

```csharp
var writer = CsvWriter.ToFile("orders.csv");
writer.WriteRecords(orders);
// No Dispose: orders.csv is empty or incomplete, and stays open until the process exits.
```

A using declaration lasts until the end of its block, which is often later than you think. Here the writer is still
open when the file is read back, so the reader gets an incomplete file, or an `IOException` because the file is in use:

```csharp
using var writer = CsvWriter.ToFile("orders.csv");
writer.WriteRecords(orders);

using var reader = CsvReader.FromFile("orders.csv");      // the writer has not been disposed yet
```

A using statement ends it first:

```csharp
using (var writer = CsvWriter.ToFile("orders.csv"))
{
    writer.WriteRecords(orders);
}   // disposed: the file is complete and closed

using var reader = CsvReader.FromFile("orders.csv");
```

Disposing a reader or writer disposes the stream, file, `TextReader` or `TextWriter` it was created with, unless it
was created with `leaveOpen: true` (see [Streams](#streams)). In async code, use `await using` (see
[Async and await](#async-and-await)). A class that keeps a reader or writer in a field owns it: make the class
`IDisposable` too, and dispose the reader or writer in its `Dispose`.

## Files and paths

### Relative paths

A relative path such as `"orders.csv"` is relative to the current directory of the process, not to your project or
source file. That depends on how the app was started: Visual Studio starts a console app in its output folder
(`bin\Debug\net10.0`), `dotnet run` in the folder you ran it from, and Windows starts services in
`C:\Windows\System32`. The `FileNotFoundException` says where it looked:

```
System.IO.FileNotFoundException: Could not find file 'C:\Projects\Import\bin\Debug\net10.0\orders.csv'.
```

For a file that ships with your app, build the path from the app's folder, and have the build copy the file there
(in Visual Studio, the file's Copy to Output Directory property):

```csharp
var path = Path.Combine(AppContext.BaseDirectory, "data", "orders.csv");
using var reader = CsvReader.FromFile(path);
```

```xml
<ItemGroup>
  <None Update="data\orders.csv" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### Backslashes

In a C# string, `\` starts an escape sequence, so a Windows path in an ordinary string may not be the path you wrote.
Use a verbatim string (`@"..."`), doubled backslashes, forward slashes (which Windows accepts too) or `Path.Combine`:

```csharp
var wrong = "C:\temp\new.csv";                      // \t is a tab and \n a line feed: not the path you wrote
var path = @"C:\temp\new.csv";                      // verbatim: a backslash is a backslash
var same = "C:\\temp\\new.csv";
var built = Path.Combine(@"C:\temp", "new.csv");
```

### Files in use

A file that another program has open for writing cannot be opened by `CsvReader.FromFile` or `CsvWriter.ToFile`.
Excel locks the files it has open, so close the file there first:

```
System.IO.IOException: The process cannot access the file 'C:\data\orders.csv' because it is being used by another process.
```

To read a file while another program writes it, such as a log, open the stream yourself and let the other program
keep writing:

```csharp
var stream = new FileStream("app-log.csv", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var reader = CsvReader.Create(stream);
```

### Appending

`CsvWriter.ToFile` creates the file, or overwrites it. To add records to the end of an existing file, open it in append
mode yourself, and leave the header out when the file already has one:

```csharp
var options = new CsvWriterOptions { HasHeader = !File.Exists("orders.csv") };
var stream = new FileStream("orders.csv", FileMode.Append, FileAccess.Write);
using var writer = CsvWriter.Create(stream, options);
writer.WriteRecords(newOrders);
```

The file must end with a line break, or the first new record continues its last line. Files that CsvHelper wrote do.

## Streams

A `Stream` is a sequence of bytes with a position: reading and writing start at the position and move it forward.
Files, memory (`MemoryStream`), network connections and HTTP request and response bodies are all streams.

### The position

`CsvReader.Create(stream)` reads from the stream's current position. After you write to a `MemoryStream`, its position
is at the end, so a reader created then reads nothing. Here is the round trip done right:

```csharp
var stream = new MemoryStream();
using (var writer = CsvWriter.Create(stream, leaveOpen: true))
{
    writer.WriteRecords(orders);
}   // disposing writes the buffer into the stream

stream.Position = 0;                                    // back to the start
using var reader = CsvReader.Create(stream);
List<Order> copies = reader.GetRecords<Order>().ToList();
```

Each of these three lines is needed, and each is a common question:

- **Dispose (or flush) the writer before using the stream.** Until then, the last records are in the writer's buffer,
  not in the stream.
- **`leaveOpen: true`.** Without it, disposing the writer disposes the stream too, and reading it afterwards throws
  `ObjectDisposedException`.
- **`Position = 0`.** Writing left the position at the end, where there is nothing to read.

`MemoryStream.ToArray()` returns everything written, whatever the position, and `CsvReader.Create(byte[])` reads it.

### Streams that are read once

Network streams and HTTP request and response bodies can't go back: their `CanSeek` is `false`, and setting `Position`
throws `NotSupportedException`. Read them once, front to back. To read one twice, copy it into memory first:

```csharp
var buffer = new MemoryStream();
await request.Body.CopyToAsync(buffer, cancellationToken);
byte[] bytes = buffer.ToArray();

using var check = CsvReader.Create(bytes);
using var import = CsvReader.Create(bytes);
```

## Text and encodings

Files hold bytes, and a .NET `string` holds UTF-16 characters. An encoding says how characters are stored as bytes, and
reading a file with the wrong one garbles every character outside ASCII. CsvHelper reads UTF-8 unless a byte order mark
says otherwise, and writes UTF-8.

| You see | What happened |
|---|---|
| `Jos�` | A file in another encoding, such as Windows-1252, was read as UTF-8. Set `Encoding` in the [reader options](reading.html#encodings). |
| `JosÃ©` | A UTF-8 file was read as Windows-1252, usually by Excel. Write a [byte order mark](writing.html#encoding-and-byte-order-mark) (`WriteByteOrderMark = true`). |
| `Jos?` in the console | The console can't show the character, but the string is fine. Set `Console.OutputEncoding = Encoding.UTF8`. |

Without a byte order mark, the bytes don't say what encoding they are in, so ask whoever produces the file. Files saved
by older Windows programs are often Windows-1252, which .NET (Core) only has once the code pages provider is
registered; .NET Framework has it built in:

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);    // once, at startup
var options = new CsvReaderOptions { Encoding = Encoding.GetEncoding(1252) };
using var reader = CsvReader.FromFile("legacy.csv", options);
```

Without the provider, `Encoding.GetEncoding(1252)` throws `NotSupportedException: No data is available for encoding
1252.`

## Cultures

Numbers and dates are written differently around the world: `1,234.5` in the US is `1.234,5` in Germany, and
`03/01/2024` is March 1 in the US and January 3 in the UK. .NET's `Parse` and `ToString` methods use the current
culture, the regional settings of the machine they run on, unless you pass one. So the same code can work on your
computer and give wrong values on a server:

```csharp
// On a machine set to German (de-DE):
decimal price = decimal.Parse("1.50");      // 150: '.' separates thousands in German
string text = 1.5m.ToString();              // "1,5"
```

CsvHelper parses and formats with the invariant culture by default, on every machine: `.` for decimals and ISO 8601
dates (`2024-03-01`). For files from another culture, set `FormatProvider` in the options of the
[reader](reading.html#numbers-dates-and-cultures) or the [writer](writing.html#cultures). In your own code, prefer
`reader.Parse<decimal>(i)` to `decimal.Parse(reader.GetString(i))`, and pass `CultureInfo.InvariantCulture` when you
call `Parse` or `ToString` for values that go to or come from a file.

Excel saves CSV with the list separator of the regional settings, which is `;` in most countries that write decimals
with a comma, so a file from a German Excel looks like `Tee;1,5`. The reader detects the separator; set
`FormatProvider` for the numbers.

## Lazy sequences

`GetRecords<T>` returns an `IEnumerable<T>` that reads a record each time the enumeration moves forward, not a list of
records already read. It never holds the whole file in memory, so it reads files of any size, but it has three
consequences.

**The reader must still be open while you enumerate.** A method that returns the sequence and disposes the reader
returns something that can no longer be read:

```csharp
public static IEnumerable<Order> LoadOrders(string path)
{
    using var reader = CsvReader.FromFile(path);

    return reader.GetRecords<Order>();
}   // the reader is disposed here, before the caller reads anything

foreach (var order in LoadOrders("orders.csv")) { }    // ObjectDisposedException: Cannot access a disposed object.
```

Read the records into a list before the reader is disposed, or make the method an iterator:

```csharp
public static List<Order> LoadOrders(string path)
{
    using var reader = CsvReader.FromFile(path);

    return reader.GetRecords<Order>().ToList();
}

public static IEnumerable<Order> StreamOrders(string path)
{
    using var reader = CsvReader.FromFile(path);
    foreach (var order in reader.GetRecords<Order>())
    {
        yield return order;
    }
}
```

`yield return` makes a method an iterator: its body runs as the caller enumerates, so the reader stays open until the
caller's `foreach` ends, or leaves the loop early.

**The sequence can be enumerated once.** Each enumeration goes on from where the reader is, so anything that enumerates
the sequence uses up records: `Count()` reads all of them, and `Any()` reads the first one, which a `foreach` after it
then misses:

```csharp
var records = reader.GetRecords<Order>();
if (records.Any())                          // reads the first record
{
    foreach (var order in records) { }      // starts at the second
}
```

The debugger counts too: expanding Results View in a watch window enumerates the sequence. When you need the records
more than once, call `ToList()` once and use the list.

**`ToList()` reads the whole file into memory.** For a large file, handle each record in the `foreach` instead of
collecting them first. LINQ operators such as `Where` and `Select` are lazy too, so
`reader.GetRecords<Order>().Where(o => o.Price > 100)` still reads one record at a time.

All of this applies to `GetRecordsAsync<T>` as well, an `IAsyncEnumerable<T>` that you read with `await foreach`.

## Async and await

Async methods free their thread while they wait for I/O, which matters in servers and UI apps. The reader and writer
have async methods for reading and writing, such as `ReadAsync`, `ReadHeaderAsync`, `GetRecordsAsync`,
`WriteRecordsAsync`, `EndRecordAsync`, `FlushAsync` and `DisposeAsync`. Use them all the way through:

- **`await` async calls.** `.Result`, `.Wait()` and `.GetAwaiter().GetResult()` block a thread until the task
  finishes, which wastes the thread in a server and can deadlock a UI app.
- **Return `Task`, not `void`.** Nothing can await an `async void` method or catch its exceptions; only event handlers
  should be `async void`.
- **Dispose with `await using`.** A plain `using` disposes synchronously, and disposing a writer writes what is left in
  its buffer.

```csharp
public static async Task ExportAsync(Stream stream, IAsyncEnumerable<Order> orders, CancellationToken cancellationToken)
{
    await using var writer = CsvWriter.Create(stream);
    await writer.WriteRecordsAsync(orders, cancellationToken);
}
```

ASP.NET Core does not allow synchronous I/O on request and response bodies. A synchronous call that has to read or
write the body, such as `Read`, `WriteRecords`, `Flush` or the `Dispose` of a plain `using`, throws:

```
System.InvalidOperationException: Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true instead.
```

Use the async methods and `await using` there, and call `ReadHeaderAsync` before `Headers` or `GetFieldIndex`, which
would otherwise read the header synchronously. [Writing](writing.html#flushing-and-async-writing) has an example that
streams a CSV response.

## Nullable values

A value type such as `int`, `decimal` or `DateTime` always has a value, so it can't hold an empty field: reading an
empty field as one throws. Declare the member nullable (`int?` is short for `Nullable<int>`) to read an empty field as
`null`, or give it a value for empty fields with [`[CsvDefault]`](records.html#empty-fields-csvdefault):

```csharp
[CsvRecord]
public partial class OrderLine
{
    public int? Quantity { get; set; }       // "" reads as null
    [CsvDefault(0)]
    public int Discount { get; set; }        // "" reads as 0
    public int Count { get; set; }           // "" throws: an int can't be empty
}
```

Without records it is the same: `reader.Parse<int>(i)` throws `FormatException` for an empty field, and
`reader[i].ParseOrNull<int>()` returns `null` (see [Empty and missing fields](reading.html#empty-and-missing-fields)).

Nullable reference types (`string?`) are different. A `string` can always be `null`; the `?` only tells the compiler,
and readers of your code, that `null` is expected. An empty field reads as `""` into a `string` member, and as `null`
into a `string?` member only when `NullValues` or `[CsvNullValues]` says it should (see
[Null values](records.html#null-values-csvnullvalues)).

## Spans

`ReadOnlySpan<byte>` is a view of memory that belongs to something else, not a copy. `GetSpan` returns one that points
into the reader's buffer, which the next `Read` reuses, so the span is only valid until then. `CsvRow` and `CsvField`
are views of the buffer too. All three are `ref struct`s, which can only live on the stack: they can't be stored in
fields, lists or arrays, captured by lambdas, or kept across an `await`.

The bytes of a span are UTF-8, not characters. Compare them with UTF-8 literals (`"EUR"u8`), and create a string for
anything you keep:

```csharp
var currency = reader.GetFieldIndex("Currency");
while (reader.Read())
{
    ReadOnlySpan<byte> code = reader.GetSpan(currency);     // a view of the buffer: valid until the next Read
    if (code.SequenceEqual("EUR"u8))                        // compared as UTF-8, without creating a string
    {
        euroOrders.Add(reader.GetString(0));                // a new string: yours to keep
    }
}
```

Spans are there for speed. When you don't need it, ignore them: `GetString`, `Parse<T>` and
[records](records.html) give you ordinary values. [Spans and raw text](reading.html#spans-and-raw-text) has more.

## Source generators and partial types

`[CsvRecord]` types are read and written by code that a source generator writes while your project compiles, rather
than by reflection while it runs. That is where the rules come from:

- **The type must be `partial`**, and so must any type that contains it, because the generator adds code to it from
  a file of its own. Otherwise the build fails with CSVH001.
- **Mapping mistakes show up when you build**, as errors and warnings CSVH001 to CSVH012 in the build output and the
  Error List. The [diagnostics table](records.html#compile-time-diagnostics) says what each one means.
- **The generator must run in the project that declares the type.** The package includes it, but a project reference
  to CsvHelper needs it added (see [Setup and requirements](records.html#setup-and-requirements)). Without it, no
  mapper is generated, and `GetRecords<T>` throws `InvalidOperationException`.

To see the generated code, open Dependencies, Analyzers, CsvHelper.Generators in Visual Studio's Solution Explorer, or
add `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` to the project, which writes it under `obj`.

## Exceptions

An exception says what went wrong and where. Before you search or ask:

- **Read the whole message.** CsvHelper's messages name the record, the field, the column and the member:
  `Cannot read Order.Price from field 1 'Unit Price' of record 3: The input string 'abc' was not in a correct format.`
- **Read the `InnerException`.** An exception that wraps another, such as the `CsvFormatException` of a record that
  can't be read, keeps the original error there.
- **Read the stack trace.** Its first line in your own code is where to start looking.
- **Log `exception.ToString()`, not `exception.Message`.** It has the type, the message, the inner exceptions and the
  stack traces.
- **Don't catch exceptions you can't handle.** `catch (Exception) { }` hides the error and leaves you with wrong or
  missing data instead.

The exception tables of [Reading](reading.html#errors), [Records](records.html#errors) and
[Writing](writing.html#errors) say what each exception means.

## Asking for help

If this page doesn't cover it, ask on [Stack Overflow](https://stackoverflow.com/questions/tagged/csvhelper) with the
`csvhelper` tag, or in [GitHub Discussions](https://github.com/JoshClose/CsvHelper/discussions?discussions_q=category%3AQ%26A).
A question gets answered quickly when it has:

- **A small program that shows the problem and runs as it is.** Put the CSV in the code with `CsvReader.FromText`, so
  nobody needs your file:

  ```csharp
  using CsvHelper;

  var csv = """
      Id,Unit Price
      A-1,9.99
      A-2,
      """;

  using var reader = CsvReader.FromText(csv);
  foreach (Order order in reader.GetRecords<Order>())
  {
      Console.WriteLine($"{order.Id}: {order.Price}");
  }

  [CsvRecord]
  public partial class Order
  {
      public string Id { get; set; } = "";
      [CsvColumn("Unit Price")]
      public decimal Price { get; set; }
  }
  ```

- **What you expected, and what happened instead:** the output, or the whole `exception.ToString()`.
- **The CsvHelper version and the target framework** (`net10.0`, `net48`).

Bugs and feature requests go to [GitHub issues](https://github.com/JoshClose/CsvHelper/issues).
