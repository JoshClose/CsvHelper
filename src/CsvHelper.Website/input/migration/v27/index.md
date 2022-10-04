# Migrating from version 26 to 27

## CsvConfiguration.WhiteSpaceChars

- Removed `\t` from the array of default characters.

If you are expecting `\t` to be trimmed, you will need to add this to the 
whitespace characters.

```cs
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	WhiteSpaceChars = new[] { ' ', '\t' },
};
```

## IParserConfiguration

- Added property `bool DetectDelimiter { get; }`.
- Added property `string[] DetectDelimiterValues { get; }`.
 
Any class that implements `IParserConfiguration` will need these changes
applied to it.

## IWriter

- Added method `Task WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)`.
This does not apply to projects that reference the `net45` version of CsvHelper.

Any class that implements `IWriter` will need these changes applied to it.

## IParser

- Added property `string Delimiter { get; }`.

Any class that implements `IParser` will need these changes applied to it.
