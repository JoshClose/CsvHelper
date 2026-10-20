# Migrating from CsvHelper 25 to 26

CsvHelper 26 undoes the `init` properties of 25: they caused compiler problems for too many people. The args of the
configuration functions have constructors with parameters again, and their members are read-only fields. It also adds
`ExceptionMessagesContainRawData`, to keep CSV data out of exception messages, which adds a property to the
configuration interfaces. Code that only receives args in a callback doesn't change; you are affected if you create
args yourself or implement `IParserConfiguration` or `IWriterConfiguration`. The target frameworks don't change.

## Delegate args

Create the args with their constructor instead of an object initializer.

```csharp before
PrepareHeaderForMatch prepareHeader = args => args.Header.Trim().ToLower();

var header = prepareHeader(new PrepareHeaderForMatchArgs
{
    Header = " Name ",
    FieldIndex = 0,
});
```

```csharp after
PrepareHeaderForMatch prepareHeader = args => args.Header.Trim().ToLower();

var header = prepareHeader(new PrepareHeaderForMatchArgs(" Name ", 0));
```

| CsvHelper 25 | CsvHelper 26 |
|---|---|
| `new BadDataFoundArgs { Field, RawRecord, Context }` | `new BadDataFoundArgs(field, rawRecord, context)` |
| `new ConvertFromStringArgs { Row }` | `new ConvertFromStringArgs(row)` |
| `new ConvertToStringArgs<T> { Value }` | `new ConvertToStringArgs<T>(value)` |
| `new GetConstructorArgs { ClassType }` | `new GetConstructorArgs(classType)` |
| `new GetDynamicPropertyNameArgs { FieldIndex, Context }` | `new GetDynamicPropertyNameArgs(fieldIndex, context)` |
| `new HeaderValidatedArgs { InvalidHeaders, Context }` | `new HeaderValidatedArgs(invalidHeaders, context)` |
| `new MissingFieldFoundArgs { HeaderNames, Index, Context }` | `new MissingFieldFoundArgs(headerNames, index, context)` |
| `new PrepareHeaderForMatchArgs { Header, FieldIndex }` | `new PrepareHeaderForMatchArgs(header, fieldIndex)` |
| `new ReadingExceptionOccurredArgs { Exception }` | `new ReadingExceptionOccurredArgs(exception)` |
| `new ReferenceHeaderPrefixArgs { MemberType, MemberName }` | `new ReferenceHeaderPrefixArgs(memberType, memberName)` |
| `new ShouldQuoteArgs { Field, FieldType, Row }` | `new ShouldQuoteArgs(field, fieldType, row)` |
| `new ShouldSkipRecordArgs { Record }` | `new ShouldSkipRecordArgs(record)` |
| `new ShouldUseConstructorParametersArgs { ParameterType }` | `new ShouldUseConstructorParametersArgs(parameterType)` |
| `new ValidateArgs { Field }` | `new ValidateArgs(field)` |

- **From 24:** the constructors are the ones 24 had, except `BadDataFoundArgs`, which takes the raw record as its
  second parameter.
- **Fields, not properties:** reading a member, such as `args.Field`, compiles as before, but an assembly built
  against 25 that reads the args has to be rebuilt against 26.
- **`CsvParser.ProcessedField`**, the struct that `CsvParser` subclasses get from the `Process...Field` methods, is
  created with `new ProcessedField(start, length, buffer)` too.

## Raw data in exception messages

Exception messages show the raw record, and a `TypeConverterException` shows the text it couldn't convert. If your
CSV files hold data that must not end up in logs, set `ExceptionMessagesContainRawData` to `false`: the messages say
the data is hidden instead. It is `true` by default, so messages are as before unless you change it.

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ExceptionMessagesContainRawData = false,
};
using var csv = new CsvReader(reader, config);
```

## Custom configuration classes

`IParserConfiguration` and `IWriterConfiguration` each get the new property:
`bool ExceptionMessagesContainRawData { get; }`. A class that implements either of them adds it.

```csharp before
public class ParserConfiguration : IParserConfiguration
{
    public bool CacheFields => false;
    public bool LeaveOpen => false;
    public string NewLine => "\r\n";
    public bool IsNewLineSet => false;
    public CsvMode Mode => CsvMode.RFC4180;
    public int BufferSize => 0x1000;
    public int ProcessFieldBufferSize => 1024;
    public bool CountBytes => false;
    public Encoding Encoding => Encoding.UTF8;
    public BadDataFound BadDataFound => ConfigurationFunctions.BadDataFound;
    public bool LineBreakInQuotedFieldIsBadData => false;
    public char Comment => '#';
    public bool AllowComments => false;
    public bool IgnoreBlankLines => true;
    public char Quote => '"';
    public string Delimiter => ",";
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public char[] WhiteSpaceChars => new[] { ' ', '\t' };
}
```

```csharp after
public class ParserConfiguration : IParserConfiguration
{
    public bool CacheFields => false;
    public bool LeaveOpen => false;
    public string NewLine => "\r\n";
    public bool IsNewLineSet => false;
    public CsvMode Mode => CsvMode.RFC4180;
    public int BufferSize => 0x1000;
    public int ProcessFieldBufferSize => 1024;
    public bool CountBytes => false;
    public Encoding Encoding => Encoding.UTF8;
    public BadDataFound BadDataFound => ConfigurationFunctions.BadDataFound;
    public bool LineBreakInQuotedFieldIsBadData => false;
    public char Comment => '#';
    public bool AllowComments => false;
    public bool IgnoreBlankLines => true;
    public char Quote => '"';
    public string Delimiter => ",";
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public char[] WhiteSpaceChars => new[] { ' ', '\t' };
    public bool ExceptionMessagesContainRawData => true; // New in 26.
}
```

A class that implements `IReaderConfiguration` adds it too, because `IReaderConfiguration` extends
`IParserConfiguration`. A record that derives from `CsvConfiguration` already has it.

## Behavior changes

- **Header record in exception messages:** the details of a `CsvHelperException` thrown while reading list the
  header record. 25 printed `[""]` instead, by mistake.
