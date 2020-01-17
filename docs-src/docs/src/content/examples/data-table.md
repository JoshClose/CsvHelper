# DataTable

The question on how to load a data table using CsvHelper came up so often that I just built the functionality in.

[CsvDataReader](/api/CsvHelper/CsvDataReader) implements `IDataReader`. This means it has all the capabilities of a forward only data reader. There is really no reason to use this class directly over using `CsvReader`. `CsvDataReader` requires an instance of `CsvReader` and uses it internally to do it's work.

Loading a `DataTable` in CsvHelper is simple. By default, a table will be loaded with all columns populated as strings. For the reader to be ready after instantiation, the first row needs to be read immediately, so you need to make any configuration changes before creating an instance of CsvDataReader.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
	// Do any configuration to `CsvReader` before creating CsvDataReader.
	using (var dr = new CsvDataReader(csv))
	{		
		var dt = new DataTable();
		dt.Load(dr);
	}
}
```

If you want to specify columns and column types, the data table will be loaded with the types automatically converted.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
	// Do any configuration to `CsvReader` before creating CsvDataReader.
	using (var dr = new CsvDataReader(csv))
	{		
		var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));

		dt.Load(dr);
	}
}
```
