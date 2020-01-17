# Mapping Properties

This will map the properties of a class to the header names of the CSV data. The mapping needs to be registered in the configuration. This example is identical to not using a class mapping at all. The headers match the property names.

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
}

public sealed class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id);
		Map(m => m.Name);
	}
}
```
