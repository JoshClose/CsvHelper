# Reading by Hand

Sometimes it's easier to not try and configure a mapping to match your class definition for various reasons. It's usually only a few more lines of code to just read the rows by hand instead.

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
        var records = new List<Foo>();
		csv.Read();
		csv.ReadHeader();
		while (csv.Read())
		{
			var record = new Foo
			{
				Id = csv.GetField<int>("Id"),
				Name = csv.GetField("Name")
			};
			records.Add(record);
		}
    }
}

public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```
