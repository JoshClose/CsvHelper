# Type Conversion

If you need to convert to or from a non-standard .NET type, you can supply a type converter to use for a property.

###### Data

```
Id,Name,Json
1,one,"{ ""Foo"": ""Bar"" }"
```

###### Example

```cs
void Main()
{
	using (var reader = new StreamReader("path\\to\\file.csv"))
	using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
	{
		csv.Configuration.RegisterClassMap<FooMap>();
		csv.GetRecords<Foo>().ToList().Dump();
	}
}

public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
	public Json Json { get; set; }
}

public class Json
{
	public string Foo { get; set; }
}

public class JsonConverter<T> : DefaultTypeConverter
{
	public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
	{
		return JsonConvert.DeserializeObject<T>(text);
	}

	public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
	{
		return JsonConvert.SerializeObject(value);
	}
}

public class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id);
		Map(m => m.Name);
		Map(m => m.Json).TypeConverter<JsonConverter<Json>>();
	}
}
```
