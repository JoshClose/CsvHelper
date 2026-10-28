# Migrating from CsvHelper 18 to 19

CsvHelper 19 completes the mapping of constructor parameters that 18 started: a parameter takes the same
configuration as a member, with attributes on the parameter or with `Parameter` in a class map. A few names changed on
the way: `TypeConverterOptions.NumberStyle` is `NumberStyles`, `MapTypeConverterOption` is
`MemberMapTypeConverterOption`, and a parameter map's `Data.Name` is `Data.Names`. The package depends on
Microsoft.Bcl.HashCode on .NET Framework 4.7 and .NET Standard 2.0.

## Constructor parameters

`[BooleanFalseValues]`, `[BooleanTrueValues]`, `[Constant]`, `[CultureInfo]`, `[DateTimeStyles]`, `[Default]`,
`[Format]`, `[HeaderPrefix]`, `[Ignore]`, `[NameIndex]`, `[NullValues]`, `[NumberStyles]`, `[Optional]` and
`[TypeConverter]` can go on a constructor parameter now, as `[Name]` and `[Index]` could in 18:

```csharp
public class Order
{
    public Order(
        int id,
        [Name("Ordered On"), Format("yyyyMMdd")] DateTime orderedOn,
        [Optional, Default(1)] int quantity,
        [Ignore] string note)
    {
        // ...
    }
}
```

A class map configures them the same way, with methods like a member map's: `Name`, `NameIndex`, `Index`, `Ignore`,
`Default`, `Constant`, `Optional`, `TypeConverter` and `TypeConverterOption`:

```csharp
public class Order
{
    public Order(int id, DateTime orderedOn, int quantity, string note)
    {
        // ...
    }
}

public class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Parameter("id");
        Parameter("orderedOn").Name("Ordered On").TypeConverterOption.Format("yyyyMMdd");
        Parameter("quantity").Optional().Default(1);
        Parameter("note").Ignore();
    }
}
```

- **Every parameter gets a value,** since the constructor needs one: a constant parameter gets the constant, and an
  ignored one gets its `Default`, else the parameter's default value, else `default`.
- **Types:** `ParameterMap.Default` and `Constant` take a value of the parameter's type, and throw
  `ArgumentException` for another type.
- **Your own attributes** implement `IParameterMapper`, or `IParameterReferenceMapper` for a parameter whose type is
  mapped as a reference.

## Class maps

The non-generic `MemberMap`, which `MemberMaps.Find` returns, no longer has `Default(string)`. A string now goes to
`Default(object)`, which throws `ArgumentException` when the member isn't a `string`. The code compiles, and fails
when the map is created. Pass a value of the member's type:

```csharp before
public class Foo
{
    public int Id { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        MemberMaps.Find<Foo>(m => m.Id).Default("0");
    }
}
```

```csharp after
public class Foo
{
    public int Id { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        MemberMaps.Find<Foo>(m => m.Id).Default(0);
    }
}
```

`MemberMap<TClass, TMember>`, which `Map(m => m.Id)` returns, keeps `Default(string)`.

| Before | After | Notes |
|---|---|---|
| `MapTypeConverterOption` | `MemberMapTypeConverterOption` | The type of `MemberMap.TypeConverterOption`. Calls such as `.TypeConverterOption.Format(...)` don't change. |
| `ParameterMapData.Name` | `ParameterMapData.Names` | A `MemberNameCollection`, as for members; `ParameterMap.Name` takes several names. |
| `ClassMap.GetMaxIndex()` | `ClassMap.GetMaxIndex(bool isParameter = false)` | Overrides need the parameter. |

## Type converter options

`TypeConverterOptions.NumberStyle` is `NumberStyles`, in the options cache and in your type converters:

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.TypeConverterOptionsCache.GetOptions<decimal>().NumberStyle = NumberStyles.Currency;
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture);
config.TypeConverterOptionsCache.GetOptions<decimal>().NumberStyles = NumberStyles.Currency;
```
