title: Getting Started
---
# Getting Started

## Installation
<hr />

### Package Manager Console

```
PM> Install-Package CsvHelper
```

### .NET CLI Console

```
> dotnet add package CsvHelper
```

## Prerequisites

There is some basic .NET knowledge that is implied when using this documentation. Please look over the prequisites to make sure you have an
understanding of them. [Prerequisites](/examples/prerequisites)

## CultureInfo

CsvHelper requires you to specify the `CultureInfo` that you want to use. The culture is used to determine the default delimiter, default line ending, and formatting when type converting. You can change the configuration of any of these too if you like. Choose the appropriate culture for your data. `InvariantCulture` will be the most portable for writing a file and reading it back again, so that will be used in most of the examples.

## Newlines

By default, CsvHelper will follow [RFC 4180](https://tools.ietf.org/html/rfc4180#page-2) and use `\r\n` for writing newlines no matter what operating system
you are running on. CsvHelper can read `\r\n`, `\r`, or `\n` without any configuration changes. If you want to reaad or write in a non-standard format, you can
change the configuration for `NewLine`.

```cs
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	NewLine = Environment.NewLine,
};
```

## Reading a CSV File
<hr />

Let's say we have CSV file that looks like this.

```
Id,Name
1,one
2,two
```

And a class definition that looks like this.

```cs
public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```

If our class property names match our CSV file header names, we can read the file without any configuration.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
	var records = csv.GetRecords<Foo>();
}
```

The `GetRecords<T>` method will return an `IEnumerable<T>` that will `yield` records.
What this means is that only a single record is returned at a time as you iterate the records.
That also means that only a small portion of the file is read into memory. Be careful though.
If you do anything that executes a LINQ projection, such as calling `.ToList()`, the entire file
will be read into memory. `CsvReader` is forward only, so if you want to run any LINQ queries
against your data, you'll have to pull the whole file into memory. Just know that is what you're doing.

Let's say our CSV file names are a little different than our class properties and we don't want to
make our properties match.

```
id,name
1,one
2,two
```

In this case, the names are lower case. We want our property names to be Pascal Case, so we can
just change how our properties match against the header names.

```cs
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	PrepareHeaderForMatch = (string header, int index) => header.ToLower(),
};
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, config))
{
	var records = csv.GetRecords<Foo>();
}
```

Using the configuration `PrepareHeaderForMatch`, we're able to change how the header matching
is done against the property name. Both the header and the property name are ran through the
`PrepareHeaderForMatch` function. When the reader needs to find the property to set for the
header, they will now match. You can use this function to do other things such as remove
whitespace or other characters.

Let's say out CSV file doesn't have a header at all.

```
1,one
2,two
```

First we need to tell the reader that there is no header record, using configuration.

```cs
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	HasHeaderRecord = false,
};
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, config))
{
	var records = csv.GetRecords<Foo>();
}
```

CsvReader will use the position of the properties in the class as the index position. There is an
issue with this though. [You can't rely on the ordering of class members in .NET](https://blogs.msdn.microsoft.com/haibo_luo/2006/07/10/member-order-returned-by-getfields-getmethods/).
We can solve this by mapping the property to a position in the CSV file.

One way to do this is with attribute mapping.

```cs
public class Foo
{
	[Index(0)]
	public int Id { get; set; }

	[Index(1)]
	public string Name { get; set; }
}
```

The `IndexAttribute` allows you to specify which position the CSV field is that you want to use
for the property.

You can also map by name. Let's use our lower case header example from before and see how we can
use attributes instead of changing the header matching.

```cs
public class Foo
{
	[Name("id")]
	public int Id { get; set; }

	[Name("name")]
	public string Name { get; set; }
}
```

[There are many other attributes you can use also.](/api/CsvHelper.Configuration.Attributes)

What if we don't have control over the class we want to map to so we can't add attributes to it?
In this case, we can use a fluent `ClassMap` to do the mapping.

```cs
public class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id).Name("id");
		Map(m => m.Name).Name("name");
	}
}
```

To use the mapping, we need to register it in the context.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
	csv.Context.RegisterClassMap<FooMap>();
	var records = csv.GetRecords<Foo>();
}
```

Creating a class map is the recommended way of mapping files in CsvHelper because it's a
lot more powerful.

You can also read rows by hand.

```cs
using (var reader = new StreamReader("path\\to\file.csv"))
using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
{
	csv.Read();
	csv.ReadHeader();
	while (csv.Read())
	{
		var record = csv.GetRecord<Foo>();
		// Do something with the record.
	}
}
```

`Read` will advance row. `ReadHeader` will read the row into CsvHelper as the header values.
Separating `Read` and `ReadHeader` allows you to do other things with the header row before
moving onto the next row. `GetRecord` also does not advance the reader to allow you to do
other things with the row you might need to do. You may need to `GetField` for a single field
or maybe call `GetRecord` multiple times to fill more than one object.


## Writing a CSV File

Now let's look at how we can write CSV files. It's basically the same thing, but in reverse order.

Let's use the same class definition as before.

```cs
public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```

And we have a set of records like this.

```cs
var records = new List<Foo>
{
	new Foo { Id = 1, Name = "one" },
	new Foo { Id = 2, Name = "two" },
};
```

We can write the records to a file without any configuration.

```cs
using (var writer = new StreamWriter("path\\to\\file.csv"))
using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
	csv.WriteRecords(records);
}
```

The `WriteRecords` method will write all the records to the file. After you are done writing,
you should call `writer.Flush()` to ensure that all the data in the writer's internal buffer
has been flushed to the file. Once a `using` block has exited, the writer is automatically
flushed, so we don't have to explicitly do it here. It's recommended to always wrap any
`IDisposable` object with `using` blocks. The object will dispose of itself (and in our case
flush too) as soon as possible after the `using` block has exited.

Remember how we can't rely on property order in .NET? If we are writing a class that has a header,
it doesn't matter, as long as we are reading using the headers later. If we want to position
the headers in the CSV file, we need to specify an index to guarantee it's order. It's
recommended to always set an index when writing.

```cs
public class FooMap : ClassMap<Foo>
{
	public FooMap()
	{
		Map(m => m.Id).Index(0).Name("id");
		Map(m => m.Name).Index(1).Name("name");
	}
}
```

You can also write rows by hand.

```cs
using (var writer = new StreamWriter("path\\to\\file.csv"))
using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
	csv.WriteHeader<Foo>();
	csv.NextRecord();
	foreach (var record in records)
	{
		csv.WriteRecord(record);
		csv.NextRecord();
	}
}
```

`WriteHeader` will not advance you to the next row. Separating `NextRecord` from `WriteHeader`
allows you to write more things in the header if you need to. `WriteRecord` also will not
advance you to the next row to give you the ability to write multiple objects or use
`WriteField` to write individual fields.

<br/>
