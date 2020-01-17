# Optional Maps

If you have data that may or may not have a header, you can make the mapping optional.

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
		csv.GetRecords<Foo>().ToList().Dump();
	}
}

public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
	public DateTimeOffset? Date { get; set; }
}

public class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id);
		Map(m => m.Name);
		Map(m => m.Date).Optional();
	}
}
```

