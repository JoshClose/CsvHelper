# Appending to an Existing CSV File

###### Example

```cs
void Main()
{
	var records = new List<Foo>
	{
		new Foo { Id = 1, Name = "one" },
	};

	// Write to a file.
	using (var writer = new StreamWriter("path\\to\\file.csv"))
	using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
	{
		csv.WriteRecords(records);
	}

	records = new List<Foo>
	{
		new Foo { Id = 2, Name = "two" },
	};

	// Append to the file.
	var config = new CsvConfiguration(CultureInfo.InvariantCulture)
	{
		// Don't write the header again.
		HasHeaderRecord = false,
	};
	using (var stream = File.Open("path\\to\\file.csv", FileMode.Append))
	using (var writer = new StreamWriter(stream))
	using (var csv = new CsvWriter(writer, config))
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
2,two
```
