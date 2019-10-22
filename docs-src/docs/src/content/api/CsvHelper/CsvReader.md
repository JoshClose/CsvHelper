# CsvReader Class

Namespace: [CsvHelper](/api/CsvHelper)

Reads data that was parsed from ``CsvHelper.IParser`` .

```cs
[System.Reflection.DefaultMemberAttribute]
public class CsvReader : IReader, IReaderRow, IDisposable
```

Inheritance Object -> CsvReader

## Constructors
&nbsp; | &nbsp;
- | -
CsvReader(TextReader) | Creates a new CSV reader using the given ``System.IO.TextReader`` .
CsvReader(IParser) | Creates a new CSV reader using the given ``CsvHelper.IParser`` .
CsvReader(TextReader, Boolean) | Creates a new CSV reader using the given ``System.IO.TextReader`` .
CsvReader(TextReader, Configuration) | Creates a new CSV reader using the given ``System.IO.TextReader`` and ``CsvHelper.Configuration.Configuration`` and ``CsvHelper.CsvParser`` as the default parser.
CsvReader(TextReader, Configuration, Boolean) | Creates a new CSV reader using the given ``System.IO.TextReader`` .

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the reading context.
this[Int32] | Gets the raw field at position (column) index.
this[String] | Gets the raw field at position (column) name.
this[String, Int32] | Gets the raw field at position (column) name.
Parser | Gets the parser.

## Methods
&nbsp; | &nbsp;
- | -
CanRead(MemberMap) | Determines if the member for the ``CsvHelper.Configuration.MemberMap`` can be read.
CanRead(MemberReferenceMap) | Determines if the member for the ``CsvHelper.Configuration.MemberReferenceMap`` can be read.
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
EnumerateRecords&lt;T&gt;(T) | Enumerates the records hydrating the given record instance with row data. The record instance is re-used and not cleared on each enumeration. This only works for streaming rows. If any methods are called on the projection that force the evaluation of the IEnumerable, such as ToList(), the entire list will contain the same instance of the record, which is the last row.
GetField(Int32) | Gets the raw field at position (column) index.
GetField(String) | Gets the raw field at position (column) name.
GetField(String, Int32) | Gets the raw field at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
GetField(Type, Int32) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField(Type, String) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField(Type, String, Int32) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField(Type, Int32, ITypeConverter) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField(Type, String, ITypeConverter) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField(Type, String, Int32, ITypeConverter) | Gets the field converted to ``System.Object`` using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField&lt;T&gt;(Int32) | Gets the field converted to ``System.Type`` T at position (column) index.
GetField&lt;T&gt;(String) | Gets the field converted to ``System.Type`` T at position (column) name.
GetField&lt;T&gt;(String, Int32) | Gets the field converted to ``System.Type`` T at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
GetField&lt;T&gt;(Int32, ITypeConverter) | Gets the field converted to ``System.Type`` T at position (column) index using the given ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField&lt;T&gt;(String, ITypeConverter) | Gets the field converted to ``System.Type`` T at position (column) name using the given ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField&lt;T&gt;(String, Int32, ITypeConverter) | Gets the field converted to ``System.Type`` T at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
GetField&lt;T, TConverter&gt;(Int32) | Gets the field converted to ``System.Type`` T at position (column) index using the given ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField&lt;T, TConverter&gt;(String) | Gets the field converted to ``System.Type`` T at position (column) name using the given ``CsvHelper.TypeConversion.ITypeConverter`` .
GetField&lt;T, TConverter&gt;(String, Int32) | Gets the field converted to ``System.Type`` T at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
GetFieldIndex(String, Int32, Boolean) | Gets the index of the field at name if found.
GetFieldIndex(String[], Int32, Boolean, Boolean) | Gets the index of the field at name if found.
GetRecord&lt;T&gt;() | Gets the record converted into ``System.Type`` T.
GetRecord&lt;T&gt;(T) | Get the record converted into ``System.Type`` T.
GetRecord(Type) | Gets the record.
GetRecords&lt;T&gt;() | Gets all the records in the CSV file and converts each to ``System.Type`` T. The Read method should not be used when using this.
GetRecords&lt;T&gt;(T) | Gets all the records in the CSV file and converts each to ``System.Type`` T. The read method should not be used when using this.
GetRecords(Type) | Gets all the records in the CSV file and converts each to ``System.Type`` T. The Read method should not be used when using this.
Read() | Advances the reader to the next record. This will not read headers. You need to call ``CsvHelper.CsvReader.Read`` then ``CsvHelper.CsvReader.ReadHeader`` for the headers to be read.
ReadAsync() | Advances the reader to the next record. This will not read headers. You need to call ``CsvHelper.CsvReader.ReadAsync`` then ``CsvHelper.CsvReader.ReadHeader`` for the headers to be read.
ReadHeader() | Reads the header record without reading the first row.
TryGetField(Type, Int32, out Object) | Gets the field converted to ``System.Type`` T at position (column) index.
TryGetField(Type, String, out Object) | Gets the field converted to ``System.Type`` T at position (column) name.
TryGetField(Type, String, Int32, out Object) | Gets the field converted to ``System.Type`` T at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
TryGetField(Type, Int32, ITypeConverter, out Object) | Gets the field converted to ``System.Type`` T at position (column) index using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField(Type, String, ITypeConverter, out Object) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField(Type, String, Int32, ITypeConverter, out Object) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T&gt;(Int32, out T) | Gets the field converted to ``System.Type`` T at position (column) index.
TryGetField&lt;T&gt;(String, out T) | Gets the field converted to ``System.Type`` T at position (column) name.
TryGetField&lt;T&gt;(String, Int32, out T) | Gets the field converted to ``System.Type`` T at position (column) name and the index instance of that field. The index is used when there are multiple columns with the same header name.
TryGetField&lt;T&gt;(Int32, ITypeConverter, out T) | Gets the field converted to ``System.Type`` T at position (column) index using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T&gt;(String, ITypeConverter, out T) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T&gt;(String, Int32, ITypeConverter, out T) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T, TConverter&gt;(Int32, out T) | Gets the field converted to ``System.Type`` T at position (column) index using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T, TConverter&gt;(String, out T) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
TryGetField&lt;T, TConverter&gt;(String, Int32, out T) | Gets the field converted to ``System.Type`` T at position (column) name using the specified ``CsvHelper.TypeConversion.ITypeConverter`` .
ValidateHeader&lt;T&gt;() | Validates the header. A header is bad if all the mapped members don't match. If the header is not valid, a ``CsvHelper.ValidationException`` will be thrown.
ValidateHeader(Type) | Validates the header. A header is bad if all the mapped members don't match. If the header is not valid, a ``CsvHelper.ValidationException`` will be thrown.
