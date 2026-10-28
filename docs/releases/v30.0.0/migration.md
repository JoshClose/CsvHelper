# Migrating from CsvHelper 29 to 30

CsvHelper 30 moves `LeaveOpen` from the configuration to the constructors of `CsvParser`, `CsvReader` and `CsvWriter`,
and `CsvWriter` takes any `IWriterConfiguration` instead of only a `CsvConfiguration`. `BadDataException` carries the
bad field and the raw record, `ValidateArgs` carries the row being read, and `WriteRecord` no longer writes the header
of a dynamic record. New: `MaxFieldSize`, configuration from attributes on a class
(`new CsvConfiguration(culture, typeof(T))`), header prefixes that include their parent's, constructor arguments for
`[TypeConverter]`, and a message function for `Validate`. Use 30.0.1 or later: 30.0.0 ignores `leaveOpen` in a
`CsvWriter` constructor and lacks a `CsvReader` constructor that takes it (see below). 30.0.0 keeps the target
frameworks of 29. 30.0.3 replaces the .NET Framework 4.5 build with 4.6.2 and the .NET 5 build with .NET 7 and 8, and
adds .NET Framework 4.8; 30.1.0 adds a dependency on the System.Linq.Async package, which 31.0.2 removes again.

## Leaving the reader or writer open

`CsvConfiguration.LeaveOpen` is removed. Pass `leaveOpen` to the constructor instead.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    LeaveOpen = true,
};

using (var csv = new CsvReader(reader, config))
{
    // ...
}

using (var csv = new CsvWriter(writer, config))
{
    // ...
}
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
};

using (var csv = new CsvReader(new CsvParser(reader, config, leaveOpen: true)))
{
    // ...
}

using (var csv = new CsvWriter(writer, config, leaveOpen: true))
{
    // ...
}
```

| CsvHelper 29, with `LeaveOpen = true` | CsvHelper 30 |
|---|---|
| `new CsvParser(reader, config)` | `new CsvParser(reader, config, leaveOpen: true)` |
| `new CsvReader(reader, config)` | `new CsvReader(new CsvParser(reader, config, leaveOpen: true))`, or `new CsvReader(reader, config, leaveOpen: true)` from 30.0.1 |
| `new CsvWriter(writer, config)` | `new CsvWriter(writer, config, leaveOpen: true)` |

- **Don't just delete the line:** without `leaveOpen: true`, disposing the `CsvReader` or `CsvWriter` closes your
  `TextReader` or `TextWriter`, which `LeaveOpen = true` prevented.
- **30.0.0:** `new CsvReader(reader, config, leaveOpen: true)` doesn't exist until 30.0.1, and
  `new CsvWriter(writer, culture, leaveOpen: true)` ignores `leaveOpen` in 30.0.0. 30.0.1 adds the one and fixes the
  other. The code above works in every 30 release.

## Bad data exceptions

`BadDataException` has two new read-only fields, `Field` and `RawRecord`, and every constructor takes them first. If
you throw it yourself, for example from `BadDataFound`, pass them from the args.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args => throw new BadDataException(args.Context, $"Bad data in '{args.Field}'."),
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args => throw new BadDataException(
        args.Field, args.RawRecord, args.Context, $"Bad data in '{args.Field}'."),
};
```

- **Reading them:** catch `BadDataException` and use `ex.Field` and `ex.RawRecord`. For the inconsistent column
  count error of `DetectColumnCountChanges`, `Field` is empty and `RawRecord` is the record.
- **Raw data:** `Field` and `RawRecord` hold the data even when `ExceptionMessagesContainRawData` is `false`, which
  only keeps it out of the message.

## Validation

`ValidateArgs` has the row being read, `Row`, so a validation can look at other fields. Its constructor takes the row
too, which matters when you call a validation function yourself, as in a test.

```csharp before
Validate validate = args => !string.IsNullOrEmpty(args.Field);

var isValid = validate(new ValidateArgs("1"));
```

```csharp after
Validate validate = args => !string.IsNullOrEmpty(args.Field);

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var isValid = validate(new ValidateArgs("1", csv));
```

`Validate` in a class map also takes a function that makes the exception message. Without it the message is
`Field '...' is not valid.`

```csharp
public class Foo
{
    public int Id { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.Id).Validate(
            args => args.Field.Length > 0,
            args => $"Id is empty on row {args.Row.Parser.Row}.");
    }
}
```

## Writing dynamic records

`WriteRecord` no longer writes the header of a dynamic record. 29 wrote it before the first dynamic record when
`HasHeaderRecord` was `true`; now you write it with `WriteDynamicHeader`. `WriteRecords` still writes the header.

```csharp before
dynamic item = new System.Dynamic.ExpandoObject();
item.Id = 1;
item.Name = "one";

using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
csv.WriteRecord(item);
csv.NextRecord();
```

