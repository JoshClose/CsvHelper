---
title: Unit testing
icon: vial-circle-check
description: Test code that reads or writes CSV with in-memory readers and writers instead of mocks, and where to put an interface when you need one.
---

# Unit testing

`CsvReader` and `CsvWriter` are sealed classes without interfaces or virtual members, so every call is direct and the
JIT can inline the hot paths. Mocking libraries such as Moq, NSubstitute and FakeItEasy cannot fake them. Tests don't
need them to: a reader over a string and a writer over a `StringWriter` are the real reader and writer, need no files,
and run in microseconds. The test then checks the CSV your code actually reads and writes, quoting and all.

The examples on this page use xUnit and this record type:

```csharp
[CsvRecord]
public partial class Order
{
    public string Id { get; set; } = "";
    [CsvColumn("Unit Price")]
    public decimal Price { get; set; }
    public int? Quantity { get; set; }
}
```

## Testing code that reads

Let the code under test take a `CsvReader`, and create one from a string in the test:

```csharp
public static class OrderImporter
{
    public static decimal Total(CsvReader reader)
    {
        var price = reader.GetFieldIndex("Unit Price");
        var quantity = reader.GetFieldIndex("Quantity");
        var total = 0m;
        while (reader.Read())
        {
            total += reader.GetDecimal(price) * reader.GetInt32(quantity);
        }

        return total;
    }
}

[Fact]
public void TotalAddsEveryOrder()
{
    using var reader = CsvReader.FromText("""
        Id,Unit Price,Quantity
        A-1,9.99,3
        A-2,24.50,1
        """);

    Assert.Equal(54.47m, OrderImporter.Total(reader));
}
```

Code that takes a `Stream`, such as an upload or a request body, gets a `MemoryStream`. Bytes already in memory can
also go to `CsvReader.Create(byte[])` directly.

```csharp
public static List<Order> Import(Stream upload)
{
    using var reader = CsvReader.Create(upload);

    return reader.GetRecords<Order>().ToList();
}

[Fact]
public void ImportReadsTheUpload()
{
    var upload = new MemoryStream(Encoding.UTF8.GetBytes("Id,Unit Price,Quantity\nA-1,9.99,3\n"));

    var orders = OrderImporter.Import(upload);

    Assert.Equal("A-1", Assert.Single(orders).Id);
}
```

[`[CsvRecord]`](records.html) types read the same way. Their mappers are generated in the project that declares them,
so a test project needs the source generator only for record types it declares itself:

```csharp
[Fact]
public void ReadsOrders()
{
    using var reader = CsvReader.FromText("Id,Unit Price,Quantity\nA-1,9.99,3\nA-2,24.50,\n");

    var orders = reader.GetRecords<Order>().ToList();

    Assert.Equal(2, orders.Count);
    Assert.Equal(9.99m, orders[0].Price);
    Assert.Null(orders[1].Quantity);
}
```

Numbers and dates are read with the invariant culture by default (`FormatProvider`), so these tests pass on machines
with any culture.

### Bad input

