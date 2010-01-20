CsvHelper is a library for reading and writing CSV files. CsvHelper follows the CSV format specified here: http://www.creativyst.com/Doc/Articles/CSV/CSV01.htm

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
