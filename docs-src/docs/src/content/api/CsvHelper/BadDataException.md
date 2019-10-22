# BadDataException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur due to bad data.

```cs
[System.SerializableAttribute]
public class BadDataException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> BadDataException

## Constructors
&nbsp; | &nbsp;
- | -
BadDataException(ReadingContext) | Initializes a new instance of the ``CsvHelper.BadDataException`` class.
BadDataException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.BadDataException`` class with a specified error message.
BadDataException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.BadDataException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
