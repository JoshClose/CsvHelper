# Migrating from CsvHelper 32 to 33

CsvHelper 33 annotates its API for nullable reference types: the parameters, return values and properties that can be
null are marked with `?`. In a project that doesn't enable nullable reference types, the compiler ignores the
annotations and the upgrade needs no changes, unless you derive from `MemberMap`. In a project that does
(`<Nullable>enable</Nullable>`), the compiler now checks your code against them and warns where it assumes a value is
never null; that is where your changes are. Use 33.0.1 or later: it marks some values nullable that 33.0.0 missed,
and unmarks some that 33.0.0 marked by mistake.

## Nullable reference types

CsvHelper 32 had no annotations, so the compiler knew nothing about what could be null and didn't warn. 33 tells it,
and code that compiled cleanly can get these warnings:

| Warning | Means |
|---|---|
| CS8600 | A value that may be null is assigned to a variable that isn't nullable. |
| CS8602 | A value that may be null is dereferenced. |
| CS8604 | A value that may be null is passed to a parameter that isn't nullable. |
| CS8765, CS8767 | An override or interface implementation says a parameter can't be null, where the base says it can. |

Fix them as you would in your own code: declare the variable nullable, check for null, or give a fallback with `??`.
They are warnings, not errors, unless your project treats warnings as errors. Value types don't change: `GetField<int>`
still returns an `int`.

### Reading fields

The field getters return `string?`: a field can be missing, and `HeaderRecord` is null until the header is read.

```csharp before
#nullable enable
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Read();
csv.ReadHeader();
var columns = csv.HeaderRecord.Length;  // 33: warning CS8602, HeaderRecord is string[]?
while (csv.Read())
{
    string name = csv.GetField("Name");  // 33: warning CS8600, GetField returns string?
    var id = csv.GetField<int>("Id");    // No warning: an int is still an int.
    Console.WriteLine(name.Trim());
}
```

```csharp after
#nullable enable
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Read();
csv.ReadHeader();
var columns = csv.HeaderRecord?.Length ?? 0;
while (csv.Read())
{
    var name = csv.GetField("Name") ?? "";
    var id = csv.GetField<int>("Id");
    Console.WriteLine(name.Trim());
}
```

| Member | In 33 |
|---|---|
| `GetField(index)`, `GetField(name)`, `csv[index]`, `csv[name]` | `string?` |
| `GetField<T>(...)`, `TryGetField<T>(..., out T? field)` | `T?`: nullable for reference types such as `string`, unchanged for value types |
| `GetField(Type, ...)`, `TryGetField(Type, ..., out object? field)` | `object?` |
| `CsvReader.HeaderRecord`, `Parser.Record` | `string[]?` |
| `Context.Maps[type]`, `Context.Maps.Find<T>()` | Nullable: null when there is no map for the type |
| `CsvContext.Reader`, `.Writer`, `.Parser`, `CsvHelperException.Context` | Nullable |

The writing methods take nullable values (`WriteField(string? field)`, `WriteField<T>(T? field)`,
`WriteRecord<T>(T? record)`), which changes nothing for callers.

### Type converters

`ITypeConverter` and `DefaultTypeConverter` take `string? text` in `ConvertFromString` and `object? value` in
`ConvertToString`, and return `object?` and `string?`. `TypeConverter<T>` is the same with `T?`. A converter that
overrides them with `string text` or `object value` gets CS8765 (CS8767 for an `ITypeConverter` implementation): the
base says null can come in, so handle it. The return types can stay non-nullable.

```csharp before
#nullable enable
public class TrimConverter : DefaultTypeConverter
{
    // 33: warning CS8765 on text and value, which are nullable in DefaultTypeConverter.
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
        text.Trim();

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) =>
        ((string)value).Trim();
}
```

```csharp after
#nullable enable
public class TrimConverter : DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData) =>
        text?.Trim();

    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData) =>
        ((string?)value)?.Trim();
}
```

