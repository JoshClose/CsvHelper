# Writing

This library was created to work as easy as possible without any configuration by default. If your class property names match your CSV file header names, it's as simple as this.

```cs
var records = new List<MyClass> { ... };
var csv = new CsvWriter( textWriter );
csv.WriteRecords( records );
```

<h2 id="injection-warning" class="title is-2 has-text-danger">
	<span>Injection Warning</span>
</h2>

<hr/>

When opening a CSV in an external program, a formula in a field could be ran that contains a vulnerability. Read more here: [Comma Separated Vulnerabilities](https://www.contextis.com/blog/comma-separated-vulnerabilities). Due to this issue, if a field starts with characters `=`, `@`, `+`, or `-`, that field will be prepended with a `\t`. If the field is quoted, the `\t` will come after the `"`.

```
=one   -> \t=one
"=one" -> "\t=one"
```

You are able to turn off this functionality in configuration.

```cs
csv.Configuration.SanitizeForInjection = false;
```

## Writing All Records

<hr/>

The most common scenario is using the `WriteRecords` method. You can pass it an `IEnumerable` of records and it will write those objects.

### WriteRecords

Writes all records.

```cs
var records = new List<MyClass>
{
	new MyClass { Id = 1, Name = "one" },
	new MyClass { Id = 2, Name = "two" },
};
csv.WriteRecords( records );

// Dynamic
var records = new List<dynamic>();
dynamic record = new ExpandoObject();
record.Id = 1;
record.Name = "one";
records.Add( record );
csv.WriteRecords( records );

// Anonymous
var records = new List<object>
{
	new { Id = 1, Name = "one" },
	new { Id = 2, Name = "two" },
};
csv.WriteRecords( records );
```

## Writing a Single Record

<hr/>

Sometimes you want to write individual records by themselves.

### WriteHeader

Writes the header record. You can call this method on any row if you want to write multiple headers.

```cs
csv.WriteHeader<MyClass>();
csv.WriteHeader( Type type );
```

### WriteRecord

Writes a record.

```cs
var record = new MyClass { Id = 1, Name = "one" };
csv.WriteRecord( record );

// Dynamic
dynamic record = new ExpandoObject();
record.Id = 1;
record.Name = "one";
csv.WriteRecord( record );

// Anonymous
var record = new { Id = 1, Name = "one" };
csv.WriteRecord( record );
```

## Writing Fields

<hr/>

You can even write a single field.

### WriteField

Write any type of object to a field. You can specify your own `ITypeConverter` to handle converting the type to a string if none of the built in converters work for you.

```cs
// Write a string
csv.WriteField( "field" );

// Write a string passing in a value indicating
// if the field should be quoted. This will ignore
// any configuration and only quote based on the
// shouldQuote parameter passed in.
csv.WriteField( "field", true );

// Write any type
csv.WriteField( 1 );

// Write any type and use the given type converter
// to convert the type to a string.
csv.WriteField( value, myTypeConverter );

// Write any type and use the given type converter
// to convert the type to a string.
csv.WriteField<MyTypeConverter>( value );

// Write a field that has already been converted
// by a type converter. If the field is null, it
// won't get written.
csv.WriteConvertedField( "field" );
```

### WriteComment

This will write text to the field using the comment character supplied in `Configuration.Comment`.

```cs
csv.WriteComment( "This is a comment. ");
```

## Ending the Row

<hr/>

When you are done writing the row, you need to flush the fields and start a new row. Flushing and starting a new row are separated so that you can flush without creating a new row.

### Flush

Serialize the fields to the `TextReader`.

```cs
csv.Flush();
```

### FlushAsync

Serialize the fields to the `TextReader` asynchronously. If the `TextReader` supplied is tied to a network or some other slow to write functionality, flushing asynchronously is probably a good idea.

```cs
csv.FlushAsync();
```

### NextRecord

Ends the current record and starts a new record. This will call `Flush` then write a newline.

```cs
csv.NextRecord();
```

### NextRecordAsync

Ends the current record and start a new record asynchronously. This will call `FlushAsync` then asynchronously write a newline.

```cs
csv.NextRecordAsync();
```

## Writing Context

<hr/>

When writing, all the information in the system is held in a context object. If you need to get raw system information for some reason, it's available here. When an exception is throw, the context is included so you can inspect the current state of the writer.

## Configuration

<hr/>

See [configuration](/configuration)

<br/>
