# Migrating from CsvHelper 23 to 24

CsvHelper 24 gives `WriteConvertedField` the type of the field, so that `ShouldQuote` gets the real type of each field
when you write records, and adds a `CancellationToken` to the async methods that read and write records. Only code
that calls or overrides `WriteConvertedField`, overrides those async methods, or implements `IReader`, `IWriter` or
`IWriterRow`, has to change. The
token is an optional parameter, so calls compile as they are, but a library built against 23 that calls these methods
must be rebuilt. The target frameworks don't change.

## WriteConvertedField

`WriteConvertedField` takes the type the field had before it was converted to a string, on `CsvWriter` and on
`IWriterRow`. The writer passes it on to `ShouldQuote` as `FieldType`.

```csharp before
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
var total = 12.5m;
csv.WriteConvertedField(total.ToString("0.00", CultureInfo.InvariantCulture));
csv.NextRecord();
```

```csharp after
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
var total = 12.5m;
csv.WriteConvertedField(total.ToString("0.00", CultureInfo.InvariantCulture), typeof(decimal));
csv.NextRecord();
```

A subclass that overrides it overrides the new signature, which the writer calls when it writes records and from
`WriteField<T>`.

```csharp before
public class UpperCaseCsvWriter : CsvWriter
{
    public UpperCaseCsvWriter(TextWriter writer, CsvConfiguration configuration) : base(writer, configuration) { }

    public override void WriteConvertedField(string field)
    {
        base.WriteConvertedField(field?.ToUpperInvariant());
    }
}
```

```csharp after
public class UpperCaseCsvWriter : CsvWriter
{
    public UpperCaseCsvWriter(TextWriter writer, CsvConfiguration configuration) : base(writer, configuration) { }

    public override void WriteConvertedField(string field, Type fieldType)
    {
        base.WriteConvertedField(field?.ToUpperInvariant(), fieldType);
    }
}
```

## Cancellation

`GetRecordsAsync`, `EnumerateRecordsAsync` and `WriteRecordsAsync` take an optional `CancellationToken`. It is
checked before each record, and throws an `OperationCanceledException` when it is canceled.

```csharp
public class Order
{
    public int Id { get; set; }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
await foreach (var order in csv.GetRecordsAsync<Order>(cancellationToken))
{
    // ...
}
```

`csv.GetRecordsAsync<Order>().WithCancellation(cancellationToken)` passes the token too, and
`await csv.WriteRecordsAsync(orders, cancellationToken)` stops writing when it is canceled. If you implement `IReader`
or `IWriter`, or override these methods, add the parameter to them.

## Behavior changes

- **`ShouldQuote` gets each member's type when you write records.** In 23, `WriteRecord` and `WriteRecords` passed
  `string` as `FieldType` for every member of a class. In 24, it is the member's type (a member mapped with `Convert`
  still gets `string`), so a `ShouldQuote` that tests `args.FieldType` quotes differently.
