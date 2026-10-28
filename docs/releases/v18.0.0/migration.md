# Migrating from CsvHelper 17 to 18

CsvHelper 18 lets you map constructor parameters. A type without a parameterless constructor is created with its
constructor, and each parameter is read from the column with the parameter's name. Now you can give a parameter
another column name or an index, with `[Name]` and `[Index]` on the parameter or with `Parameter` in a class map.
Nothing was removed or renamed: `NameAttribute` and `IndexAttribute` implement a new interface, `IParameterMapper`,
and can be put on parameters.

## Constructor parameters

Put `[Name]` or `[Index]` on the parameter:

```csharp
public class Foo
{
    public Foo([Name("Foo Id")] int id, [Name("Foo Name")] string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var records = csv.GetRecords<Foo>().ToList();
```

Or map the parameters in a class map, with `Parameter` and the parameter's name:

```csharp
public class Foo
{
    public Foo(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Parameter("id").Name("Foo Id");
        Parameter("name").Name("Foo Name");
    }
}

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Configuration.RegisterClassMap<FooMap>();
var records = csv.GetRecords<Foo>().ToList();
```

- **Map every parameter, in order.** The constructor is called with the parameters of the map, in the order of the
  `Parameter` calls.
- **Which constructor:** `Parameter(name)` looks for the parameter in the constructor with the most parameters, the
  one records are created with by default. `Parameter(() => constructor, name)` and
  `Parameter(constructor, parameter)` take another; set the configuration's `GetConstructor` to create records with
  it.
- **Name or index:** a parameter is read by its name when you set one, or when the file has a header and you didn't
  set an index; otherwise by its index. A parameter with only an index isn't checked by header validation.
- **Registering:** a class map with only `Parameter` maps can be registered; 17 threw "No mappings were specified".
