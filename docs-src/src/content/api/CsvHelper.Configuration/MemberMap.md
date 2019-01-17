# MemberMap Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Mapping info for a member to a CSV field.

```cs
[System.Diagnostics.DebuggerDisplayAttribute]
public abstract class MemberMap 
```

Inheritance Object -> MemberMap

## Properties
&nbsp; | &nbsp;
- | -
Data | Gets the member map data.
TypeConverterOption | Type converter options.

## Methods
&nbsp; | &nbsp;
- | -
Constant(Object) | The constant value that will be used for every record when reading and writing. This value will always be used no matter what other mapping configurations are specified.
CreateGeneric(Type, MemberInfo) | Creates an instance of ``CsvHelper.Configuration.MemberMap`` using the given Type and ``System.Reflection.MemberInfo`` .
Default(Object) | The default value that will be used when reading when the CSV field is empty.
Default(String) | The default value that will be used when reading when the CSV field is empty. This value is not type checked and will use a ``CsvHelper.TypeConversion.ITypeConverter`` to convert the field. This could potentially have runtime errors.
Ignore() | Ignore the member when reading and writing. If this member has already been mapped as a reference member, either by a class map, or by automapping, calling this method will not ingore all the child members down the tree that have already been mapped.
Ignore(Boolean) | Ignore the member when reading and writing. If this member has already been mapped as a reference member, either by a class map, or by automapping, calling this method will not ingore all the child members down the tree that have already been mapped.
Index(Int32, Int32) | When reading, is used to get the field at the given index. When writing, the fields will be written in the order of the field indexes.
Name(String[]) | When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.
NameIndex(Int32) | When reading, is used to get the index of the name used when there are multiple names that are the same.
TypeConverter(ITypeConverter) | Specifies the ``CsvHelper.Configuration.MemberMap.TypeConverter(CsvHelper.TypeConversion.ITypeConverter)`` to use when converting the member to and from a CSV field.
TypeConverter&lt;TConverter&gt;() | Specifies the ``CsvHelper.Configuration.MemberMap.TypeConverter(CsvHelper.TypeConversion.ITypeConverter)`` to use when converting the member to and from a CSV field.
Validate(Func&lt;String, Boolean&gt;) | Specifies an expression to be used to validate a field when reading.
