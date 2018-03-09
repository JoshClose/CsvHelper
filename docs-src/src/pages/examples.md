# Examples

- [Private Members](#private-members)
- [Writing Blank Fields](#writing-blank-fields)

## Private Members

<hr/>

If your class has private members and needs to use constructor parameters instead, you won't be able to use a class map. Instead, the auto mapper will detect this and automatically inject the constructor parameters.

The constructor parameter names will be used for the column header names. If there is no header, the position of the parameters will be used instead. You can configure the header matching of the properties.

If the class has multiple constructors, the constructor with the most parameters will be chosen. You can configure the constructor selection to choose whatever constructor you like.

### Headers Match Parameters

```cs
public class PrivateMembers
{
	public int Id { get; private set; }

	public string Name { get; private set; }

	public PrivateMembers( int Id, string Name )
	{
		this.Id = Id;
		this.Name = Name;
	}
}

var records = csv.GetRecords<PrivateMembers>();
```

### Header Don't Match Parameters

```cs
public class PrivateMembers
{
	public int Id { get; private set; }

	public string Name { get; private set; }

	public PrivateMembers( int id, string name )
	{
		Id = id;
		Name = name;
	}
}

// Change the header matching to match the constructor parameters.
csv.Configuration.PrepareHeaderForMatch = header =>
	CultureInfo.CurrentCulture.TextInfo.ToTitleCase( header );

var records = csv.GetRecords<PrivateMembers>();
```

### Choose Alternate Constructor

```cs
public class PrivateMembers
{
	public int Id { get; private set; }

	public string Name { get; private set; }

	public PrivateMembers( int Id, string Name )
	{
		Id = id;
		Name = name;
	}

	public PrivateMembers( int a, int b, int c )
	{
		...
	}
}

// Change the selected constructor.
csv.Configuration.GetConstructor = type =>
	type.GetConstructor( new [] { typeof( int ), typeof(string ) } );

var records = csv.GetRecords<PrivateMembers>();
```

## Writing Blank Fields

<hr/>

If you want to write blank fields, you can have a member that is `null` or an empty `string`. If you don't have members for the field, you can map a constant instead.

```cs
Map( m => m.Id ).Index( 0 );
Map().Index( 1 ).Constant( null );
Map( m => m.Name ).Index( 2 );
```

<br/>
