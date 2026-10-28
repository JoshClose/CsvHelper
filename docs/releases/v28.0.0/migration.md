# Migrating from CsvHelper 27 to 28

CsvHelper 28 changes `ShouldSkipRecord`: the function gets the reader row instead of the record's fields, and it is
`null` by default, so it isn't called at all unless you set it. `CsvParser` and `CsvReader` take your own
`IParserConfiguration` and `IReaderConfiguration` implementations instead of only `CsvConfiguration`, so
`IParserConfiguration` gets the `CultureInfo` and the `Validate` method that the parser needs. The parser also caches
the fields it processes, and delimiter detection looks at every line it reads. The target frameworks don't change.

## Skipping records

`ShouldSkipRecordArgs.Record`, the record's fields as a `string[]`, is replaced by `ShouldSkipRecordArgs.Row`, the
`IReaderRow` being read. `args.Row.Parser.Record` has the fields that `args.Record` had, and you can also read single
fields from the row, such as `args.Row[0]`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = args => args.Record.All(string.IsNullOrWhiteSpace),
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = args => args.Row.Parser.Record.All(string.IsNullOrWhiteSpace),
};
```

`ConfigurationFunctions.ShouldSkipRecord`, the default function that returned `false`, is removed. Where you assigned
it, to reset the setting, assign `null`, which is the default now.

## Custom configuration classes

A class that implements `IParserConfiguration` can be passed to `new CsvParser(reader, configuration)`, and one that
implements `IReaderConfiguration` to `new CsvReader(reader, configuration)`; 27 took only a `CsvConfiguration`. The
parser needs two more members for that:

| Interface | Change |
|---|---|
| `IParserConfiguration` | New: `CultureInfo CultureInfo { get; }`, moved from `IReaderConfiguration`. |
| `IParserConfiguration` | New: `void Validate()`, which the parser calls when it is created. Throw a `ConfigurationException` for settings that can't work together. |
| `IReaderConfiguration` | `CultureInfo` comes from `IParserConfiguration` now, so a reader configuration only adds `Validate`. |
| `IReaderConfiguration` | `ShouldSkipRecord` can be `null`, which skips no records. |

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
    public bool DetectDelimiter => false;
    public string[] DetectDelimiterValues => new[] { ",", ";", "|", "\t" };
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public char[] WhiteSpaceChars => new[] { ' ' };
    public bool ExceptionMessagesContainRawData => true;
}
```

```csharp after
public class ParserConfiguration : IParserConfiguration
{
    public CultureInfo CultureInfo => CultureInfo.InvariantCulture; // New in 28.
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
    public bool DetectDelimiter => false;
    public string[] DetectDelimiterValues => new[] { ",", ";", "|", "\t" };
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public char[] WhiteSpaceChars => new[] { ' ' };
    public bool ExceptionMessagesContainRawData => true;

    public void Validate() // New in 28.
    {
        if (Delimiter == Quote.ToString())
        {
            throw new ConfigurationException("The delimiter and the quote can't be the same.");
        }
    }
}

using var parser = new CsvParser(reader, new ParserConfiguration());
```

## Behavior changes

- **`IParser.Record` is cached:** reading it twice for the same row gives the same array, until the next `Read`. If
  you change that array, copy it first, or the next read of `Record` for that row returns your changes.
- **`BadDataFound` is called once per bad field.** 27 called it each time the field was read, for example once for
  `GetField` and again for `Record`.
- **Delimiter detection** (`DetectDelimiter = true`) ignores text in quotes (or escaped text in `CsvMode.Escape`),
  only considers delimiters that are on every line it reads, and picks the culture's list separator when that is one
  of them. It can choose a different delimiter than 27 did for the same file.
- **No header, mapped names:** with `HasHeaderRecord = false`, a member mapped with `Name` is read by its index (its
  position in the map, unless you set `Index`). 27 threw a `ReaderException` saying there is no header record to
  find the name in.
- **Nullable annotations in 28.0.0 only:** 28.0.0 is built with nullable reference types enabled. In a project that
  enables them too, you get warnings for values such as `HeaderRecord` and `Parser.Record`, which are `string[]?`, and
  for `BadDataFound = null`. 28.0.1 builds without the annotations again, as 27 did.
