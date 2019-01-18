# CsvHelper

A .NET library for reading and writing CSV files. Extremely fast, flexible and easy to use. Supports reading and
writing of custom class objects.

## Getting Started

To install CsvHelper, run the following from the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console).

```
Install-Package CsvHelper
```

## Reading

### [reading] Reading all records

Reading is setup to be as simple as possible. If you have a class structure setup that mirrors the CSV file, you can
read the whole file into an enumerable.

```cs
var csv = new CsvReader( textReader );
var records = csv.GetRecords<MyClass>();
```

If you want to customize how the CSV file maps to your custom class objects, you can use [mapping](#mapping).

The `IEnumerable<T>` that is returned will yield results. This means that a result isn't returned until you actually
access it. This is handy because the whole file won't be loaded into memory, and the file will be read as you access
each row. If you do something like `Count()` on the `IEnumerable<T>`, the whole file needs to be read and you won't be
able to iterate over it again without starting over. If you need to iterate the records more than once (like using
`Count`), you can load everything into a list and the work on the data.

```cs
var csv = new CsvReader( textReader );
var records = csv.GetRecords<MyClass>().ToList();
```

### [reading] Reading records manually

You can loop the rows and read them manually.

```cs
var csv = new CsvReader( textReader );
while( csv.Read() )
{
	var record = csv.GetRecord<MyClass>();
}
```

### [reading] Reading individual fields

You can also read each field manually if you like.

```cs
var csv = new CsvReader( textReader );
while( csv.Read() )
{
	var intField = csv.GetField<int>( 0 );
	var stringField = csv.GetField<string>( 1 );
	var boolField = csv.GetField<bool>( "HeaderName" );
}
```

### [reading] TryGetField

If you might have inconsistencies with getting fields, you can use TryGetField.

```cs
var csv = new CsvReader( textReader );
while( csv.Read() )
{
	int intField;
	if( !csv.TryGetField( 0, out intField ) )
	{
		// Do something when it can't convert.
	}
}
```

### [reading] Parsing

You can also use the parser directly without using the reader. The parser will give back an array of strings for each row that is read, and null when it is finished.

```cs
var parser = new CsvParser( textReader );
while( true )
{
	var row = parser.Read();
	if( row == null )
	{
		break;
	}
}
```

## Writing

### [writing] Writing all records

Writing is setup to be as simple as possible. If you have a class structure setup that mirrors the CSV file, you can write the whole file from an enumerable.

```cs
var csv = new CsvWriter( textWriter );
csv.WriteRecords( records );
```

### [writing] Writing records manually

You can loop the objects and write them manually.

```cs
var csv = new CsvWriter( textWriter );
foreach( var item in list )
{
	csv.WriteRecord( item );
}
```

### [writing] Writing individual fields

You can also write each field manually if you like.

```cs
var csv = new CsvWriter( textWriter );
foreach( var item in list )
{
	csv.WriteField( "a" );
	csv.WriteField( 2 );
	csv.WriteField( true );
	csv.NextRecord();
}
```

## Mapping

### [mapping] Auto Mapping

If you don't supply a mapping file, auto mapping will be used. Auto mapping will map the properties in your class in the order they appear in. If the property is a custom class, it recursively maps the properties from that class in the order they appear in. If the auto mapper hits a circular reference, it will stop going down that reference branch.

### [mapping] Fluent Class Mapping

If your CSV file doesn't match up exactly with your custom class, you can use a fluent class map to set options for how the class maps to the file. You need to register your class map in configuration.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Id );
		Map( m = > m.Name );
	}
}
```

#### [mapping] Reference Map

Reference maps are used to map a property that is a custom class to it's own mapping that maps those properties to several CSV columns. You can nest reference maps as many layers deep as you like.

```cs
public sealed class PersonMap : CsvClassMap<Person>
{
	public PersonMap()
	{
		Map( m => m.Id );
		Map( m => m.Name );
		References<AddressMap>( m => m.Address );
	}
}

public sealed class AddressMap : CsvClassMap<Address>
{
	public AddressMap()
	{
		Map( m => m.Street );
		Map( m => m.City );
		Map( m => m.State );
		Map( m => m.Zip );
	}
}
```

#### [mapping] Index

When mapping by index you specify the index of the CSV column that that you want to use for that property.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Id ).Index( 0 );
		Map( m => m.Name ).Index( 1 );
	}
}
```

