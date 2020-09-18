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

## CultureInfo

CsvHelper requires you to specify the `CultureInfo` that you want to use. The culture is used to determine the default delimiter, default line ending, and formatting when type converting. You can change the configuration of any of these too if you like. Choose the appropriate culture for your data. `InvariantCulture` will be the most portable for writing a file and reading it back again, so that will be used in most of the examples.

## Reading a CSV File
<hr />

Let's say we have CSV file that looks like this.

```
Id,Name
1,one
2,two
```

And a class definition that looks like this.

<!-- snippet: Poco -->
<a id='snippet-poco'></a>
```cs
public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L16-L24' title='File snippet `poco` was extracted from'>snippet source</a> | <a href='#snippet-poco' title='Navigate to start of snippet `poco`'>anchor</a></sup>
<!-- endSnippet -->

If our class property names match our CSV file header names, we can read the file without any configuration.

<!-- snippet: Reading -->
<a id='snippet-reading'></a>
```cs
using var reader = new StreamReader(@"path\to\file.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
var records = csv.GetRecords<Foo>();
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L76-L82' title='File snippet `reading` was extracted from'>snippet source</a> | <a href='#snippet-reading' title='Navigate to start of snippet `reading`'>anchor</a></sup>
<!-- endSnippet -->

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

<!-- snippet: PrepareHeaderForMatch -->
<a id='snippet-prepareheaderformatch'></a>
```cs
using var reader = new StreamReader(@"path\to\file.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture)
{
	Configuration =
	{
		PrepareHeaderForMatch = (string header, int index) => header.ToLower()
	}
};
var records = csv.GetRecords<Foo>();
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L87-L99' title='File snippet `prepareheaderformatch` was extracted from'>snippet source</a> | <a href='#snippet-prepareheaderformatch' title='Navigate to start of snippet `prepareheaderformatch`'>anchor</a></sup>
<!-- endSnippet -->

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

<!-- snippet: HasHeaderRecord -->
<a id='snippet-hasheaderrecord'></a>
```cs
using var reader = new StreamReader(@"path\to\file.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture)
{
	Configuration =
	{
		HasHeaderRecord = false
	}
};
var records = csv.GetRecords<Foo>();
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L104-L116' title='File snippet `hasheaderrecord` was extracted from'>snippet source</a> | <a href='#snippet-hasheaderrecord' title='Navigate to start of snippet `hasheaderrecord`'>anchor</a></sup>
<!-- endSnippet -->

CsvReader will use the position of the properties in the class as the index position. There is an
issue with this though. [You can't rely on the ordering of class members in .NET](https://blogs.msdn.microsoft.com/haibo_luo/2006/07/10/member-order-returned-by-getfields-getmethods/).
We can solve this by mapping the property to a position in the CSV file.

One way to do this is with attribute mapping.

<!-- snippet: IndexPoco -->
<a id='snippet-indexpoco'></a>
```cs
public class FooWithIndex
{
	[Index(0)] public int Id { get; set; }

	[Index(1)] public string Name { get; set; }
}
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L26-L35' title='File snippet `indexpoco` was extracted from'>snippet source</a> | <a href='#snippet-indexpoco' title='Navigate to start of snippet `indexpoco`'>anchor</a></sup>
<!-- endSnippet -->

The `IndexAttribute` allows you to specify which position the CSV field is that you want to use
for the property.

You can also map by name. Let's use our lower case header example from before and see how we can
use attributes instead of changing the header matching.

<!-- snippet: NamePoco -->
<a id='snippet-namepoco'></a>
```cs
public class FooWithName
{
	[Name("id")] public int Id { get; set; }

	[Name("name")] public string Name { get; set; }
}
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L37-L46' title='File snippet `namepoco` was extracted from'>snippet source</a> | <a href='#snippet-namepoco' title='Navigate to start of snippet `namepoco`'>anchor</a></sup>
<!-- endSnippet -->

[There are many other attributes you can use also.](/api/CsvHelper.Configuration.Attributes)

What if we don't have control over the class we want to map to so we can't add attributes to it?
In this case, we can use a fluent `ClassMap` to do the mapping.

<!-- snippet: ClassMap -->
<a id='snippet-classmap'></a>
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
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L48-L59' title='File snippet `classmap` was extracted from'>snippet source</a> | <a href='#snippet-classmap' title='Navigate to start of snippet `classmap`'>anchor</a></sup>
<!-- endSnippet -->

To use the mapping, we need to register it in the configuration.

<!-- snippet: RegisterClassMap -->
<a id='snippet-registerclassmap'></a>
```cs
using var reader = new StreamReader(@"path\to\file.csv");
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
csv.Configuration.RegisterClassMap<FooMap>();
var records = csv.GetRecords<Foo>();
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L142-L149' title='File snippet `registerclassmap` was extracted from'>snippet source</a> | <a href='#snippet-registerclassmap' title='Navigate to start of snippet `registerclassmap`'>anchor</a></sup>
<!-- endSnippet -->

Creating a class map is the recommended way of mapping files in CsvHelper because it's a
lot more powerful.

## Writing a CSV File

Now let's look at how we can write CSV files. It's basically the same thing, but in reverse order.

Let's use the same class definition as before.

<!-- snippet: Poco -->
<a id='snippet-poco'></a>
```cs
public class Foo
{
	public int Id { get; set; }
	public string Name { get; set; }
}
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L16-L24' title='File snippet `poco` was extracted from'>snippet source</a> | <a href='#snippet-poco' title='Navigate to start of snippet `poco`'>anchor</a></sup>
<!-- endSnippet -->

And we have a set of records like this.

<!-- snippet: ListForWriting -->
<a id='snippet-listforwriting'></a>
```cs
var records = new List<Foo>
{
	new Foo {Id = 1, Name = "one"},
	new Foo {Id = 2, Name = "two"},
};
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L121-L129' title='File snippet `listforwriting` was extracted from'>snippet source</a> | <a href='#snippet-listforwriting' title='Navigate to start of snippet `listforwriting`'>anchor</a></sup>
<!-- endSnippet -->

We can write the records to a file without any configuration.

<!-- snippet: Writing -->
<a id='snippet-writing'></a>
```cs
using var writer = new StreamWriter(@"path\to\file.csv");
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
csv.WriteRecords((IEnumerable) records);
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L131-L137' title='File snippet `writing` was extracted from'>snippet source</a> | <a href='#snippet-writing' title='Navigate to start of snippet `writing`'>anchor</a></sup>
<!-- endSnippet -->

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

<!-- snippet: IndexAndNameMap -->
<a id='snippet-indexandnamemap'></a>
```cs
public class IndexAndNameMap : ClassMap<Foo>
{
	public IndexAndNameMap()
	{
		Map(m => m.Id).Index(0).Name("id");
		Map(m => m.Name).Index(1).Name("name");
	}
}
```
<sup><a href='https://github.com/JoshClose/CsvHelper/tests/CsvHelper.Tests/Snippets/Snippets.cs#L61-L72' title='File snippet `indexandnamemap` was extracted from'>snippet source</a> | <a href='#snippet-indexandnamemap' title='Navigate to start of snippet `indexandnamemap`'>anchor</a></sup>
<!-- endSnippet -->

<br/>