To test how your code handles bad files, give it the bad text with the options it uses. `MalformedData = Throw`
raises a `CsvFormatException` at the record with bad quoting, and `OnMalformedRecord` reports each one (see
[Messy files](messy-files.html#throwing-or-skipping)):

```csharp
[Fact]
public void BadQuotingThrows()
{
    var options = new CsvReaderOptions { MalformedData = CsvMalformedDataHandling.Throw };
    using var reader = CsvReader.FromText("Id,Unit Price,Quantity\nA-1,9.99,3\n\"A-2\"x,24.50,1\n", options);

    var e = Assert.Throws<CsvFormatException>(() => OrderImporter.Total(reader));
    Assert.Equal(3, e.RecordNumber);
}

[Fact]
public void BadQuotingIsReported()
{
    var reported = new List<CsvMalformedRecord>();
    var options = new CsvReaderOptions { OnMalformedRecord = reported.Add };
    using var reader = CsvReader.FromText("Id,Unit Price,Quantity\nA-1,9.99,3\n\"A-2\"x,24.50,1\n", options);

    OrderImporter.Total(reader);

    Assert.Equal(CsvMalformedReason.TextAfterClosingQuote, Assert.Single(reported).Reason);
}
```

A field of a record type that does not parse throws a `CsvFormatException` that names the member
([Records](records.html#errors)):

```csharp
[Fact]
public void BadPriceNamesTheMember()
{
    using var reader = CsvReader.FromText("Id,Unit Price,Quantity\nA-1,abc,3\n");

    var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Order>().ToList());
    Assert.Equal("Order.Price", e.Member);
}
```

## Testing code that writes

Let the code under test take a `CsvWriter`, give it one over a `StringWriter`, and compare the text:

```csharp
public static class OrderExporter
{
    public static void Export(CsvWriter writer, IEnumerable<Order> orders)
    {
        writer.WriteHeader("Id", "Unit Price");
        foreach (var order in orders)
        {
            writer.WriteField(order.Id);
            writer.WriteField(order.Price);
            writer.EndRecord();
        }
    }
}

[Fact]
public void ExportQuotesIds()
{
    var text = new StringWriter();
    using (var writer = CsvWriter.Create(text))
    {
        OrderExporter.Export(writer, [new Order { Id = "A,1", Price = 9.99m }]);
    }

    Assert.Equal("Id,Unit Price\r\n\"A,1\",9.99\r\n", text.ToString());
}
```

- **Dispose the writer (or call `Flush`) before you read the output.** The writer collects records in a buffer, and
  until then the `StringWriter` is empty ([Flushing](writing.html#flushing-and-async-writing)).
- **Records end with CRLF.** That is the RFC 4180 default (`NewLine`), so write expected strings with `\r\n`. Don't
  compare the output with a raw string literal: those take the line endings of the source file, which Git can change
  from one machine to the next. If the line endings don't matter to the test, set `NewLine = "\n"` in the options you
  create the writer with.
- Values are formatted with the invariant culture by default, as for reading.

For code that writes to a `Stream`, use a `MemoryStream` and `leaveOpen: true`. There is no byte order mark unless you
set `WriteByteOrderMark`, so the bytes decode to the same text:

```csharp
var stream = new MemoryStream();
using (var writer = CsvWriter.Create(stream, leaveOpen: true))
{
    OrderExporter.Export(writer, [new Order { Id = "A-1", Price = 9.99m }]);
}

Assert.Equal("Id,Unit Price\r\nA-1,9.99\r\n", Encoding.UTF8.GetString(stream.ToArray()));
```

To test that what you write can be read back, read the output with `CsvReader.FromText`:

```csharp
[Fact]
public void OrdersReadBack()
{
    Order[] orders = [new() { Id = "A-1", Price = 9.99m, Quantity = 3 }, new() { Id = "A-2", Price = 24.50m }];
    var text = new StringWriter();
    using (var writer = CsvWriter.Create(text))
    {
        writer.WriteRecords(orders);
    }

    using var reader = CsvReader.FromText(text.ToString());
    var read = reader.GetRecords<Order>().ToList();

    Assert.Equal(orders.Select(o => (o.Id, o.Price, o.Quantity)), read.Select(o => (o.Id, o.Price, o.Quantity)));
}
```

## Code that opens files

Code that calls `CsvReader.FromFile` or `CsvWriter.ToFile` itself is easier to test when it takes the reader, writer or
stream instead, and leaves opening the file to its caller. When it has to take a path, write the input to a temporary
file in the test:

```csharp
public static decimal TotalOfFile(string path)
{
    using var reader = CsvReader.FromFile(path);

    return Total(reader);
}

[Fact]
public void ImportsFile()
{
    var path = Path.GetTempFileName();
    try
    {
        File.WriteAllText(path, "Id,Unit Price,Quantity\nA-1,9.99,3\n");

        Assert.Equal(29.97m, OrderImporter.TotalOfFile(path));
    }
    finally
    {
        File.Delete(path);
    }
}
```

## When you need a mock

When the code under test is about something else, and CSV is only how its input arrives, put an interface in front of
the reading, in terms of your own types, and mock that:

```csharp
public interface IOrderSource
{
    IEnumerable<Order> ReadOrders();
}

public sealed class CsvOrderSource(string path) : IOrderSource
{
    public IEnumerable<Order> ReadOrders()
    {
        using var reader = CsvReader.FromFile(path);
        foreach (var order in reader.GetRecords<Order>())
        {
            yield return order;
        }
    }
}

public sealed class Invoice(IOrderSource orders)
{
    public decimal Total() => orders.ReadOrders().Sum(o => o.Price * (o.Quantity ?? 0));
}

[Fact]
public void InvoiceAddsOrders()
{
    var source = new Mock<IOrderSource>();
    source.Setup(s => s.ReadOrders()).Returns([new Order { Id = "A-1", Price = 9.99m, Quantity = 3 }]);

    Assert.Equal(29.97m, new Invoice(source.Object).Total());
}
```

`CsvOrderSource` itself is then tested with a file, as above.

An interface that copies `CsvReader` member by member doesn't work as well. `GetSpan` and `GetRawSpan` return
`ReadOnlySpan<byte>`, a ref struct, and mocking libraries cannot set up members that return one: their setup methods
take the return type as a type argument or in an expression tree, and a ref struct can be neither. And a test that sets
up `GetString(0)`, `GetString(1)` and `Read()` is longer and more brittle than the CSV text it replaces.

Two other places take ordinary .NET types:

- **`IDataReader`**: code that takes a `DbDataReader` or `IDataReader` (bulk imports, `DataTable.Load`) gets a
  [`CsvDataReader`](reading.html#datatable-and-idatareader) in production. In tests it can get one over
  `CsvReader.FromText`, or any other data reader, such as `DataTable.CreateDataReader()`.
- **Converters**: an [`ICsvConverter<T>`](records.html#converters-csvconverter) takes the reader or writer, so test it
  with real ones. For the `PercentConverter` of [Records](records.html#converters-csvconverter):

```csharp
[Fact]
public void PercentConverterReadsAndWrites()
{
    var converter = new PercentConverter();
    using var reader = CsvReader.FromText("12.5%\n", new CsvReaderOptions { HasHeader = false });
    reader.Read();

    Assert.Equal(0.125, converter.Read(reader, 0));

    var text = new StringWriter();
    using (var writer = CsvWriter.Create(text))
    {
        converter.Write(writer, 0.125);
    }

    Assert.Equal("12.5%\r\n", text.ToString());     // disposing ends the record
}
```
