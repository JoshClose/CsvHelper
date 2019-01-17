# IWriterConfiguration Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Configuration used for the ``CsvHelper.IWriter`` .

```cs
public interface IWriterConfiguration : ISerializerConfiguration
```

## Properties
&nbsp; | &nbsp;
- | -
AllowComments | Gets or sets a value indicating if comments are allowed. True to allow commented out lines, otherwise false.
Comment | Gets or sets the character used to denote a line that is commented out. Default is '#'.
CultureInfo | Gets or sets the culture info used to read an write CSV files.
DoubleQuoteString | Gets a string representation of two of the currently configured Quote characters.
DynamicPropertySort | Gets or sets the comparer used to order the properties of dynamic objects when writing. The default is null, which will preserve the order the object properties were created with.
HasHeaderRecord | Gets or sets a value indicating if the CSV file has a header record. Default is true.
IgnoreReferences | Gets or sets a value indicating whether references should be ignored when auto mapping. True to ignore references, otherwise false. Default is false.
IncludePrivateMembers | Gets or sets a value indicating if private member should be read from and written to. True to include private member, otherwise false. Default is false.
Maps | The configured ``CsvHelper.Configuration.ClassMap`` s.
MemberTypes | Gets or sets the member types that are used when auto mapping. MemberTypes are flags, so you can choose more than one. Default is Properties.
QuoteString | Gets a string representation of the currently configured Quote character.
ReferenceHeaderPrefix | Gets or sets a callback that will return the prefix for a reference header. Arguments: memberType, memberName
ShouldQuote | Gets or sets a function that is used to determine if a field should get quoted when writing. Arguments: field, context
TypeConverterCache | Gets or sets the ``CsvHelper.Configuration.IWriterConfiguration.TypeConverterCache`` .
TypeConverterOptionsCache | Gets or sets the ``CsvHelper.Configuration.IWriterConfiguration.TypeConverterOptionsCache`` .
UseNewObjectForNullReferenceMembers | Gets or sets a value indicating that during writing if a new object should be created when a reference member is null. True to create a new object and use it's defaults for the fields, or false to leave the fields empty for all the reference member's member.

## Methods
&nbsp; | &nbsp;
- | -
AutoMap&lt;T&gt;() | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
AutoMap(Type) | Generates a ``CsvHelper.Configuration.ClassMap`` for the type.
RegisterClassMap&lt;TMap&gt;() | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no member are mapped by default. Only member specified in the mapping are used.
RegisterClassMap(Type) | Use a ``CsvHelper.Configuration.ClassMap<TClass>`` to configure mappings. When using a class map, no member are mapped by default. Only member specified in the mapping are used.
RegisterClassMap(ClassMap) | Registers the class map.
UnregisterClassMap&lt;TMap&gt;() | Unregisters the class map.
UnregisterClassMap(Type) | Unregisters the class map.
UnregisterClassMap() | Unregisters all class maps.
