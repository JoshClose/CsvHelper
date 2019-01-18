# Configuration Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Configuration used for reading and writing CSV data.

```cs
public class Configuration : IReaderConfiguration, IParserConfiguration, IWriterConfiguration, ISerializerConfiguration
```

Inheritance Object -> Configuration

## Constructors
&nbsp; | &nbsp;
- | -
Configuration() | Initializes a new instance of the ``CsvHelper.Configuration.Configuration`` class.
Configuration(CultureInfo) | Initializes a new instance of the ``CsvHelper.Configuration.Configuration`` class using the given ``System.Globalization.CultureInfo`` . Since ``CsvHelper.Configuration.Configuration.Delimiter`` uses ``CsvHelper.Configuration.Configuration.CultureInfo`` for it's default, the given ``System.Globalization.CultureInfo`` will be used instead.

## Properties
&nbsp; | &nbsp;
- | -
AllowComments | Gets or sets a value indicating if comments are allowed. True to allow commented out lines, otherwise false.
BadDataFound | Gets or sets the function that is called when bad field data is found. A field has bad data if it contains a quote and the field is not quoted (escaped). You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: context
BufferSize | Gets or sets the size of the buffer used for reading CSV files. Default is 2048.
Comment | Gets or sets the character used to denote a line that is commented out. Default is '#'.
CountBytes | Gets or sets a value indicating whether the number of bytes should be counted while parsing. Default is false. This will slow down parsing because it needs to get the byte count of every char for the given encoding. The ``CsvHelper.Configuration.Configuration.Encoding`` needs to be set correctly for this to be accurate.
CultureInfo | Gets or sets the culture info used to read an write CSV files. Default is ``System.Globalization.CultureInfo.CurrentCulture`` .
Delimiter | Gets or sets the delimiter used to separate fields. Default is CultureInfo.TextInfo.ListSeparator.
DetectColumnCountChanges | Gets or sets a value indicating whether changes in the column count should be detected. If true, a ``CsvHelper.BadDataException`` will be thrown if a different column count is detected.
DoubleQuoteString | Gets a string representation of two of the currently configured Quote characters.
DynamicPropertySort | Gets or sets the comparer used to order the properties of dynamic objects when writing. The default is null, which will preserve the order the object properties were created with.
Encoding | Gets or sets the encoding used when counting bytes.
Escape | Gets or sets the escape character used to escape a quote inside a field. Default is '"'.
GetConstructor | Chooses the constructor to use for constuctor mapping.
HasHeaderRecord | Gets or sets a value indicating if the CSV file has a header record. Default is true.
HeaderValidated | Gets or sets the function that is called when a header validation check is ran. The default function will throw a ``CsvHelper.ValidationException`` if there is no header for a given member mapping. You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: isValid, headerNames, headerNameIndex, context
IgnoreBlankLines | Gets or sets a value indicating if blank lines should be ignored when reading. True to ignore, otherwise false. Default is true.
IgnoreQuotes | Gets or sets a value indicating if quotes should be ignored when parsing and treated like any other character.
IgnoreReferences | Gets or sets a value indicating whether references should be ignored when auto mapping. True to ignore references, otherwise false. Default is false.
IncludePrivateMembers | Gets or sets a value indicating if private member should be read from and written to. True to include private member, otherwise false. Default is false.
InjectionCharacters | Gets or sets the characters that are used for injection attacks.
InjectionEscapeCharacter | Gets or sets the character used to escape a detected injection.
LineBreakInQuotedFieldIsBadData | Gets or sets a value indicating if a line break found in a quote field should be considered bad data. True to consider a line break bad data, otherwise false. Defaults to false.
Maps | The configured ``CsvHelper.Configuration.ClassMap`` s.
MemberTypes | Gets or sets the member types that are used when auto mapping. MemberTypes are flags, so you can choose more than one. Default is Properties.
MissingFieldFound | Gets or sets the function that is called when a missing field is found. The default function will throw a ``CsvHelper.MissingFieldException`` . You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: headerNames, index, context
PrepareHeaderForMatch | Prepares the header field for matching against a member name. The header field and the member name are both ran through this function. You should do things like trimming, removing whitespace, removing underscores, and making casing changes to ignore case.
Quote | Gets or sets the character used to quote fields. Default is '"'.
QuoteString | Gets a string representation of the currently configured Quote character.
ReadingExceptionOccurred | Gets or sets the function that is called when a reading exception occurs. The default function will re-throw the given exception. If you want to ignore reading exceptions, you can supply your own function to do other things like logging the issue. Arguments: exception
ReferenceHeaderPrefix | Gets or sets a callback that will return the prefix for a reference header. Arguments: memberType, memberName
SanitizeForInjection | Gets or sets a value indicating if fields should be sanitized to prevent malicious injection. This covers MS Excel, Google Sheets and Open Office Calc.
ShouldQuote | Gets or sets a function that is used to determine if a field should get quoted when writing. Arguments: field, context
ShouldSkipRecord | Gets or sets the callback that will be called to determine whether to skip the given record or not.
ShouldUseConstructorParameters | Determines if constructor parameters should be used to create the class instead of the default constructor and members.
TrimOptions | Gets or sets the field trimming options.
TypeConverterCache | Gets or sets the ``CsvHelper.Configuration.Configuration.TypeConverterOptionsCache`` .
TypeConverterOptionsCache | Gets or sets the ``CsvHelper.Configuration.Configuration.TypeConverterOptionsCache`` .
UseNewObjectForNullReferenceMembers | Gets or sets a value indicating that during writing if a new object should be created when a reference member is null. True to create a new object and use it's defaults for the fields, or false to leave the fields empty for all the reference member's member.

## Methods
&nbsp; | &nbsp;
- | -
AutoMap&lt;T&gt;() | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
AutoMap(Type) | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
RegisterClassMap&lt;TMap&gt;() | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no members are mapped by default. Only member specified in the mapping are used.
RegisterClassMap(Type) | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no members are mapped by default. Only members specified in the mapping are used.
RegisterClassMap(ClassMap) | Registers the class map.
UnregisterClassMap&lt;TMap&gt;() | Unregisters the class map.
UnregisterClassMap(Type) | Unregisters the class map.
UnregisterClassMap() | Unregisters all class maps.
