# CsvFieldReader Class

Namespace: [CsvHelper](/api/CsvHelper)

Reads fields from a ``System.IO.TextReader`` .

```cs
public class CsvFieldReader : IFieldReader, IDisposable
```

Inheritance Object -> CsvFieldReader

## Constructors
&nbsp; | &nbsp;
- | -
CsvFieldReader(TextReader, Configuration) | Creates a new ``CsvHelper.CsvFieldReader`` using the given ``System.IO.TextReader`` and ``CsvHelper.Configuration.Configuration`` .
CsvFieldReader(TextReader, Configuration, Boolean) | Creates a new ``CsvHelper.CsvFieldReader`` using the given ``System.IO.TextReader`` , ``CsvHelper.Configuration.Configuration`` and leaveOpen flag.

## Properties
&nbsp; | &nbsp;
- | -
Context | Gets the reading context.
IsBufferEmpty | Gets a value indicating if the buffer is empty. True if the buffer is empty, otherwise false.

## Methods
&nbsp; | &nbsp;
- | -
AppendField() | Appends the current reading progress.
Dispose() | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
FillBuffer() | Fills the buffer.
FillBufferAsync() | Fills the buffer.
GetChar() | Gets the next char as an ``System.Int32`` .
GetField() | Gets the field. This will append any reading progress.
SetBufferPosition(Int32) | Move's the buffer position according to the given offset.
SetFieldEnd(Int32) | Sets the end of the field to the current buffer position.
SetFieldStart(Int32) | Sets the start of the field to the current buffer position.
SetRawRecordEnd(Int32) | Sets the raw record end to the current buffer position.
SetRawRecordStart(Int32) | Sets the raw recodr start to the current buffer position;
