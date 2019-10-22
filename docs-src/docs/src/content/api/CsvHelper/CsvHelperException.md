# CsvHelperException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur in CsvHelper.

```cs
[System.SerializableAttribute]
public class CsvHelperException : Exception, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException

## Constructors
&nbsp; | &nbsp;
- | -
CsvHelperException(ReadingContext) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class.
CsvHelperException(WritingContext) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class.
CsvHelperException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class with a specified error message.
CsvHelperException(WritingContext, String) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class with a specified error message.
CsvHelperException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
CsvHelperException(WritingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.CsvHelperException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.

## Properties
&nbsp; | &nbsp;
- | -
ReadingContext | Gets the context used when reading.
WritingContext | Gets the context used when writing.
