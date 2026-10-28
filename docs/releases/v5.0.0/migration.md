# Migrating from CsvHelper 4 to 5

CsvHelper 5 brings back the class map methods for references that CsvHelper 4 removed: `References<TClassMap>` on
`ClassMap<TClass>`, and `Prefix` on `MemberReferenceMap` and `ParameterReferenceMap`. Nothing is removed, so code
written for 4 compiles unchanged. The target frameworks don't change: .NET Standard 2.0 and .NET Framework 4.5.

## Reference maps

In 4 you mapped the members of a reference (a member whose type is a class mapped to columns of its own) from the
parent map, with sub property mapping. That still works. You can also map the reference with the reference type's own
class map again, and prefix its headers in the parent map:

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

public class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name);
        Map(m => m.Address.Street).Name("Address.Street");
        Map(m => m.Address.City).Name("Address.City");
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

public class AddressMap : ClassMap<Address>
{
    public AddressMap()
    {
        Map(m => m.Street);
        Map(m => m.City);
    }
}

public class CustomerMap : ClassMap<Customer>
{
    public CustomerMap()
    {
        Map(m => m.Name);
        References<AddressMap>(m => m.Address).Prefix();
    }
}
```

- **The prefix:** `Prefix()` without an argument uses the member name and a dot, `Address.` here. Pass a string for
  another prefix.
- **One or the other:** once a member is mapped with `References`, mapping its members with sub property mapping
  doesn't work. Choose one way for each reference.
- **The configuration wins:** when `ReferenceHeaderPrefix` is set in the configuration, it's applied to each reference
  when the map is registered, and replaces the prefix from `Prefix()`.

## Behavior changes

- **Conversion error messages:** when a built-in type converter can't convert a field, the `TypeConverterException`
  has the detailed message the converter builds instead of only "The conversion cannot be performed.": the field's
  text, the member type and the type converter. The message contains the field's text, so keep that in mind if you
  log it.
