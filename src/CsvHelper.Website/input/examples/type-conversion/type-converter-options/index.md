# Type converter options

Data formats can be specified on the properties using the attributes or `ClassMap` can be used to do the same.  
The below method performs the same on the csv reader/writer level using `TypeConverterOptions` from `CsvHelper.TypeConversion` namespace.  

###### Example

```cs
// sample data to be written.
var reportingRows = new List<PositionRow>
{
    new PositionRow
    {
        Date = new DateTime(2022, 10, 22),
        Position = 10.0
    },

    new PositionRow
    {
        Date = new DateTime(2022, 10, 23),
        Position = 20.0,
    }
};

var configuration = new CsvConfiguration(CultureInfo.InvariantCulture);
var dateTimeConverterOptions = new TypeConversion.TypeConverterOptions()
{
    Formats = new[]
    {
        "yyyy-MM-dd",
    }
};

// Declare a writer and configure the type converter options before writing.
using var writer = new StreamWriter(@"C:\Temp\csv-helper\position-report-using-type-converter-options.csv");
using var csvWriter = new CsvWriter(writer, configuration);
csvWriter.Context.TypeConverterOptionsCache.AddOptions<DateTime>(dateTimeConverterOptions);
csvWriter.WriteRecords(reportingRows);


// Csv row definition
public class PositionRow
{
    public DateTime Date { get; set; }

    public double Position { get; set; }
}

```

###### Output

```
Date,Position
2022-10-22T00:00,10
2022-10-23T00:00,20
```

