# Type Conversion

When reading and writing a custom class will get converted to and from
a CSV row. Each CSV field can be converted to and from a class property.  
This conversion for class properties is done via type converters.  

There are many built in converters already available to you.  

CsvHelper Converter | C# type keyword | .NET Type
- | -
ArrayConverter | [ ] | System.Array
BigIntegerConverter | | System.Numerics.BigInteger
BooleanConverter | bool | System.Boolean
ByteArrayConverter | byte[ ] | System.Array
ByteConverter | byte | System.Byte
CharConverter | char | System.Char
CollectionGenericConverter | | System.Collections.Generic.Collection\<T\>, System.Collections.Generic.List\<T\>
DateOnlyConverter | | System.DateOnly
DateTimeConverter | | System.DateTime
DateTimeOffsetConverter | | System.DateTimeOffset
DecimalConverter | decimal | System.Decimal
DoubleConverter | double | System.Double
EnumConverter | enum | System.Enum
GuidConverter | | System.Guid
IDictionaryConverter | | System.Collections.Generic.Dictionary\<string, string\>
IDictionaryGenericConverter | | System.Collections.Generic.Dictionary\<TKey, TValue\>
IEnumerableConverter | | System.Collections.ICollection, System.Collections.IEnumerable, System.Collections.IList
IEnumerableGenericConverter | | System.Collections.Generic.ICollection\<T\>, System.Collections.Generic.IEnumerable\<T\>, System.Collections.Generic.IList\<T\>
Int16Converter | short | System.Int16
Int32Converter | int | System.Int32
Int64Converter | long | System.Int64
NullableConverter | | System.Nullable\<T\>
SByteConverter | sbyte | System.SByte
SingleConverter | float | System.Single
StringConverter | string | System.String
TimeOnlyConverter | | System.TimeOnly
UInt16Converter | ushort | System.UInt16
UInt32Converter | uint | System.UInt32
UInt64Converter | ulong | System.UInt64
UriConverter | | System.Uri