#### [mapping] Name

When mapping by name you specify the name of the CSV column that you want to use for that property. For this to work, the CSV file must have a header record. The name you specify must match with the name of the header record.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Id ).Name( "The Id Column" );
		Map( m => m.Name ).Name( "The Name Column" );
	}
}
```

#### [mapping] Name Index

Sometimes CSV files have multiple columns with the same name. When this happens, you can use NameIndex to specify which column name you are referring to. The NameIndex is NOT the column in the CSV file.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.FirstName ).Name( "Name" ).NameIndex( 0 );
		Map( m => m.LastName ).Name( "Name" ).NameIndex( 1 );
	}
}
```

#### [mapping] Ignore

Currently this is not used. Mapping will only map properties that you specify. In the future there will be an option to auto map within a class map, and any mappings explicitly stated will override the auto mapped ones. When this happens, ignore will be used to ignore a property that was auto mapped.

#### [mapping] Default

Default is used to set a default value you want to use if the field is empty.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public override void MyClassMap()
	{
		Map( m => m.Id ).Index( 0 ).Default( -1 );
		Map( m => m.Name ).Index( 1 ).Default( "Unknown" );
	}
}
```

#### [mapping] Type Converter

If the value of the CSV field can't automatically be converted into the type of the property, you can specify a custom CsvHelper.TypeConversion.ITypeConverter to be used to convert the value. See Type Conversion for documentation on how to create a custom type converter.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Id ).Index( 0 ).TypeConverter<MyIdConverter>();
	}
}
```

#### [mapping] Type Converter Options

The default built in converters will handle most cases of type conversion, but sometimes there are some small changes that you'd like to make, but don't want to create a whole new type converter that just parses an int (for example) differently. You can specify some type converter options to handle these cases.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Description ).Index( 0 ).TypeConverterOption( CultureInfo.InvariantCulture );
		Map( m => m.TimeStamp ).Index( 1 ).TypeConverterOption( DateTimeStyles.AdjustToUniversal );
		Map( m => m.Cost ).Index( 2 ).TypeConverterOption( NumberStyles.Currency );
		Map( m => m.CurrencyFormat ).Index( 3 ).TypeConverterOption( "C" );
		Map( m => m.BooleanValue ).Index( 4 ).TypeConverterOption( true, "sure" ).TypeConverterOption( false, "nope" );
	}
}
```

#### [mapping] Convert Using

When all else fails, you can use ConvertUsing. ConvertUsing allows you to write custom code inline to convert the row into a single property value.

```cs
public sealed class MyClassMap : CsvClassMap<MyClass>
{
	public MyClassMap()
	{
		// Constant value.
		Map( m => m.Constant ).ConvertUsing( row => 3 );
		// Aggregate of two rows.
		Map( m => m.Aggregate ).ConvertUsing( row => row.GetField<int>( 0 ) + row.GetField<int>( 1 ) );
		// Collection with a single value.
		Map( m => m.Names ).ConvertUsing( row => new List<string>{ row.GetField<string>( "Name" ) } );
		// Just about anything.
		Map( m => m.Anything ).ConvertUsing( row =>
		{
			// You can do anything you want in a block.
			// Just make sure to return the same type as the property.
		} );
	}
}
```

#### [mapping] Runtime Mapping

Maps can be created at runtime. In fact the auto map feature does everything dynamically. You can look at the following link for some inspiration: https://github.com/JoshClose/CsvHelper/blob/master/src/CsvHelper/Configuration/CsvClassMap.cs#L181

Another simple example is shown below:

```cs
var customerMap = new DefaultCsvClassMap();

// mapping holds the Property - csv column mapping 
foreach( string key in mapping.Keys )
{
	var columnName = mapping[key].ToString();
	
	if( !String.IsNullOrEmpty( columnName ) )
	{
		var propertyInfo = typeof( Customer ).GetType().GetProperty( key );
		var newMap = new CsvPropertyMap( propertyInfo );
		newMap.Name( columnName );
		customerMap.PropertyMaps.Add( newMap );
	}
}

