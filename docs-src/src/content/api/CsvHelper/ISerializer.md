# ISerializer Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to serialize data into a CSV file.

```cs
public interface ISerializer : IDisposable
```

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the writing context.

## Methods
&nbsp; | &nbsp;
- | -
Write(String[]) | Writes a record to the CSV file.
WriteAsync(String[]) | Writes a record to the CSV file.
WriteLine() | Writes a new line to the CSV file.
WriteLineAsync() | Writes a new line to the CSV file.
