# Configuration

CsvHelper was created to be fast and simple out of the box. Sometimes the box doesn't fit, and you need to change things. CsvHelper has a lot of configuration options to change the behavior of reading and writing. This will especially help with reading non-[standard](https://tools.ietf.org/html/rfc4180) files.

## Malicious Injection Protection
<hr/>

From [Comma Separated Vulnerabilities](https://www.contextis.com/blog/comma-separated-vulnerabilities):

> Many modern web applications and frameworks offer spreadsheet export functionality, allowing users to download data in a .csv or .xls file suitable for handling in spreadsheet applications like Microsoft Excel and OpenOffice Calc.  The resulting spreadsheet’s cells often contain input from untrusted sources such as survey responses, transaction details, and user-supplied addresses. This is inherently risky, because any cells starting with the `=` character will be interpreted by the spreadsheet software as formulae.

Another good article: [The Absurdly Underestimated Dangers of CSV Injection](http://georgemauer.net/2017/10/07/csv-injection.html)

### SanitizeForInjection

Sanitizes fields to prevent malicious injection. When opening a CSV in an external program, a formula in a field could be ran that contains a vulnerability. Due to this issue, if a field starts with characters `=`, `@`, `+`, or `-`, that field will be prepended with a `\t`. If the field is quoted, the `\t` will come after the `"`. Sanitization covers MS Excel, Google Sheets, and Open Office Calc.

```cs
// Turn off sanitization.
csv.Configuration.SanitizeForInjection = false;
```

### InjectionCharacters

The list of characters that cause sanitization to occur.

```cs
// Default.
csv.Configuration.InjectionCharacters = new [] { '=', '@', '+', '-' };
```

### InjectionEscapeCharacter

The character that is used to escape the injection characters.

```cs
// Default.
csv.Configuration.InjectionEscapeCharacter = '\t';
```

## Headers
<hr/>

### HasHeaderRecord

By default CsvHelper assumes there is a header record. If your file doesn't have a header record, you can turn this off.

```cs
csv.Configuration.HasHeaderRecord = false;
```

### HeaderValidationCallback

Function that is called when a header validation check is ran. The default function will throw a `ValidationException` if there is no header for a given member mapping.

```cs
// Turn off header validation.
csv.Configuration.HeaderValidationCallback = null;

// Log instead of throwing an exception.
csv.Configuration.HeaderValidationCallback = ( isValid, headerNames, headerNameIndex, context ) =>
{
	if( !isValid )
	{
		logger.WriteLine( $"Header matching ['{string.Join( "', '", headerNames )}'] names at index {headerNameIndex} was not found." );
	}
};
```

### PrepareHeaderForMatch

Prepares the header field for matching against a member name. The header field and the member name are both ran through this function. You should do things like trimming, removing whitespace, removing underscores, and making casing changes to ignore case.

```cs
// Trim
csv.Configuration.PrepareHeaderForMatch = header => header?.Trim();

// Remove whitespace
csv.Configuration.PrepareHeaderForMatch = header => header.Replace( " ", string.Empty );

// Remove underscores
csv.Configuration.PrepareHeaderForMatch = header => header.Replace( "_", string.Empty );

// Ignore case
csv.Configuration.PrepareHeaderForMatch = header => header.ToLower();
```

## Mapping
<hr/>

Configuration used with class mapping.

### RegisterClassMap

Adds a class map. You can register multiple class maps. If you register a map for the same type, the new map will overwrite the old one.

```cs
// Creates and adds a map for the given  class map type `TMap`.
csv.Configuration.RegisterClassMap<TMap>();

// Creates and adds a map for the given class map type
csv.Configuration.RegisterClassMap( classMapType );

// Adds the given class map.
csv.Configuration.RegisterClassMap( classMap );
```

### UnregisterClassMap

Removes a class map.

```cs
// Removes the class map for the given class map type.
csv.Configuration.UnregisterClassMap<TMap>();

// Removes the class map for the given class map type.
csv.Configuration.UnregisterClassMap( Type classMapType );

// Removes all class maps.
csv.Configuration.UnregisterClassMap();
```

### Maps

The configured maps. This is of type `ClassMapCollection` which has some extra methods to help with managing the class maps.

```cs
// Finds the `ClassMap` for the given record type `TRecord`.
var map = csv.Maps.Find<TRecord>();

// Finds the `ClassMap` for the given type.
var map = csv.Maps[recordType];
```

### AutoMap

Creates a `ClassMap` automatically using defaults.

```cs
// Creates a ClassMap for given record type `TRecord`.
var map = csv.Configuration.AutoMap<TRecord>();

// Creates a ClassMap for the given type.
var map = csv.Configuration.AutoMap( recordType );
```

### MemberTypes

Specifies the type of member mapped when auto mapping.

```cs
// Default.
csv.Configuration.MemberTypes = MemberTypes.Properties;

// Fields.
csv.Configuration.MemberTypes = MemberTypes.Fields;

// Both;
csv.Configuration.MemberTypes = MemberTypes.Properties | MemberTypes.Fields;
```

### IncludePrivateMembers

Include private members when auto mapping. This can only be used for writing. Reading will need to use constructor mapping.

```cs
// Turn on.
csv.Configuration.IncludePrivateMembers = true;
```

### IgnoreReferences

Ignores reference members when auto mapping. Default is false.

```cs
// Ignore reference members.
csv.Configuration.IgnoreReferences = true;
```

### PrefixReferenceHeaders

Prefix headers of reference members with the parent member name.

```cs
// Turn on.
csv.Configuration.PrefixReferenceHeaders = true;
```

## Constructor Mapping
<hr/>

If you would like your class created using a constructor with csv fields as the parameters, you can use these options.

### ShouldUseConstructorParameters

Function to determine if constructor parameters should be used to create the class instead of the default constructor and member initialization.

```cs
// Default
csv.Configuration.ShouldUseConstructorParameters = type =>
	!type.HasParameterlessConstructor()
	&& type.HasConstructor()
	&& !type.IsUserDefinedStruct()
	&& !type.IsInterface
	&& Type.GetTypeCode( type ) == TypeCode.Object;

// Always use constructor parameters.
csv.Configuration.ShouldUseConstructorParameters = type => true;

// Never use constructor parameters.
csv.Configuration.ShouldUseConstructorParameters = type => false;
```

### GetConstructor

Function that chooses the constructor to use for constructor mapping. Default gets the constructor with the most parameters.

```cs
// Default
csv.Configuration.GetConstructor = type => type.GetConstructors().OrderByDescending( c => c.GetParameters().Length ).First();
```

## Error Handling
<hr/>

When some errors occur, you can change the behavior to do something besides throw an exception; such as ignoring the error and logging it.

### BadDataFound

Function that is called when bad field data is found. A field has bad data if it contains a quote and the field is not quoted (escaped). The default function will throw a `BadDataException`.

```cs
// Ignore bad data.
csv.Configuration.BadDataFound = null;

// Log bad data.
csv.Configuration.BadDataFound = context =>
{
	logger.WriteLine( $"Bad data found on row '{context.RawRow}'" );
};
```

### MissingFieldFound

Function that is called when a missing field is found. The default function will throw a `MissingFieldException`.

```cs
// Ignore missing field.
csv.Configuration.MissingFieldFound = null;

// Log missing field.
csv.Configuration.MissingFieldFound = ( headerNames, index, context ) =>
{
	logger.WriteLine( $"Field with names ['{string.Join( "', '", headerNames )}'] at index '{index}' was not found." );
};
```

### ReadingExceptionOccurred

Function that is called when a reading exception occurs. The default function will re-throw the given exception.

```cs
// Ignore reading exception.
csv.Configuration.ReadingExceptionOccurred = null;

// Log reading exception.
csv.Configuration.ReadingExceptionOccurred = exception =>
{
	logger.WriteLine( $"Reading exception: {exception.Message}" );
};
```

## Type Conversion
<hr/>

### TypeConverterCache

Manages `TypeConverter`s. You can add, remove, and get type converters for a type.

```cs
// Add converter for a type.
csv.Configuration.TypeConverterCache.AddConverter<MyClass>( converter );
csv.Configuration.TypeConverterCache.AddConverter( typeof( MyClass ), converter );

// Remove converter.
csv.Configuration.TypeConverterCache.RemoveConverter<MyClass>();
csv.Configuration.TypeConverterCache.RemoveConverter( typeof( MyClass ) );

// Get existing converter.
csv.Configuration.TypeConverterCache.GetConverter<MyClass>();
csv.Configuration.TypeConverterCache.GetConverter( typeof( MyClass ) );
```

### TypeConverterOptionsCache

Manages `TypeConverterOptions`. You can add, remove, and get options for a type.

```cs
// Add options for a type.
csv.Configuration.TypeConverterOptionsCache.AddOptions<MyClass>( options );
csv.Configuration.TypeConverterOptionsCache.AddOptions( typeof( MyClass ), options );

// Remove options.
csv.Configuration.TypeConverterOptionsCache.RemoveOptions<MyClass>();
csv.Configuration.TypeConverterOptionsCache.RemoveOptions( typeof( MyClass ) );

// Get existing options.
csv.Configuration.TypeConverterOptionsCache.GetOptions<string>().CultureInfo = CultureInfo.CurrentCulture;
csv.Configuration.TypeConverterOptionsCache.GetOptions( typeof( string )).CultureInfo = CultureInfo.CurrentCulture;
```

## Reading
<hr/>

### DetectColumnCountChanges

If a different column count is detected on rows a `BadDataException` is thrown. Default is false.

```cs
// Turn on.
csv.Configuration.DetectColumnCountChanges = true;
```

### ShouldSkipRecord

Function that is called when to determine if a record should be skipped. Default returns false.

```cs
// Skip if all fields are empty.
csv.Configuration.ShouldSkipRecord = record =>
{
	return record.All( string.IsNullOrEmpty );
};
```

### IgnoreBlankLines

Ignores blank lines when reading. A blank line is a line with no characters at all. This is on by default.

```cs
// Turn off.
csv.Configuration.IgnoreBlankLines = true;

// A,B,C  // Header.
//        // When on, this row is skipped. When off, it will be just like the next row.
// ,,     // Not ignored. All fields are empty.
```

## Parsing
<hr/>

### TrimOptions

Options for trimming fields. `TrimOptions.Trim` trims whitespace around a field. `TrimOptions.InsideQuotes` trims the whitespace inside of quotes around a field.

```cs
csv.Configuration.TrimOptions = TrimOptions.Trim;
` field ` -> `field`
` " field " ` -> `" field "`

csv.Configuration.TrimOptions = TrimOptions.InsideQuotes;
` field ` -> ` field `
` " field " ` -> ` "field" `

csv.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
` " field " ` -> "field"
```

### AllowComments

Allows comments when reading. If this is on, a row that starts with the comment character will be considered a commented out row. If this is off, it will be treated as a normal field. Default is off.

```cs
// Turn on.
csv.Configuration.AllowComments = true;
```

### BufferSize

The size of the buffer used when reading. Default is 2048;

```cs
// Double the buffer size.
csv.Configuration.BufferSize = csv.Configuration.BufferSize * 2;
```

### CountBytes

Counts the number of bytes while parsing. Default is false. This will slow down parsing because it needs to get the byte count of every char for the given encoding. Configuration.Encoding needs to be set correctly for this to be accurate.

```cs
// Turn on.
csv.Configuration.CountBytes = true;

// Get count.
var byteCount = csv.Context.BytePosition;
```

### Encoding

The encoding to use when counting bytes.

```cs
// Change to whatever the CSV file was encoded in.
csv.Configuration.Encoding = Encoding.Unicode;
```

## Writing
<hr/>

### QuoteAllFields

Quotes all fields when writing, regardless of other settings. `QuoteAllFields` and `QuoteNoFields` can't both be on at the same time. Turning one on will turn the other off.

```cs
// Turn on.
csv.Configuration.QuoteAllFields = true;
```

### QuoteNoFields

Quotes no fields when writing, regardless of other settings. `QuoteNoFields` and `QuoteAllFields` can't both be on at the same time. Turning one on will turn the other off.

```cs
// Turn on.
csv.Configuration.QuoteNoFields = true;
```

### UseNewObjectForNullReferenceMembers

Specifies if a new object should be created and used when a reference is null. If true, a new object is created and will have default values that are written. If false, all the fields will be empty. Default is true.

```cs
public class A
{
	public int AId { get; set;}
	public B B { get; set; }
}

public class B
{
	public int BId { get; set; }
}

var list = new List<A>
{
	new A { AId = 1 }
};

// Default.
csv.Configuration.UseNewObjectForNullReferenceMembers = true;
csv.WriteRecords( list );

// Output:
// AId,BId
// 1,0

// Turn off.
csv.Configuration.UseNewObjectForNullReferenceMembers = false;
csv.WriteRecords( list );

// Output:
// AId,BId
// 1,
```

## Formatting
<hr/>

### Delimiter

The delimiter used to separate fields.

```cs
// Default.
csv.Configuration.Delimiter = ",";
```

### Quote

The quote used to escape fields.

```cs
// Default.
csv.Configuration.Quote = '"';
```

### Comment

The character used to denote a line that is commented out.

```cs
// Default.
csv.Configuration.Comment = '#';
```

### CultureInfo

### IgnoreQuotes

<br/>
