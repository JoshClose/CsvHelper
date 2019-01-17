# ParserException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur while parsing a CSV file.

```cs
[System.SerializableAttribute]
public class ParserException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ParserException

## Constructors
&nbsp; | &nbsp;
- | -
ParserException(ReadingContext) | Initializes a new instance of the ``CsvHelper.ParserException`` class.
ParserException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.ParserException`` class with a specified error message.
ParserException(ReadingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.ParserException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
