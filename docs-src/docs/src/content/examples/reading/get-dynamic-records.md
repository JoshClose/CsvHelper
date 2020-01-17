# Get Dynamic Records

Convert CSV rows into `dynamic` objects. Since there is no way to tell what type the properties should be, all the properties on the dynamic object are strings.

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
        var records = csv.GetRecords<dynamic>();
    }
}
```
