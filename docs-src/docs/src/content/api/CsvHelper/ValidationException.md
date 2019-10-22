# ValidationException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents a user supplied validation failure.

```cs
[System.SerializableAttribute]
public abstract class ValidationException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ValidationException

## Constructors
&nbsp; | &nbsp;
- | -
ValidationException(ReadingContext) | Initializes a new instance of the ``CsvHelper.ValidationException`` class.
ValidationException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message.
ValidationException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
