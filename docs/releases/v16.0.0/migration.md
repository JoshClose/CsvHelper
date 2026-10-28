# Migrating from CsvHelper 15 to 16

CsvHelper 16 adds `GetDynamicPropertyName`, a setting that names the properties of the records `GetRecords<dynamic>()`
returns. With it you can read a file whose header has the same name twice, which failed before, since both columns
got the same property name. The names stay the same unless you set it. Code that implements `IReaderConfiguration`
needs the new property.

## Dynamic record property names

In 15, a dynamic record's property names were the header names passed through `PrepareHeaderForMatch`, so making
them unique meant changing how every header is matched, for class records too. In 16, `GetDynamicPropertyName` names
them, and changes only dynamic records:

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = (header, index) => header + index,
};
using var csv = new CsvReader(reader, config);
var records = csv.GetRecords<dynamic>().ToList();
// Name,Name,Age gives Name0, Name1 and Age2
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    GetDynamicPropertyName = (context, index) =>
    {
        var header = context.HeaderRecord[index];
        var count = context.HeaderRecord.Take(index).Count(h => h == header);

        return count == 0 ? header : header + (count + 1);
    },
};
using var csv = new CsvReader(reader, config);
var records = csv.GetRecords<dynamic>().ToList();
// Name,Name,Age gives Name, Name2 and Age
```

- **The default** is `ConfigurationFunctions.GetDynamicPropertyName`, which names them as 15 did: the header name
  passed through `PrepareHeaderForMatch`, or `Field1`, `Field2` and so on when there is no header.
- **Without a header,** `context.HeaderRecord` is `null`: the function above is for files with one.
