# Reading Multiple Data Sets

For some reason there are CSV files out there that contain multiple sets of CSV data in them. You should be able to read files like this without issue. You will need to detect when to change class types you are retreiving.

###### Data
```
FooId,Name
1,foo

BarId,Name
07a0fca2-1b1c-4e44-b1be-c2b05da5afc7,bar
```

###### Example

```cs
void Main()
{
    using (var reader = new StreamReader("path\\to\\file.csv"))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
		csv.Configuration.IgnoreBlankLines = false;
		csv.Configuration.RegisterClassMap<FooMap>();
		csv.Configuration.RegisterClassMap<BarMap>();
		var fooRecords = new List<Foo>();
		var barRecords = new List<Bar>();
		var isHeader = true;
		while (csv.Read())
		{
			if (isHeader)
			{
				csv.ReadHeader();
				isHeader = false;
				continue;
			}
			
			if (string.IsNullOrEmpty(csv.GetField(0)))
			{
				isHeader = true;
				continue;
			}

			switch (csv.Context.HeaderRecord[0])
			{
				case "FooId":
					fooRecords.Add(csv.GetRecord<Foo>());
					break;
				case "BarId":
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
		Map(m => m.Id).Name("FooId");
		Map(m => m.Name);
	}
}

public sealed class BarMap : ClassMap<Bar>
{
	public BarMap()
	{
		Map(m => m.Id).Name("BarId");
		Map(m => m.Name);
	}
}
```
