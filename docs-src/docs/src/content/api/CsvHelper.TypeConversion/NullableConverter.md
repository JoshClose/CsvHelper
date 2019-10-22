# NullableConverter Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Converts a ``System.Nullable<T>`` to and from a ``System.String`` .

```cs
public class NullableConverter : DefaultTypeConverter, ITypeConverter
```

Inheritance Object -> DefaultTypeConverter -> NullableConverter

## Constructors
&nbsp; | &nbsp;
- | -
NullableConverter(Type, TypeConverterCache) | Creates a new ``CsvHelper.TypeConversion.NullableConverter`` for the given ``System.Nullable<T>`` ``System.Type`` .

## Properties
&nbsp; | &nbsp;
- | -
NullableType | Gets the type of the nullable.
UnderlyingType | Gets the underlying type of the nullable.
UnderlyingTypeConverter | Gets the type converter for the underlying type.

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Converts the string to an object.
ConvertToString(Object, IWriterRow, MemberMapData) | Converts the object to a string.
