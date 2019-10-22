# WriterException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur while writing a CSV file.

```cs
[System.SerializableAttribute]
public class WriterException : CsvHelperException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> WriterException

## Constructors
&nbsp; | &nbsp;
- | -
WriterException(WritingContext) | Initializes a new instance of the ``CsvHelper.WriterException`` class.
WriterException(WritingContext, String) | Initializes a new instance of the ``CsvHelper.WriterException`` class with a specified error message.
WriterException(WritingContext, String, Exception) | Initializes a new instance of the ``CsvHelper.WriterException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.
