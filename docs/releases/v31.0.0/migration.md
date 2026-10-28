# Migrating from CsvHelper 30 to 31

CsvHelper 31 adds `TypeConverter<T>`, a base class for type converters that works with the member's type instead of
`object`. To make room for that name, the class that was called `TypeConverter`, the converter that throws for
`System.Type` members, is now `NotSupportedTypeConverter<T>`. Only code that names `TypeConverter` itself has to
change. Converter factories that you add are now asked before the built-in ones. The target frameworks are the same as
in 30.1.0. 31.0.2 drops the System.Linq.Async package that 30.1.0 brought in: if your code uses its extension methods
for `IAsyncEnumerable<T>`, such as `ToListAsync`, add the package to your project.

## Type converters

### NotSupportedTypeConverter

`TypeConverter` was the converter that CsvHelper registers for members of type `System.Type`: it throws a
`TypeConverterException` saying that the conversion isn't supported. It's `NotSupportedTypeConverter<T>` now, and its
message names `T`, so it can stand in for any type. `TypeConverter` without a type argument no longer exists, so code
that creates it, derives from it or checks for it doesn't compile.

```csharp before
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.TypeConverterCache.AddConverter<Stream>(new TypeConverter());
```

```csharp after
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.TypeConverterCache.AddConverter(new NotSupportedTypeConverter<Stream>());
```

| Before | After | Notes |
|---|---|---|
| `TypeConverter` | `NotSupportedTypeConverter<Type>` | Still registered for `System.Type` by default. |
| `TypeConverter` registered for another type | `NotSupportedTypeConverter<T>` | The message names `T`; 30's always said `System.Type`. |

### Generic converters

`TypeConverter<T>` is an abstract class whose `ConvertFromString` returns a `T` and whose `ConvertToString` takes one,
so your converter no longer casts from `object`. It implements `ITypeConverter`, so it works wherever a converter
did: `[TypeConverter(typeof(...))]`, `Map(...).TypeConverter<...>()` and `TypeConverterCache`. Converters that derive
from `DefaultTypeConverter` or implement `ITypeConverter` work as before; you don't have to change them.

```csharp before
public class PercentConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
        double.Parse(text.TrimEnd('%'), CultureInfo.InvariantCulture) / 100;

    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) =>
        ((double)value * 100).ToString(CultureInfo.InvariantCulture) + "%";
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.TypeConverterCache.AddConverter<double>(new PercentConverter());
```

```csharp after
public class PercentConverter : TypeConverter<double>
{
    public override double ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData) =>
        double.Parse(text.TrimEnd('%'), CultureInfo.InvariantCulture) / 100;

    public override string ConvertToString(double value, IWriterRow row, MemberMapData memberMapData) =>
        (value * 100).ToString(CultureInfo.InvariantCulture) + "%";
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Context.TypeConverterCache.AddConverter(new PercentConverter());
```

- **Registering:** the new overload `TypeConverterCache.AddConverter<T>(TypeConverter<T>)` takes the type from the
  converter, so `AddConverter(new PercentConverter())` needs no type argument.
- **Use 31.0.3 or later.** In 31.0.0 to 31.0.2, writing a null value through a `TypeConverter<T>`, such as a null
  string or an empty `int?`, fails with an `InvalidCastException` before your `ConvertToString` is called. 31.0.3
  passes the null to it.

## Behavior changes

Converter factories that you add with `TypeConverterCache.AddConverterFactory` are now asked before the built-in
factories for enums, nullable types and collections. In 30 the built-in factories came first, so a factory of yours for
a type that they also handle, such as a type that implements `IEnumerable<T>`, was never used. If a factory of yours
relied on the built-in ones coming first, make its `CanCreate` return `false` for the types they should handle.
Converters added with `AddConverter` still come before any factory.