```csharp after
dynamic item = new System.Dynamic.ExpandoObject();
item.Id = 1;
item.Name = "one";

using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
csv.WriteDynamicHeader(item);
csv.NextRecord();
csv.WriteRecord(item);
csv.NextRecord();
```

`WriteRecord` also doesn't mark the header as written any more. In 29, after a `WriteRecord`, a `WriteRecords` on the
same writer wrote no header; in 30 it writes one unless you wrote a header yourself.

## Custom configuration classes

`CsvWriter` takes an `IWriterConfiguration`, so you can pass your own implementation; 29 took only a
`CsvConfiguration`. The interfaces change with the `LeaveOpen` move and the new settings:

| Interface | Change |
|---|---|
| `IWriterConfiguration` | Removed: `bool LeaveOpen { get; }`. |
| `IWriterConfiguration` | New: `int BufferSize { get; }`, the size of the writer's buffer (`CsvConfiguration` uses `0x1000`). |
| `IWriterConfiguration` | New: `void Validate()`, which the writer calls when it is created. |
| `IParserConfiguration` | Removed: `bool LeaveOpen { get; }`. |
| `IParserConfiguration` | New: `double MaxFieldSize { get; }`. `0`, the default, means no limit; a longer field throws a `MaxFieldSizeException`. |

A class that still has a `LeaveOpen` property compiles, but nothing reads it: pass `leaveOpen` to the constructor.

```csharp before
public class WriterConfiguration : IWriterConfiguration
{
    public CsvMode Mode => CsvMode.RFC4180;
    public bool LeaveOpen => true;
    public string Delimiter => ",";
    public char Quote => '"';
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public InjectionOptions InjectionOptions => InjectionOptions.None;
    public char[] InjectionCharacters => new[] { '=', '@', '+', '-', '\t', '\r' };
    public char InjectionEscapeCharacter => '\'';
    public string NewLine => "\r\n";
    public bool IsNewLineSet => false;
    public ShouldQuote ShouldQuote => ConfigurationFunctions.ShouldQuote;
    public CultureInfo CultureInfo => CultureInfo.InvariantCulture;
    public bool AllowComments => false;
    public char Comment => '#';
    public bool HasHeaderRecord => true;
    public bool IgnoreReferences => false;
    public bool IncludePrivateMembers => false;
    public ReferenceHeaderPrefix ReferenceHeaderPrefix => null;
    public MemberTypes MemberTypes => MemberTypes.Properties;
    public bool UseNewObjectForNullReferenceMembers => true;
    public IComparer<string> DynamicPropertySort => null;
    public bool ExceptionMessagesContainRawData => true;
}
```

```csharp after
public class WriterConfiguration : IWriterConfiguration
{
    public int BufferSize => 0x1000; // New in 30.
    public CsvMode Mode => CsvMode.RFC4180;
    public string Delimiter => ",";
    public char Quote => '"';
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public InjectionOptions InjectionOptions => InjectionOptions.None;
    public char[] InjectionCharacters => new[] { '=', '@', '+', '-', '\t', '\r' };
    public char InjectionEscapeCharacter => '\'';
    public string NewLine => "\r\n";
    public bool IsNewLineSet => false;
    public ShouldQuote ShouldQuote => ConfigurationFunctions.ShouldQuote;
    public CultureInfo CultureInfo => CultureInfo.InvariantCulture;
    public bool AllowComments => false;
    public char Comment => '#';
    public bool HasHeaderRecord => true;
    public bool IgnoreReferences => false;
    public bool IncludePrivateMembers => false;
    public ReferenceHeaderPrefix ReferenceHeaderPrefix => null;
    public MemberTypes MemberTypes => MemberTypes.Properties;
    public bool UseNewObjectForNullReferenceMembers => true;
    public IComparer<string> DynamicPropertySort => null;
    public bool ExceptionMessagesContainRawData => true;

    public void Validate() // New in 30.
    {
        if (Delimiter == Quote.ToString())
        {
            throw new ConfigurationException("The delimiter and the quote can't be the same.");
        }
    }
}

using var csv = new CsvWriter(writer, new WriterConfiguration(), leaveOpen: true);
```

## Behavior changes

- **Records enumerated twice:** when `WriteRecords` or `WriteRecordsAsync` takes the header from a record (`dynamic`
  or `object` records, or the non-generic `WriteRecords(IEnumerable)`), it goes over the records once to find the
  header and again to write them, so a query behind them runs twice. 29 went over them once, and so does 32.0.2.
- **Header with the non-generic `WriteRecords`:** in 30.0.0 to 30.0.3, `WriteRecords(IEnumerable)` writes a header
  even when `HasHeaderRecord` is `false`, and again on each call. 30.1.0 fixes it.
