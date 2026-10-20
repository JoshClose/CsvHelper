# Migrating from CsvHelper 21 to 22

CsvHelper 22 renames `ParserMode` to `CsvMode`, because the writer now uses the mode too. `ShouldQuote` gets the type
of the field, so you can decide by type what to quote. Enums are read case-sensitively unless you set the new
`EnumIgnoreCase` option. The target frameworks don't change.

## CsvMode

`ParserMode` is renamed `CsvMode`, with the same members: `RFC4180`, `Escape` and `NoEscape`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = ParserMode.Escape,
    Escape = '\\',
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Mode = CsvMode.Escape,
    Escape = '\\',
};
```

The writer uses the mode now, so writing with `Escape` or `NoEscape` gives different output: see
[Behavior changes](#behavior-changes).

## Quoting

`ShouldQuote` gets the type of the field as its second parameter, before the writer. The default function,
`ConfigurationFunctions.ShouldQuote`, takes it too.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = (field, row) => true,
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = (field, fieldType, row) => true,
};
```

With the type, you can quote text and leave numbers and dates alone:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = (field, fieldType, row) => fieldType == typeof(string),
};
```

**When you write records, `fieldType` is `string`.** `WriteRecord` and `WriteRecords` pass `string` for every field,
except for `dynamic` records; `WriteField<T>` passes the type of the value. CsvHelper 24 passes the type of each
member (see [Migrating from 23 to 24](../v24.0.0/migration.md)).

## Enums

Enum names, and the names you give enum members with `[Name]`, are matched case-sensitively when reading. In 21, the
names were matched ignoring case. To keep that, set `EnumIgnoreCase` on the member map.

```csharp before
public enum Status
{
    Active,
    Closed,
}

public class Account
{
    public int Id { get; set; }
    public Status Status { get; set; }
}

public sealed class AccountMap : ClassMap<Account>
{
    public AccountMap()
    {
        Map(m => m.Id);
        Map(m => m.Status);
    }
}
```

```csharp after
public enum Status
{
    Active,
    Closed,
}

public class Account
{
    public int Id { get; set; }
    public Status Status { get; set; }
}

public sealed class AccountMap : ClassMap<Account>
{
    public AccountMap()
    {
        Map(m => m.Id);
        Map(m => m.Status).TypeConverterOption.EnumIgnoreCase();
    }
}
```

To ignore case for every member of type `Status`, set it on the type's options instead:
`csv.Context.TypeConverterOptionsCache.GetOptions<Status>().EnumIgnoreCase = true`.

**Use 22.1.0 or later.** In 22.0.0, the option doesn't reach the converter when reading, so it has no
effect. 22.1.0 fixes this and adds the `[EnumIgnoreCase]` attribute, for when you map with attributes.

## Configuration

`QuoteString` and `DoubleQuoteString` are removed from `CsvConfiguration`. Build them from `Quote` and `Escape`.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
var quote = config.QuoteString;
var escapedQuote = config.DoubleQuoteString;
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
var quote = config.Quote.ToString();
var escapedQuote = $"{config.Escape}{config.Quote}";
```

If you implement the configuration interfaces, add `ProcessFieldBufferSize` to `IParserConfiguration` and `Mode` to
`IWriterConfiguration`.

## Behavior changes

- **The writer uses `Mode`.** In 21 it ignored the mode and always quoted fields as RFC 4180 does. In 22,
  `CsvMode.Escape` puts `Escape` before each quote and before each character of a delimiter or `NewLine` in a field
  instead of quoting it (an `Escape` character in the field isn't escaped), and `CsvMode.NoEscape` writes fields as
  they are. In these two modes, the result of `ShouldQuote` isn't used.
- **The configuration is checked when it is used.** `Delimiter`, `Quote` and `Escape` checked their values when you
  set them, and threw a `ConfigurationException`. In 22, `CsvConfiguration.Validate()` checks them, with `NewLine`,
  when `CsvParser` or `CsvWriter` is created (and so `CsvReader`). Until 26.0.1, the exception has an empty message.
  Call `Validate()` yourself to check a configuration earlier.
