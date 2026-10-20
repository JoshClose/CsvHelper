# Migrating from CsvHelper 8 to 9

CsvHelper 9 is the changes of 8.2 and 8.3 under a new major version. Those two releases were unlisted on NuGet because
8.2 adds a member to the class map builder interfaces, which breaks code that implements them. If you don't implement
those interfaces, your code compiles unchanged: 9 adds optional members to attributes and the class map builder, and
allows a null character as the delimiter. The target frameworks don't change: .NET Standard 2.0 and .NET Framework 4.5.

## Optional members

8.1 added `Optional()` to class maps: an optional member is skipped when reading if no field has its name, instead of
failing header validation. 9 adds the same option as an attribute:

```csharp
public class Order
{
    public int Id { get; set; }

    [Optional]
    public string Notes { get; set; }
}
```

And to the class map builder, which `Factory.CreateClassMapBuilder<T>()` returns:

```csharp
public class Order
{
    public int Id { get; set; }
    public string Notes { get; set; }
}

var map = new Factory().CreateClassMapBuilder<Order>()
    .Map(m => m.Id)
    .Map(m => m.Notes).Optional()
    .Build();
```

If you implement `IHasMapOptions<TClass, TMember>` yourself, it now extends `IHasOptional<TClass, TMember>`: add
`IHasOptionalOptions<TClass, TMember> Optional()`.

## Behavior changes

- **Null character delimiter:** setting `Delimiter` to `"\0"` threw a `ConfigurationException`. It's allowed now.
