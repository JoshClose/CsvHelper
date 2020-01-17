# Get Class Records

Convert CSV rows into class objects.

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
		var records = csv.GetRecords<Foo>();
	}
}

public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```
