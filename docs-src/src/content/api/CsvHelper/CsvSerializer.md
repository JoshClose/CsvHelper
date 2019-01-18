# CsvSerializer Class

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to serialize data into a CSV file.

```cs
public class CsvSerializer : ISerializer, IDisposable
```

Inheritance Object -> CsvSerializer

## Constructors
&nbsp; | &nbsp;
- | -
CsvSerializer(TextWriter) | Creates a new serializer using the given ``System.IO.TextWriter`` .
CsvSerializer(TextWriter, Boolean) | Creates a new serializer using the given ``System.IO.TextWriter`` .
CsvSerializer(TextWriter, Configuration) | Creates a new serializer using the given ``System.IO.TextWriter`` and ``CsvHelper.Configuration.Configuration`` .
CsvSerializer(TextWriter, Configuration, Boolean) | Creates a new serializer using the given ``System.IO.TextWriter`` and ``CsvHelper.Configuration.Configuration`` .

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the writing context.

## Methods
&nbsp; | &nbsp;
- | -
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
Write(String[]) | Writes a record to the CSV file.
WriteAsync(String[]) | Writes a record to the CSV file.
WriteLine() | Writes a new line to the CSV file.
WriteLineAsync() | Writes a new line to the CSV file.
