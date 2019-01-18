# IReaderConfiguration Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Configuration used for the ``CsvHelper.IReader`` .

```cs
public interface IReaderConfiguration : IParserConfiguration
```

## Properties
&nbsp; | &nbsp;
- | -
CultureInfo | Gets or sets the culture info used to read an write CSV files.
DetectColumnCountChanges | Gets or sets a value indicating whether changes in the column count should be detected. If true, a ``CsvHelper.BadDataException`` will be thrown if a different column count is detected.
GetConstructor | Chooses the constructor to use for constuctor mapping.
HasHeaderRecord | Gets or sets a value indicating if the CSV file has a header record. Default is true.
HeaderValidated | Gets or sets the function that is called when a header validation check is ran. The default function will throw a ``CsvHelper.ValidationException`` if there is no header for a given member mapping. You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: isValid, headerNames, headerNameIndex, context
IgnoreReferences | Gets or sets a value indicating whether references should be ignored when auto mapping. True to ignore references, otherwise false. Default is false.
IncludePrivateMembers | Gets or sets a value indicating if private member should be read from and written to. True to include private member, otherwise false. Default is false.
Maps | The configured ``CsvHelper.Configuration.ClassMap`` s.
MemberTypes | Gets or sets the member types that are used when auto mapping. MemberTypes are flags, so you can choose more than one. Default is Properties.
MissingFieldFound | Gets or sets the function that is called when a missing field is found. The default function will throw a ``CsvHelper.MissingFieldException`` . You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: headerNames, index, context
PrepareHeaderForMatch | Prepares the header field for matching against a member name. The header field and the member name are both ran through this function. You should do things like trimming, removing whitespace, removing underscores, and making casing changes to ignore case.
ReadingExceptionOccurred | Gets or sets the function that is called when a reading exception occurs. The default function will re-throw the given exception. If you want to ignore reading exceptions, you can supply your own function to do other things like logging the issue. Arguments: exception
ReferenceHeaderPrefix | Gets or sets a callback that will return the prefix for a reference header. Arguments: memberType, memberName
ShouldSkipRecord | Gets or sets the callback that will be called to determine whether to skip the given record or not.
ShouldUseConstructorParameters | Determines if constructor parameters should be used to create the class instead of the default constructor and members.
TypeConverterCache | Gets or sets the ``CsvHelper.Configuration.IReaderConfiguration.TypeConverterCache`` .
TypeConverterOptionsCache | Gets or sets the ``CsvHelper.Configuration.IReaderConfiguration.TypeConverterOptionsCache`` .

## Methods
&nbsp; | &nbsp;
- | -
AutoMap&lt;T&gt;() | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
AutoMap(Type) | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
RegisterClassMap&lt;TMap&gt;() | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no members are mapped by default. Only member specified in the mapping are used.
RegisterClassMap(Type) | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no member are mapped by default. Only member specified in the mapping are used.
RegisterClassMap(ClassMap) | Registers the class map.
UnregisterClassMap&lt;TMap&gt;() | Unregisters the class map.
UnregisterClassMap(Type) | Unregisters the class map.
UnregisterClassMap() | Unregisters all class maps.
