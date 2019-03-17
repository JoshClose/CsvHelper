# CsvHelper.Configuration.Attributes Namespace

## Classes

### Member level attributes
&nbsp; | &nbsp;
- | -
[BooleanFalseValuesAttribute](/api/CsvHelper.Configuration.Attributes/BooleanFalseValuesAttribute) | The string values used to represent a boolean false when converting.
[BooleanTrueValuesAttribute](/api/CsvHelper.Configuration.Attributes/BooleanTrueValuesAttribute) | The string values used to represent a boolean true when converting.
[ConstantAttribute](/api/CsvHelper.Configuration.Attributes/ConstantAttribute) | The constant value that will be used for every record when reading and writing. This value will always be used no matter what other mapping configurations are specified.
[CultureInfoAttribute](/api/CsvHelper.Configuration.Attributes/CultureInfoAttribute) | The ``CsvHelper.Configuration.Attributes.CultureInfoAttribute.CultureInfo`` used when type converting. This will override the global ``CsvHelper.Configuration.Configuration.CultureInfo`` setting.
[DateTimeStylesAttribute](/api/CsvHelper.Configuration.Attributes/DateTimeStylesAttribute) | The ``CsvHelper.Configuration.Attributes.DateTimeStylesAttribute.DateTimeStyles`` to use when type converting. This is used when doing any ``System.DateTime`` conversions.
[DefaultAttribute](/api/CsvHelper.Configuration.Attributes/DefaultAttribute) | The default value that will be used when reading when the CSV field is empty.
[FormatAttribute](/api/CsvHelper.Configuration.Attributes/FormatAttribute) | The string format to be used when type converting.
[HeaderPrefixAttribute](/api/CsvHelper.Configuration.Attributes/HeaderPrefixAttribute) | Appends a prefix to the header of each field of the reference member.
[IgnoreAttribute](/api/CsvHelper.Configuration.Attributes/IgnoreAttribute) | Ignore the member when reading and writing. If this member has already been mapped as a reference member, either by a class map, or by automapping, calling this method will not ingore all the child members down the tree that have already been mapped.
[IndexAttribute](/api/CsvHelper.Configuration.Attributes/IndexAttribute) | When reading, is used to get the field at the given index. When writing, the fields will be written in the order of the field indexes.
[NameAttribute](/api/CsvHelper.Configuration.Attributes/NameAttribute) | When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.
[NameIndexAttribute](/api/CsvHelper.Configuration.Attributes/NameIndexAttribute) | When reading, is used to get the index of the name used when there are multiple names that are the same.
[NullValuesAttribute](/api/CsvHelper.Configuration.Attributes/NullValuesAttribute) | The string values used to represent null when converting.
[NumberStylesAttribute](/api/CsvHelper.Configuration.Attributes/NumberStylesAttribute) | The ``CsvHelper.Configuration.Attributes.NumberStylesAttribute.NumberStyles`` to use when type converting. This is used when doing any number conversions.
[OptionalAttribute](/api/CsvHelper.Configuration.Attributes/OptionalAttribute) | Ignore the member when reading if no matching field name can be found.
[TypeConverterAttribute](/api/CsvHelper.Configuration.Attributes/TypeConverterAttribute) | Specifies the ``CsvHelper.Configuration.Attributes.TypeConverterAttribute.TypeConverter`` to use when converting the member to and from a CSV field.

### Class level attributes
&nbsp; | &nbsp;
- | -
[AllowCommentsAttribute](/api/CsvHelper.Configuration.Attributes/AllowCommentsAttribute) | A value indicating if comments are allowed.
[CommentAttribute](/api/CsvHelper.Configuration.Attributes/CommentAttribute) | The character used to denote a line that is commented out.
[CultureInfoAttribute](/api/CsvHelper.Configuration.Attributes/CultureInfoAttribute) | Default for type converting, this set the global ``CsvHelper.Configuration.Configuration.CultureInfo`` setting.
[DelimiterAttribute](/api/CsvHelper.Configuration.Attributes/DelimiterAttribute) | The delimiter used to separate fields.
[EncodingAttribute](/api/CsvHelper.Configuration.Attributes/EncodingAttribute) | The encoding used when counting bytes.
[EscapeAttribute](/api/CsvHelper.Configuration.Attributes/EscapeAttribute) | The escape character used to escape a quote inside a field.
[HasHeaderRecordAttribute](/api/CsvHelper.Configuration.Attributes/HasHeaderRecordAttribute) | A value indicating if the CSV file has a header record.
[IgnoreBlankLinesAttribute](/api/CsvHelper.Configuration.Attributes/IgnoreBlankLinesAttribute) | A value indicating if blank lines should be ignored when reading.
[IgnoreQuotesAttribute](/api/CsvHelper.Configuration.Attributes/IgnoreQuotesAttribute) | A value indicating if quotes should be ignored when parsing and treated like any other character.
[IncludePrivateMembersAttribute](/api/CsvHelper.Configuration.Attributes/IncludePrivateMembersAttribute) | A value indicating if private member should be read from and written to.
[QuoteAttribute](/api/CsvHelper.Configuration.Attributes/QuoteAttribute) | The character used to quote fields.
[TrimOptionsAttribute](/api/CsvHelper.Configuration.Attributes/TrimOptionsAttribute) | The fields trimming options.