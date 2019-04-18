# IFactory Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to create CsvHelper classes.

```cs
public interface IFactory 
```

## Methods
&nbsp; | &nbsp;
- | -
CreateClassMapBuilder&lt;T&gt;() | Provides a fluent interface for dynamically creating ``CsvHelper.Configuration.ClassMap<TClass>`` s
CreateParser(TextReader, Configuration) | Creates an ``CsvHelper.IParser`` .
CreateParser(TextReader) | Creates an ``CsvHelper.IParser`` .
CreateReader(TextReader, Configuration) | Creates an ``CsvHelper.IReader`` .
CreateReader(TextReader) | Creates an ``CsvHelper.IReader`` .
CreateReader(IParser) | Creates an ``CsvHelper.IReader`` .
CreateWriter(TextWriter, Configuration) | Creates an ``CsvHelper.IWriter`` .
CreateWriter(TextWriter) | Creates an ``CsvHelper.IWriter`` .
