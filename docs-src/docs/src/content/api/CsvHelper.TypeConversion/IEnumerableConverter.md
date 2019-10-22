# IEnumerableConverter Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Converts an ``System.Collections.IEnumerable`` to and from a ``System.String`` .

```cs
public class IEnumerableConverter : DefaultTypeConverter, ITypeConverter
```

Inheritance Object -> DefaultTypeConverter -> IEnumerableConverter

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Converts the string to an object.
ConvertToString(Object, IWriterRow, MemberMapData) | Converts the object to a string.
