# ITypeConverter Interface

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Converts objects to and from strings.

```cs
public interface ITypeConverter 
```

## Methods
&nbsp; | &nbsp;
- | -
ConvertFromString(String, IReaderRow, MemberMapData) | Converts the string to an object.
ConvertToString(Object, IWriterRow, MemberMapData) | Converts the object to a string.
