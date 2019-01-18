# Factory Class

Namespace: [CsvHelper](/api/CsvHelper)

Creates CsvHelper classes.

```cs
public class Factory : IFactory
```

Inheritance Object -> Factory

## Methods
&nbsp; | &nbsp;
- | -
CreateClassMapBuilder&lt;T&gt;() | Access point for fluent interface to dynamically build a ``CsvHelper.Configuration.ClassMap<TClass>``
CreateParser(TextReader, Configuration) | Creates an ``CsvHelper.IParser`` .
CreateParser(TextReader) | Creates an ``CsvHelper.IParser`` .
CreateReader(TextReader, Configuration) | Creates an ``CsvHelper.IReader`` .
CreateReader(TextReader) | Creates an ``CsvHelper.IReader`` .
CreateReader(IParser) | Creates an ``CsvHelper.IReader`` .
CreateWriter(TextWriter, Configuration) | Creates an ``CsvHelper.IWriter`` .
CreateWriter(TextWriter) | Creates an ``CsvHelper.IWriter`` .
