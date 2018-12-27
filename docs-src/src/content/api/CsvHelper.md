# CsvHelper Namespace

## Classes
&nbsp; | &nbsp;
- | -
[BadDataException](/api/csvhelper/baddataexception) | Represents errors that occur due to bad data.
[CsvDataReader](/api/csvhelper/csvdatareader) | Provides a means of reading a CSV file forward-only by using CsvReader.
[CsvFieldReader](/api/csvhelper/csvfieldreader) | Reads fields from a System.IO.TextReader.
[CsvHelperException](/api/csvhelper/csvhelperexception) | Represents errors that occur in CsvHelper.
[CsvParser](/api/csvhelper/csvparser) | Parses a CSV file.
[CsvReader](/api/csvhelper/csvreader) | Reads data that was parsed from CsvHelper.IParser.
[CsvSerializer](/api/csvserializer) | Defines methods used to serialize data into a CSV file.
[CsvWriter](/api/csvhelper/csvwriter) | Used to write CSV files.
[Factory](/api/csvhelper/factory) | Creates CsvHelper classes.
[FieldValidationException](/api/csvhelper/fieldvalidationexception) | Represents a user supplied field validation failure.
[HeaderValidationException](/api/csvhelper/headervalidationexception) | Represents a header validation failure.
[MissingFieldException](/api/csvhelper/missingfieldexception) | Represents an error caused because a field is missing in the header while reading a CSV file.
[ObjectResolver](/api/csvhelper/objectresolver) | Creates objects from a given type.
[ParserException](/api/csvhelper/parserexception) | Represents errors that occur while parsing a CSV file.
[ReaderException](/api/csvhelper/readerexception) | Represents errors that occur while reading a CSV file.
[ReadingContext](/api/csvhelper/readingexception) | CSV reading state.
[RecordBuilder](/api/csvhelper/recordbuilder) | Builds CSV records.
[ValidationException](/api/csvhelper/validationexception) | Represents a user supplied validation failure.
[WriterException](/api/csvhelper/writerexception) | Represents errors that occur while writing a CSV file.
[WritingContext](/api/csvhelper/writingcontext) | CSV writing state.

## Interfaces
&nbsp; | &nbsp;
- | -
[IFactory](/api/csvhelper/ifactory) | Defines methods used to create CsvHelper classes.
[IFieldReader](/api/csvhelper/ifieldreader) | Defines methods used to read a field in a CSV file.
[IObjectResolver](/api/csvhelper/iobjectresolver) | Defines the functionality of a class that creates objects from a given type.
[IParser](/api/csvhelper/iparser) | Defines methods used the parse a CSV file.
[IReader](/api/csvhelper/ireader) | Defines methods used to read parsed data from a CSV file.
[IReaderRow](/api/csvhelper/ireaderrow) | Defines methods used to read parsed data from a CSV file row.
[ISerializer](/api/csvhelper/iserializer) | Defines methods used to serialize data into a CSV file.
[IWriter](/api/csvhelper/iwriter) | Defines methods used to write to a CSV file.
[IWriterRow](/api/csvhelper/iwriterrow) | Defines methods used to write a CSV row.

## Enums
&nbsp; | &nbsp;
- | -
[Caches](/api/csvhelper/caches) | Types of caches.
