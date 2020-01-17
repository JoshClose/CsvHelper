# Attributes

Most of the configuration done via class maps can also be done using attributes.

[You can view the full list of available attributes here.](/api/CsvHelper.Configuration.Attributes)

###### Data

```
Identifier,name,IsBool,Constant
1,one,yes,a
2,two,no,b
```

###### Example

```cs
void Main()
{
	using (var reader = new StreamReader("path\\to\\file.csv"))
	using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
	{
		csv.GetRecords<Foo>().ToList().Dump();
	}
}

public class Foo
{
	[Name("Identifier")]
	public int Id { get; set; }
	
	[Index(1)]
	public string Name { get; set; }
	
	[BooleanTrueValues("yes")]
	[BooleanFalseValues("no")]
	public bool IsBool { get; set; }
	
	[Constant("bar")]
	public string Constant { get; set; }
	
	[Optional]
	public string Optional { get; set; }
	
	[Ignore]
	public string Ignored { get; set; }	
}

```
