# Ignoring Properties

When you use auto mapping in your class map, every property will get mapped. If there are properties that you don't want mapped, you can ignore them.

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
    public string Name { get; set; }
    public bool IsDirty { get; set; }
}

public sealed class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
        Map(m => m.IsDirty).Ignore();
    }
}
```
