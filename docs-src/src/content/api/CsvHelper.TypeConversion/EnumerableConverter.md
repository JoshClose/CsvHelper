# EnumerableConverter Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Throws an exception when used. This is here so that it's apparent that there is no support for ``System.Collections.IEnumerable`` type coversion. A custom converter will need to be created to have a field convert to and from an IEnumerable.

```cs
public class EnumerableConverter : DefaultTypeConverter, ITypeConverter
```

Inheritance Object -> DefaultTypeConverter -> EnumerableConverter

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Throws an exception.
ConvertToString(Object, IWriterRow, MemberMapData) | Throws an exception.
