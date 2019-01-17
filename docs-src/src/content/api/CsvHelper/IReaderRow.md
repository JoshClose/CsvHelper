# IReaderRow Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to read parsed data from a CSV file row.

```cs
[System.Reflection.DefaultMemberAttribute]
public interface IReaderRow 
```

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets or sets the configuration.
Context | Gets the reading context.
this[Int32] | Gets the raw field at position (column) index.
this[String] | Gets the raw field at position (column) name.
this[String, Int32] | Gets the raw field at position (column) name.

## Methods
&nbsp; | &nbsp;
- | -
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
GetRecord&lt;T&gt;() | Gets the record converted into ``System.Type`` T.
GetRecord&lt;T&gt;(T) | Get the record converted into ``System.Type`` T.
GetRecord(Type) | Gets the record.
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
