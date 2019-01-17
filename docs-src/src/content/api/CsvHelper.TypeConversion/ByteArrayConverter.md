# ByteArrayConverter Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Converts a ``Byte[]`` to and from a ``System.String`` .

```cs
public class ByteArrayConverter : DefaultTypeConverter, ITypeConverter
```

Inheritance Object -> DefaultTypeConverter -> ByteArrayConverter

## Constructors
&nbsp; | &nbsp;
- | -
ByteArrayConverter(ByteArrayConverterOptions) | Creates a new ByteArrayConverter using the given ``CsvHelper.TypeConversion.ByteArrayConverterOptions`` .

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Converts the string to an object.
ConvertToString(Object, IWriterRow, MemberMapData) | Converts the object to a string.
