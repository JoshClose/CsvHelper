# Reading Multiple Record Types

If you have CSV data where each row may be a different record type, you should be able to read based on a row type or something similar.

###### Data

```
A,1,foo
B,07a0fca2-1b1c-4e44-b1be-c2b05da5afc7,bar
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
		csv.Configuration.RegisterClassMap<BarMap>();
		var fooRecords = new List<Foo>();
		var barRecords = new List<Bar>();
		while (csv.Read())
		{
			switch (csv.GetField(0))
			{
				case "A":
					fooRecords.Add(csv.GetRecord<Foo>());
					break;
				case "B":
					barRecords.Add(csv.GetRecord<Bar>());
					break;
				default:
					throw new InvalidOperationException("Unknown record type.");
			}
		}
    }
}

public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Bar
{
	public Guid Id { get; set; }
	public string Name { get; set; }
}

public sealed class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id).Index(1);
		Map(m => m.Name).Index(2);
	}
}

public sealed class BarMap : ClassMap<Bar>
{
	public BarMap()
	{
		Map(m => m.Id).Index(1);
		Map(m => m.Name).Index(2);
	}
}
```
