# Constant Value

You can set a constant value to a property instead of mapping it to a field.

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
		Map(m => m.Id);
		Map(m => m.Name);
        Map(m => m.IsDirty).Constant(true);
    }
}
```
