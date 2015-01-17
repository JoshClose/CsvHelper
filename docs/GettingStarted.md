
## Reading

### Reading records

You can loop the rows and read them manually.

    var csv = new CsvReader( textReader );
	while( csv.Read() )
	{
        var record = csv.GetRecord<MyClass>();
	}
	
### Reading individual fields

You can also read each field manually if you like.

    var csv = new CsvReader( textReader );
    while( csv.Read() )
    {
        var intField = csv.GetField<int>( 0 );
        var stringField = csv.GetField<string>( 1 );
        var boolField = csv.GetField<bool>( "HeaderName" );
    }

### TryGetField

If you might have inconsistencies with getting fields, you can use `TryGetField`.

    var csv = new CsvReader( textReader );
    while( csv.Read() )
    {
        int intField;
        if( !csv.TryGetField( 0, out intField ) )
        {
            // Do something when it can't convert.
        }
    }

### Reading all records

**This is not available for iOS currently due to platform restrictions**

Reading is setup to be as simple as possible. If you have a class structure setup that mirrors the CSV file, you can read the whole file into an enumerable.

    var csv = new CsvReader( textReader );
    var records = csv.GetRecords<MyClass>();
	
If you want to customize how the CSV file maps to your custom class objects, you can use mapping.

The `IEnumerable<T>` that is returned will yield results. This means that a result isn't returned until you actually access it. This is handy because the whole file won't be loaded into memory, and the file will be read as you access each row. If you do something like `Count()` on the `IEnumerable<T>`, the whole file needs to be read and you won't be able to iterate over it again without starting over. If you need to iterate the records more than once (like using Count), you can load everything into a list and the work on the data.

    var csv = new CsvReader( textReader );
    var records = csv.GetRecords<MyClass>().ToList();
	
## Parsing

You can also use the parser directly without using the reader. The parser will give back an array of strings for each row that is read, and null when it is finished.

    var parser = new CsvParser( textReader );
    while( true )
    {
        var row = parser.Read();
        if( row == null )
        {
            break;
        }
    }

## Writing

### Writing records

You can loop the objects and write them manually.

    var csv = new CsvWriter( textWriter );
    foreach( var item in list )
    {
        csv.WriteRecord( item );
    }

### Writing individual fields

You can also write each field manually if you like.

    var csv = new CsvWriter( textWriter );
    foreach( var item in list )
    {
        csv.WriteField( "a" );
        csv.WriteField( 2 );
        csv.WriteField( true );
        csv.NextRecord();
    }

### Writing all records

**This is not available for iOS currently due to platform restrictions**

Writing is setup to be as simple as possible. If you have a class structure setup that mirrors the CSV file, you can write the whole file from an enumerable.

    var csv = new CsvWriter( textWriter );
    csv.WriteRecords( records );

If you want to customize how the CSV file maps to your custom class objects, you can use mapping.
