# MissingFieldException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents an error caused because a field is missing in the header while reading a CSV file.

```cs
[System.SerializableAttribute]
public class MissingFieldException : ReaderException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ReaderException -> MissingFieldException

## Constructors
&nbsp; | &nbsp;
- | -
MissingFieldException(ReadingContext) | Initializes a new instance of the ``CsvHelper.MissingFieldException`` class.
MissingFieldException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.MissingFieldException`` class with a specified error message.
MissingFieldException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.MissingFieldException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
