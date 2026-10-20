# Migrating from CsvHelper 26 to 27

CsvHelper 27 adds delimiter detection (`DetectDelimiter`, off by default), writing records from an
`IAsyncEnumerable<T>`, `Default(value, useOnConversionFailure: true)` in class maps to use the default when a field
fails to convert, and `TypeConverterCache.AddConverter(converter)` and `TypeConverterOptionsCache.AddOptions(options)`
to apply a converter or options to every registered type. The breaking changes are small: tab is no longer trimmed by
default, and the parser, writer and parser configuration interfaces get new members. The target frameworks don't
change; 27.2.0 adds a .NET 6 build.

## Trimming whitespace

`WhiteSpaceChars`, the characters that `TrimOptions` trims, is `[' ']` by default instead of `[' ', '\t']`. If you
trim fields and expect tabs to be trimmed too, add the tab back.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    TrimOptions = TrimOptions.Trim,
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    TrimOptions = TrimOptions.Trim,
    WhiteSpaceChars = new[] { ' ', '\t' },
};
```

- **New check:** the configuration check that runs when a parser or writer is created also rejects a delimiter, quote
  or escape character that is one of `WhiteSpaceChars`, with a `ConfigurationException`. So don't add the tab when
  tab is your delimiter.
- **Use 27.0.1 or later:** 27.0.0 has the new check but still has the tab in the default `WhiteSpaceChars`, so a tab
  delimiter throws there unless you set `WhiteSpaceChars` yourself. 27.0.1 removes the tab from the default.

## Writing an IAsyncEnumerable

`WriteRecordsAsync` has a new overload that takes an `IAsyncEnumerable<T>` (not in the .NET Framework 4.5 build), and
writes each record as the source produces it.

A type that implements both `IEnumerable<T>` and `IAsyncEnumerable<T>`, such as an Entity Framework Core `DbSet<T>`,
now matches two overloads, and the call doesn't compile (CS0121, the call is ambiguous). Cast it to the interface you
want to write from.

```csharp before
public class Item
{
    public int Id { get; set; }
}

public class Items : IEnumerable<Item>, IAsyncEnumerable<Item>
{
    private readonly List<Item> items = new List<Item>();

    public IEnumerator<Item> GetEnumerator() => items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<Item> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            yield return await Task.FromResult(item);
        }
    }
}

var items = new Items();
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
await csv.WriteRecordsAsync(items);
```

```csharp after
public class Item
{
    public int Id { get; set; }
}

public class Items : IEnumerable<Item>, IAsyncEnumerable<Item>
{
    private readonly List<Item> items = new List<Item>();

    public IEnumerator<Item> GetEnumerator() => items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<Item> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            yield return await Task.FromResult(item);
        }
    }
}

var items = new Items();
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
await csv.WriteRecordsAsync((IAsyncEnumerable<Item>)items);
```

## Custom implementations

These interfaces get new members, which a class that implements them adds:

| Interface | New member | Notes |
|---|---|---|
| `IParserConfiguration` | `bool DetectDelimiter { get; }` | `CsvConfiguration` returns `false`. |
| `IParserConfiguration` | `string[] DetectDelimiterValues { get; }` | `CsvConfiguration` returns comma, semicolon, vertical bar and tab. |
| `IParser` | `string Delimiter { get; }` | The delimiter in use, which can be the detected one. |
| `IWriter` | `Task WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)` | Not in the .NET Framework 4.5 build. |

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
    public char[] WhiteSpaceChars => new[] { ' ' };
    public bool ExceptionMessagesContainRawData => true;
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
    public bool DetectDelimiter => false; // New in 27.
    public string[] DetectDelimiterValues => new[] { ",", ";", "|", "\t" }; // New in 27.
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public char[] WhiteSpaceChars => new[] { ' ' };
    public bool ExceptionMessagesContainRawData => true;
}
```

A record that derives from `CsvConfiguration`, and a class that derives from `CsvParser` or `CsvWriter`, get the new
members from the base class.
