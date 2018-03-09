# Class Mapping

Sometimes your class members and CSV headers don't match up. Sometimes your CSV files don't have a header row and you need to specify an index for a member [because you can't rely on the ordering of class members in .NET](https://blogs.msdn.microsoft.com/haibo_luo/2006/07/10/member-order-returned-by-getfields-getmethods/). For these situations you can create a class map that maps a class member to a CSV field using the configuration you specify.

To create a mapping from a class to a CSV file, you use a `ClassMap`. You can map any public members (properties or fields).

```cs
public class MyClass
{
	public int Id { get; set; }
	public string Name { get; set; }
}

public sealed class MyClassMap : ClassMap<MyClass>
{
	public MyClassMap()
	{
		Map( m => m.Id );
		Map( m => m.Name );
	}
}
```

To use this mapping, you need to register the mapping in the configuration.

```cs
var csv = new CsvReader( textReader );
csv.Configuration.RegisterClassMap<MyClassMap>();
```

## Reference Mapping

<hr/>

To map a reference member, just reference the member down the tree. You can reference as far down the tree as you need.

```cs
public class A
{
	public int Id { get; set; }
	public B B { get; set; }
}

public class B
{
	public int Id { get; set; }
	public C C { get; set; }
}

public class C
{
	public int Id { get; set; }
}

public sealed class AMap : ClassMap<A>
{
	public AMap()
	{
		Map( m => m.Id ).Name( "A" );
		Map( m => m.B.Id ).Name( "B" );
		Map( m => m.B.C.Id).Name( "C" );
	}
}
```

## Auto Mapping

<hr/>

If you don't supply a mapping file and try to read or write, a mapping file is automatically created for you through auto mapping. Auto mapping will traverse the object graph and create member mappings for you using defaults. You can change some of these defaults through configuration. If a circular reference is detected, the auto mapper will stop traversing that tree node and continue with the next.

You can also call `AutoMap` in your `ClassMap`. If you only have a few changes you want to make, you can use `AutoMap` to create the initial map, then make only the changes you want.

```cs
public class MyClass
{
	public int Id { get; set; }

	public string Name { get; set; }

	public DateTime CreatedDate { get; set; }
}

public sealed class MyClassMap : ClassMap<MyClass>
{
	public MyClassMap()
	{
		AutoMap();
		Map( m => m.CreatedDate ).Ignore();
	}
}
```

## Options

<hr/>

You are able change the behavior of the member mapping through options.

### Name

Specifies the name of the field header. You can pass in multiple names if the field might have more than one name that is used for it when reading. All the names will be checked when looking for the field. When writing, only the first name is used.

```cs
// Single name
Map( m => m.Id ).Name( "id" );

// Multiple possible names
Map( m => m.Id ).Name( "id", "the_id", "Id" );
```

### NameIndex

Specifies the zero-based index of the header name if the header name appears in more than one column.

```cs
// Example header
id,name,id

// Mapping
Map( m => m.Id ).Name( "id" ).Index( 1 );
```

### Index

Specifies the zero-based index of the field. When reading this is used if there is no header. A name will override an index. When writing you can specify both so that the position of the column is guaranteed.

```cs
Map( m => m.Id ).Index( 0 );
```

### Default

Specifies a default value when reading that will be used when a field is empty.

```cs
Map( m => m.Name ).Default( "empty" );
```

### Constant

Specifies a value that will be used as a constant for a field when reading and writing. This value will always be used regardless of other mapping configurations.

```cs
Map( m => m.Name ).Constant( "never changes" );
```

### Ignore

Ignores the member when reading and writing. Note: If this member has already been mapped as a reference member, either by a class map, or by auto mapping, calling this method will not ignore all the child members down the tree that have already been mapped.

```cs
Map( m => m.Name ).Ignore();
```

### TypeConverter

Specifies the `ITypeConverter` that is used when converting the member to and from a CSV field.

```cs
Map( m => m.Name ).TypeConverter( new MyConverter() );
Map( m => m.Name ).TypeConverter<MyConverter>();
```

### ConvertUsing

Specifies an expression to be used to convert a field to a member, or a member to a field.

```cs
// Convert to member
Map( m => m.Aggregate ).ConvertUsing( row => row.Get<int>( "A" ) + row.Get<int>( "B" ) );

// Block
Map( m => m.Aggregate ).ConvertUsing( row =>
{
	var a = row.Get<int>( "A" );
	var b = row.Get<int>( "B" );
	return a + b;
} );

// Convert to field
Map( m => m.Aggregate ).ConvertUsing( m => $"A + B = {m.A + m.B}" );

// Block
Map( m => m.Aggregate ).ConvertUsing( m =>
{
	var field = "A + B = ";
	field += ( m.A + m.B ).ToString();
	return field;
} );
```

### Validate

Specifies an expression to be used to validate a field when reading. If the expression returns `false`, a `ValidationException` is thrown.

```cs
// Ensure field isn't blank.
Map( m => m.Number ).Validate( field => !string.IsNullOrEmpty( field ) );

// Log error instead of throwing an exception.
Map( m => m.Number ).Validate( field =>
{
	var isValid = !string.IsNullOrEmpty( field );
	if( !isValid )
	{
		logger.AppendLine( $"Field '{field}' is not valid!" );
	}

	return true;
} );
```

<br/>
