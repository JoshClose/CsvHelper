---
title: Records
icon: cubes
description: Read and write your own classes, structs and records with the [CsvRecord] source generator, and control the mapping with [CsvColumn], [CsvIgnore], [CsvConstructor], and attributes for formats, cultures, converters, null and boolean values, defaults, constants, validation, collections, fixed widths and nested records; map types you cannot change with [CsvRecordMap]. No reflection, trimming and AOT safe.
---

# Records

Mark a `partial` class, struct or record `[CsvRecord]` and the CsvHelper source generator, which ships in the
package, writes its mapping code at compile time. The columns are looked up once per reader, then each record is read
with the same `GetString` and `Parse<T>` calls you would write by hand. The same code
[writes records](#writing-records), with one `WriteField` call per member. There is no reflection, so it works with
trimming and native AOT, and it runs as fast as hand-written code.

```csharp
using CsvHelper;

[CsvRecord]
public partial class Order
{
    public string Id { get; set; } = "";
    [CsvColumn("Unit Price")]
    public decimal Price { get; set; }
    [CsvColumn(Optional = true)]
    public int? Quantity { get; set; }
    [CsvIgnore]
    public decimal Total { get; set; }
}

[CsvRecord]
public partial record Line(string Sku, [CsvColumn(Index = 3)] double Weight, int Count = 1);

using var reader = CsvReader.FromFile("orders.csv");
foreach (Order order in reader.GetRecords<Order>()) { }          // also GetRecordsAsync<T>() and ParallelEnumerate<T>()
while (reader.Read()) { Order order = reader.GetRecord<Order>(); } // the current record

using var writer = CsvWriter.ToFile("copy.csv");
writer.WriteRecords(orders);                                     // the header, then the records
```

The examples on this page read this file, `orders.csv`, unless they say otherwise:

```csv
Id,Unit Price,Quantity
A-1,9.99,3
A-2,24.50,
```

## Declaring a record type

`[CsvRecord]` goes on a `partial` class, struct, record class or record struct. Each kind works:

```csharp
[CsvRecord]
public partial class Product                      // a class with settable properties
{
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
}

[CsvRecord]
public partial record Measurement(DateTime Time, double Value);   // a positional record: read through its constructor

[CsvRecord]
public partial struct Point                       // a struct with fields
{
    public double X;
    public double Y;
}

[CsvRecord]
public readonly partial record struct Rate(string Currency, decimal Value);
```

A type nested in another type needs every containing type to be `partial` too. Generic types, `ref struct`s,
`file` types and types that are not accessible from their whole assembly (`private` or `protected` nested types)
can't be records; the generator reports these as [compile errors](#compile-time-diagnostics). A type that you cannot
change, such as one of another library, is mapped with a [map class](#types-you-cannot-change-csvrecordmap) instead.

## Reading records

| Method | Reads |
|---|---|
| `reader.GetRecords<T>()` | The remaining records, as an `IEnumerable<T>` that reads one record each time it moves. |
| `reader.GetRecordsAsync<T>(cancellationToken)` | The remaining records, as an `IAsyncEnumerable<T>`. |
| `reader.GetRecord<T>()` | The current record, in a `while (reader.Read())` loop. |
| `reader.EnumerateRecords(record)`, `reader.EnumerateRecordsAsync(record, cancellationToken)` | The remaining records, each read into the same instance (see [Reusing one instance](#reusing-one-instance)). |
| `reader.GetRecord(record)` | The current record, into an instance of yours. |
| `reader.ParallelEnumerate<T>(maxDegreeOfParallelism)` | The remaining records, mapped on several threads and returned in input order (see [Reading on several threads](reading.html#reading-on-several-threads)). |
| `reader.ValidateHeader<T>()`, `reader.TryValidateHeader<T>(out missing)` | Nothing: they check that the header has the columns of `T`. |

```csharp
using var reader = CsvReader.FromFile("orders.csv");
foreach (Order order in reader.GetRecords<Order>())
{
    Console.WriteLine($"{order.Id}: {order.Price} x {order.Quantity?.ToString() ?? "?"}");
}

// A-1: 9.99 x 3
// A-2: 24.50 x ?
```

```csharp
await using var reader = CsvReader.Create(stream);
await foreach (Order order in reader.GetRecordsAsync<Order>(cancellationToken))
{
    await SaveAsync(order);
}
```

The sequences can be enumerated once, and records are created as you enumerate, so a large file is never in memory at
once (unless you call `ToList`). `GetRecords<T>` and `ParallelEnumerate<T>` read the header and look up the columns
when you call them, so a missing column throws there, before the first record; `GetRecordsAsync<T>` does both when the
enumeration starts.

`GetRecord<T>` reads the current record, which lets you mix records with other field access: filter records before
mapping them, or read files whose records have different shapes.

```csharp
// Id,Unit Price,Kind
// A-1,9.99,order
// A-2,1,comment
// A-3,2,order
using var reader = CsvReader.FromFile("mixed.csv");
var kind = reader.GetFieldIndex("Kind");
while (reader.Read())
{
    if (!reader.GetSpan(kind).SequenceEqual("order"u8)) continue;
    Order order = reader.GetRecord<Order>();       // A-1, then A-3
}
```

`GetRecord<T>` creates its mapper on the first call and reuses it while `T` stays the same. To hold on to a mapper
yourself, `CsvRecordMapper.Create<T>(reader)` creates one for the header of a reader, and `mapper.Read(reader)` reads
the current record. A mapper does not change after it is created, so it can be shared between threads and used with
any reader that has the same columns.

### Checking the header

`ValidateHeader<T>` makes the check that `GetRecords<T>` makes, without reading a record: it throws the same
`KeyNotFoundException` when required columns are missing. `TryValidateHeader<T>` returns them instead, so you can
report them your own way:

```csharp
using var reader = CsvReader.FromFile("orders.csv");
if (!reader.TryValidateHeader<Order>(out IReadOnlyList<string> missing))
{
    // missing: ["Order.Price ('Unit Price')"]
    throw new InvalidDataException($"orders.csv lacks {string.Join(", ", missing)}");
}

foreach (Order order in reader.GetRecords<Order>()) { }
```

### Reusing one instance

`EnumerateRecords(record)` reads every record into the instance you pass, so reading allocates no object per record
(strings are still created, unless [`PoolStrings`](reading.html#performance-tips) shares them). Each step returns the
same instance, so copy what you keep:

```csharp
var order = new Order();
decimal total = 0;
foreach (Order current in reader.EnumerateRecords(order))
{
    total += current.Price * (current.Quantity ?? 0);   // current is order
}
```

The type must be a class whose members can all be set after construction: a constructor with parameters or `init`
accessors make the enumeration throw `NotSupportedException` (`required` members with a `set` accessor are fine). Every member that a record sets is
set again for each record; a member whose [optional](#optional-columns) column is missing keeps its value, and a
[nested record](#nested-records) member gets a new instance. `EnumerateRecordsAsync` does the same asynchronously, and
`GetRecord(record)` reads the current record into an instance.

## Choosing columns: [CsvColumn]

### By name

A member reads the header column with its name. `[CsvColumn("name")]` reads another one. Names are matched as
`GetFieldIndex` matches them: an exact match wins, then case and surrounding whitespace are ignored, so `Id` reads a
column called `id` or ` ID `.

```csharp
[CsvRecord]
public partial class Contact
{
    public string Name { get; set; } = "";                    // the column "Name" (or "name", " NAME ", ...)
    [CsvColumn("E-mail address")]
    public string Email { get; set; } = "";
    [CsvColumn("Phone #")]
    public string? Phone { get; set; }
}
```

A column whose name changed between versions of a file can list its other names. The first of the names that the
header has is read, each matched as above before the next one is tried, and a missing-column error names them all:

```csharp
[CsvRecord]
public partial class Item
{
    [CsvColumn("Unit Price", "Price", "unit_price")]
    public decimal Price { get; set; }
}

// KeyNotFoundException: The CSV header has no column for Item.Price ('Unit Price' or 'Price' or 'unit_price').
```

When several columns have the same name, `NameIndex` says which one a member reads: 0 (the default) for the first, 1
for the second, and so on. The [record writer](#columns) writes the columns in that order:

```csharp
// Id,Amount,Tax,Amount
// A-1,10.00,1.90,11.90
[CsvRecord]
public partial class Invoice
{
    public string Id { get; set; } = "";
    [CsvColumn("Amount")]
    public decimal Net { get; set; }                   // 10.00
    [CsvColumn("Amount", NameIndex = 1)]
    public decimal Gross { get; set; }  // 11.90
}
```

The reader's [`NormalizeHeaderName`](reading.html#how-names-are-matched) applies to the names of members too.

### By index

`[CsvColumn(Index = n)]` reads the column at that 0-based position and ignores the header. Files without a header
(`HasHeader = false`) need an index on every member:

```csharp
[CsvRecord]
public partial class Account
{
    [CsvColumn(Index = 0)]
    public int Id { get; set; }
    [CsvColumn(Index = 1)]
    public string Name { get; set; } = "";
    [CsvColumn(Index = 2)]
    public decimal Balance { get; set; }
}

using var reader = CsvReader.FromText("1,Anna,10.5\n2,Ben,-3.75\n", new CsvReaderOptions { HasHeader = false });
List<Account> accounts = reader.GetRecords<Account>().ToList();
```

Names and indexes can be mixed: in the `Line` record at the top of the page, `Weight` reads the fourth column, whatever
its header says, while `Sku` and `Count` are found by name.

A [collection](#collections) member reads a range of columns: `[CsvColumn(Index = 1, EndIndex = 4)]` reads columns 1
to 4, and `Index` without `EndIndex` reads to the end of each record.

### Optional columns

A column that a member needs is required: if the header lacks it, `GetRecords<T>` throws one `KeyNotFoundException`
that names every missing column:

```csharp
// Code,Price
// A-1,9.99
reader.GetRecords<Order>();
// KeyNotFoundException: The CSV header has no column for Order.Id ('Id'), Order.Price ('Unit Price').
```

`[CsvColumn(Optional = true)]` lets a column be missing. Constructor parameters with a default value are always
optional. When the column is missing:

- a property or field with `set` keeps the value of its initializer;
- an `init` or `required` member gets the default of its type (`null`, `0`), since it can only be set while the object
  is created;
- a constructor parameter gets its default value.

```csharp
[CsvRecord]
public partial class Settings
{
    public string Key { get; set; } = "";
    [CsvColumn(Optional = true)]
    public int Retries { get; set; } = 3;          // 3 when the column is missing
    [CsvColumn(Optional = true)]
    public string Region { get; init; } = "EU";    // null when the column is missing
}

[CsvRecord]
public partial record Line(string Sku, [CsvColumn(Index = 3)] double Weight, int Count = 1);   // Count: 1 when missing
```

Optional is about the header. A column that is there but has an empty field is not missing: an empty field reads as
`""` for a `string` and as `null` for a nullable type, and throws `FormatException` for `int`, `double` and the other
value types. Use `int?` (or another nullable type) for values that can be empty.

## Members that are read

These members are read:

- public and internal properties and fields that can be set (`set`, `init` or `required`), including those of base
  classes;
- the parameters of the [constructor](#constructors-csvconstructor);
- other properties and fields, such as `private` ones or properties with a `private set`, only with `[CsvColumn]`.

Properties without a setter, computed properties, `readonly` fields and constants are not read. Members that a
constructor parameter of the same name sets (as in positional records) are not set again.

```csharp
public class Entity
{
    public int Id { get; set; }                                    // read: members of base classes are included
}

[CsvRecord]
public partial class Customer : Entity
{
    public required string Name { get; init; }                     // read
    internal string Country = "";                                  // read: internal fields too
    [CsvColumn]
    public string Secret { get; private set; } = "";   // read, because of [CsvColumn]
    public string Display => $"{Name} ({Country})";                // not read: no setter
    [CsvIgnore]
    public DateTime Imported { get; set; }             // not read
}
```

### Skipping members: [CsvIgnore]

`[CsvIgnore]` leaves a property or field out, for values you fill in yourself or that come from elsewhere. A member of
a type that can't be read from a field must be ignored, or the build fails with
[CSVH002](#compile-time-diagnostics). A `required` member can only be ignored when the constructor is marked
`[SetsRequiredMembers]`; otherwise nothing would set it ([CSVH006](#compile-time-diagnostics)).

### Leaving out base classes: [CsvIgnoreBase]

`[CsvIgnoreBase]` on a class reads and writes only the members it declares itself, for base classes whose members don't
belong in the file, such as the audit fields every entity of an application inherits:

```csharp
public class Audited
{
    public DateTime Created { get; set; }
    public string CreatedBy { get; set; } = "";
}

[CsvRecord]
[CsvIgnoreBase]
public partial class Memo : Audited
{
    public string Text { get; set; } = "";       // the only column: Text
}
```

The parameters of the constructor are still read, whichever class declares the members they set.

## Constructors: [CsvConstructor]

The record is created with one constructor:

1. the constructor marked `[CsvConstructor]`;
2. else the parameterless constructor (a struct's implicit one only when no constructor takes parameters);
3. else the public or internal constructor with the most parameters, which for a positional record is its primary
   constructor.

Each parameter reads the column with its name (ignoring case, so the parameter `name` reads a column `Name`), or the
column that `[CsvColumn]` on the parameter, or on the property of the same name, chooses. Members the constructor does
not set are then read as usual.

```csharp
[CsvRecord]
public partial class Person
{
    [CsvConstructor]
    public Person(string name, [CsvColumn("Years")] int age)
    {
        Name = name;
        Age = age;
    }

    public Person(string name) : this(name, 0)
    {
    }

    public string Name { get; }
    public int Age { get; }
    public string? Email { get; set; }        // set after the constructor runs
}

// Name,Years,Email
// Anna,31,anna@example.com
```

Without `[CsvConstructor]`, two constructors with the same, largest number of parameters are ambiguous, and so is a
constructor with `ref` or `out` parameters; the generator reports [CSVH003](#compile-time-diagnostics).

## Supported types

| Type | Read with |
|---|---|
| `string` | `GetString`. An empty field is `""`. |
| `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint`, `ulong`, `double`, `float`, `decimal`, `bool`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `BigInteger` | [`Parse<T>`](reading.html#parse-and-tryparse). |
| Enums | Names matched in UTF-8 ignoring ASCII case; numbers and flag lists (`"Read, Write"`) go through `Enum.TryParse`. |
| `Uri` | `new Uri(text, UriKind.RelativeOrAbsolute)`, so relative URIs such as `docs/readme.md` read too. A `Uri?` member is `null` for an empty field. |
| `Nullable<T>` of any of these | An empty field is `null`. |
| Types with a static `Parse` | `ISpanParsable<T>` on .NET 8 and later (`DateOnly`, `TimeOnly`, ...); `Parse(string, IFormatProvider)` or `Parse(string)` on the other targets and for your own types. |
| Any type | A [converter](#converters-csvconverter), on the member or [for the whole assembly](#converters-for-every-member-of-a-type). |
| Arrays, lists and dictionaries of these | Several columns, as [collections](#collections). |
| Record types | Their own columns of the same row, as [nested records](#nested-records). |

Parsing uses the reader's `FormatProvider`, so a record reads `9,99` as 9.99 when the reader has
`FormatProvider = CultureInfo.GetCultureInfo("de-DE")`.

### Enums

```csharp
public enum Status { Active, OnHold, Closed }

[Flags]
public enum Access { None = 0, Read = 1, Write = 2, Admin = 4 }

[CsvRecord]
public partial class Account
{
    public Status Status { get; set; }
    public Access Access { get; set; }
    public Status? Previous { get; set; }
}

// Status,Access,Previous
// onhold,"Read, Write",
// Closed,6,Active
//
// OnHold, Read | Write, null
// Closed, Write | Admin, Active
```

A value that is neither a name nor a number throws `FormatException` (`'pending' is not a value of Status.`, with the
full name of the type).

`[CsvEnumName]` gives an enum member other names, such as names with spaces, which are matched like its own name, by
`[CsvRecord]` members and by [`reader.Parse<T>`](reading.html#enums) alike. The own name still matches. The other
names match whole fields only, not the parts of a list of flags:

```csharp
public enum Stage
{
    Open,
    [CsvEnumName("on hold", "hold")]
    OnHold,
    Done,
}

// "on hold", "HOLD" and "OnHold" read as Stage.OnHold
```

### Your own types

A type with a static `Parse(string, IFormatProvider)` (or `Parse(string)`) method that returns it can be a member. On
.NET 8 and later, implementing `ISpanParsable<T>` works too.

```csharp
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Parse(string text, IFormatProvider? provider)
    {
        var space = text.IndexOf(' ');

        return new Money(decimal.Parse(text[..space], provider), text[(space + 1)..]);
    }
}

[CsvRecord]
public partial class Invoice
{
    public string Number { get; set; } = "";
    public Money Total { get; set; }                 // "12.50 EUR"
    public TimeSpan Duration { get; set; }           // "01:30:00"
    public DateOnly Due { get; set; }                // "2024-04-01": ISpanParsable<DateOnly> on .NET 8+
    [CsvIgnore]
    public Color Color { get; set; }     // a type without Parse: without [CsvIgnore], error CSVH002
}
```

A type that you cannot give a `Parse` method, such as one of another library, needs a
[converter](#converters-csvconverter).

## Reading values

These attributes change how a member's field is read. Like `[CsvColumn]`, they go on properties, fields and
constructor parameters (a parameter also takes those of the property it sets).

### Exact formats: [CsvFormat]

`[CsvFormat]` reads a date or time in exactly the given format, as `ParseExact` does, instead of the formats that the
format provider accepts:

```csharp
// Day,Time,Due
// 20240401,01:30,24.12.2024
[CsvRecord]
public partial class Shift
{
    [CsvFormat("yyyyMMdd")]
    public DateTime Day { get; set; }
    [CsvFormat("hh\\:mm")]
    public TimeSpan Time { get; set; }
    [CsvFormat("dd.MM.yyyy")]
    public DateOnly? Due { get; set; }       // an empty field is null
}
```

`DateTime` and `DateTimeOffset` members are read with `ParseExact<T>(index, format)`, which parses without creating a
string. Other types need a static `ParseExact(string, string, IFormatProvider)` method, as `DateOnly`, `TimeOnly` and
`TimeSpan` have. A field in another format throws `FormatException`.

For files that mix formats, such as data merged from several sources, give several: a field may have any of them,
tried in order, and the record writer writes the first.

```csharp
// Day
// 2024-04-01
// 15/04/2024
[CsvRecord]
public partial class Entry
{
    [CsvFormat("yyyy-MM-dd", "dd/MM/yyyy")]
    public DateTime Day { get; set; }
}
```

They are read with `ParseExact<T>(index, formats)`, or a static `ParseExact(string, string[], IFormatProvider)` method
for other types.

### Cultures: [CsvCulture]

`[CsvCulture]` parses a member with another culture than the reader's `FormatProvider`, for files whose columns
were written in different cultures:

```csharp
// Price;Preis;Datum
// 1.5;1.234,5;3. März 2024
[CsvRecord]
public partial class Mixed
{
    public decimal Price { get; set; }                                       // 1.5: the reader's format provider
    [CsvCulture("de-DE")]
    public decimal Preis { get; set; }                 // 1234.5
    [CsvCulture("de-DE")]
    [CsvFormat("d. MMMM yyyy")]
    public DateTime Datum { get; set; }
}
```

It applies to numbers, dates, times and types parsed with a format provider; the build warns
([CSVH010](#compile-time-diagnostics)) about others. The culture is looked up when the mapper is created, so an
unknown name throws `CultureNotFoundException` from `GetRecords<T>`. Apps that run in globalization invariant mode
only have the invariant culture (`""`).

### Booleans: [CsvBooleanValues]

`[CsvBooleanValues]` gives a `bool` member its own true and false values, which replace the built-in ones (`true`,
`yes`, `y`, `t`, `1` and their opposites):

```csharp
// Aktiv,Power
// Ja,on
// nein,0
[CsvRecord]
public partial class Switches
{
    [CsvBooleanValues("Ja", "Nein")]
    public bool Aktiv { get; set; }
    [CsvBooleanValues(new[] { "on", "1" }, new[] { "off", "0" })]
    public bool? Power { get; set; }
}
```

They are matched as the built-in ones are: ignoring ASCII case and surrounding spaces and tabs. Any other value throws
`FormatException` (`'true' is not a valid boolean. Expected Ja or Nein.`).

### Null values: [CsvNullValues]

`[CsvNullValues]` reads fields that equal one of the given values, exactly and with case, as `null`, such as the
`NULL` and `\N` of database exports:

```csharp
// Name,Count
// NULL,\N
[CsvRecord]
public partial class Export
{
    [CsvNullValues("NULL", "\\N")]
    public string? Name { get; set; }        // null
    [CsvNullValues("NULL", "\\N")]
    public int? Count { get; set; }          // null
    [CsvNullValues("")]
    public string? Comment { get; set; }                // an empty field is null, not ""
}
```

An empty field is already `null` for a nullable value type. The member must be able to be `null`: on an `int` the
build warns ([CSVH010](#compile-time-diagnostics)).

The reader's [`NullValues`](reading.html#null-values) apply to every `Nullable<T>` member and every string member
declared as nullable (`string?`), with or without the attribute:

```csharp
var options = new CsvReaderOptions { NullValues = ["NULL"] };
// Name,Count
// NULL,NULL
// "",
// Export.Name is null, then ""; Export.Count is null both times.
```

### Empty fields: [CsvDefault]

An empty field reads as `""` for a `string`, as `null` for a nullable type, and throws `FormatException` for the other
types. `[CsvDefault]` gives the value of an empty field instead:

```csharp
[CsvRecord]
public partial class OrderLine
{
    [CsvDefault(0)]
    public int Quantity { get; set; }                // "" reads as 0
    [CsvDefault(1.0)]
    public double Factor { get; set; }
    [CsvDefault(Stage.Open)]
    public Stage Stage { get; set; }
    [CsvDefault(null)]
    public string? Comment { get; set; }          // "" reads as null
}
```

The value must be a constant that converts implicitly to the member's type (`1.5` does not convert to `float`; write
`1.5f`), or `null` for reference and nullable types. Anything else fails the build
([CSVH011](#compile-time-diagnostics)). A column that is missing from the header is a different case, which
[`Optional`](#optional-columns) handles.

### Converters: [CsvConverter]

For types that cannot be read otherwise, and for formats of your own, an `ICsvConverter<T>` reads the field, and
writes it for [record writing](#writing-records):

```csharp
public sealed class PercentConverter : ICsvConverter<double>
{
    public double Read(CsvReader reader, int index) =>
        double.Parse(reader.GetString(index).TrimEnd('%'), CultureInfo.InvariantCulture) / 100;

    public void Write(CsvWriter writer, double value) => writer.WriteField((value * 100).ToString(CultureInfo.InvariantCulture) + "%");
}

public sealed class HexConverter : ICsvConverter<int>
{
    public int Read(CsvReader reader, int index) =>
        int.Parse(reader.GetString(index), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    public void Write(CsvWriter writer, int value) => writer.WriteField(value, "x");
}

[CsvRecord]
public partial class Link
{
    [CsvConverter(typeof(PercentConverter))]
    public double Share { get; set; }   // "12.5%" is 0.125
    [CsvConverter(typeof(HexConverter))]
    public int? Code { get; set; }          // "ff" is 255, an empty field null
}
```

- The generated mapper creates one instance of each converter with `new` and calls it directly, so converters work
  with trimming and native AOT. A converter needs a public or internal parameterless constructor.
- The threads of `ParallelEnumerate<T>` share a mapper, and those of `ParallelWriteRecords` a record writer, so a
  converter must be thread-safe (one without fields is).
- For a `T?` member, an `ICsvConverter<T>` reads the fields that are not empty, and an empty field is `null`; an
  `ICsvConverter<T?>` gets every field.
- `Write` writes one field, with any of the writer's `WriteField` methods, in a form that `Read` reads back. A `null`
  value is written without calling it (as an empty field, or the first `[CsvNullValues]` value), except that the
  `ICsvConverter<T?>` of a `Nullable<T>` member gets nulls too.
- `[CsvNullValues]` and `[CsvDefault]` still apply before the converter is called. `[CsvFormat]`, `[CsvCulture]` and
  `[CsvBooleanValues]` don't apply ([CSVH010](#compile-time-diagnostics)).

The converter gets the reader, so it can read other fields of the record too, but it only knows the index of its own
column. To build a member from several columns, use a [nested record](#nested-records), a
[collection](#collections) or a [hand-written mapper](#hand-written-mappers).

### Converters for every member of a type

A type that appears in many record types, such as a money type of another library, gets its converter once for the
whole assembly instead of a `[CsvConverter]` on every member:

```csharp
[assembly: CsvConverter(typeof(MoneyConverter))]

public sealed class MoneyConverter : ICsvConverter<Money>
{
    public Money Read(CsvReader reader, int index) => Money.FromCents(reader.Parse<long>(index));

    public void Write(CsvWriter writer, Money value) => writer.WriteField(value.Cents);
}

[CsvRecord]
public partial class Order
{
    public Money Total { get; set; }                                  // MoneyConverter
    public Money? Discount { get; set; }                              // MoneyConverter; an empty field is null
    [CsvConverter(typeof(TaxConverter))]
    public Money Tax { get; set; }   // the member's own converter wins
}
```

- The converter applies to every member, in the record types of the assembly, whose type is one it implements
  `ICsvConverter<T>` for, or a `Nullable<T>` of it. The attribute names only the converter; one that implements
  `ICsvConverter<T>` for several types converts all of them.
- Built-in types can have one too: `[assembly: CsvConverter(typeof(UkDateConverter))]` reads and writes every
  `DateTime` member with it.
- A member's own `[CsvConverter]`, `[CsvFormat]`, `[CsvBooleanValues]` or `[CsvCulture]` takes precedence.
- The generated code is the same as with `[CsvConverter]` on each member, so it costs nothing per field. Two converters
  of the assembly cannot convert the same type ([CSVH009](#compile-time-diagnostics)).
- The attribute applies to the record types of its own project. The converters themselves can come from a shared
  library.

### Constant values: [CsvConstant]

`[CsvConstant]` gives a member the same value in every record, without reading a column, and the record writer writes
that value in the member's column whatever the member holds:

```csharp
[CsvRecord]
public partial class Reading
{
    public string Sensor { get; set; } = "";
    public double Value { get; set; }
    [CsvConstant("v2")]
    public string Schema { get; set; } = "";    // "v2", also when the file has no Schema column
}
```

The value must be a constant that converts implicitly to the member's type, or `null` for reference and nullable types
([CSVH011](#compile-time-diagnostics) otherwise). It works on constructor parameters too.

### Checking values: [CsvValidate]

`[CsvValidate]` checks a member's value with a static method of the type, which returns `true` for a valid value. A
value it rejects makes the record fail with a `CsvValidationException`, reported like a field that does not parse (see
[Errors](#errors)):

```csharp
[CsvRecord]
public partial class Signup
{
    [CsvValidate(nameof(IsName), Message = "A name is required.")]
    public string Name { get; set; } = "";
    [CsvValidate(nameof(IsAge))]
    public int Age { get; set; }

    private static bool IsName(string name) => name.Trim().Length > 0;
    private static bool IsAge(int age) => age is >= 0 and <= 150;
}

// Name,Age
// Ann,200
// CsvFormatException: Cannot read Signup.Age from field 1 'Age' of record 2: '200' is not valid for Signup.Age.
```

- The method takes one parameter of the member's type and may have any accessibility. It sees the value as the member
  gets it: after `[CsvDefault]`, `null` for an empty field of a nullable member.
- Without `Message`, the message names the member and, unless `IncludeValuesInExceptions` is off, the value.
- It does not run when the column of an optional member is missing, nor when records are written.
- `OnRecordError` can skip the records that fail their checks, as it skips fields that do not parse.

### How the attributes combine

For each record, a member's value is decided in this order:

1. `[CsvConstant]`: the constant, without looking at the record.
2. A column that is missing from the header (an [optional](#optional-columns) column): the member keeps its default.
3. An empty field: the `[CsvDefault]` value, or `null` for a nullable type.
4. A field in `[CsvNullValues]`, or in the reader's `NullValues` for a `Nullable<T>` or `string?` member: `null`.
5. Otherwise the field is parsed: by the converter, the boolean values or the format if the member has one, else as its
   [type](#supported-types) is read, with the member's culture.
6. `[CsvValidate]` checks the value.

## Records and the reader options

Records are read with the reader's options, so everything on the [Reading](reading.html) page applies: the
[mode](reading.html#parsing-modes), the separator, trimming, the format provider, string pooling, malformed-data
handling and field count checks. Also:

- `NullValues` for nullable members ([above](#null-values-csvnullvalues)), and `TrueValues` and `FalseValues` for
  `bool` members without `[CsvBooleanValues]`.
- `NormalizeHeaderName` for the names that members look for.
- `ShouldSkipRecord`: the records it skips are never mapped.
- `IncludeValuesInExceptions = false`: the generated enum and boolean parsers leave the value out of their messages,
  as the getters do.
- `OnRecordError`: records that cannot be mapped can be skipped (see [Errors](#errors)).

When you keep many records in memory, `PoolStrings = true` makes repeated values of a column share one string, which
can save a lot of memory and garbage collection work.

### Options on the record type: [CsvOptions]

`[CsvOptions]` keeps the dialect of a type's files next to the type. `CsvReaderOptions.For<T>()` and
`CsvWriterOptions.For<T>()` return the default options with the settings the attribute gives:

```csharp
[CsvRecord]
[CsvOptions(Separator = ';', HasHeader = false, Culture = "de-DE", NullValues = new[] { "NULL" })]
public partial class EuroPrice
{
    [CsvColumn(Index = 0)]
    public string Item { get; set; } = "";
    [CsvColumn(Index = 1)]
    public decimal Price { get; set; }
    [CsvColumn(Index = 2)]
    public decimal? Discount { get; set; }
}

// Tea;1,50;NULL
using var reader = CsvReader.FromFile("prices.csv", CsvReaderOptions.For<EuroPrice>());
using var writer = CsvWriter.ToFile("copy.csv", CsvWriterOptions.For<EuroPrice>() with { Quoting = CsvQuoting.Always });
```

| Property | Reader options | Writer options |
|---|---|---|
| `Separator`, `Mode`, `Escape`, `CommentPrefix`, `HasHeader` | The same names | The same names |
| `TrimWhitespace` | `TrimWhitespace` | |
| `NewLine` | | `NewLine` |
| `Culture` | `FormatProvider` | `FormatProvider` |
| `NullValues`, `TrueValues`, `FalseValues` | The same names | `NullValue`, `TrueValue`, `FalseValue`: the first value |

- Only the properties that are set change the options. The options are created on the first call and the same
  instance is returned after that; change the rest with `with`. A type without the attribute gets the defaults.
- `Mode = CsvMode.FixedWidth` also sets `FieldWidths` to the type's [layout](#fixed-width-records).
- `HasHeader = false` makes `WriteRecords` write no header (`CsvWriterOptions.HasHeader`).
- The generator turns the attribute into code, so no reflection is involved. The options are checked when a reader or
  writer is created with them, and an unknown culture throws `CultureNotFoundException` from `For<T>()`.

## Fixed-width records

In [FixedWidth mode](reading.html#fixedwidth), a header line names the columns as in the other modes, so a record type
reads them by name, with the widths in the reader options:

```csharp
// Id    Name        Balance
// 1     Anna         1250.00
// 2     Benedikt       -3.75
[CsvRecord]
public partial class Balance
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    [CsvColumn("Balance")]
    public decimal Amount { get; set; }
}

var options = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = [6, 12, 10] };
using var reader = CsvReader.FromFile("accounts.txt", options);
List<Balance> balances = reader.GetRecords<Balance>().ToList();
```

The record type can also hold the layout. `Width` on `[CsvColumn]` gives the width of a member's column, and `Index`
its position; `CsvRecordMapper.GetFieldWidths<T>()` returns the widths in index order, for the reader options:

```csharp
// 1     Anna      20240115  1250.00
// 22    Benedikt  20231231    -3.75
[CsvRecord]
public partial class Account
{
    [CsvColumn(Index = 0, Width = 6)]
    public int Id { get; set; }
    [CsvColumn(Index = 1, Width = 10)]
    public string Name { get; set; } = "";
    [CsvColumn(Index = 2, Width = 8)]
    [CsvFormat("yyyyMMdd")]
    public DateTime Opened { get; set; }
    [CsvColumn(Index = 3, Width = 9)]
    public decimal Balance { get; set; }
}

var options = new CsvReaderOptions
{
    Mode = CsvMode.FixedWidth,
    FieldWidths = CsvRecordMapper.GetFieldWidths<Account>(),    // [6, 10, 8, 9]
    HasHeader = false,
};
using var reader = CsvReader.FromFile("accounts.txt", options);
List<Account> accounts = reader.GetRecords<Account>().ToList();
```

- Every column from 0 to the highest index needs exactly one width, so a column you don't need still needs a member,
  such as a `string` property that you never use. The build fails ([CSVH008](#compile-time-diagnostics)) when a
  column has no width or two, or a width has no index.
- Widths count characters, as `FieldWidths` does.
- The widths only make the layout that `GetFieldWidths<T>()` returns: the reader reads with the `FieldWidths` of its
  options. The same list works for [writing](writing.html#fixedwidth) the layout
  (`new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = CsvRecordMapper.GetFieldWidths<Account>() }`), and
  `WriteRecord<T>` writes the records in it (see [Writer options](#writer-options)).
- A layout cannot extend into [nested records](#nested-records) yet.

## Nested records

A member whose type is itself a record type (a `[CsvRecord]` type, or one with a
[hand-written mapper](#hand-written-mappers)) is read from the same row: its own members read their columns.
`[CsvColumn(Prefix = "...")]` puts text in front of the header names that they look up, so one type can read several
groups of columns:

```csharp
// Id,From City,From Lat,From Lon,To City,To Lat,To Lon
// S1,Oslo,59.9,10.7,Rome,41.9,12.5
[CsvRecord]
public partial record struct GeoPoint(double Lat, double Lon);

[CsvRecord]
public partial class Place
{
    public string City { get; set; } = "";
    public GeoPoint Point { get; set; }                                      // Lat and Lon, with the prefix of Place
}

[CsvRecord]
public partial class Shipment
{
    public string Id { get; set; } = "";
    [CsvColumn(Prefix = "From ")]
    public Place From { get; set; } = new();   // From City, From Lat, From Lon
    [CsvColumn(Prefix = "To ")]
    public Place To { get; set; } = new();       // To City, To Lat, To Lon
}
```

- Prefixes add up: a record nested in `From` with `Prefix = "Geo "` would read `From Geo Lat`. Indexes are not
  prefixed: `[CsvColumn(Index = n)]` in a nested type reads column `n` of the row.
- The nested mappers are created with the outer one, so the columns are still looked up once per reader, and the
  missing columns of every level are reported together:
  `The CSV header has no column for Shipment.Id ('Id'), Place.City ('To City').`
- A nested member has no column of its own, so a name, `Index`, `Optional`, `Width` and the attributes of
  [Reading values](#reading-values) don't apply to it ([CSVH010](#compile-time-diagnostics)). It is always set: a
  nested record cannot be optional yet.
- A type cannot contain itself, directly or through other nested records ([CSVH004](#compile-time-diagnostics)).
  The generator cannot see what a [hand-written mapper](#hand-written-mappers) creates, so a cycle through one throws
  `InvalidOperationException` when the mapper is created instead: records nest at most 64 deep.
- With a `[CsvConverter]`, the member reads its own column with the converter instead.

## Collections

A member that is an array, a `List<T>`, or one of the interfaces `IEnumerable<T>`, `IReadOnlyList<T>`,
`IReadOnlyCollection<T>`, `IList<T>` and `ICollection<T>`, reads several columns, each as an element. Without an
index it reads every header column with its name, for files that repeat a column; with `Index` it reads the columns
from `Index` to `EndIndex`, or to the end of each record:

```csharp
// Name,Score,Score,Score
// Ann,7,9,8
[CsvRecord]
public partial class Player
{
    public string Name { get; set; } = "";
    [CsvColumn("Score")]
    public int[] Scores { get; set; } = [];          // [7, 9, 8]
}

// Year,Q1,Q2,Q3,Q4,Notes...
// 2024,1.5,2,,4,late,audited
[CsvRecord]
public partial class Quarters
{
    [CsvColumn(Index = 0)]
    public int Year { get; set; }
    [CsvColumn(Index = 1, EndIndex = 4)]
    public decimal?[] Amounts { get; set; } = [];    // [1.5, 2, null, 4]
    [CsvColumn(Index = 5)]
    public List<string> Notes { get; set; } = [];                    // ["late", "audited"]
}
```

A dictionary with `string` keys (`Dictionary<string, T>`, `IDictionary<string, T>` or `IReadOnlyDictionary<string, T>`)
reads the columns from its `Index` on, keyed by their header names, such as the answers of a survey whose questions
change from file to file:

```csharp
// Respondent,Q1,Q2,Q3
// ann,5,,3
[CsvRecord]
public partial class Survey
{
    [CsvColumn(Index = 0)]
    public string Respondent { get; set; } = "";
    [CsvColumn(Index = 1)]
    public Dictionary<string, int?> Answers { get; set; } = [];     // Q1: 5, Q2: null, Q3: 3
}
```

- Each element is read as a member of its type would be, with the member's `[CsvFormat]`, `[CsvCulture]`,
  `[CsvNullValues]`, `[CsvDefault]` and `[CsvConverter]` (a converter of the element type; one of the collection type
  reads the whole member from one column). A field that a short record lacks reads as empty.
- A collection read by name is a required column unless it is `Optional`, which reads an empty collection when the
  header has none. Its alternate names are tried in order, and the first that the header has gives the columns.
- A dictionary needs an `Index` ([CSVH011](#compile-time-diagnostics)) and a header; a column name that comes twice is
  read once, from its first column.
- Only a collection with both `Index` and `EndIndex` can be [written](#types-that-cannot-be-written): its columns are
  written at their indexes, with empty fields where it has fewer elements.

## Types you cannot change: [CsvRecordMap]

A type of another library, or generated code, cannot get the attributes of a record type. A map class maps it
instead: `[CsvRecordMap]` on a `partial` class makes the generator write the mapper and record writer of the mapped
type, with the attributes of the map class's members.

```csharp
// A type of another library, without CsvHelper attributes
public sealed class Supplier
{
    public Supplier(string code) => Code = code;
    public string Code { get; }
    public string Name { get; set; } = "";
    public decimal Rating { get; init; }
    public DateTime Since { get; set; }
    public string? Notes { get; set; }
}

[CsvRecordMap(typeof(Supplier))]
[CsvOptions(Separator = ';')]
internal partial class SupplierMap
{
    [CsvColumn("Supplier Code")]
    public string Code { get; set; } = "";
    [CsvColumn("Full Name")]
    public string Name { get; set; } = "";
    [CsvValidate(nameof(IsRating))]
    public decimal Rating { get; set; }
    [CsvFormat("yyyyMMdd")]
    public DateTime Since { get; set; }
    [CsvIgnore]
    public string? Notes { get; set; }

    private static bool IsRating(decimal rating) => rating is >= 0 and <= 5;
}

using var reader = CsvReader.FromFile("suppliers.csv", CsvReaderOptions.For<Supplier>());
List<Supplier> suppliers = reader.GetRecords<Supplier>().ToList();
```

- Each property or field of the map class stands in for the member of the mapped type with its name, and must have its
  type ([CSVH012](#compile-time-diagnostics)). It only carries attributes: the map class is never created. Members
  without a stand-in are mapped as usual, and a constructor parameter takes the attributes of the stand-in with its name.
- Attributes of the type itself, such as `[CsvOptions]` and `[CsvIgnoreBase]`, go on the map class, and so do the
  methods of `[CsvValidate]`.
- The generated code uses what the map class can access: the public members and constructors of the mapped type (and
  internal ones in the same assembly). `[CsvConstructor]` on the mapped type is honored when it has one.
- `GetRecords<T>()`, `WriteRecords<T>()` and the other record methods find the mapper as they find those of
  `[CsvRecord]` types. A mapped type cannot be a [nested record](#nested-records) of another record type yet; a
  [hand-written mapper](#hand-written-mappers) can.

## Hand-written mappers

A `CsvRecordMapper<T>` reads the records of `T`. The generator writes one for each `[CsvRecord]` type, and you can
write one yourself: to compute members from several columns, or for a type that a [map class](#types-you-cannot-change-csvrecordmap)
cannot map.

```csharp
public sealed class Temperature
{
    public Temperature(string city, double celsius) => (City, Celsius) = (city, celsius);
    public string City { get; }
    public double Celsius { get; }
}

public sealed class TemperatureMapper : CsvRecordMapper<Temperature>
{
    private readonly int city;
    private readonly int fahrenheit;

    public TemperatureMapper(CsvReader reader, string columnPrefix)
        : base(columnPrefix)
    {
        city = Column(reader, "City", -1, false, "Temperature.City");
        fahrenheit = Column(reader, ["F", "Fahrenheit"], -1, false, "Temperature.Celsius");
        ThrowIfColumnsMissing(reader);
    }

    public override Temperature Read(CsvReader reader) =>
        new(reader.GetString(city), (reader.Parse<double>(fahrenheit) - 32) * 5 / 9);
}
```

- The constructor runs once per reader and finds the columns. `Column` takes an index (used when it is not negative)
  and one or more names, which get the column prefix in front. It returns -1 for a missing column, and
  `ThrowIfColumnsMissing` then throws one `KeyNotFoundException` that names every missing required column.
  `NestedMapper<TNested>(reader, prefix)` creates the mapper of a nested record. It can be the mapper's own type, as
  long as something ends the nesting (such as creating it only when `ColumnPrefix` is empty); mappers that lead back
  to their own type without end throw `InvalidOperationException` once records nest 64 deep.
- `Read` runs for every record. Read the fields with the reader's getters and the indexes the constructor found.
- A mapper must not change after its constructor has run: the threads of `ParallelEnumerate<T>` share one.
- When `Read` throws, the reader asks `FindFailingMember(reader, out fieldIndex)` which member failed, for the
  [error](#errors). Generated mappers read their members again one at a time; a hand-written one can override it, or
  leave it, and the error names the record only.

To use the mapper, put `[CsvConverter]` on the type. The generator registers the mapper as it registers its own, so
`GetRecords<T>()` and the other methods find it; the type does not need to be `partial`, and must not also have
`[CsvRecord]`. For a type that you cannot change, register the mapper once at startup instead:

```csharp
[CsvConverter(typeof(TemperatureMapper))]
public sealed class Temperature { /* ... */ }

// Or, for a type that you cannot change:
CsvRecordMapper.Register<Temperature>((reader, columnPrefix) => new TemperatureMapper(reader, columnPrefix));
```

A mapper with a `(CsvReader, string)` constructor that passes the column prefix on to the base constructor can be a
nested record with a `Prefix`. One with only a `(CsvReader)` constructor (registered with
`Register<T>(reader => new Mapper(reader))`) works everywhere else; asked for a prefix, it makes `GetRecords<T>` throw
`InvalidOperationException`.

A type with a hand-written mapper is written by a hand-written `CsvRecordWriter<T>`, which you register at startup:

```csharp
public sealed class TemperatureWriter(string columnPrefix) : CsvRecordWriter<Temperature>(columnPrefix)
{
    public override int FieldCount => 2;

    public override void WriteHeader(CsvWriter writer)
    {
        WriteColumnName(writer, "City");                 // with the column prefix in front
        WriteColumnName(writer, "F");
    }

    public override void Write(CsvWriter writer, Temperature record)
    {
        writer.WriteField(record.City);
        writer.WriteField(record.Celsius * 9 / 5 + 32);
    }
}

CsvRecordWriter.Register<Temperature>(columnPrefix => new TemperatureWriter(columnPrefix));
```

`WriteHeader` and `Write` write `FieldCount` fields and do not end the record. `WriteEmpty`, which writes
`FieldCount` empty fields, is what a nested record that is `null` writes, and `NestedWriter<TNested>(prefix)` creates
the writer of a nested record. Like a mapper, a record writer must not change after its constructor has run: the
threads of `ParallelWriteRecords` share one. `writer.WriteRecords(records, recordWriter)` writes records with a record
writer that is not registered for the type, and so does `writer.ParallelWriteRecords(records, recordWriter)`.

## Reading some members: projections

A projection is a mapper that reads only some members of a record type, so the columns of the others are neither
looked up nor parsed: the others keep their defaults (constructor parameters their default values), their columns may
be missing from the header, and their `[CsvValidate]` checks do not run. [CsvHelper.Linq](linq.html) uses them to read
only the members that a query uses. The source generator writes them for the record types of an assembly that
references CsvHelper.Linq, or that asks for them:

```csharp
[assembly: CsvRecordProjections]

using var reader = CsvReader.FromFile("orders.csv");
CsvRecordMapper.TryGetMembers<Order>(out var members);                // Id (Column 'Id'), Price (Column 'Unit Price'), ...
var mapper = CsvRecordMapper.CreateProjection<Order>(reader, ["Id", "Price"]);
while (reader.Read())
{
    var order = reader.GetRecord(mapper);                             // Quantity is not read
}
```

`TryGetMembers` describes the members that the generated mapper reads (`CsvRecordMember`: the name, type and kind of
each, and the columns it reads), and returns `false` for hand-written mappers and types without projections.
`CreateProjection` selects members by name. `reader.GetRecord(mapper)` and `reader.TryGetRecord(mapper, out record)`
read a record with any mapper, with the error handling of `GetRecord<T>()` (and of `OnRecordError`, for
`TryGetRecord`). Without the attribute, an assembly's generated code has no projections, which keeps it smaller for
native AOT.

## Dynamic records

Without a record type, `GetRecords<dynamic>()` reads each record as a `CsvDynamicRecord`, whose members are its
columns:

```csharp
// Name,Age,Unit Price
// Ann,31,9.50
using var reader = CsvReader.FromFile("people.csv");
foreach (var person in reader.GetRecords<dynamic>())
{
    string name = person.Name;                    // "Ann"
    int age = person.Parse<int>("Age");           // 31: values are strings, and Parse<T> parses them
    string price = person["Unit Price"];          // "9.50": an indexer for names that are not identifiers
}
```

- Members are matched as the reader matches header names: exactly, then ignoring case and surrounding whitespace,
  after `NormalizeHeaderName`. Without a header they are `Field1`, `Field2` and so on. A name that is not a column
  throws `RuntimeBinderException`, as with `ExpandoObject`. A column wins over a member of `CsvDynamicRecord` with the
  same name, such as `FieldCount`.
- Values are strings, as in CsvHelper 33 and earlier: `""` for an empty field and for a field the record lacks (unless `MissingField`
  is `Throw`), `null` for one of the reader's `NullValues`. `int age = person.Age;` fails at run time;
  `Parse<T>("Age")` parses a column by name, as the reader's `Parse<T>` does, with the reader's `FormatProvider`,
  `TrueValues` and `FalseValues`.
- Without `dynamic`, `GetRecords<CsvDynamicRecord>()` returns the same records. A record is an
  `IReadOnlyDictionary<string, string?>` (each name once, with its first column), with `record[index]`,
  `GetSpan(index)` for the UTF-8 bytes, and `ToDictionary()` for a copy you can change. `reader.Current.ToDictionary()`
  copies the current row without reading it as a record.
- The other record methods work too: `GetRecord<dynamic>()`, `GetRecordsAsync<dynamic>()` and
  `ParallelEnumerate<dynamic>()`.

They are fast because a record is not a dictionary of strings:

- A record holds the bytes of its fields as they are in the input, in a block that it shares with the records read
  after it: one copy of the record, or none for input in a byte array, which the records reference in place. A quoted
  field loses its quotes, and a field becomes a string, only when you read it, so the fields you never read cost
  nothing. (With trimming, and in the Escape and FixedWidth modes, the fields are decoded when the record is read.)
- The records of a reader share a schema, and `person.Name` is bound once to its column: the rule that the runtime
  binder caches reads the field at that index for every record with that header, without looking the name up again.
  Readers with the same header and options share the schema, so the next file reuses the bindings.

Reading the PackageAssets [benchmark](benchmarks.html#dynamic-records) data (50,000 records of 25 columns, 16 MB) and
three of the fields of each record by name:

| Dynamic records | Read 3 fields of each | Keep every record (`ToList`) |
|---|---:|---:|
| **CsvHelper** | **5.05 ms**, 29.7 MB | **5.41 ms**, 23.6 MB |
| CsvHelper, from a byte array | **3.87 ms**, 14.3 MB | |
| An `ExpandoObject` per record | 82.1 ms, 136 MB (16x slower) | 119 ms, 137 MB (22x slower) |

Times are BenchmarkDotNet means, with the memory allocated per run.

A record keeps its block in memory (64 KB, about 200 records of this data), and one read from a byte array keeps the
array; `record.Detach()` copies a record you keep while dropping the rest. C#'s `dynamic` needs the runtime binder
(`Microsoft.CSharp`), which native AOT does not support; `GetRecords<CsvDynamicRecord>()` works there.

### Writing dynamic records

`WriteRecords` writes dynamic records, `ExpandoObject`s and dictionaries keyed by column name
(`IDictionary<string, object?>`, `IReadOnlyDictionary<string, string?>`): the names of the first record are the
header and the columns, and every record writes its values in them, with an empty field where it has none. A
`CsvDynamicRecord` of the same header as the first writes the bytes it read, so a file read as dynamic is written back
as it was:

```csharp
using (var reader = CsvReader.FromFile("people.csv"))
using (var writer = CsvWriter.ToFile("filtered.csv"))
{
    writer.WriteRecords(reader.GetRecords<dynamic>().Where(p => p.Name != "Bob"));
}

dynamic row = new ExpandoObject();
row.Name = "Ann";
row.Total = 9.5m;
using (var writer = CsvWriter.ToFile("totals.csv"))
{
    writer.WriteRecords(new List<ExpandoObject> { row });       // Name,Total / Ann,9.5: values as WriteField(object) writes them
}
```

`WriteRecord(record)` writes one record in the order of its own names, without a header; `WriteHeader<dynamic>()`
throws, because the columns are not known before the first record. `ParallelWriteRecords` writes dynamic records on
several threads, in the columns of the first record too.

## Errors

| Exception | When |
|---|---|
| `KeyNotFoundException` | The header lacks a required column (all of them are named), or the input has no header and a member has no `Index`. Thrown by `GetRecords<T>`, `ParallelEnumerate<T>`, the first `GetRecord<T>`, or the start of a `GetRecordsAsync<T>` enumeration. |
| `CsvFormatException` | A record cannot be read: a field does not parse as the type of its member, a value fails its [check](#checking-values-csvvalidate), or a converter, constructor or setter throws. Thrown while enumerating, for that record; the cause is its `InnerException`. Also a malformed record, as for any [read](reading.html#errors). |
| `InvalidOperationException` | The type has no mapper: it is not `[CsvRecord]`, or the project does not run the source generator. Or a nested record with a `Prefix` has a hand-written mapper that does not take one, or a dictionary member has no header for its keys, or hand-written mappers nest records that lead back to their own type without end. When writing: the type has no record writer, or its records [cannot be written](#types-that-cannot-be-written), or its nested record writers lead back to their own type without end. |
| `CultureNotFoundException` | A `[CsvCulture]` names a culture that does not exist, or the app runs in globalization invariant mode. Thrown where `KeyNotFoundException` is. |

```csharp
// No header and a member without an index:
// KeyNotFoundException: The CSV has no header, so these members need a column index ([CsvColumn(Index = n)]): Order.Id, Order.Price.
```

A record that cannot be read throws a `CsvFormatException` that says where: the record and its line (with
`TrackLineNumbers`), and the member and the field that failed. Its `InnerException` is what was thrown, such as a
`FormatException` or `OverflowException` from a getter, a `CsvValidationException`, or the exception of your own
`Parse` method or converter:

```csharp
// Id,Unit Price,Quantity
// A-1,9.99,3
// A-2,abc,3
try
{
    List<Order> orders = reader.GetRecords<Order>().ToList();
}
catch (CsvFormatException e)
{
    // Cannot read Order.Price from field 1 'Unit Price' of record 3: The input string 'abc' was not in a correct format.
    Console.WriteLine($"{e.Message} ({e.Member}, field {e.FieldIndex}, record {e.RecordNumber}, byte {e.ByteOffset})");
}
```

| `CsvFormatException` | For a record that cannot be read |
|---|---|
| `Member` | The member that failed, as `Order.Price`; for a [nested record](#nested-records), the member of the nested type. `null` when no member failed on its own, such as when the constructor or a setter threw. |
| `FieldIndex` | The 0-based index of its field, or -1 when it is not known. |
| `RecordNumber`, `LineNumber`, `ByteOffset` | Where the record is, as for malformed records. |
| `InnerException` | The exception that the field, check, converter or member threw. |

Finding the member costs nothing until a record fails: then the mapper reads its members again, one at a time, and the
first that throws is the one named. The message includes the inner exception's message, which leaves values out when
`IncludeValuesInExceptions` is off, except in the messages of your own parsers and converters.

A record that cannot be read ends the enumeration. To skip such records and go on, set `OnRecordError`: it gets the
row number, the byte offset, the exception (the `CsvFormatException` above) and the raw record, and returns `true` to
skip the record or `false` to throw. It applies to `GetRecords<T>`, `GetRecordsAsync<T>`, `EnumerateRecords`,
`EnumerateRecordsAsync` and `ParallelEnumerate<T>`, where it can be called on several threads at once:

```csharp
var rejected = new List<CsvRecordError>();
var options = new CsvReaderOptions { OnRecordError = error => { rejected.Add(error); return true; } };
using var reader = CsvReader.FromFile("orders.csv", options);
List<Order> orders = reader.GetRecords<Order>().ToList();

// rejected[0]: RowNumber 2, RawRecord "A-2,abc,3", Exception: CsvFormatException for Order.Price
```

With `ParallelEnumerate<T>`, the callback can run on several threads at once: collect into a
`ConcurrentQueue<CsvRecordError>`, or lock around the list.

Errors of the reader itself (malformed records that `MalformedData` says to throw for, missing columns) are not
passed to it. A mapper called directly (`mapper.Read(reader)`) throws the exception of the field as it is.

## Writing records

`WriteRecords<T>` writes records of a `[CsvRecord]` type: its header if nothing has been written yet, then a record per
item. The generated record writer writes the columns that the mapper reads, with the same attributes, so a reader reads
the file back into the same values:

```csharp
var orders = new List<Order>
{
    new() { Id = "A-1", Price = 9.99m, Quantity = 3 },
    new() { Id = "A-2", Price = 24.50m },
};
using var writer = CsvWriter.ToFile("orders.csv");
writer.WriteRecords(orders);

// Id,Unit Price,Quantity
// A-1,9.99,3
// A-2,24.50,
```

| Method | Writes |
|---|---|
| `writer.WriteRecords<T>(records)` | The header of `T` if nothing has been written yet (comments aside), then a record per item. |
| `writer.WriteRecordsAsync<T>(records, cancellationToken)` | The same for an `IEnumerable<T>` or an `IAsyncEnumerable<T>`, flushing asynchronously. |
| `writer.ParallelWriteRecords<T>(records, maxDegreeOfParallelism)`, `writer.ParallelWriteRecordsAsync<T>(...)` | The same output, formatted on several threads (see [Writing from several threads](writing.html#writing-from-several-threads)). |
| `writer.WriteHeader<T>()` | The header of `T`. |
| `writer.WriteRecord<T>(record)`, `writer.WriteRecordAsync<T>(record)` | One record, without a header. |

The writer creates the record writer of `T` on first use, and `CsvRecordWriter.Create<T>()` creates one yourself. A
record that fails halfway, for example in a converter, is discarded, and the records before it stay written.

### Columns

The columns are those the mapper reads, in this order: the constructor parameters, then the other members in
declaration order, a base type's first.

- The header has the name the mapper looks for first: the `[CsvColumn]` name, else the member's or constructor
  parameter's name.
- A column with `[CsvColumn(Index = n)]` is written at position `n`. Where no member has a column, the field is empty:
  `Index = 0` and `Index = 3` write `A,,,B`.
- Columns with the same name are written in `NameIndex` order. A member that reads the third `Amount` column with no
  member for the first two gets two empty `Amount` columns before its own.
- Members with `[CsvIgnore]` and members that the mapper cannot set are not written. Optional columns are written like
  the others.
- A nested record writes its own columns at its place, with its `Prefix` in front of their names. A nested record that
  is `null` writes empty fields.

### Values

| Member | Written as |
|---|---|
| `string` | As it is; `null` as the writer's `NullValue` (an empty field by default). |
| Numbers, `bool`, `Guid`, `TimeSpan` | As `WriteField` writes them, with the writer's `FormatProvider`: shortest round-trip floats, `TrueValue` and `FalseValue` (`True` and `False`). |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` | In ISO 8601 round-trip format (`"O"`), which reads back in every culture. |
| Enums | The member's first `[CsvEnumName]`, else its name. Combinations of flags and other values as `ToString()` writes them (`"Read, Write"`, `7`). |
| `Uri` | Its `OriginalString`, as it was read. |
| `Nullable<T>` | The value, or the writer's `NullValue` for `null`. |
| [Collections](#collections) with `Index` and `EndIndex` | An element per column, as a member of the element's type; an empty field where there is no element. |
| `[CsvConstant]` members | The constant, whatever the member holds. |
| Other types | With `IUtf8SpanFormattable` or `IFormattable` and the format provider, else `ToString()`. A type that `ToString()` does not write in the form its `Parse` reads needs a [converter](#converters-csvconverter). |

The attributes of [Reading values](#reading-values) apply as far as writing has a counterpart:

- `[CsvFormat]` and `[CsvCulture]` format the value as they parse it.
- `[CsvBooleanValues("Ja", "Nein")]` writes the first true or false value, and `[CsvEnumName]` the first name.
- `[CsvNullValues("NULL")]` writes `null` as its first value, which reads back as `null`.
- `[CsvConverter]` writes with the converter's `Write`.
- `[CsvDefault]` does not change what is written: a `null` member with `[CsvDefault(5)]` and without `[CsvNullValues]`
  is written as an empty field, which reads back as 5.

### Types that cannot be written

Some types can be read but not written. Many types are only read, so the generator does not report them; the first
write throws `InvalidOperationException` with the reason (`Records of Order cannot be written: ...`):

- a constructor parameter that sets no property or field, so the writer has nothing to read its value from;
- a property without a getter;
- two members with the same column index;
- column indexes together with nested records, or a nested record whose own members have indexes (an index is a
  column of the whole row);
- a collection without a fixed number of columns (read by name, or to the end of the record), or a dictionary, whose
  keys are header names that the writer does not know before the first record.

A type with a [hand-written mapper](#hand-written-mappers) needs a hand-written record writer.

### Writer options

The writer's options apply to records too: the separator, quoting and `ShouldQuote` (which gets the type of each
member's value), `NullValue`, `TrueValue` and `FalseValue`, line endings, comments and protection against spreadsheet
formulas (see [Writing](writing.html)). With `NullValue = "NULL"`, a reader with `NullValues = ["NULL"]` reads null and
empty strings back apart. `HasHeader = false` makes `WriteRecords` leave out the header, and
`CsvWriterOptions.For<T>()` gives the options of the type's [`[CsvOptions]`](#options-on-the-record-type-csvoptions). To
write the [fixed-width layout](#fixed-width-records) of a type, set the writer's
`FieldWidths = CsvRecordMapper.GetFieldWidths<T>()`, and write records without a header with `WriteRecord<T>`, as such
files usually have none:

```csharp
var options = new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = CsvRecordMapper.GetFieldWidths<Account>() };
using var writer = CsvWriter.ToFile("accounts.txt", options);
foreach (Account account in accounts) writer.WriteRecord(account);

// 1     Anna      202401151250.00
// 22    Benedikt  20231231-3.75
```

## Compile-time diagnostics

The generator reports problems when you compile:

| Id | Problem |
|---|---|
| CSVH001 | The type, or a type that contains it, is not `partial`. |
| CSVH002 | A member's type cannot be read from a field. |
| CSVH003 | No constructor to use: the type is abstract or static, has no public or internal constructor, or several constructors have the most parameters. |
| CSVH004 | The type is generic, a `ref struct`, file-local or not accessible from its whole assembly, or it contains itself through [nested records](#nested-records). |
| CSVH005 | Warning: `[CsvColumn]` is on a member that cannot be set. |
| CSVH006 | A `required` member has `[CsvIgnore]`. |
| CSVH007 | The project uses C# 8 or earlier. |
| CSVH008 | The [fixed-width layout](#fixed-width-records) is not valid: a column has no width or two, a width has no index, or the type has nested records. |
| CSVH009 | A [converter](#converters-csvconverter) cannot be used. On a member: it does not implement `ICsvConverter<T>` for the member's type, or has no parameterless constructor. On a type: it does not derive from `CsvRecordMapper<T>`, has no `(CsvReader)` constructor, or the type also has `[CsvRecord]`. On the assembly: it implements no `ICsvConverter<T>`, has no parameterless constructor, or converts a type that another converter of the assembly converts. A member or type with two `[CsvConverter]`s. |
| CSVH010 | Warning: an attribute does not apply to its member, such as `[CsvFormat]` on an `int`, several formats for a type without `ParseExact(string, string[], IFormatProvider)`, `Prefix` on a member that is not a nested record, or `Width` on a collection, and is ignored. |
| CSVH011 | An attribute argument is not valid: a `[CsvDefault]` or `[CsvConstant]` that does not convert to the member's type, an empty list of values, a value that is both true and false, an enum name that names two members, a `[CsvValidate]` without a suitable method, a dictionary without `Index`, or an `EndIndex` before `Index`. |
| CSVH012 | A [`[CsvRecordMap]`](#types-you-cannot-change-csvrecordmap) cannot map its type: the type is abstract, open generic, inaccessible or has a mapper already, or a member of the map class stands in for no member of the type, or for one of another type. |

For example, a member of a type without a static `Parse` fails the build with
`error CSVH002: 'Invoice.Color' has type 'Color', which cannot be read from a CSV field; use a type with a static Parse method or add [CsvIgnore]`.

## Setup and requirements

The generator needs C# 9 and a Roslyn 4.3 compiler or later (the .NET 6.0.400 SDK, Visual Studio 2022 17.3). It runs as part of the build: there is nothing to call at
startup. A generated module initializer registers each mapper when its assembly is loaded, so `GetRecords<T>()`
finds it with no reflection.

The package includes the generator. A project that references CsvHelper as a project instead of a package adds the
generator itself:

```xml
<ProjectReference Include="..\CsvHelper\src\CsvHelper.Generators\CsvHelper.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

Members of `ISpanParsable<T>` types other than those in the table above are parsed through `Parse<T>` on .NET 8 and
later. On .NET Framework and .NET Standard such members need a static `Parse(string, IFormatProvider)` or
`Parse(string)` (see [Target frameworks](target-frameworks.html)).
