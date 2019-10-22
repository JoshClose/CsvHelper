# MemberMapData Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

The configured data for the member map.

```cs
public class MemberMapData 
```

Inheritance Object -> MemberMapData

## Constructors
&nbsp; | &nbsp;
- | -
MemberMapData(MemberInfo) | Initializes a new instance of the ``CsvHelper.Configuration.MemberMapData`` class.

## Properties
&nbsp; | &nbsp;
- | -
Constant | Gets or sets the constant value used for every record.
Default | Gets or sets the default value used when a CSV field is empty.
Ignore | Gets or sets a value indicating whether the field should be ignored.
Index | Gets or sets the column index.
IndexEnd | Gets or sets the index end. The Index end is used to specify a range for use with a collection member. Index is used as the start of the range, and IndexEnd is the end of the range.
IsConstantSet | Gets or sets a value indicating if a constant was explicitly set.
IsDefaultSet | Gets or sets a value indicating whether this instance is default value set. the default value was explicitly set. True if it was explicitly set, otherwise false.
IsIndexSet | Gets or sets a value indicating if the index was explicitly set. True if it was explicitly set, otherwise false.
IsNameSet | Gets or sets a value indicating if the name was explicitly set. True if it was explicity set, otherwise false.
IsOptional | Gets or sets a value indicating if a field is optional.
Member | Gets the ``System.Reflection.MemberInfo`` that the data is associated with.
NameIndex | Gets or sets the index of the name. This is used if there are multiple columns with the same names.
Names | Gets the list of column names.
ReadingConvertExpression | Gets or sets the expression used to convert data in the row to the member.
TypeConverter | Gets or sets the type converter.
TypeConverterOptions | Gets or sets the type converter options.
ValidateExpression | Gets or sets the expression use to validate a field.
WritingConvertExpression | Gets or sets the expression to be used to convert the object to a field.
