# Write Anonymous Type Objects

###### Example

```cs
void Main()
{
	var records = new List<object>
	{
		new { Id = 1, Name = "one" },
	};
	
	using (var writer = new StreamWriter("path\\to\\file.csv"))
	using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
	{
		csv.WriteRecords(records);
	}
}
```

###### Output

```
Id,Name
1,one
```
