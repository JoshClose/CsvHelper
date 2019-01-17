# CsvParser Class

Namespace: [CsvHelper](/api/CsvHelper)

Parses a CSV file.

```cs
public class CsvParser : IParser, IDisposable
```

Inheritance Object -> CsvParser

## Constructors
&nbsp; | &nbsp;
- | -
CsvParser(TextReader) | Creates a new parser using the given ``System.IO.TextReader`` .
CsvParser(IFieldReader) | Creates a new parser using the given ``CsvHelper.CsvParser.FieldReader`` .
CsvParser(TextReader, Boolean) | Creates a new parser using the given ``System.IO.TextReader`` .
CsvParser(TextReader, Configuration) | Creates a new parser using the given ``System.IO.TextReader`` and ``CsvHelper.CsvParser.Configuration`` .
CsvParser(TextReader, Configuration, Boolean) | Creates a new parser using the given ``System.IO.TextReader`` and ``CsvHelper.CsvParser.Configuration`` .

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the reading context.
FieldReader | Gets the ``CsvHelper.CsvParser.FieldReader`` .

## Methods
&nbsp; | &nbsp;
- | -
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
Read() | Reads a record from the CSV file.
ReadAsync() | Reads a record from the CSV file asynchronously.
