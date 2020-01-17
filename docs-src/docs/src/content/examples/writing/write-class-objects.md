# Write Class Objects

###### Example

```cs
void Main()
{
	var records = new List<Foo>
	{
		new Foo { Id = 1, Name = "one" },
	};
	
	using (var writer = new StreamWriter("path\\to\\file.csv"))
	using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
	{
		csv.WriteRecords(records);
	}
}

public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```

###### Output

```
Id,Name
1,one
```
