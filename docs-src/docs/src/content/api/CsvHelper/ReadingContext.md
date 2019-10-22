# ReadingContext Class

Namespace: [CsvHelper](/api/CsvHelper)

CSV reading state.

```cs
public class ReadingContext : IDisposable
```

Inheritance Object -> ReadingContext

## Constructors
&nbsp; | &nbsp;
- | -
ReadingContext(TextReader, Configuration, Boolean) | Initializes a new instance.

## Fields
&nbsp; | &nbsp;
- | -
Buffer | Gets the buffer used to store data from the ``CsvHelper.ReadingContext.Reader`` .
BufferPosition | Gets the buffer position.
BytePosition | Gets the byte position.
CharPosition | Gets the character position.
CharsRead | Gets the number of characters read from the ``CsvHelper.ReadingContext.Reader`` .
ColumnCount | Gets the column count.
CreateRecordFuncs | Gets the create record functions.
CurrentIndex | Gets the current index.
FieldBuilder | Gets the field builder.
FieldEndPosition | Gets the field end position.
FieldStartPosition | Gets the field start position.
HasBeenRead | Gets a value indicating if reading has begun.
HeaderRecord | Gets the header record.
HydrateRecordActions | Gets the hydrate record actions.
IsFieldBad | Gets a value indicating if the field is bad. True if the field is bad, otherwise false. A field is bad if a quote is found in a field that isn't escaped.
LeaveOpen | Gets a value indicating if the ``CsvHelper.ReadingContext.Reader`` should be left open when disposing.
NamedIndexCache | Getse the named indexes cache.
NamedIndexes | Gets the named indexes.
RawRecordBuilder | Gets the raw record builder.
RawRecordEndPosition | Gets the raw record end position.
RawRecordStartPosition | Gets the raw record start position.
RawRow | Gets the row of the CSV file that the parser is currently on. This is the actual file row.
Reader | Gets the ``System.IO.TextReader`` that is read from.
Record | Gets the record.
RecordBuilder | Gets the record builder.
ReusableMemberMapData | Gets the reusable member map data.
Row | Gets the row of the CSV file that the parser is currently on.
TypeConverterOptionsCache | Gets the type converter options cache.

## Properties
&nbsp; | &nbsp;
- | -
Field | Gets the field.
ParserConfiguration | Gets the ``CsvHelper.CsvParser`` configuration.
RawRecord | Gets all the characters of the record including quotes, delimeters, and line endings.
ReaderConfiguration | Gets the ``CsvHelper.CsvReader`` configuration.

## Methods
&nbsp; | &nbsp;
- | -
ClearCache(Caches) | Clears the specified caches.
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
