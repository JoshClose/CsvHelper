# ReaderException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur while reading a CSV file.

```cs
[System.SerializableAttribute]
public class ReaderException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ReaderException

## Constructors
&nbsp; | &nbsp;
- | -
ReaderException(ReadingContext) | Initializes a new instance of the ``CsvHelper.ReaderException`` class.
ReaderException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.ReaderException`` class with a specified error message.
ReaderException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.ReaderException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
