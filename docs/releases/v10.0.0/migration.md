# Migrating from CsvHelper 9 to 10

CsvHelper 10 changes three things you may rely on: the default delimiter is the current culture's list separator
instead of a comma, `ReadingExceptionOccurred` returns whether to throw instead of throwing itself, and
`PrepareHeaderForMatch` also gets an index. Validation failures have their own exception types, dynamic records
changed a little, and there is an `IDataReader` for loading a `DataTable`. The target frameworks don't change (.NET
Standard 2.0 and .NET Framework 4.5), but on .NET Framework the package now depends on `System.ValueTuple`.

## Delimiter

The default delimiter was a comma. It's now `CultureInfo.CurrentCulture.TextInfo.ListSeparator`, taken when the
configuration is created, so it follows the culture the code runs under: a comma for English cultures, but a
semicolon for others, such as German. Set the delimiter when your files use commas wherever the code runs, for reading
and for writing:

```csharp before
using var csv = new CsvReader(reader);
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.Delimiter = ",";
```

Setting `Configuration.CultureInfo` doesn't change the delimiter: it comes from the current culture.

### New options

- **`Escape`:** the character that escapes a quote in a quoted field, when reading and writing. It's `"` by default,
  so quotes are doubled as before; a file that escapes them as `\"` reads with `Escape = '\\'`.
- **`LineBreakInQuotedFieldIsBadData`:** when `true`, a line break inside a quoted field calls `BadDataFound`, which
  throws by default. It's `false` by default.
- **Your own configuration class:** if you implement `IParserConfiguration`, add `Escape` and
  `LineBreakInQuotedFieldIsBadData`; if you implement `ISerializerConfiguration`, add `Escape`.

## Header matching

`PrepareHeaderForMatch` gets an index after the header name, so it's a `Func<string, int, string>`. Add the parameter
to your function:

```csharp before
using var csv = new CsvReader(reader);
csv.Configuration.PrepareHeaderForMatch = header => header.Trim().ToLower();
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.PrepareHeaderForMatch = (header, index) => header.Trim().ToLower();
```

- **What the index is:** when the header record is read, and for the property names of dynamic records, it's the
  column's index. When CsvHelper looks up a member, it's the position of the name among the member's names (the names
  given to `Name("a", "b")`).
- **The lookup cache:** `ReadingContext.NamedIndexCache` holds `(string, int)` value tuples instead of
  `Tuple<string, int>`.

## Reading errors

`ReadingExceptionOccurred` is called when a record can't be read, or when the number of columns changes with
`DetectColumnCountChanges`. In 9 it was an `Action`: it threw to stop reading (the default did), and returning kept
reading. It's now a `Func<CsvHelperException, bool>` that returns whether to throw, and CsvHelper does the throwing:
`true` throws, `false` keeps reading (`GetRecords` skips the record, and `GetRecord` returns `default`).

```csharp before
using var csv = new CsvReader(reader);
csv.Configuration.ReadingExceptionOccurred = ex =>
{
    Console.WriteLine($"Skipped row {ex.ReadingContext.Row}: {ex.Message}");
};
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.ReadingExceptionOccurred = ex =>
{
    Console.WriteLine($"Skipped row {ex.ReadingContext.Row}: {ex.Message}");
    return false;
};
```

| CsvHelper 9 | CsvHelper 10 |
|---|---|
| `ex => throw ex` (the default) | `ex => true` (the default) |
| `ex => { Log(ex); }` to skip | `ex => { Log(ex); return false; }` |
| `ReadingExceptionOccurred = null` to ignore errors | `ReadingExceptionOccurred = ex => false` |

**`null` throws now.** In 9, setting `ReadingExceptionOccurred` to `null` ignored the errors; in 10 a `null` callback
means every error is thrown. Use `ex => false` to ignore them.

## Validation errors

`ValidationException` is `abstract`. A missing header throws a `HeaderValidationException`, with the names it looked
for in `HeaderNames` and the name index in `HeaderNameIndex`. A field that fails a class map's `Validate` throws a
`FieldValidationException`, with the field's text in `Field`. `catch (ValidationException)` still catches both. If you
throw `ValidationException` yourself, throw one of these:

```csharp before
using var csv = new CsvReader(reader);
csv.Configuration.HeaderValidated = (isValid, headerNames, headerNameIndex, context) =>
{
    if (!isValid)
    {
        throw new ValidationException(context, $"The file has no '{headerNames[0]}' column.");
    }
};
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.HeaderValidated = (isValid, headerNames, headerNameIndex, context) =>
{
    if (!isValid)
    {
        throw new HeaderValidationException(
            context, headerNames, headerNameIndex, $"The file has no '{headerNames[0]}' column.");
    }
};
```

The default message for a missing header reads differently too, such as "Header with name 'Id' was not found.",
followed by how to turn the validation off.

## Loading a DataTable

`CsvDataReader` is an `IDataReader` over a `CsvReader`, so a `DataTable` (or anything else that takes an
`IDataReader`) can load a file:

```csharp
using var csv = new CsvReader(reader);
using var dataReader = new CsvDataReader(csv);
var table = new System.Data.DataTable();
table.Load(dataReader);
```

Creating it reads the first record, and makes it the header when `HasHeaderRecord` is `true`, so pass a `CsvReader`
that hasn't read anything yet. Disposing it disposes the `CsvReader`.

## Behavior changes

- **Dynamic records have every column:** a record read with `GetRecords<dynamic>()` has a property for each header
  column, and a field that's missing from a short row is `null`. Before, the record had properties only for the fields
  the row had.
- **Dynamic objects are written in name order:** an `ExpandoObject` or other dynamic object is written with its
  properties, and its header, sorted by name. Before, they were in the order the object listed them (for an
  `ExpandoObject`, the order they were added), so objects whose properties were added in a different order didn't
  line up with the header.
