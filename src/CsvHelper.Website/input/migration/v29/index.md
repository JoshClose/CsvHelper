# Migrating from version 28 to 29

## CsvConfiguration.SanitizeForInjection

`bool CsvConfiguration.SanitizeInjection` changed to `InjectionOptions CsvConfiguration.InjectionOptions`.

```cs
// 28
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    SanitizeForInjection = true,
}

// 29
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    InjectionOptions = InjectionOptions.Escape,
}
```

## IWriterConfiguration.SanitizeForInjection

`bool IWriterConfiguration.SanitizeInjection` changed to `InjectionOptions IWriterConfiguration.InjectionOptions`.
Any class that implements `IWriterConfiguration` will need this changed.

## IParserConfiguration.GetDelimiter

`GetDelimiter IParserConfiguration.GetDelimiter` was added.
Any class that implements `IParserConfiguration` will need to add this.
