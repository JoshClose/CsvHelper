# EnumConverter Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Converts an ``System.Enum`` to and from a ``System.String`` .

```cs
public class EnumConverter : DefaultTypeConverter, ITypeConverter
```

Inheritance Object -> DefaultTypeConverter -> EnumConverter

## Constructors
&nbsp; | &nbsp;
- | -
EnumConverter(Type) | Creates a new ``CsvHelper.TypeConversion.EnumConverter`` for the given ``System.Enum`` ``System.Type`` .

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Converts the string to an object.
