# Mapping by Alternate Names

If you have a header name that could vary, you can specify multiple header names.

###### Data

```
Id,Name
1,one
```

###### Example

```cs
void Main()
{
    using (var reader = new StreamReader("path\\to\\file.csv"))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        csv.Configuration.RegisterClassMap<FooMap>();
        var records = csv.GetRecords<Foo>();
    }
}

public class Foo
{
    public int Id { get; set; }
    public string Name { get set; }
}

public sealed class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.Id).Name("TheId", "Id");
        Map(m => m.Name).Name("TheName", "Name");
    }
}
```
