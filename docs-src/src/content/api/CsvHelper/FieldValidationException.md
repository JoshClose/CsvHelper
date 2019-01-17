# FieldValidationException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents a user supplied field validation failure.

```cs
public class FieldValidationException : ValidationException, ISerializable
```

Inheritance Object -> Exception -> CsvHelperException -> ValidationException -> FieldValidationException

## Constructors
&nbsp; | &nbsp;
- | -
FieldValidationException(ReadingContext, String) | Initializes a new instance of the ``CsvHelper.ValidationException`` class.
FieldValidationException(ReadingContext, String, String) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message.
FieldValidationException(ReadingContext, String, String, Exception) | Initializes a new instance of the ``CsvHelper.ValidationException`` class with a specified error message and a reference to the inner exception that is the cause of this exception.

## Properties
&nbsp; | &nbsp;
- | -
Field | Gets the field that failed validation.
