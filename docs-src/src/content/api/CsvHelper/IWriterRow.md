# IWriterRow Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to write a CSV row.

```cs
public interface IWriterRow 
```

## Properties
&nbsp; | &nbsp;
- | -
Configuration | Gets or sets the configuration.
Context | Gets the writing context.

## Methods
&nbsp; | &nbsp;
- | -
WriteComment(String) | Writes a comment.
WriteConvertedField(String) | Writes a field that has already been converted to a ``System.String`` from an ``CsvHelper.TypeConversion.ITypeConverter`` . If the field is null, it won't get written. A type converter will always return a string, even if field is null. If the converter returns a null, it means that the converter has already written data, and the returned value should not be written.
WriteField(String) | Writes the field to the CSV file. The field may get quotes added to it. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField(String, Boolean) | Writes the field to the CSV file. This will ignore any need to quote and ignore ``CsvHelper.Configuration.Configuration.ShouldQuote`` and just quote based on the shouldQuote parameter. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T&gt;(T) | Writes the field to the CSV file. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T&gt;(T, ITypeConverter) | Writes the field to the CSV file. When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteField&lt;T, TConverter&gt;(T) | Writes the field to the CSV file using the given ``CsvHelper.TypeConversion.ITypeConverter`` . When all fields are written for a record, ``CsvHelper.IWriter.NextRecord`` must be called to complete writing of the current record.
WriteHeader&lt;T&gt;() | Writes the header record from the given members.
WriteHeader(Type) | Writes the header record from the given members.
WriteRecord&lt;T&gt;(T) | Writes the record to the CSV file.
