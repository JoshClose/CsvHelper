# Get Class Records

Convert CSV rows into class objects.

Sometimes OS settings for CSV's overwrites the default comma delimiter. If you experience problems try setting the delimiter manually - `csv.Configuration.Delimiter = ",";`

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
	using (var csv = new CsvReader(reader))
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
