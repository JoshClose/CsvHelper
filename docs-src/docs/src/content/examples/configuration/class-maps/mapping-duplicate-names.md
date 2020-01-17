# Mapping Duplicate Names

Sometimes you have duplicate header names. This is handled through a header name index. The name index is the index of how many occurrences of that header name there are, not the position of the header.

###### Data

```
Id,Name,Name
1,first,last
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
    public string FirstName { get set; }
	public string LastName { get; set; }
}

public sealed class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.Id);
        Map(m => m.FirstName).Name("Name").NameIndex(0);
		Map(m => m.LastName).Name("Name").NameIndex(1);
    }
}
```
