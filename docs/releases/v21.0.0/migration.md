# Migrating from CsvHelper 20 to 21

CsvHelper 21 has one breaking change: `CsvConfiguration.NewLine` is a `string` instead of a `char?`. You can now write
any line ending, including `\r\n`, which 20 could only write where it is `Environment.NewLine`. When you set it, the
parser uses it too, in every mode. The `NewLine` enum and the `NewLines` constants, unused since 20, are removed. The
target frameworks don't change.

## Line endings

Give `NewLine` a string instead of a character.

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = '\n',
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    NewLine = "\n",
};
```

| CsvHelper 20 | CsvHelper 21 | Notes |
|---|---|---|
| `NewLine = '\n'` | `NewLine = "\n"` | |
| `NewLine = '\r'` | `NewLine = "\r"` | |
| `NewLine = null` | Leave it unset | Setting `null` counts as setting it: creating the parser then throws, and so does writing a non-empty field with the default `ShouldQuote`. |
| none | `NewLine = "\r\n"` | |
| `NewLines.CRLF`, `NewLines.CR`, `NewLines.LF` | `"\r\n"`, `"\r"`, `"\n"` | The `NewLines` class is removed. |

- **The default when writing:** 21.0.0 writes `Environment.NewLine`, as 20 did. 21.0.2 and later write `\r\n`.
- **`IsNewLineSet`** is new: it tells whether `NewLine` was set or is the default. If you implement
  `IParserConfiguration` or `IWriterConfiguration`, change `NewLine` to a `string` and add `IsNewLineSet`.

## Behavior changes

- **A `NewLine` you set is used for reading too, in every mode.** In 20, the default mode ignored it and ended a
  record at `\r\n`, `\r` or `\n`. In 21, only `NewLine` ends a record. If you set `NewLine = "\n"` to write files and
  read with the same configuration, a file with `\r\n` line endings keeps the `\r` at the end of the last field of
  each record (or, when that field is quoted, it is bad data). Read with a configuration that doesn't set `NewLine`.
- **The Visual Basic constructor** that 21.0.5 adds, with every setting as an optional argument, sets `NewLine` even
  when you don't pass it, so a configuration created with it reads only `\r\n` as a line ending. 22.1.1 fixes this.
- **`ParserMode.Escape` without `NewLine`** ends a record at `\r\n`, `\r` or `\n`. In 20 it used `\n` by default.
  Before 21.1.1, it reads `\r\n` as `\r` followed by a blank line, which `IgnoreBlankLines` (on by default) skips.
- **Quoting:** the default `ShouldQuote` quotes a field that contains `\r` or `\n` when `NewLine` isn't set, and a
  field that contains `NewLine` when it is.