csv.Configuration.RegisterClassMap(CustomerMap);
```

## Configuration

### [configuration] Allow Comments

This flag tells the parser whether comments are enabled.

```cs
// Default value
csv.Configuration.AllowComments = false;
```

### [configuration] Auto Map

This is used to generate a CsvClassMap from a type automatically without a fluent class mapping. This will try to map all properties including creating reference maps for properties that aren't native types. If the auto mapper detects a circular reference, it will not continue down that path.

```cs
var generatedMap = csv.Configuration.AutoMap<MyClass>();
```

### [configuration] Buffer Size

The size of the internal buffer that is used when reader or writing data to and from the TextReader and TextWriter. Depending on where your TextReader or TextWriter came from, you may want to make this value larger or smaller.

```cs
// Default value
csv.Configuration.BufferSize = 2048;
```

### [configuration] Comment

The value used to denote a line that is commented out.

```cs
// Default value
csv.Configuration.Comment = '#';
```

### [configuration] Count Bytes

A flag that will tell the parser to keep a count of all the bytes that have been read. You need to set Configuration.Encoding to the same encoding of the CSV file for this to work properly. This will also slow down parsing of the file.

```cs
// Default value
csv.Configuration.CountBytes = false;
```

### [configuration] Culture Info

The culture info used to read and write. This can be overridden per property in the mapping configuration.

```cs
// Default value
csv.Configuration.CultureInfo = CultureInfo.CurrentCulture;
```

### [configuration] Delimiter

The value used to separate the fields in a CSV row.

```cs
// Default value
csv.Configuration.Delimiter = ",";
```

### [configuration] Detect Column Count Changes

This flag will check for changes in the number of column from row to row. If true and a change is detected, a CsvBadDataException will be thrown.

```cs
// Default value
csv.Configuration.DetectColumnCountChanges = false;
```

### [configuration] Encoding

The encoding of the CSV file. This is only used when counting bytes. The underlying TextReader and TextWriter will have it's own encoding that is used.

```cs
// Default value
csv.Configuration.Encoding = Encoding.UTF8;
```

### [configuration] Has Header Record

This flag tells the reader/writer if there is a header row in the CSV file. The must be true for mapping properties by name to work (and there must be a header row).

```cs
// Default value
csv.Configuration.HasHeaderRecord = true;
```

### [configuration] Ignore Header White Space

This flag tells the reader to ignore white space in the headers when matching the columns to the properties by name.

```cs
// Default value
csv.Configuration.IgnoreHeaderWhiteSpace = false;
```

### [configuration] Ignore Private Accessor

A flag that tells the reader and writer to ignore private accessors when reading and writing. By default you can't read from a private getter or write to a private setter. Turn this on will allow that. Properties that can't be read from or written to are silently ignored.

```cs
// Default value
csv.Configuration.IgnorePrivateAccessor = false;
```

### [configuration] Ignore Reading Exceptions

A flag that tells the reader to swallow any exceptions that occur while reading and to continue on. Exceptions that occur in the parser will not be ignored. Parser exceptions mean the file is bad in some way, and the parser isn't able to recover.

```cs
// Default value
csv.Configuration.IgnoreReadingExceptions = false;
```

### [configuration] Ignore Quotes

A flag that tells the parser to ignore quotes as an escape character and treat it like any other character.

```cs
// Default value
csv.Configuration.IgnoreQuotes = false;
```

### [configuration] Is Header Case Sensitive

This flag sets whether matching CSV header names will be case sensitive.

```cs
// Default value
csv.Configuration.IsHeaderCaseSensitive = true;
```

### [configuration] Maps

You are able to access the registered class maps.

```cs
var myMap = csv.Configuration.Maps[typeof( MyClass )];
```

### [configuration] Property Binding Flags

PropertyBindingFlags are the flags used to find the properties on the custom class.

```cs
// Default value
csv.Configuration.PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance;
```

### [configuration] Quote

The value used to escape fields that contain a delimiter, quote, or line ending.

```cs
// Default value
csv.Configuration.Quote = '"';
```

### [configuration] Quote All Fields

A flag that tells the writer whether all fields written should have quotes around them; regardless if the field contains anything that should be escaped. Both QuoteAllFields and QuoteNoFields cannot be true at the same time. Setting one to true will set the other to false.

```cs
// Default value
csv.Configuration.QuoteAllFields = false;
```

### [configuration] Quote No Fields

A flag that tell the writer whether all fields written should not have quotes around them; regardless if the field contains anything that should be escaped. Both QuoteAllFields and QuoteNoFields cannot be true at the same time. Setting one to true will set the other to false.

```cs
// Default value
csv.Configuration.QuoteNoFields = false;
```

### [configuration] Reading Exception Callback

If you have Configuration.IgnoreReaderExceptions on and you want to know that the exceptions have occurred and possibly do something with them, you can use this.

```cs
csv.Configuration.ReadingExceptionCallback = ( ex, row ) =>
{
	// Log the exception and current row information.
};
```

### [configuration] Register Class Map

When using fluent class mapping, you need to register class maps for them to be used. You can register multiple class maps to be used.

```cs
csv.Configuration.RegisterClassMap<MyClassMap>();
csv.Configuration.RegisterClassMap<AnotherClassMap>();
```

### [configuration] Skip Empty Records

A flag to let the reader know if a record should be skipped when reading if it's empty. A record is considered empty if all fields are empty.

```cs
// Default value
csv.Configuration.SkipEmptyRecords = false;
```

### [configuration] Trim Fields

This flag tells the reader to trim whitespace from the beginning and ending of the field value when reading.

```cs
// Default value
csv.Configuration.TrimFields = false;
```

### [configuration] Trim Headers

This flag tells the reader to ignore white space from the beginning and ending of the headers when matching the columns to the properties by name.

```cs
// Default value
csv.Configuration.TrimHeaders = false;
```

### [configuration] Unregister Class Map

You can unregister a class map if needed.

```cs
// Unregister single map.
csv.Configuration.UnregisterClassMap<MyClassMap>();
// Unregister all class maps.
csv.Configuration.UnregisterClassMap();
```

### [configuration] Will Throw On Missing Field

This flag indicates if an exception should be thrown if reading and an expected field is missing. This is useful if you want to know if there is an issue with the CSV file.

```cs
// Default value
csv.Configuration.WillThrowOnMissingField = true;
```

## Type Conversion

Type converters are the way CsvHelper converts strings into .NET types, and .NET types into strings. The CsvHelper type converter ecosystem stays close to the .NET

## Miscellaneous

### [misc] Culture Specifics

#### [misc] Delimiter

Even though a culture has a specific list separator denoted by `CultureInfo.TextInfo.ListSeparator`, CSV files use a , as a separator per RFC 4180. If you have a file that uses a different delimiter, you can change the `Configuration.Delimiter`.

### [misc] FAQ

#### I got an exception. How do I tell what line the exception was on?

There is a lot of information held in `Exception.Data["CsvHelper"]`.

ex:
```txt
Row: '3' (1 based)
Type: 'CsvHelper.Tests.CsvReaderTests+TestBoolean'
Field Index: '0' (0 based)
Field Name: 'BoolColumn'
Field Value: 'two'
```

#### How can I use a DataReader or DataTable with CsvHelper?

Writing to a CSV using a DataReader:

```cs
var hasHeaderBeenWritten = false;
while( dataReader.Read() )
{
	if( !hasHeaderBeenWritten )
	{
		for( var i = 0; i < dataReader.FieldCount; i++ )
		{
			csv.WriteField( dataReader.GetName( i ) );
		}
		csv.NextRecord();
		hasHeaderBeenWritten = true;
	}

	for( var i = 0; i < dataReader.FieldCount; i++ )
	{
		csv.WriteField( dataReader[i] );
	}
	csv.NextRecord();
}
```

Writing to a CSV using a DataTable:

```cs
using( var dt = new DataTable() )
{
	dt.Load( dataReader );
	foreach( DataColumn column in dt.Columns )
	{
		csv.WriteField( column.ColumnName );
	}
	csv.NextRecord();

	foreach( DataRow row in dt.Rows )
	{
		for( var i = 0; i < dt.Columns.Count; i++ )
		{
			csv.WriteField( row[i] );
		}
		csv.NextRecord();
	}
}
```

Reading from a CSV into a DataTable:

```cs
while( csv.Read() )
{
	var row = dt.NewRow();
	foreach( DataColumn column in dt.Columns )
	{
		row[column.ColumnName] = csv.GetField( column.DataType, column.ColumnName );
	}
	dt.Rows.Add( row );
}
```

### [misc] Change Log

#### 2.12

##### Features

- Added Windows Phone 8.1 support to the PCL assembly.
- Added ability to set a prefix for reference maps. i.e. `Prefix( string prefix = null)`
- Added callback to use to determine if a record should be skipped when reading.
- Excel leading zeros number formatting. This allows you to read and write numbers that will preserve the zeros on the front. i.e. `="0001"`
- Use default value when a field is null because of a missing field in the row.
- Added `TrimFields` to CsvWriter.
- ability to specify constructor arguments when referencing another map within a mapping.
- Added `Names` property on `CsvPropertyNameCollection` to get raw list of property names.
- Added raw file line number to parser.
- Mapping methods on `CsvClassMap<T>` are now public to more easily allow mapping during runtime.
- Added `DateTimeOffset` converter.

##### Bug Fixes

- Fixed exception that was occuring when fields were empty and `UseExcelLeadingZerosFormatForNumerics = true`.
- Excel compatibility fix. If a field starts with a quote but never ends and the end of the file is reached, the field would be null. The field will now contain everything.
- Don't get static properties when automapping.
- Made all exceptions thrown contain Exception.Data["CsvHelper"].
- Fixed missing support writing the double quotes for inner quotes on a quoted field. This used to be there and was removed at some point. A unit test is now in place so this doesn't happen again.

#### 2.11.1.1

##### Bug Fixes

- Fixed issue with writing an array of records.

#### 2.11

##### Features

- Allow preservation of numeric strings for Excel.

##### Bug Fixes

- Fixed writing issue with anonymous objects outputting wrong headers.

#### 2.10

##### Features

- Updated writer methods to match reader methods.

#### 2.9.1

##### Bug Fixes

- Fixed issue where char converter would trim a single space string.

#### 2.9

##### Features

- Added support to ignore whitespace when determining a record is empty.

#### 2.8.4

##### Bug Fixes

- Fixed breaking change to not break.

#### 2.8.3

##### Bug Fixes

- Fixed issue where header wasn't written when there were no records in the IEnumerable on WriteRecords( IEnumerable ).

#### 2.8.2

##### Bug Fixes

- Fixed issue where an exception was always thrown if Configuration.ThrowOnBadData is on.

#### 2.8

##### Features

- Added configurations for a callback when a bad field is detected. Added configuration to throw an exception when a bad field is detected.
- Made mapping with interfaces not duplicate property maps.

#### 2.7.1

##### Bug Fixes

- Fixed issue with mappings overwriting an explicitly set index when re-indexing.
- Auto mapping will ignore Enumerable properties instead of throwing an exception. Exceptions will still be thrown if an Enumerable is used outside of auto mapping.

#### 2.7

##### Bug Fixes

- Fixed issue where using dynamic proxy objects would always automap instead of using a registered class map.
- Fixed issue when trimming fields and the field is null.
- Fixed issue when writing a field and the value is null.
- Removed deprecated writer methods.

#### 2.6.1

##### Features

- PCL implementation. .NET 4.0+, Silveright 4.0+, WP7 7.5+, Windows 8
- Excel separator record reading and writing.
- Writer speed enhancements. Thanks to thecontrarycat.

##### Bug Fixes

- Fixed issue with mapping order when no index is specified.

#### 2.6

##### Features

- Added config to prefix headers of reference properties with the parent property name when automapping.
- Ability to ignore blank lines. When this config option is set to false, the parser will return and array of nulls. You can differentiate between a row with commas this way. The reader will behave the same way as a blank record.

##### Bug Fixes

- Fixed issue when writing and a reference map type is a struct.

#### 2.5

##### Features

- Global type converter options.
- Easier access to property maps to allow for changing maps on the fly.
- Option to ignore references when auto mapping.
- AutoMap functionality is available in class maps.
- Mappings can be specified in the constructor of the mapping class. Overriding CreateMap is now deprecated.

##### Bug Fixes

- Updated ConvertUsing to not cause the exception "Operation Could Destabilize the Runtime" when property is a nullable type and a non-nullable type is returned.

#### 2.4.1

##### Bug Fixes

- Fixed issue where parsing would add delimiter chars to the field when the buffer ran out in the middle of the delimiter.

#### 2.4

##### Features

- Split writing up into a writer and serializer so the writer can write other things besides CSV files.

##### Bug Fixes

- Fixed issue where a NullReferenceException was thrown when using reference maps and a reference was null.
- Fixed issue where TryGetField was throwing MissingFieldException.
- Fixed issue where a commented row on the last line that doesn't have a newline will return the commented row.
- Fixed NuGet package for WP8.
- Added missing WriteHeader methods to ICsvWriter that were a part of CsvWriter.

#### 2.3

##### Features

- Support for TimeSpan.
- Support for writing records of type dynamic. The dynamic objects do not work with collections, which means ExpandoObject doesn't currently work.

##### Bug Fixes

- Fixed issue with extra exception info not being added when the reading exception callback is used.
- Fixed issue where having only reference maps throws exception.

#### 2.2.2

##### Bug Fixes

- Fixed issue with parser where a line wouldn't end if the previous char was a \0.

#### 2.2.1

##### Bug Fixes

- Fixed issue with trimming fields not working under one path.
2.2.0

##### Features

- Added Row property to ICsvReader.
- Config option to trim headers and values when reading.

#### 2.1.1

##### Bug Fixes

- Fixed issue when WillThrowOnMissingField is off and exception was still being thrown.
 
#### 2.1.0

##### Features

- Made RegisterClassMap overload with CsvClassMap instance public.

#### 2.0.1

##### Bug Fixes

- Made a WinRT Any CPU build and removed the arch specific WinRT builds.

#### 2.0.0

##### Features

- Added parser configuration to ignoring quotes and treating them like any other character.
- Added CsvFactory to create ICsvParser, ICsvReader, and ICsvWriter classes. This is useful when you need to unit test code that uses CsvHelper since these 3 classes require a TextReader or TextWriter to work.
- All assembly versions are strong named but will use a single version of 2.0.0.0. The file version and NuGet versions will change with every release.
- Removed class type constraint from reading and writing.
- Added non-generic class mapping overload.
- WriteRecords param changed from IEnumerable<object> to non-generic IEnumerable.
- Value types can be read and written instead of just custom classes.
- Indexes are automatically set and incremented when mapping in order of the Map and Reference calls.
- Auto mapping with circular reference detection.
- Config option to ignore spaces in header names.
- Fixed exception handling. Exception are no longer wrapped. Exception.Data["CsvHelper"] contains CsvHelper specific exception info.
- Row exception can be skipped during GetRecords.
- Renamed IsStrictMode to WillThrowOnMissingField.
- Window Phone 7 & 8 builds.
- Auto mapping will use defined maps if available.
- Type converter options.
- Added IEnumerable converter that throws an exception so people will know that converting to/from and enumerable is not supported instead of getting a cryptic error message.
- Dynamic support for reading and writing.
- Multiple maps can be supplied.
- Renamed InvalidateRecordCache to ClearRecordCache.
- Recursive reference mapping down the whole mapping tree.
- Configuration.CultureInfo was added in place of Configuration.UseInvariantCulture.

##### Bug Fixes

- Getting the exception helper message failed when writing because no parser is available.
- WriteRecords Dynamic invoke had wrong parameter count.
- GetField( string ) was not returning null if the header is not found.
- CsvBadDataException when there were extra columns in the row.
- Raw record corruption.

#### 1.17.0

##### Features

- Ignore properties that can't be set in attribute mapping.
- Made TypeConverterFactory thread safe.
- Added remove converter method.

##### Bug Fixes

- Issue with writer exception in WinRT.

#### 1.16.0

##### Features

- Change TypeConverterFactory to use a set of cache type converters so global type converters can be used.
- Added GetField<T, TConverter> overloads.
- Changed all Activator.CreateInstance calls to use compiled expression trees to create them instead.
- Changed mapping for ConvertUsing to accept a Func so a block expression can be used.

#### 1.15.0

##### Features

- Support for Silverlight 4 & 5.

##### Bug Fixes

- Issue where writing with Configuration.QuoteAllFields enabled will not quote the quotes inside the field.
- Issue with WinRT not building after pull request merge.

#### 1.14.0

##### Features

- Parse full line on read. This allows for the parser to retain the whole unchanged raw CSV lin on a read.
- Changed delimiter config from a char to a string.
- Iterating records multiple times will throw a CsvReaderException. This is to help stop confusion when 0 results are returned the second iteration.

##### Bug Fixes

- Issue where EnumConverter isn't created correctly from the TypeConverterFactory.
- Issue with updating count for all closing quotes.

#### 1.13.0

##### Features

- Configuration to always not quote all fields.
- WriteHeader method is public.
- Added enum converter.

##### Bug Fixes

- Issue with boolean converter returning true for "no" value.
- Issue with GetMethod in WinRT.

#### 1.12.1

##### Bug Fixes

- Issue where an exception was being thrown when reading all records multiple times.

#### 1.12.0

##### Features

- WinRT support.

#### 1.11.0

##### Features

- Better exception information added to CsvBadDataException.

#### 1.10.0

##### Features

- Mapping property for CreateUsing which allows user to specify how the property gets created.

#### 1.9.2

##### Bug Fixes

- Issue with skipping empty records.

#### 1.9.1

##### Bug Fixes

- Issue with detecting column count changes.

#### 1.9.0

##### Features

- Added properties to CsvReaderException to give more information about the error.
- Ability to skip empty records based on config settings.
- Getting by index that doesn't exist will give a default or CsvMissingFieldException.
- Made column count detection a config setting.
- Map option for constructing the row object.
- Throw exception when inconsistent column lengths are detected.
- String.Format support in CsvWriter.
- Excel compatible parsing.
- Parser can keep track of the byte position using an encoding so a user can seek to a stream and start reading from there.

##### Bug Fixes

- Fixed bug with column count detection.
- Issue with double counting the closing quote.
- Issue where parsing was incorrect when the last row didn't have a CRLF at the end.
- Issue with error messages.

#### 1.8.0

##### Features

- Writer overload for shouldQuote when writing a field.
- Ability for using alternative names for headers in the configuration.
- Better error messages.

#### 1.7.0

##### Features

- Configuration to quote all fields when writing.
- Parser keeps a char count of where it's at.

##### Bug Fixes

- Fixed subclass issue by having the reader and writer use interfaces instead of concrete classes.

#### 1.6.0

##### Features

- Custom boolean type converter that can convert from 1 and 0 besides the normal conversion.
- Property map configuration to set a default value.
- CsvWriter no longer flushes to the output stream after every record.
- Non-generic overloads for reading, writing, and attribute mapping.
- Invalidate record cache will clear the properties list.

#### 1.5.0

##### Features

- Support .NET 2.0 and 3.5 builds.

#### 1.4.0

##### Features

- Case insensitive header matching.

#### 1.3.0

##### Features

- Removed CsvHelper class.
- Property reference mapping. One level deep.

#### 1.2.0

##### Features

- Support for multiple duplicate header names.

#### 1.1.2

##### Bug Fixes

- Issue when using a readonly or writeonly stream and disposing causes an exception.

#### 1.1.1

##### Features

- Updated CsvHelper.cs to allow for readonly and writeonly stream.

##### Bug Fixes

- Fixed DateTimeConverter issue where a white space string would return a - DateTime.MinValue instead of null.

#### 1.1.0

##### Features

- Changed .NET 3.5 project to client profile.
- Added getter for the current record in the header.

#### 1.0.0

##### Features

- Changed strict mode to default to true.
- Renamed strict mode configuration property.
- Changed reader to not throw an exception when there are duplicate header records unless in strict mode.

##### Bug Fixes

- Fixed bug where if there is no line ending at the end of the file, the last field would be null instead of an empty string.
- Fixed configuration references and constructor signatures.

#### 0.16.0

##### Features

- Added configuration option for using CultureInvariant to read/write.
- Updated the reader/writer to use the config option.
- Both CsvReader and CsvWriter are using Local culture when converting from/to strings.
- CsvClassMap without generic argument.

#### 0.15.0

##### Features

- Changed TryGetField<T> to do a low level check instead of jsut wrapping in try/catch blocks.
- Removed non generic TryGetField methods.
- Formatting changes.
- Changed CsvParser to use the Configuration.Comment char instead of #.

##### Bug Fixes

- Fixed indentation error caused by new constructor in CsvPropertyMap.

#### 0.14.0

##### Features

- Changed GetRecords<T> to return IEnumerable<T>.
- Added convenience constructor to CsvPropertyMap.
- Major configuration overhaul.
- Changed end of file check to be more low level.
- Final record is returned if there is a trailing delimiter.
- Added an exception re-throw to parsing that tells the line and character number.
- Added ability to change what the quote char is.
- Added CSV specific exceptions.

##### Bug Fixes

- Fix for issue when CsvHelper uses CurrentCulture instead of InvariantCulture.

#### 0.13.0

##### Features

- Changed StreamReader to TextReader to be more generic.

#### 0.12.0

##### Features

- Added option to have a commented out line using '#' as the first character of the line.

##### Bug Fixes

- Fixed issue with spaces in non-quoted field.
