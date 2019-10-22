# ConfigurationFunctions Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Holds the default callback methods for delegate members of ``CsvHelper.Configuration.Configuration`` .

```cs
public static class ConfigurationFunctions 
```

Inheritance Object -> ConfigurationFunctions

## Methods
&nbsp; | &nbsp;
- | -
BadDataFound(ReadingContext) | Throws a ``CsvHelper.BadDataException`` .
GetConstructor(Type) | Returns the type's constructor with the most parameters. If two constructors have the same number of parameters, then there is no guarantee which one will be returned. If you have that situation, you should probably implement this function yourself.
HeaderValidated(Boolean, String[], Int32, ReadingContext) | Throws a ``CsvHelper.ValidationException`` if ``isValid`` is ``false`` .
MissingFieldFound(String[], Int32, ReadingContext) | Throws a ``MissingFieldException`` .
PrepareHeaderForMatch(String, Int32) | Returns the ``header`` as given.
ReadingExceptionOccurred(CsvHelperException) | Throws the given ``exception`` .
ShouldQuote(String, WritingContext) | Returns true if the field contains a ``CsvHelper.Configuration.IWriterConfiguration.QuoteString`` , starts with a space, ends with a space, contains \r or \n, or contains the ``CsvHelper.Configuration.ISerializerConfiguration.Delimiter`` .
ShouldSkipRecord(String[]) | Returns ``false`` .
ShouldUseConstructorParameters(Type) | Returns ``true`` if ``type`` : 1. does not have a parameterless constructor 2. has a constructor 3. is not a user defined struct 4. is not an interface 5. TypeCode is not an Object.
