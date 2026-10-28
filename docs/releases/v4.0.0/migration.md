# Migrating from CsvHelper 3 to 4

CsvHelper 4 changes how the headers of reference members are prefixed, and removes two class map methods:
`References<TClassMap>` on `ClassMap<TClass>`, and `ConstructUsing`. You're affected if you prefix reference headers, or
if your class maps map references or say how records are constructed. Reading and writing with auto mapping or
attributes works as before. The target frameworks don't change: .NET Standard 2.0 and .NET Framework 4.5.

## Reference header prefixes

A reference is a member whose type is a class mapped to columns of its own, like `Address` on a `Customer`.
`PrefixReferenceHeaders` turned prefixing on or off, and the prefix was always the member name and a dot. It's
replaced by `ReferenceHeaderPrefix`, a function that gets the member's type and name and returns the prefix, so you
choose the format. It's `null` by default, which means no prefix.

```csharp before
using var csv = new CsvReader(reader);
csv.Configuration.PrefixReferenceHeaders = true;
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.ReferenceHeaderPrefix = (memberType, memberName) => $"{memberName}.";
```

- **Class maps:** `Prefix()` on `MemberReferenceMap` and `ParameterReferenceMap` is removed. The configuration's
  function prefixes the references of class maps too: it's applied to each reference when the map is registered, so
  set it before you call `RegisterClassMap`. It replaces a prefix set in the map. It gets the member's type and name,
  so it can return a different prefix for each reference.
- **Your own configuration class:** if you implement `IReaderConfiguration` or `IWriterConfiguration`, replace
  `PrefixReferenceHeaders` with `ReferenceHeaderPrefix`. `ISerializerConfiguration.Quote` needs a setter now.

## Reference maps

`References<TClassMap>(m => m.Address)` mapped a reference with the reference type's own class map. It's removed from
`ClassMap<TClass>`. Map the reference's members from the parent map instead, with an expression that goes through the
reference (sub property mapping). The reference type no longer needs a class map.

```csharp before
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class Customer
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class AddressMap : ClassMap<Address>
{
    public AddressMap()
    {
        Map(m => m.Street).Name("Street Address");
        Map(m => m.City);
    }
}

public class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name);
        References<AddressMap>(m => m.Address);
    }
}
```

```csharp after
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class Customer
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name);
        Map(m => m.Address.Street).Name("Street Address");
        Map(m => m.Address.City);
    }
}
```

CsvHelper 5 brings `References<TClassMap>` back (see [Migrating from 4 to 5](../v5.0.0/migration.md)), so if you go
straight to 5 you can keep it.

## Creating records

`ConstructUsing` set the expression that created each record, such as `() => new Order { Source = "import" }`. It's
removed, with `ClassMap.Constructor`: CsvHelper creates objects through `ObjectResolver.Current`. Defaults that you set
in the expression go in the class's constructor.

```csharp before
public class Order
{
    public int Id { get; set; }
    public string Source { get; set; }
}

public class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        ConstructUsing(() => new Order { Source = "import" });
        Map(m => m.Id);
    }
}
```

```csharp after
public class Order
{
    public Order()
    {
        Source = "import";
    }

    public int Id { get; set; }
    public string Source { get; set; }
}

public class OrderMap : ClassMap<Order>
{
    public OrderMap()
    {
        Map(m => m.Id);
    }
}
```

To create another type, such as a subclass, set `ObjectResolver.Current`. The reader asks the resolver for an object
of the record type, and creates the records with the parameterless constructor of the type it gets back: the resolver
chooses the type, and that type's constructor sets the values.

```csharp
public class Order
{
    public int Id { get; set; }
}

public class ImportedOrder : Order
{
}

var defaultResolver = ObjectResolver.Current;
ObjectResolver.Current = new ObjectResolver(
    type => true,
    (type, constructorArgs) => type == typeof(Order)
        ? new ImportedOrder()
        : defaultResolver.Resolve(type, constructorArgs));
```

- **Resolve every type:** CsvHelper also creates class maps and type converters through the resolver. Have
  `CanResolve` (the first function) return `true` and hand the other types to the resolver you replace, as above. A
  resolver that can't resolve a type falls back to `ObjectResolver.Current`, which is itself, so it overflows the
  stack. CsvHelper 8 fixes this: the fallback creates the object itself.
- **It's global:** `ObjectResolver.Current` is static, so it applies to every reader and writer in the process.
