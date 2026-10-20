# Migrating from CsvHelper 14 to 15

CsvHelper 15 makes `[Ignore]` work on a reference member: a member whose type is a class that auto mapping maps as
its own set of columns, such as an `Address` property with `Street` and `City`. The attribute now ignores all of that
class's columns. In 14 it had no effect there. No API was removed or renamed.

## Ignoring a reference member

In 14, leaving a reference's columns out took an `[Ignore]` on each member of the referenced class, which left them
out wherever that class was used. Now one attribute on the reference member does it:

```csharp before
public class Person
{
    public int Id { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    [Ignore]
    public string Street { get; set; }

    [Ignore]
    public string City { get; set; }
}
```

```csharp after
public class Person
{
    public int Id { get; set; }

    [Ignore]
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}
```

Either way, writing a `Person` writes the header `Id`, and reading one doesn't read `Street` or `City`.

- **An `[Ignore]` you already have starts working.** If a reference member has the attribute, its columns are no
  longer read or written. Remove the attribute to keep them.
- **Nested references:** 15.0.0 ignores the members of the referenced class, but not the references inside it.
  15.0.10 ignores the whole tree.
