# Migrating from version 22 to 23

## CsvConfiguration

All the constructor parameters were removed in favor of using
property setters. Apply this change to any of the options.

```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture, delimiter = ";");

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	Delimiter = ";",
};
```

## Delegates

All delegates now take in a single struct argument.

**BadDataFound**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	BadDataFound = (field, context) => 
	{
		Console.WriteLine($"field: {field}");
		Console.WriteLine($"context: {context}");
	},
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	BadDataFound = args =>
	{
		Console.WriteLine($"field: {args.Field}");
		Console.WriteLine($"context: {args.Context}");
	},
};
```

**ConvertFromString**
```cs
// v22
Map(m => m.Property).Convert(row => row.GetField(0));

// v23
Map(m => m.Property).Convert(args => args.Row.GetField(0));
```

**ConvertToString**
```cs
// v22
Map(m => m.Property).Convert(value => value.ToString());

// v23
Map(m => m.Property).Convert(args => args.Value.ToString());
```

**GetConstructor**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	GetConstructor = classType => classType.GetConstructors().First(),
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	GetConstructor = args => args.ClassType.GetConstructors().First(),
};
```

**GetDynamicPropertyName**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	GetDynamicPropertyName = (fieldIndex, context) => $"Field{fieldIndex}";
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	GetDynamicPropertyName = args => $"Field{args.FieldIndex}",
};
```

**HeaderValidated**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	HeaderValidated = (invalidHeaders, context) => Console.WriteLine($"Invalid headers count: {invalidHeaders.Count}"),
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	HeaderValidated = args => Console.WriteLine($"Invalid headers count: {args.InvalidHeaders.Count}"),
};
```

**MissingFieldFound**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	MissingFieldFound = (headerNames, index, context) => Console.WriteLine($"Missing field: {headerNames[0]}"),
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	MissingFieldFound = args => Console.WriteLine($"Missing field: {args.HeaderNames[0]}"),
};
```

**PrepareHeaderForMatch**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	PrepareHeaderForMatch = (header, fieldIndex) => header.ToLower(),
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	PrepareHeaderForMatch = args => args.Header.ToLower(),
};
```

**ReadingExceptionOccurred**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReadingExceptionOccurred = exception =>
    {
        Console.WriteLine(exception.Message);
        throw args.Exception;
    },
};
// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReadingExceptionOccurred = args =>
    {
        Console.WriteLine(args.Exception.Message);
        throw args.Exception;
    },
};
```

**ReferenceHeaderPrefix**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReferenceHeaderPrefix = (memberType, memberName) => $"{memberName}.",
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ReferenceHeaderPrefix = args => $"{args.MemberName}.",
};
```

**ShouldQuote**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = (field, fieldType, row) => true,
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldQuote = args => true,
};
```

**ShouldSkipRecord**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = (records) => record.Length == 0,
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    ShouldSkipRecord = args => args.Record.Length == 0,
};
```

**ShouldUseConstructorParameters**
```cs
// v22
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldUseConstructorParameters = type => true;
};

// v23
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	ShouldUseConstructorParameters = args => true;
};
```

**Validate**
```cs
// v22
Map(m => m.Property).Validate(field => !string.IsNullOrEmpty(field));

// v23
Map(m => m.Property).Validate(args => !string.IsNullOrEmpty(args.Field));
```
