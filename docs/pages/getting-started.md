---
title: Getting started
icon: rocket
description: Add CsvHelper to a project, then read and write your first CSV file.
---

# Getting started

CsvHelper runs on .NET 8 and later with SIMD, and on .NET Framework 4.7+ and .NET Standard 2.0 and 2.1 (.NET 5 to 7,
older .NET Core, Mono, Unity) with a portable scanner. Every build reads and writes the same CSV; see
[Target frameworks](target-frameworks.html) for what differs.

New to .NET? [Prerequisites](prerequisites.html) covers the basics that CsvHelper assumes, and that most questions
about it turn out to be about: disposing, files and streams, encodings, cultures and lazy sequences.

## Upgrading from CsvHelper 33

Version 34 is a rewrite with a new API: immutable options instead of `CsvConfiguration`, source-generated records
instead of class maps, and one `CsvHelper` namespace. The [migration guide](../releases/v34.0.0/migration.md) goes
through it concept by concept, with before and after code.

## Install

<!-- if:released -->
Add the package:

```shell
dotnet add package CsvHelper
```

The package includes the source generator for `[CsvRecord]` types, so there is nothing else to install.
<!-- endif -->
<!-- if:unreleased -->
CsvHelper is not on NuGet yet. Build it from source and reference the project:

```shell
git clone https://github.com/JoshClose/CsvHelper.git
cd CsvHelper
dotnet build -c Release
```

```xml
<ItemGroup>
  <ProjectReference Include="..\CsvHelper\src\CsvHelper\CsvHelper.csproj" />
  <!-- For [CsvRecord] types: the source generator, which the package will include. -->
  <ProjectReference Include="..\CsvHelper\src\CsvHelper.Generators\CsvHelper.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```
<!-- endif -->

The options are records with `init` properties, so a project that sets them needs C# 9 or later
(`<LangVersion>latest</LangVersion>` works for .NET Framework projects too). The `[CsvRecord]` source generator needs
a Roslyn 4.3 compiler or later (the .NET 6.0.400 SDK, Visual Studio 2022 17.3).

## Read a file

```csharp
using CsvHelper;

using var reader = CsvReader.FromFile("orders.csv");
var price = reader.GetFieldIndex("Price");
while (reader.Read())
{
    string id = reader.GetString(0);
    decimal value = reader.Parse<decimal>(price);
}
```

The reader detects the separator, the encoding and the line endings, matches header names ignoring case and
surrounding whitespace, and recovers from stray quotes. [Messy files](messy-files.html) lists what it adapts to and the
options for malformed input.

## Read records into objects

Mark a `partial` type with `[CsvRecord]` and the source generator writes its mapping code at compile time:

```csharp
[CsvRecord]
public partial class Order
{
    public string Id { get; set; } = "";
    [CsvColumn("Unit Price")]
    public decimal Price { get; set; }
}

using var reader = CsvReader.FromFile("orders.csv");
foreach (Order order in reader.GetRecords<Order>())
{
    Console.WriteLine($"{order.Id}: {order.Price}");
}
```

## Write a file

```csharp
using var writer = CsvWriter.ToFile("out.csv");
writer.WriteHeader("Id", "Price");
writer.WriteField("A-1");
writer.WriteField(9.99m);
writer.EndRecord();
```

By default the writer produces strict RFC 4180: CRLF line endings, quotes only where needed, doubled quotes inside them, and
the same number of fields in every record.

## Next steps

- [Prerequisites](prerequisites.html): the .NET basics that CsvHelper assumes, such as disposing, streams, encodings,
  cultures and lazy sequences.
- [Reading](reading.html): sources, headers, fields and rows, async and parallel reading, the parsing modes and every
  reader option.
- [Records](records.html): the `[CsvRecord]`, `[CsvColumn]`, `[CsvIgnore]` and `[CsvConstructor]` attributes and
  everything the generator can map.
- [Messy files](messy-files.html): what the reader recovers from, and how to report, skip or reject malformed records.
- [Writing](writing.html): values and formats, quoting, the output modes, async and parallel writing and every writer
  option.
- [Unit testing](testing.html): test code that reads or writes CSV without mocks.
- [API reference](api/index.html): every type and member.
