# Reading

This library was created to work as easy as possible without any configuration by default. If your class property names match your CSV file header names, it's as simple as this.

```cs
var csv = new CsvReader( textReader );
var records = csv.GetRecords<MyClass>();
```

## Getting All Records

<hr/>

The most common scenario is using one of the `GetRecords` methods.  You can specify the type of object you want returned, and it will return an `IEnumerable` that will `yield` results. This means when iterating the results, only a single record will be in memory at a time, instead of the entire file. Records aren't pulled until you actually start iterating the results.

If you want to use an anonymous type as your record, you can get the records by supplying a type definition. You can use `default( type )` for value types, and `new` for reference types.

### GetRecords

Returns an `IEnumerable<T>` of records.

```cs
// By type
var records = csv.GetRecords<MyClass>();
var records = csv.GetRecords( typeof( MyClass ) );

// Dynamic
var records = csv.GetRecords<dynamic>();

// Using anonymous type for the class definition
var anonymousTypeDefinition =
{
	Id = default( int ),
	Name = string.Empty,
	MyClass = new MyClass()
};
var records = csv.GetRecords( anonymousTypeDefinition );
```

### EnumerateRecords

You can also enumerate the records while re-using a class instance that you give.
Each enumeration will hydrate the given record, but only the mapped members. If you
supplied a map and didn't map one of the members, that member will not get hydrated
with the current row's data. Be careful. Any methods that you call on the projection
that force the evaluation of the `IEnumerable`, such as `ToList()`, you will get a
list where all the records are the same instance you provided that is hydrated with
the last record in the CSV file.

```cs
var record = new MyClass();
var records = csv.EnumerateRecords( record );
foreach( var r in records )
{
	// r is the same instance as record.
}
```

## Reading Records

<hr/>

To be able to get individual records or even fields, you need to iterate through the records. This is done using the `Read` methods. `Read` will advance the reader to the next record. You must call `Read` before you can get any records or fields. When `GetRecords`, `Read` is automatically called for you.

### Read

This will advance the reader to the next record.

```cs
csv.Read();
```

### ReadAsync

This will advance the reader to the next record asynchronously. If the `TextReader` that was supplied is tied to a network or some other slow to read functionality, reading asynchronously is probably a good idea.

```cs
await csv.ReadAsync();
```

### ReadHeader

The header in a CSV file is just another record, but it has special meaning. If your file has a header record, you'll need to read the header after the first read. After that you can loop the records and read them. This will allow you to be able to read headers on different rows, or even multiple headers.

```cs
csv.Read();
csv.ReadHeader();
while( csv.Read() )
{
	var record = csv.GetRecord<MyClass>();
}
```

## Getting a Single Record

<hr/>

Sometimes there is a reason that you may need to loop the records yourself. You can still easily get a single record, just like with multiple records.

### GetRecord

```cs
// By type
var record = csv.GetRecord<MyClass>();
var record = csv.GetRecord( typeof( MyClass ) );

// Dynamic
var record = csv.GetRecord<dynamic>();

// Using anonymous type for the class definition
var anonymousTypeDefinition =
{
	Id = default( int ),
	Name = string.Empty,
	MyClass = new MyClass()
};
var record = csv.GetRecord( anonymousTypeDefinition );
```

## Getting Fields

<hr/>

If you need a more granular way of getting records, you are able to get individual fields.

### Indexer

You can use an indexer to get by position or name. This will return the field as a `string`.

```cs
// By position
var field = csv[0];

// By header name
var field = csv["HeaderName"];
```

### GetField

You can get fields converted to a specific type.

```cs
// Gets field by position returning string
var field = csv.GetField( 0 );

// Gets field by position returning int
var field = csv.GetField<int>( 0 );

// Gets field by header name returning bool
var field = csv.GetField<bool>( "IsTrue" );

// Gets field by header name returning object
var field = csv.GetField( typeof( bool ), "IsTrue" );
```

### TryGetField

If there data isn't consistent and sometimes may not be able to be converted to the correct type, you can use the `TryGetField` methods.

```cs
var success = csv.TryGetField<int>( 0, out string field );
```

## Reading Context

<hr/>

When reading, all the information in the system is held in a context object. If you need to get raw system information for some reason, it's available here. When an exception is thrown, the context is included so you can inspect the current state of the reader.

## Configuration

<hr/>

See <a href="/CsvHelper/configuration">configuration</a>

<br/>
