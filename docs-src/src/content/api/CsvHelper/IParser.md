# IParser Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used the parse a CSV file.

```cs
public interface IParser : IDisposable
```

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the reading context.
FieldReader | Gets the ``CsvHelper.IParser.FieldReader`` .

## Methods
&nbsp; | &nbsp;
- | -
Read() | Reads a record from the CSV file.
ReadAsync() | Reads a record from the CSV file asynchronously.
