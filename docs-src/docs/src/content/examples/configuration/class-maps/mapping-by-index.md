# Mapping by Index

If your data doesn't have a header you can map by index instead of name. You can't rely on the order of class properties in .NET, so if you're not mapping by name, make sure you specify an index.

###### Data
```
1,one
```

###### Example

```cs
void Main()
{
    using (var reader = new StreamReader("path\\to\\file.csv"))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
		csv.Configuration.HasHeaderRecord = false;
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
        Map(m => m.Id).Index(0);
        Map(m => m.Name).Index(1);
    }
}
```
