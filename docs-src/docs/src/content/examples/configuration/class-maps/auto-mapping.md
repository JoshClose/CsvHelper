# Auto Mapping

If you don't supply a map to the configuration, one is automatically created for you on the fly. You can call auto mapping directly in your class map also. You may want to do this if you have a large number of properties that will be set up correctly by default, and only need to make a couple changes.

###### Data

```
Id,The Name
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
}

public sealed class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		AutoMap(CultureInfo.InvariantCulture);
		Map(m => m.Name).Name("The Name");
	}
}
```
