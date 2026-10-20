# Migrating from CsvHelper 22 to 23

CsvHelper 23 changes every callback to take one argument: a struct with a property for each value the callback used
to get as a parameter. New values can then be added without breaking your code, and `BadDataFound` already gets one,
the bad field. The properties of `CsvConfiguration` have setters again (they were `init` from 20 to 22) so that
Visual Basic can set them, and the constructor that took every setting as an optional argument is removed. The target
frameworks don't change.

## Configuration

The constructor with every setting as an optional argument, added in 21.0.5 for Visual Basic, is removed. The
properties have setters now, so set them in an object initializer, or one at a time.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture, delimiter: ";", hasHeaderRecord: false);
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    HasHeaderRecord = false,
};
```

Code that sets the properties in an object initializer doesn't change. Set them before you create the reader or
writer: the reader, the parser and the writer copy most settings when they are created.

## Callbacks

Each callback gets an `args` struct named after it (`BadDataFoundArgs`, `PrepareHeaderForMatchArgs` and so on), with
a property for each former parameter.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = (header, fieldIndex) => header.ToLower(),
    ShouldSkipRecord = record => record.All(string.IsNullOrWhiteSpace),
    MissingFieldFound = (headerNames, index, context) => Console.WriteLine($"Missing field {index}"),
    BadDataFound = context => Console.WriteLine($"Bad data: {context.Parser.RawRecord}"),
    ReadingExceptionOccurred = exception =>
    {
        Console.WriteLine(exception.Message);
        return false;
    },
    GetDynamicPropertyName = (context, fieldIndex) => $"Field{fieldIndex}",
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.ToLower(),
    ShouldSkipRecord = args => args.Record.All(string.IsNullOrWhiteSpace),
    MissingFieldFound = args => Console.WriteLine($"Missing field {args.Index}"),
    BadDataFound = args => Console.WriteLine($"Bad data: {args.Context.Parser.RawRecord}"),
    ReadingExceptionOccurred = args =>
    {
        Console.WriteLine(args.Exception.Message);
        return false;
    },
    GetDynamicPropertyName = args => $"Field{args.FieldIndex}",
};
```

| Callback | CsvHelper 22 parameters | CsvHelper 23 `args` properties |
|---|---|---|
| `BadDataFound` | `context` | `Field` (new: the bad field), `Context` |
| `GetConstructor` | `classType` | `ClassType` |
| `GetDynamicPropertyName` | `context`, `fieldIndex` | `Context`, `FieldIndex` |
| `HeaderValidated` | `invalidHeaders`, `context` | `InvalidHeaders`, `Context` |
| `MissingFieldFound` | `headerNames`, `index`, `context` | `HeaderNames`, `Index`, `Context` |
| `PrepareHeaderForMatch` | `header`, `fieldIndex` | `Header`, `FieldIndex` |
| `ReadingExceptionOccurred` | `exception` | `Exception` |
| `ReferenceHeaderPrefix` | `memberType`, `memberName` | `MemberType`, `MemberName` |
| `ShouldQuote` | `field`, `fieldType`, `row` | `Field`, `FieldType`, `Row` |
| `ShouldSkipRecord` | `record` | `Record` |
| `ShouldUseConstructorParameters` | `parameterType` | `ParameterType` |
| `Convert` when reading | `row` | `Row` (`ConvertFromStringArgs`) |
| `Convert` when writing | `value` | `Value` (`ConvertToStringArgs<TClass>`) |
| `Validate` | `field` | `Field` |

Setting a callback to `null`, such as `MissingFieldFound = null`, doesn't change.

### Mapping

`Convert` and `Validate` on a member map take the args too.

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
        Map(m => m.Id).Validate(field => !string.IsNullOrEmpty(field));
        Map(m => m.Total)
            .Convert(row => row.GetField<decimal>("Net") + row.GetField<decimal>("Tax"))
            .Convert(order => order.Total.ToString("0.00"));
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
        Map(m => m.Id).Validate(args => !string.IsNullOrEmpty(args.Field));
        Map(m => m.Total)
            .Convert(args => args.Row.GetField<decimal>("Net") + args.Row.GetField<decimal>("Tax"))
            .Convert(args => args.Value.Total.ToString("0.00"));
    }
}
```

### Calling a callback

The default functions in `ConfigurationFunctions` take the args too, so a callback that calls the default passes its
`args` on.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // Quote what CsvHelper quotes, and numbers with leading zeros.
    ShouldQuote = (field, fieldType, row) => ConfigurationFunctions.ShouldQuote(field, fieldType, row) || (field != null && field.StartsWith("0")),
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // Quote what CsvHelper quotes, and numbers with leading zeros.
    ShouldQuote = args => ConfigurationFunctions.ShouldQuote(args) || (args.Field != null && args.Field.StartsWith("0")),
};
```

To call a callback yourself, in a test for example, create its args with their constructor.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = (header, fieldIndex) => header.Trim().ToLower(),
};
var name = config.PrepareHeaderForMatch(" Name ", 0);
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.Trim().ToLower(),
};
var name = config.PrepareHeaderForMatch(new PrepareHeaderForMatchArgs(" Name ", 0));
```

## Behavior changes

- **`Flush` flushes the `TextWriter`.** `CsvWriter.Flush()` and `FlushAsync()` write the writer's buffer to the
  `TextWriter` and then flush the `TextWriter` too. `NextRecord()` and `NextRecordAsync()` write the buffer to the
  `TextWriter` without flushing it, as before, but no longer call `Flush()` and `FlushAsync()`: if your subclass
  overrides them to run after each record, override the new protected `FlushBuffer()` and `FlushBufferAsync()`
  instead. A subclass can also write text to the buffer with the new protected
  `WriteToBuffer(string)`.
