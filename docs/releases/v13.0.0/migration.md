# Migrating from CsvHelper 12 to 13

CsvHelper 13 makes you choose the culture. Without a configuration, the reader, writer, parser and serializer used the
machine's current culture; now their constructors require a `CultureInfo`, and so does the configuration, which is
renamed `CsvConfiguration`. The culture reads and writes numbers and dates, and gives the default delimiter. 13 also
adds `GetRecordsAsync` and `WriteRecordsAsync`, a choice of line endings, mapping attributes of your own, and a
.NET Standard 2.1 build next to .NET Standard 2.0 and .NET Framework 4.5 and 4.7.

## Creating a reader or writer

Pass the culture as the second argument:

```csharp before
using var csv = new CsvReader(reader);
```

```csharp after
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
```

Which culture? 12 used `CultureInfo.CurrentCulture`, both for values and for the delimiter, which was the culture's
list separator (`;` in many European cultures). Pass `CultureInfo.CurrentCulture` to keep that, or
`CultureInfo.InvariantCulture` (`,` between fields, `.` in numbers) for files that shouldn't depend on the settings
of the machine that reads or writes them.

| Before | After | Notes |
|---|---|---|
| `new CsvReader(textReader)` | `new CsvReader(textReader, culture)` | |
| `new CsvReader(textReader, leaveOpen)` | `new CsvReader(textReader, culture, leaveOpen)` | |
| `new CsvWriter(textWriter)` | `new CsvWriter(textWriter, culture)` | The culture also chooses the line ending: see [Behavior changes](#behavior-changes). |
| `new CsvWriter(textWriter, leaveOpen)` | `new CsvWriter(textWriter, culture, leaveOpen)` | |
| `new CsvParser(...)`, `new CsvSerializer(...)` | The same, with the culture second | |
| `factory.CreateParser(textReader)`, `CreateReader`, `CreateWriter` | `factory.CreateParser(textReader, culture)`, and so on | Also on `IFactory`. |

## Configuration

`Configuration` is renamed `CsvConfiguration`, so that the class no longer has the name of its namespace,
`CsvHelper.Configuration`. It has no parameterless constructor: pass the culture.

```csharp before
var config = new CsvHelper.Configuration.Configuration
{
    Delimiter = ";",
    HasHeaderRecord = false,
};
using var csv = new CsvReader(reader, config);
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
    HasHeaderRecord = false,
};
using var csv = new CsvReader(reader, config);
```

The constructor sets `Delimiter` to the culture's list separator, as `new Configuration(culture)` did in 12. Every
other place that took a `Configuration` takes a `CsvConfiguration`: the reader and writer constructors,
`ReadingContext`, `WritingContext`, `ClassMapCollection`, `IFactory`, and the protected `AutoMapMembers` and
`AutoMapConstructorParameters` of `ClassMap`.

## Class maps

`AutoMap()` takes the culture, or a configuration:

```csharp before
public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        AutoMap();
        Map(m => m.Name).Name("Foo Name");
    }
}
```

```csharp after
public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.Name).Name("Foo Name");
    }
}
```

`AutoMap(culture)` auto maps with a new `CsvConfiguration` for that culture. When your configuration changes how
members are found, such as `MemberTypes` or `IncludePrivateMembers`, pass it instead: `AutoMap(config)`.

## Mapping attributes

The mapping attributes are applied through two interfaces, `IMemberMapper` and `IMemberReferenceMapper`, instead of
a fixed list of attributes. Each one configures the `MemberMap` (or `MemberReferenceMap`) of the member it is on, so
an attribute of your own can configure a member the way a class map does:

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class YesNoAttribute : Attribute, IMemberMapper
{
    public void ApplyTo(MemberMap memberMap)
    {
        memberMap.Data.TypeConverterOptions.BooleanTrueValues.Add("yes");
        memberMap.Data.TypeConverterOptions.BooleanFalseValues.Add("no");
    }
}

public class Foo
{
    [YesNo]
    public bool IsActive { get; set; }
}
```

Attributes are read when a type is auto mapped, the built-in ones as before.

## Async records

`GetRecordsAsync<T>()` reads records as an `IAsyncEnumerable<T>`, and `WriteRecordsAsync` writes them with
asynchronous calls:

```csharp
public class Foo
{
    public int Id { get; set; }
}

using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
    await foreach (var foo in csv.GetRecordsAsync<Foo>())
    {
        // ...
    }
}

using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    await csv.WriteRecordsAsync(new List<Foo> { new Foo { Id = 1 } });
}
```

- **Frameworks:** `GetRecordsAsync`, its overloads and `EnumerateRecordsAsync` are in the .NET Standard and
  .NET Framework 4.7 builds, not in the .NET Framework 4.5 build. On .NET Standard 2.0 and .NET Framework 4.7,
  `IAsyncEnumerable<T>` comes from the Microsoft.Bcl.AsyncInterfaces package, a new dependency.
- **Your own `IReader` or `IWriter`:** the interfaces have the new methods, so an implementation needs them too.

## Behavior changes

The writer ended records with `\r\n` in 12 (since 12.1.3). In 13 the line ending is the new `NewLine` setting, and
its default depends on the culture of the configuration: `\r\n` for `CultureInfo.InvariantCulture`, and
`Environment.NewLine` for any other culture, which is `\n` on Linux and macOS. To write `\r\n` with another culture,
set it:

```csharp
var config = new CsvConfiguration(CultureInfo.CurrentCulture)
{
    NewLine = NewLine.CRLF,
};
using var csv = new CsvWriter(writer, config);
```

`NewLine` is `CRLF`, `CR`, `LF` or `Environment`.
