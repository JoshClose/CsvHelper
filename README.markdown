CsvHelper is a library for reading and writing CSV files.

**Reading:**

using( var csvReader = new CsvReader( stream ) )
{
    var myCustomTypeList = csvReader.GetRecords<MyCustomType>();
}

**Writing:**

using( var csvWriter = new CsvWriter( stream ) )
{
    csvWriter.WriteRecords( myCustomTypeList );
}
