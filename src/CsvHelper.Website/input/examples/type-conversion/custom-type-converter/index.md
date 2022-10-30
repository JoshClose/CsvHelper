# Custom Type Converters

The built in type converters will handle most situations for you, but if you find
a situation where they don't you can create your own type converter.

You can register the converter globally or per member via an attribute or class map.
You only need to use one, but all are shown in the example.

###### Data

```
Id,Name,Json
1,one,"{""foo"": ""bar""}"
```

###### Example

```cs
void Main()
{
    using (var reader = new new StreamReader("path\\to\\file.csv"))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        // Register globally.
        csv.Context.TypeConverterCache.AddConverter<JsonNode>(new JsonNodeConverter());
        csv.Context.RegisterClassMap<FooMap>();
        csv.GetRecords<Foo>().ToList().Dump();
    }
}

public class Foo
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Register via attribute.
    [TypeConverter(typeof(JsonNodeConverter))]
    public JsonNode Json { get; set; }
}

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.Id);
        Map(m => m.Name);
        // Register via map.
        Map(m => m.Json).TypeConverter<JsonNodeConverter>();
    }
}

public class JsonNodeConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
    {
        return JsonSerializer.Deserialize<JsonNode>(text);
    }
}
```