### Configuration

The configuration functions that are null by default are nullable: `ShouldSkipRecord`, `ReferenceHeaderPrefix` and
`DynamicPropertySort`. So are some values that the functions get, such as `ShouldQuoteArgs.Field` and
`MissingFieldFoundArgs.HeaderNames`, and the row's `Parser.Record`.

```csharp before
#nullable enable
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    // 33: warning CS8604, Parser.Record is string[]?
    ShouldSkipRecord = args => args.Row.Parser.Record.All(string.IsNullOrWhiteSpace),
};
```

```csharp after
#nullable enable
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = args => args.Row.Parser.Record?.All(string.IsNullOrWhiteSpace) ?? false,
};
```

### 33.0.0 and 33.0.1

33.0.0 is too strict in a few places, and 33.0.1 fixes them. Upgrade to 33.0.1 or later instead of working around
these warnings:

- `BadDataFound`, `MissingFieldFound`, `HeaderValidated` and `ReadingExceptionOccurred` aren't nullable in 33.0.0,
  so the usual `BadDataFound = null` or `MissingFieldFound = null` warns (CS8625). 33.0.1 makes them nullable.
- `PrepareHeaderForMatchArgs.Header` is `string?` in 33.0.0, so `args => args.Header.ToLower()` warns. In 33.0.1 it's
  `string` again.
- `GetRecord<T>()` and `GetRecord(Type)` return `T?` and `object?` in 33.0.0. In 33.0.1 they return `T` and `object`
  again (see [Behavior changes](#behavior-changes)).
- 33.0.1 also accepts null in `MemberMap.Constant` and the `Default(string)` overload, accepts `Convert` functions
  that return null, and accepts class map lambdas of `Map` and `References` that return a nullable member.

## Member maps

`MemberMap.TypeConverterOption` is an abstract property with only a getter now, and `MemberMap<TClass, TMember>`
implements it. This matters only if you derive from these classes: a class derived from `MemberMap` must implement the
property, and one derived from `MemberMap<TClass, TMember>` that set it in its constructor overrides it instead.

```csharp before
using System.Reflection;

public class CustomTypeConverterOption : MemberMapTypeConverterOption
{
    public CustomTypeConverterOption(MemberMap memberMap) : base(memberMap) { }
}

public class CustomMemberMap<TClass, TMember> : MemberMap<TClass, TMember>
{
    public CustomMemberMap(MemberInfo member) : base(member)
    {
        TypeConverterOption = new CustomTypeConverterOption(this);
    }
}
```

```csharp after
using System.Reflection;

public class CustomTypeConverterOption : MemberMapTypeConverterOption
{
    public CustomTypeConverterOption(MemberMap memberMap) : base(memberMap) { }
}

public class CustomMemberMap<TClass, TMember> : MemberMap<TClass, TMember>
{
    private readonly CustomTypeConverterOption typeConverterOption;

    public CustomMemberMap(MemberInfo member) : base(member)
    {
        typeConverterOption = new CustomTypeConverterOption(this);
    }

    public override MemberMapTypeConverterOption TypeConverterOption => typeConverterOption;
}
```

## Dependencies

On .NET Framework (4.6.2, 4.7, 4.8) and .NET Standard 2.0, CsvHelper 33 depends on Microsoft.Bcl.AsyncInterfaces 8.0.0
instead of 7.0.0. If your project references an older version of that package itself, raise it to 8.0.0 or later, or
NuGet reports a package downgrade. The target frameworks are the same as in 32, until 33.1.0 replaces the .NET 6 and 7
builds with a .NET 9 build.

## Behavior changes

- **`GetRecord` with no records:** from 33.0.1, when `GetRecord<T>()` or `GetRecord(Type)` reads the header itself
  (you didn't call `ReadHeader`) and no record follows it, it throws a `ReaderException` ("There are no records.").
  32 and 33.0.0 returned `default`, null for a class.
