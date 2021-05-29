# Migrating from version 21 to 22

## ParserMode

Name change to `CsvMode`.

```cs
// v21
ParserMode.RFC4180

//v22
CsvMode.RFC4180
```

## ShouldQuote

```cs
// v21
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldQuote = (field, context) => true,
};

// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldQuote = (field, context, row) => true,
};
```

## EnumConverter

`EnumConverter` was changed to case sensitive by default.

If you want Enums to ignore case, you need to set a type converter option.

```cs
Map(m => m.Property).TypeConverterOption.EnumIgnoreCase();
```

## IParserConfiguration

- Added `ProcessFieldBufferSizse`.

Any class that implements `IParserConfiguration` will need these changes applied to it.

## IWriterConfiguration

- Added `Mode`.

Any class that implements `IWriterConfiguration` will need these changes applied to it.
