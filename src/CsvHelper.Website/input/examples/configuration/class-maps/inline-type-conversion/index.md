﻿# Inline Type Conversion

If you don't want to write a full `ITypeConverter` implementation, you can specify a function that will do the same thing.

## Reading

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
        csv.Context.RegisterClassMap<FooMap>();
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

public class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.Id);
        Map(m => m.Name);
        Map(m => m.Json).Convert(args => JsonConvert.DeserializeObject<Json>(args.Row.GetField("Json")));
    }
}
```

## Writing

###### Example

```cs
void Main()
{
	var records = new List<Foo>
	{
		new Foo { Id = 1, Name = "one" }
	};
	
	using (var writer = new StreamWriter("path\\to\\file.csv"))
	using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
	{
		csv.Context.RegisterClassMap<FooMap>();
		csv.WriteRecords(records);
		
		writer.ToString().Dump();
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

public class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id);
		Map(m => m.Name);
		Map(m => m.Json).Convert(args => JsonConvert.SerializeObject(args.Row));
	}
}
```

###### Output

```
Id,Name,Json
1,one,"{""Id"":1,""Name"":""one"",""Json"":null}"
```
