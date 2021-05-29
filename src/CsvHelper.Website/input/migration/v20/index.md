# Migrating from version 19 to 20

## ConvertUsing

`ConvertUsing` was renamed to `Convert`.

```cs
// v19
Map(m => m.Property).ConvertUsing(row => row.GetField<int>(0) + row.GetField<int>(1));

// v20
Map(m => m.Property).Convert(row => row.GetField<int>(0) + row.GetField<int>(1));
```

## CsvConfiguration

All properties changed from `get; set;` to `get; init;`.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.Delimiter = ";";

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	Delimiter = ";",
}
```

`CsvConfiguration` changed from a `class` to a `record`.

```cs
// v19
class MyConfig : CsvConfiguration {}

// v20
record MyConfig : CsvConfiguration {}
```

### ShouldQuote

`ShouldQuote` now takes in `IWriterRow` instead of `CsvContext`.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldQuote = (field, row) => true,
};

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldQuote = (field, context) => true,
};
```

### NewLine

Changed from `enum NewLines` to `char?`.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	NewLine = NewLines.LF,
};

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	NewLine = '\n',
};
```

### NewLineString

This was removed. Any code referencing this should be deleted.

### RegisterClassMap

This moved to `CsvContext`.

```cs
// v19
csv.Configuration.RegisterClassMap<MyMap>();

// v20
csv.Context.RegisterClassMap<MyMap>();
```

### UnregisterClassMap

This moved to `CsvContext`.

```cs
// v19
csv.Configuration.UnregisterClassMap<MyMap>();

// v20
csv.Context.UnregisterClassMap<MyMap>();
```

### AutoMap

This moved to `CsvContext`.

```cs
// v19
csv.Configuration.AutoMap<MyType>();

// v20
csv.Context.AutoMap<MyType>();
```

## IParserConfiguration

All setters removed.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.Delimiter = ";";

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	Delimiter = ";",
};
```

- Added `bool CacheFields`.
- Added `bool LeaveOpen`.
- Added `char? NewLine`.
- Added `ParserMode Mode`.
- Added `char[] WhiteSpaceChars`.
- Removed `bool IgnoreQuotes`.

Any classes that implement `IParserConfiguration` will need these changes.

## IReaderConfiguration

All setters removed.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.Delimiter = ";";

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	Delimiter = ";",
};
```

- Removed `TypeConverterOptionsCache`.
- Removed `TypeConverterCache`.
- Removed `Maps`.
- Removed `RegisterClassMap`.
- Removed `UnregisterClassMap`.
- Removed `AutoMap`.

Any classes that implement `IReaderConfiguration` will need these changes.

## ISerializerConfiguration

This interface was removed and it's properties were added to `IWriteConfiguration`.

```cs
// v19
class MyConfig : ISerializerConfiguration {}

// v20
class MyConfig : IWriterConfiguration {}
```

## IWriterConfiguration

All setters removed.

```cs
// v19
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.Delimiter = ";";

// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	Delimiter = ";",
};
```

- Removed `QuoteString`.
- Removed `TypeConverterCache`.
- Removed `MemberTypes`.
- Removed `Maps`.
- Removed `RegisterClassMap`.
- Removed `UnregisterClassMap`.
- Removed `AutoMap`.

Any classes that implement `IWriterConfiguration` will need these changes.

## MemberMap

`ConvertUsing` renamed to `Convert`.

```cs
// v19
Map(m => m.Property).ConvertUsing(row => row.Get(0));
Map(m => m.Property).ConvertUsing(value => value?.ToString() ?? string.Empty);

// v20
Map(m => m.Property).Convert(row => row.Get(0));
Map(m => m.Property).Convert(value => value?.ToString() ?? string.Empty);
```

## CsvParser

`string[] Read()` changed to `bool Read()`.

```cs
// v19
string[] record;
while ((record = parser.Read()) != null)
{
}

// v20
while (parser.Read())
{
	// Only get fields you need.
	var field1 = parser[0];
	var field2 = parser[1];

	// Get all fields.
	var record = parser.Record;
}
```

Constructor paramter `IFieldReader fieldReader` removed from all constructors.

```cs
// v19
var parser = new CsvParser(fieldReader);

// v20
var parser = new CsvParser();
```

## CsvSerializer

Removed. Functionality moved into `CsvWriter`.

## IFieldReader

Removed. Functionality moved into `CsvParser`.

## IParser

- Added `long ByteCount`.
- Added `long CharCount`.
- Added `int Count`.
- Added `string this[int index]`.
- Added `string[] Record`.
- Added `string RawRecord`.
- Added `int Row`.
- Added `int RawRow`.
- Changed `string[] Read` to `bool Read`.
- Changed `Task<string[]> ReadAsync` to `Task<bool> ReadAsync`.

Any classes that implement `IParser` will need these changes.

## IReader

- Removed `ICsvParser Parser`.

Any classes that implement `IReader` will need these changes.

## IReaderRow

- Added `int ColumnCount`.
- Added `int CurrentIndex`.
- Added `string[] HeaderRecord`.
- Added `IParser Parser`.

Any classes that implement `IReaderRow` will need these changes.

## ISerializer

Removed. Functionality moved into `IWriter`.

## IWriterRow

- Added `string[] HeaderRecord`.
- Added `int Row`.
- Added `int Index`.

## RecordBuilder

Removed. Functionality moved into `CsvWriter`.

## Caches

`enum Caches` was removed. Modifying internal caches is not supported anymore.
Any code referencing this should be removed.

## ReadingContext/WritingContext

`ReadingContext` and `WritingContext` was merged into a single `CsvContext`.
Anywhere either of these was used should change to `CsvContext`.

## Func/Action

Any place a `Func` or `Action` was used now has a dedicated `delegate`.
This should only affect classes that are inheriting `ClassMap`
or `CsvConfiguration`.

## CsvFieldReader

Class removed. Code was wrapped into `CsvParser`.
