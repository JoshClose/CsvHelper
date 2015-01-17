## CsvHelper

A library for reading and writing CSV files. Extremely fast, flexible, and easy to use. Supports reading and writing of custom class objects.

## Documentation

You can read detailed documentation on 
[http://joshclose.github.io/CsvHelper/](http://joshclose.github.io/CsvHelper/).

### Reading records

You can loop the rows and read them manually.

    var csv = new CsvReader( textReader );
	while( csv.Read() )
	{
        var record = csv.GetRecord<MyClass>();
	}

### Writing records

You can loop the objects and write them manually.

    var list = List<MyClass>();
    var csv = new CsvWriter( textWriter );
    foreach( var item in list )
    {
        csv.WriteRecord( item );
    }

## License

Dual licensed

Microsoft Public License (MS-PL)

http://www.opensource.org/licenses/MS-PL

Apache License, Version 2.0

http://opensource.org/licenses/Apache-2.0