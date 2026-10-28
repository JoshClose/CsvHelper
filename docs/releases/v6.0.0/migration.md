# Migrating from CsvHelper 5 to 6

CsvHelper 6 makes the internal record classes replaceable through the object resolver, gives `TypeConverterException`
the details of the failed conversion, and adds a generic `WriteRecords<T>`. Most code compiles unchanged. You're
affected if you throw `TypeConverterException` from your own type converters, implement `IObjectResolver` or
`IWriter`, or set an object resolver that resolves every type. The target frameworks don't change: .NET Standard 2.0
and .NET Framework 4.5.

## Type converter errors

`TypeConverterException` now says what failed: its constructors take the type converter, the member's map data, and
the text being read (or the value being written), before the context. Custom converters that throw it pass them on.

```csharp before
public class YesNoConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == "yes")
        {
            return true;
        }

        if (text == "no")
        {
            return false;
        }

        throw new TypeConverterException(row.Context, $"'{text}' is not yes or no.");
    }
}
```

```csharp after
public class YesNoConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        if (text == "yes")
        {
            return true;
        }

        if (text == "no")
        {
            return false;
        }

        throw new TypeConverterException(this, memberMapData, text, row.Context, $"'{text}' is not yes or no.");
    }
}
```

When you catch the exception, `Text` (reading) or `Value` (writing), `TypeConverter` and `MemberMapData` tell you
which field and member failed:

```csharp
public class Order
{
    public int Id { get; set; }
}

using var csv = new CsvReader(reader);
try
{
    var orders = csv.GetRecords<Order>().ToList();
}
catch (TypeConverterException ex)
{
    Console.WriteLine($"Can't convert '{ex.Text}' to {ex.MemberMapData.Member.Name}.");
}
```

## Writing records

`CsvWriter` has a generic `WriteRecords<T>(IEnumerable<T>)` next to `WriteRecords(IEnumerable)`. Calls with a generic
collection use it when you recompile, with no change to your code; see [Behavior changes](#behavior-changes) for what
it writes. If you implement `IWriter` yourself, add the generic method.

## Object resolver

CsvHelper now creates its record classes through `ObjectResolver.Current`: `RecordCreatorFactory`, `RecordHydrator`,
`RecordWriterFactory`, `ExpressionManager`, and the writer's `RecordManager`, with the `CsvReader` or `CsvWriter` as
the constructor argument. That lets you replace one with a subclass (their factory methods are `virtual` now), and it
means your resolver gets these types too.

`IObjectResolver` has a generic `Resolve<T>`, and the constructor arguments of `Resolve(Type, ...)` are `params`. If
you implement the interface, add the generic method:

```csharp before
public class ActivatorResolver : IObjectResolver
{
    public bool UseFallback => true;

    public Func<Type, bool> CanResolve => type => true;

    public Func<Type, object[], object> ResolveFunction => (type, args) => Activator.CreateInstance(type, args);

    public object Resolve(Type type, object[] constructorArgs = null)
    {
        return ResolveFunction(type, constructorArgs ?? new object[0]);
    }
}
```

```csharp after
public class ActivatorResolver : IObjectResolver
{
    public bool UseFallback => true;

    public Func<Type, bool> CanResolve => type => true;

    public Func<Type, object[], object> ResolveFunction => (type, args) => Activator.CreateInstance(type, args);

    public object Resolve(Type type, params object[] constructorArgs)
    {
        return ResolveFunction(type, constructorArgs);
    }

    public T Resolve<T>(params object[] constructorArgs)
    {
        return (T)Resolve(typeof(T), constructorArgs);
    }
}
```

A resolver whose `CanResolve` returns `true` for every type, such as one backed by a dependency injection container,
must create these classes with the arguments it's given. If it can't, hand the CsvHelper types to the resolver you
replaced (`ObjectResolver.Current` before you set yours), which can.

## Behavior changes

- **`WriteRecords` writes the element type:** `WriteRecords<T>` writes each record with the members (and class map) of
  `T`, the collection's element type, the same as the header. Before, the header came from the element type but each
  row from the record's own type, so a `List<IAnimal>` holding `Dog` objects wrote the `IAnimal` header and the `Dog`
  members. To write each record's own type, pass the records as `object`, such as `records.Cast<object>()`.
- **Non-generic collections:** a collection passed as a non-generic `IEnumerable` is written by each record's own
  type, and the header comes from the first record, so an empty one writes no header.
