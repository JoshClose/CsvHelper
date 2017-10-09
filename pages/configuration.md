# Configuration

CsvHelper was created to be fast and simple out of the box. Sometimes the box doesn't fit, and you need to change things. CsvHelper has a lot of configuration options to change the behavior of reading and writing. This will especially help with reading non-[standard](https://tools.ietf.org/html/rfc4180) files.

## Malicious Injection Protection

From [Comma Separated Vulnerabilities](https://www.contextis.com/blog/comma-separated-vulnerabilities):

> Many modern web applications and frameworks offer spreadsheet export functionality, allowing users to download data in a .csv or .xls file suitable for handling in spreadsheet applications like Microsoft Excel and OpenOffice Calc.  The resulting spreadsheet’s cells often contain input from untrusted sources such as survey responses, transaction details, and user-supplied addresses. This is inherently risky, because any cells starting with the `=` character will be interpreted by the spreadsheet software as formulae.

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

### IgnoreReferences

Ignores reference members when auto mapping. Default is false.

```cs
// Ignore reference members.
csv.Configuration.IgnoreReferences = true;
```

### Maps

### RegisterClassMap

### UnRegisterClassMap

### AutoMap

## Error Handling

<hr/>

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
	logger.WriteLine( $"Field with names ['{string.Join( "', '", headerNames )}'] at index '{index}' was not found. );
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

### DetectColumnCountChanges

If a different column count is detected on rows a `BadDataException` is thrown. Default is false.

```cs
// Turn on.
csv.Configuration.DetectColumnCountChanges = true;
```

### TypeConverterOptionsFactory

Manages `TypeConverterOptions`. You can add, remove, and get options for a type.

```cs
// Add options for a type.
csv.Configuration.TypeConverterOptionsFactory.AddOptions<MyClass>( options );
csv.Configuration.TypeConverterOptionsFactory.AddOptions( typeof( MyClass ), options );

// Remove options.
csv.Configuration.TypeConverterOptionsFactory.RemoveOptions<MyClass>();
csv.Configuration.TypeConverterOptionsFactory.RemoveOptions( typeof( MyClass ) );

// Get existing options.
csv.Configuration.TypeConverterOptionsFactory.GetOptions<string>().CultureInfo = CultureInfo.CurrentCulture;
csv.Configuration.TypeConverterOptionsFactory.GetOptions( typeof( string )).CultureInfo = CultureInfo.CurrentCulture;
```

### TypeConverterFactory

Manages `TypeConverter`s. You can add, remove, and get type converters for a type.

```cs
// Add converter for a type.
csv.Configuration.TypeConverterFactory.AddConverter<MyClass>( converter );
csv.Configuration.TypeConverterFactory.AddConverter( typeof( MyClass ), converter );

// Remove converter.
csv.Configuration.TypeConverterFactory.RemoveConverter<MyClass>();
csv.Configuration.TypeConverterFactory.RemoveConverter( typeof( MyClass ) );

// Get existing converter.
csv.Configuration.TypeConverterFactory.GetConverter<MyClass>();
csv.Configuration.TypeConverterFactory.GetConverter( typeof( MyClass ) );
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

### ShouldUseConstructorParameters

Function to determine if constructor parameters should be used to create the class instead of the default constructor and member initialization.

```cs
// Default
csv.Configuration.ShouldUseConstructorParameters = type =>
	!type.HasParameterlessConstructor()
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
csv.Configuration.GetConstructor = type =>
	type.GetConstructors().OrderByDescending( c => c.GetParameters().Length ).First();
```

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

### CountBytes

### Encoding

### CultureInfo

### IgnoreQuotes

### IncludePrivateMembers

### MemberTypes

### IgnoreBlankLines

### PrefixReferenceHeaders

### BuildRequiredQuoteCharacters

### UseNewObjectForNullReferenceMembers

### QuoteString

### DoubleQuoteString

### QuoteRequiredChars

<br/>
