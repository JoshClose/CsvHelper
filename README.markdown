CsvHelper
=========

A library for reading and writing CSV files. Extremely fast, flexible, and easy to use. Supports reading and writing of custom class objects.

Install
=======

To install CsvHelper, run the following command in the Package Manager Console

    PM> Install-Package CsvHelper

Example
=======

This is one way of reading a CSV file:

    using( var reader = new CsvReader( new StreamReader( "file.csv" ) ) )
    {
        while( reader.Read() )
        {
            Console.Write( reader.GetField( 0 ));            
        }
    }
    
More examples can be found in the src/CsvHelper.Example project or in the Wiki.

License
=======

Microsoft Public License (Ms-PL)