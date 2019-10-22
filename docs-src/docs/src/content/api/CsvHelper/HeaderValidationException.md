# HeaderValidationException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents a header validation failure.

```cs
public class HeaderValidationException : ValidationException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ValidationException -> HeaderValidationException

## Constructors
&nbsp; | &nbsp;
- | -
HeaderValidationException(ReadingContext, String[], Nullable&lt;Int32&gt;) | Initializes a new instance of the ``CsvHelper.ValidationException`` class.
HeaderValidationException(ReadingContext, String[], Nullable&lt;Int32&gt;, String) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message.
HeaderValidationException(ReadingContext, String[], Nullable&lt;Int32&gt;, String, Exception) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.

## Properties
&nbsp; | &nbsp;
- | -
HeaderNameIndex | Gets the header name index that is mapped to a CSV field that couldn't be found. The index is used when a CSV header has multiple header names with the same value.
HeaderNames | Gets the header names that are mapped to a CSV field that couldn't be found.
