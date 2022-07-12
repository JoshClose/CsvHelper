# Migrating from version 27 to 28

## ShouldSkipRecordArgs

- `string[] ShouldSkipRecordArgs.Record` changed to `IRecordRow ShouldSkipRecordArgs.Row`.

```cs
// 27
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldSkipRecord = args => args.Record.Length < 10;
};

// 28

var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldSkipRecord = args => args.Row.Parser.Length < 10;
};
```

## ConfigurationFunctions.ShouldSkipRecord

- Removed `ConfigurationFunctions.ShouldSkipRecord`.

`null` can be used in place of this now, and is the default.

```cs
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldSkipRecord = null
};
```


## IParserConfiguration.Validate

Implement the `Validate` method.
