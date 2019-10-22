# IFieldReader Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines methods used to read a field in a CSV file.

```cs
public interface IFieldReader : IDisposable
```

## Properties
&nbsp; | &nbsp;
- | -
Context | Gets the reading context.
IsBufferEmpty | Gets a value indicating if the buffer is empty. True if the buffer is empty, otherwise false.

## Methods
&nbsp; | &nbsp;
- | -
AppendField() | Appends the current reading progress.
FillBuffer() | Fills the buffer.
FillBufferAsync() | Fills the buffer asynchronously.
GetChar() | Gets the next char as an ``System.Int32`` .
GetField() | Gets the field. This will append any reading progress.
SetBufferPosition(Int32) | Move's the buffer position according to the given offset.
SetFieldEnd(Int32) | Sets the end of the field to the current buffer position.
SetFieldStart(Int32) | Sets the start of the field to the current buffer position.
SetRawRecordEnd(Int32) | Sets the raw record end to the current buffer position.
SetRawRecordStart(Int32) | Sets the raw recodr start to the current buffer position;
