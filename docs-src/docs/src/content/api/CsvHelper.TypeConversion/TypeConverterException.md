# TypeConverterException Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Represents errors that occur while reading a CSV file.

```cs
[System.SerializableAttribute]
public class TypeConverterException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> TypeConverterException

## Constructors
&nbsp; | &nbsp;
- | -
TypeConverterException(ITypeConverter, MemberMapData, String, ReadingContext) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class.
TypeConverterException(ITypeConverter, MemberMapData, Object, WritingContext) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class.
TypeConverterException(ITypeConverter, MemberMapData, String, ReadingContext, String) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class with a specified error message.
TypeConverterException(ITypeConverter, MemberMapData, Object, WritingContext, String) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class with a specified error message.
TypeConverterException(ITypeConverter, MemberMapData, String, ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
TypeConverterException(ITypeConverter, MemberMapData, Object, WritingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.TypeConversion.TypeConverterException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.

## Properties
&nbsp; | &nbsp;
- | -
MemberMapData | The member map data used in ConvertFromString and ConvertToString.
Text | The text used in ConvertFromString.
TypeConverter | The type converter.
Value | The value used in ConvertToString.
