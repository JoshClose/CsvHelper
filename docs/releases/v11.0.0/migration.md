# Migrating from CsvHelper 10 to 11

CsvHelper 11 replaces the writer's quoting switches, `QuoteAllFields`, `QuoteNoFields`, `QuoteRequiredChars` and
`BuildRequiredQuoteChars`, with one function, `ShouldQuote`, that decides for each field whether to quote it. Only
writing is affected: reading, mapping and the target frameworks (.NET Standard 2.0 and .NET Framework 4.5) are the same.

## Quoting fields

`ShouldQuote` gets the field and the writing context, and returns `true` to quote the field. The two switches become
functions that always answer the same:

```csharp before
using var csv = new CsvWriter(writer);
csv.Configuration.QuoteAllFields = true;
csv.WriteField("Widget");
csv.NextRecord();
```

```csharp after
using var csv = new CsvWriter(writer);
csv.Configuration.ShouldQuote = (field, context) => true;
csv.WriteField("Widget");
csv.NextRecord();
```

| Before | After | Notes |
|---|---|---|
| `QuoteAllFields = true` | `ShouldQuote = (field, context) => true` | Empty fields are quoted too, as before. |
| `QuoteNoFields = true` | `ShouldQuote = (field, context) => false` | |
| `QuoteRequiredChars`, `BuildRequiredQuoteChars` | `ShouldQuote` | Test for the characters in your function. |

The default, `ConfigurationFunctions.ShouldQuote`, quotes a field that contains a quote, `\r`, `\n` or the delimiter,
or that starts or ends with a space. To add a rule, call it from your function:

```csharp
using var csv = new CsvWriter(writer);
csv.Configuration.ShouldQuote = (field, context) =>
    ConfigurationFunctions.ShouldQuote(field, context) || (field != null && field.Contains("\t"));
```

- **The field can be `null`:** `WriteField((string)null)` passes it to the function as it is.
- **Trimming comes first:** with `TrimOptions.Trim`, the function gets the trimmed field.
- **`WriteField(field, shouldQuote)`** still quotes as you tell it, without calling the function.

## Behavior changes

With `AllowComments`, 10 quoted the first field of a record when it started with the `Comment` character, so that
the record wasn't read back as a comment. The default `ShouldQuote` doesn't do that. If you write files with comments,
add the rule:

```csharp
using var csv = new CsvWriter(writer);
csv.Configuration.AllowComments = true;
csv.Configuration.ShouldQuote = (field, context) =>
    ConfigurationFunctions.ShouldQuote(field, context)
    || (context.WriterConfiguration.AllowComments
        && context.Record.Count == 0
        && !string.IsNullOrEmpty(field)
        && field[0] == context.WriterConfiguration.Comment);
```
