# CsvWriter Class

Namespace: [CsvHelper](/api/CsvHelper)

Used to write CSV files.

```cs
public class CsvWriter : IWriter, IWriterRow, IDisposable
```

Inheritance Object -> CsvWriter

## Constructors
&nbsp; | &nbsp;
- | -
CsvWriter(TextWriter) | Creates a new CSV writer using the given ``System.IO.TextWriter`` .
CsvWriter(ISerializer) | Creates a new CSV writer using the given ``CsvHelper.ISerializer`` .
CsvWriter(TextWriter, Boolean) | Creates a new CSV writer using the given ``System.IO.TextWriter`` .
CsvWriter(TextWriter, Configuration) | Creates a new CSV writer using the given ``System.IO.TextWriter`` .
CsvWriter(TextWriter, Configuration, Boolean) | Creates a new CSV writer using the given ``System.IO.TextWriter`` .

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets the configuration.
Context | Gets the writing context.

## Methods
&nbsp; | &nbsp;
- | -
CanWrite(MemberMap) | Checks if the member can be written.
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
Flush() | Serializes the row to the ``System.IO.TextWriter`` .
FlushAsync() | Serializes the row to the ``System.IO.TextWriter`` .
GetTypeForRecord&lt;T&gt;(T) | Gets the type for the record. If the generic type is an object due to boxing, it will call GetType() on the record itself.
NextRecord() | Ends writing of the current record and starts a new record. This automatically flushes the writer.
NextRecordAsync() | Ends writing of the current record and starts a new record. This automatically flushes the writer.
WriteComment(String) | Writes a comment.
WriteConvertedField(String) | Writes a field that has already been converted to a ``System.String`` from an ``CsvHelper.TypeConversion.ITypeConverter`` . If the field is null, it won't get written. A type converter will always return a string, even if field is null. If the converter returns a null, it means that the converter has already written data, and the returned value should not be written.
WriteDynamicHeader(IDynamicMetaObjectProvider) | Writes the header record for the given dynamic object.
WriteField(String) | Writes the field to the CSV file. The field may get quotes added to it. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField(String, Boolean) | Writes the field to the CSV file. This will ignore any need to quote and ignore ``CsvHelper.Configuration.Configuration.ShouldQuote`` and just quote based on the shouldQuote parameter. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T&gt;(T) | Writes the field to the CSV file. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T&gt;(T, ITypeConverter) | Writes the field to the CSV file. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T, TConverter&gt;(T) | Writes the field to the CSV file using the given ``CsvHelper.TypeConversion.ITypeConverter`` . When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteHeader&lt;T&gt;() | Writes the header record from the given members.
WriteHeader(Type) | Writes the header record from the given members.
WriteRecord&lt;T&gt;(T) | Writes the record to the CSV file.
WriteRecords(IEnumerable) | Writes the list of records to the CSV file.
WriteRecords&lt;T&gt;(IEnumerable&lt;T&gt;) | Writes the list of records to the CSV file.
