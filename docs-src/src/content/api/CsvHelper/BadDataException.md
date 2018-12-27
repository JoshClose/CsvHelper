# BadDataException Class

Namespace: [CsvHelper](/api/CsvHelper)

Represents errors that occur due to bad data.

```cs
public class BadDataException : CsvHelper.CsvHelperException
```

Constructors | &nbsp;
- | -
CsvHelperException() | 
CsvHelperException([CsvHelper.ReadingContext](/api/CsvHelper/ReadingContext)) | 
CsvHelperException([CsvHelper.ReadingContext](/api/CsvHelper/ReadingContext), string) | 
CsvHelperException([CsvHelper.ReadingContext](/api/CsvHelper/ReadingContext), string, System.Exception) | 
CsvHelperException([CsvHelper.WritingContext](/api/CsvHelper/WritingContext)) | 
CsvHelperException([CsvHelper.WritingContext](/api/CsvHelper/WritingContext), string) | 
CsvHelperException([CsvHelper.WritingContext](/api/CsvHelper/WritingContext), string, System.Exception) | 
CsvHelperException(string) | 
CsvHelperException(string, System.Exception) | 

Properties | &nbsp;
- | -
[ReadingContext](/api/CsvHelper/ReadingContext) | Gets the context used when reading.
[WritingContext](/api/CsvHelper/WritingContext) | Gets the context used when writing.
