# Enumerate Class Records

Convert CSV rows into a class object that is re-used on every iteration of the enumerable. Each enumeration will hydrate the given record, but only the mapped members. If you supplied a map and didn't map one of the members, that member will not get hydrated with the current row's data. Be careful. Any methods that you call on the projection that force the evaluation of the `IEnumerable`, such as `ToList()`, you will get a list where all the records are the same instance you provided that is hydrated with the last record in the CSV file.

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
		var record = new Foo();
        var records = csv.EnumerateRecords(record);
		foreach (var r in records)
		{
			// r is the same instance as record.
		}
    }
}

public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```
