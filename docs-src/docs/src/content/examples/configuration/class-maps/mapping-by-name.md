# Mapping by Name

If your property names don't match your class names, you can map the property to the column by name.

###### Data

```
Column1,Column2
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
		Map(m => m.Id).Name("ColumnA");
		Map(m => m.Name).Name("ColumnB");
	}
}
```
