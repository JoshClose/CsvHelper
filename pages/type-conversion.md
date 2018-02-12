# Type Conversion

CsvHelper uses type converters to convert a `string` into an `object`, and an `object` into a `string`.

## ITypeConverter
<hr/>

The interface that all type converters must implement.

```cs
public interface ITypeConverter
{
	string ConvertToString( object value, IWriterRow row, MemberMapData memberMapData );

	string ConvertFromString( string text, IReaderRow row, MemberMapData memberMapData );
}
```

### ConvertToString

Takes an `object` and converts it to a `string`.

Parameters
- value: The `object` to convert that can be a value or reference type.
- row: The `IWriterRow` to allow the converter to write, have access to the configuration, and context.
- memberMapData: The configured map data for the member that is being converted.

### ConvertFromString

Takes a `string` and converts it to an `object`.

Parameters
- text: The `string` to convert.
- row: The `IReaderRow` to allow the converter to read, have access to the configuration, and context.
- memberMapData: The configured map data for the member that is being converted.

## DefaultTypeConverter
<hr/>

The default converter that can be used as a base for other converters.

`ConvertToString` will `ToString` on the `object` using any formatting options that were set.

`ConvertFromString` will throw a `TypeConverterException`. Classes that implement `DefaultTypeConverter` need to override this method and provide an implementation for it's type.

## TypeConverterOptions
<hr/>

## TypeConverterCache
<hr/>

## TypeConverterOptionsCache
<hr/>

## Type Converters
<hr/>

These are the available converters. For the most part, they just mimic what the .NET type converters do, except these take into account the configuration and mapping settings.

- [ArrayConverter](#arrayconverter)
- [BooleanConverter](#booleanconverter)
- [ByteArrayConverter](#bytearrayconverter)
- [ByteArrayConverterOptions](#bytearrayconverteroptions)
- [ByteConverter](#byteconverter)
- [CharConverter](#charconverter)
- [CollectionGenericConverter](#collectiongenericconverter)
- [DateTimeConverter](#datetimeconverter)
- [DateTimeOffsetConverter](#datetimeoffsetconverter)
- [DecimalConverter](#decimalconverter)
- [DoubleConverter](#doubleconverter)
- [EnumConverter](#enumconverter)
- [EnumerableConverter](#enumerableconverter)
- [GuidConverter](#guidconverter)
- [IDictionaryConverter](#idictionaryconverter)
- [IDictionaryGenericConverter](#idictionarygenericconverter)
- [IEnumerableConverter](#ienumerableconverter)
- [IEnumerableGenericConverter](#ienumerablegenericconverter)
- [Int16Converter](#int16converter)
- [Int32Converter](#int32converter)
- [Int62Converter](#int64converter)
- [NullableConverter](#nullableconverter)
- [SByteConverter](#sbyteconverter)
- [SingleConverter](#singleconverter)
- [StringConverter](#stringconverter)
- [TimeSpanConverter](#timespanconverter)
- [UInt16Converter](#uint16converter)
- [UInt32Converter](#uint32converter)
- [UInt64Converter](#uint64converter)

### ArrayConverter

Converts multiple fields to and from an `Array` object member.

```cs
// Data
A,B,C,C,C
```

### BooleanConverter

### ByteArrayConverter

### ByteArrayConverterOptions

### ByteConverter

### CharConverter

### CollectionGenericConverter

### DateTimeConverter

### DateTimeOffsetConverter

### DecimalConverter

### DoubleConverter

### EnumConverter

### EnumerableConverter

### GuidConverter

### IDictionaryConverter

### IDictionaryGenericConverter

### IEnumerableConverter

### IEnumerableGenericConverter

### Int16Converter

### Int32Converter

### Int62Converter

### NullableConverter

### SByteConverter

### SingleConverter

### StringConverter

### TimeSpanConverter

### UInt16Converter

### UInt32Converter

### UInt64Converter

<br/>
