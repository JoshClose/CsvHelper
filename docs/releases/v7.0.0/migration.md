# Migrating from CsvHelper 6 to 7

CsvHelper 7 makes reading faster. To get there, the reading and writing state is used as classes: the
`IReadingContext` and `IWritingContext` interfaces are removed, and everything that took or returned one uses
`ReadingContext` or `WritingContext`. You're affected if your code names the interfaces: callbacks written as methods,
your own exceptions, parsers or serializers, and test doubles. Code that only uses lambdas and the contexts' members
compiles unchanged. The target frameworks don't change: .NET Standard 2.0 and .NET Framework 4.5.

## Reading and writing contexts

Replace `IReadingContext` with `ReadingContext`, and `IWritingContext` with `WritingContext`. Lambdas need no change,
since their parameter types are inferred; methods that you assign to a callback do.

```csharp before
public static class CsvCallbacks
{
    public static void OnBadData(IReadingContext context)
    {
        Console.WriteLine($"Bad data on row {context.Row}: {context.RawRecord}");
    }

    public static void OnMissingField(string[] headerNames, int index, IReadingContext context)
    {
        Console.WriteLine($"Missing field '{headerNames[0]}' on row {context.Row}.");
    }
}

using var csv = new CsvReader(reader);
csv.Configuration.BadDataFound = CsvCallbacks.OnBadData;
csv.Configuration.MissingFieldFound = CsvCallbacks.OnMissingField;
```

```csharp after
public static class CsvCallbacks
{
    public static void OnBadData(ReadingContext context)
    {
        Console.WriteLine($"Bad data on row {context.Row}: {context.RawRecord}");
    }

    public static void OnMissingField(string[] headerNames, int index, ReadingContext context)
    {
        Console.WriteLine($"Missing field '{headerNames[0]}' on row {context.Row}.");
    }
}

using var csv = new CsvReader(reader);
csv.Configuration.BadDataFound = CsvCallbacks.OnBadData;
csv.Configuration.MissingFieldFound = CsvCallbacks.OnMissingField;
```

| CsvHelper 6 | CsvHelper 7 |
|---|---|
| `BadDataFound`, `HeaderValidated` and `MissingFieldFound` get an `IReadingContext` | They get a `ReadingContext` |
| `Context` of `CsvReader`, `CsvParser`, `CsvFieldReader`, `IReaderRow`, `IParser` and `IFieldReader` is an `IReadingContext` | A `ReadingContext` |
| `Context` of `CsvWriter`, `CsvSerializer`, `IWriterRow` and `ISerializer` is an `IWritingContext` | A `WritingContext` |
| `CsvHelperException.ReadingContext` and `WritingContext` | Typed `ReadingContext` and `WritingContext` |
| The constructors of `CsvHelperException` and the exceptions derived from it take an `IReadingContext` or `IWritingContext` | They take a `ReadingContext` or `WritingContext` |

- **Your own exceptions:** an exception derived from `CsvHelperException` passes a `ReadingContext` or
  `WritingContext` to the base constructor.
- **Your own parser or serializer:** `IParser.Context` and `ISerializer.Context` return the classes, so yours returns
  a `ReadingContext` or `WritingContext`.
- **Test doubles:** there are no interfaces to mock. Create a real context:
  `new ReadingContext(textReader, configuration, leaveOpen)` or
  `new WritingContext(textWriter, configuration, leaveOpen)`.
- **`ReadingContext` holds fields:** the reading state (`Row`, `RawRow`, `Record`, `HeaderRecord`, `CurrentIndex`,
  `ColumnCount`, the buffer and its positions) is in public fields instead of virtual properties, and `RawRecord`,
  `Field`, `ParserConfiguration` and `ReaderConfiguration` are no longer `virtual`. Reading them looks the same, but a
  class derived from `ReadingContext` can't override them.
- **`ReadingContext.C` is gone:** the character the parser is on is private to the parser now.
