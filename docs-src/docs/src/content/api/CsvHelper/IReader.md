# IReader Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to read parsed data from a CSV file.

```cs
public interface IReader : IReaderRow, IDisposable
```

## Properties
&nbsp; | &nbsp;
- | -
Parser | Gets the parser.

## Methods
&nbsp; | &nbsp;
- | -
EnumerateRecords&lt;T&gt;(T) | Enumerates the records hydrating the given record instance with row data. The record instance is re-used and not cleared on each enumeration. This only works for streaming rows. If any methods are called on the projection that force the evaluation of the IEnumerable, such as ToList(), the entire list will contain the same instance of the record, which is the last row.
GetRecords&lt;T&gt;() | Gets all the records in the CSV file and converts each to ``System.Type`` T. The Read method should not be used when using this.
GetRecords&lt;T&gt;(T) | Gets all the records in the CSV file and converts each to ``System.Type`` T. The read method should not be used when using this.
GetRecords(Type) | Gets all the records in the CSV file and converts each to ``System.Type`` T. The Read method should not be used when using this.
Read() | Advances the reader to the next record. This will not read headers. You need to call ``CsvHelper.IReader.Read`` then ``CsvHelper.IReader.ReadHeader`` for the headers to be read.
ReadAsync() | Advances the reader to the next record. This will not read headers. You need to call ``CsvHelper.IReader.ReadAsync`` then ``CsvHelper.IReader.ReadHeader`` for the headers to be read.
ReadHeader() | Reads the header record without reading the first row.
