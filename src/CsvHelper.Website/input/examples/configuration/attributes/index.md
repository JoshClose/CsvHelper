# Attributes

Most of the configuration done via class maps can also be done using attributes.

###### Data

```
Identifier||Amount2|IsBool|Constant
1|1,234|1,234|yes|a
2|1.234|1.234|no|b
```

###### Example

```cs
void Main()
{
	CsvConfiguration config = CsvConfiguration.FromType<Foo>();
	using (var reader = new StreamReader("path\\to\\file.csv"))
	using (var csv = new CsvReader(reader, config))
	{
		List<Foo> records = csv.GetRecords<Foo>().ToList();

		// These all print "True"

		Console.WriteLine(records.Count == 2);
		Console.WriteLine(records[0].Id == 1);
		Console.WriteLine(records[0].Amount == 1.234m);
		Console.WriteLine(records[0].Amount2 == 1234);
		Console.WriteLine(records[0].IsBool == true);
		Console.WriteLine(records[0].Constant == "bar");
		Console.WriteLine(records[0].Optional == null);
		Console.WriteLine(records[0].Ignored == null);

		Console.WriteLine(records[1].Amount == 1234);
		Console.WriteLine(records[1].Amount2 == 1.234m);

	}
}

[Delimiter("|")]
[CultureInfo("de-DE")]
public class Foo
{
	[Name("Identifier")]
	public int Id { get; set; }

	[Index(1)]
	public decimal Amount { get; set; }
	
	[CultureInfo("InvariantCulture")]
	public decimal Amount2 { get; set; }

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
