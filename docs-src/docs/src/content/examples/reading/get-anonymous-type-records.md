# Get Anonymous Type Records

Convert CSV rows into anonymous type objects. You just need to supply the anonymous type definition.

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
		var anonymousTypeDefinition = new
		{
			Id = default(int),
			Name = string.Empty
		};
        var records = csv.GetRecords(anonymousTypeDefinition);
    }
}
```
