# Migrating from CsvHelper 28 to 29

CsvHelper 29 replaces the `SanitizeForInjection` flag with `InjectionOptions`, which can escape, strip or reject the
characters that make a spreadsheet run a field as a formula; escaping now follows the OWASP recommendation, so the
output changes. Delimiter detection moves into a configuration function, `GetDelimiter`, that you can replace. Type
converters can come from factories (`ITypeConverterFactory`), which is how enums, nullable types and collections are
converted now. The target frameworks don't change.

## Injection protection

`CsvConfiguration.SanitizeForInjection = true` becomes `InjectionOptions = InjectionOptions.Escape`. `None` is the
default, as `false` was.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    SanitizeForInjection = true,
};
using var csv = new CsvWriter(writer, config);
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    InjectionOptions = InjectionOptions.Escape,
};
using var csv = new CsvWriter(writer, config);
```

| `InjectionOptions` | A field that starts with an injection character, such as `=1+2` |
|---|---|
| `None` | Is written as it is. The default. |
| `Escape` | Is quoted and prefixed with the escape character: `"'=1+2"`. |
| `Strip` | Loses its leading injection characters: `1+2`. |
| `Exception` | Throws a `WriterException`. |

What `Escape` writes is different from what `SanitizeForInjection` wrote:

- **The field is quoted** and the escape character goes inside the quotes. 28 put the escape character in front of
  the field.
- **The default escape character** (`InjectionEscapeCharacter`) is `'` instead of a tab.
- **Tab and carriage return** are injection characters too: the default `InjectionCharacters` are `=`, `@`, `+`, `-`,
  tab and carriage return. As before, only the first character of the field (inside its quotes) is checked.

## Delimiter detection

With `DetectDelimiter = true`, the parser calls the new `GetDelimiter` function with the first block of text it reads
and uses the delimiter it returns. The default, `ConfigurationFunctions.GetDelimiter`, is the detection that 28 did
in the parser (with the fixes under [Behavior changes](#behavior-changes)), and you can replace it or wrap it:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    DetectDelimiter = true,
    GetDelimiter = args => args.Text.Contains('\t') ? "\t" : ConfigurationFunctions.GetDelimiter(args),
};
```

The delegate and its args, `GetDelimiter` and `GetDelimiterArgs`, are in a new namespace, `CsvHelper.Delegates`. Add
`using CsvHelper.Delegates;` where you name them.

## Custom implementations

These change for a class that implements the configuration interfaces or derives from `CsvReader`:

| Type | Change |
|---|---|
| `IWriterConfiguration` | `bool SanitizeForInjection { get; }` is replaced by `InjectionOptions InjectionOptions { get; }`. |
| `IParserConfiguration` | New: `GetDelimiter GetDelimiter { get; }`, from `CsvHelper.Delegates`. |
| `CsvReader.GetFieldIndex` | Takes `IEnumerable<string> names` instead of `string[] names`. Calls with an array compile as before; an override changes its parameter type. |

```csharp before
public class WriterConfiguration : IWriterConfiguration
{
    public CsvMode Mode => CsvMode.RFC4180;
    public bool LeaveOpen => false;
    public string Delimiter => ",";
    public char Quote => '"';
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public bool SanitizeForInjection => true;
    public char[] InjectionCharacters => new[] { '=', '@', '+', '-' };
    public char InjectionEscapeCharacter => '\t';
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
    public CsvMode Mode => CsvMode.RFC4180;
    public bool LeaveOpen => false;
    public string Delimiter => ",";
    public char Quote => '"';
    public char Escape => '"';
    public TrimOptions TrimOptions => TrimOptions.None;
    public InjectionOptions InjectionOptions => InjectionOptions.Escape; // Replaces SanitizeForInjection.
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

A class that implements `IParserConfiguration` adds the `GetDelimiter` property, and returns
`ConfigurationFunctions.GetDelimiter` from it for the default detection.

## Behavior changes

- **Missing fields with `IgnoreBlankLines = false`:** `GetField` with an index past the end of the record calls
  `MissingFieldFound`, which throws a `MissingFieldException` by default. 28 called it only when `IgnoreBlankLines`
  was `true` (the default), and returned `null` otherwise.
- **Dates that don't parse:** `DateTimeConverter` and `DateTimeOffsetConverter` throw a `TypeConverterException`,
  like the other converters, instead of the `FormatException` of `DateTime.Parse`. A default set with
  `Default(value, useOnConversionFailure: true)` now applies to them too.
- **Delimiter detection** also removes quoted text that spans lines, splits lines on any of `\r\n`, `\r` and `\n`
  when `NewLine` is one of them (28 split only on `NewLine`, `\r\n` by default), and picks the culture's list
  separator only when it read more than one line. It can choose a different delimiter than 28 did for the same input.
