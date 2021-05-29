# Migrating from version 24 to 25

## Delegates

All delegate args had their non-parameterless constructor removed 
in favor of using `init`.

**BadDataFoundArgs**

```cs
// v25
var args = new BadDataRoundArgs(field, rawRecord, context);

// v26
var args = new BadDataFoundArgs
{
	Field = field,
	RawRecord = rawRecord,
	Context = context,
};
```

**ConvertFromStringArgs**

```cs
// v25
var args = new ConvertFromStringArgs(row);

// v26
var args = new ConvertFromStringArgs
{
	Row = row,
};
```

**ConvertToStringArgs**
```cs
// v25
var args = new ConvertToStringArgs(value);

// v26
var args = new ConvertToStringArgs
{
	Value = value,
};
```

**GetConstructorArgs**
```cs
// v25
var args = new GetConstructorArgs(type);

// v26
var args = new GetConstructorArgs
{
	ClassType = type,
};
```

**GetDynamicPropertyNameArgs**
```cs
// v25
var args = new GetDynamicPropertyNameArgs(index, context);

// v26
var args = new GetDynamicPropertyNameArgs
{
	FieldIndex = index,
	Context = context,
};
```

**HeaderValidatedArgs**
```cs
// v25
var args = new HeaderValidatedArgs(headers, context);

// v26
var args = new HeaderValidatedArgs
{
	InvalidHeaders = headers,
	Context = context,
};
```

**MissingFieldFoundArgs**
```cs
// v25
var args = new MissingFieldFoundArgs(headerNames, index, context);

// v26
var args = new MissingFieldFoundArgs
{
	HeaderNames = headerNames,
	Index = index,
	Context = context,
};
```

**PrepareHeaderForMatchArgs**
```cs
// v25
var args = new PrepareHeaderForMatchArgs(header, fieldIndex);

// v26
var args = new PrepareHeaderForMatchArgs
{
	Header = header,
	FieldIndex = fieldIndex,
};
```

**ReadingExceptionOccurredArgs**
```cs
// v25
var args = new ReadingExceptionOccurredArgs(exception);

// v26
var args = new ReadingExceptionOccurredArgs
{
	Exception = exception,
};
```

**ReferenceHeaderPrefixArgs**
```cs
// v25
var args = new ReferenceHeaderPrefixArgs(memberType, memberName);

// v26
var args = new ReferenceHeaderPrefixArgs
{
	MemberType = memberType,
	MemberName = memberName,
};
```

**ShouldQuoteArgs**
```cs
// v25
var args = new ShouldQuoteArgs(field, fieldType, row);

// v26
var args = new ShouldQuoteArgs
{
	Field = field,
	FieldType = fieldType,
	Row = row,
};
```

**ShouldSkipRecordArgs**
```cs
// v25
var args = new ShouldSkipRecordArgs(record);

// v26
var args = new ShouldSkipRecordArgs
{
	Record = record,
};
```

**ShouldUseConstructorParametersArgs**
```cs
// v25
var args = new ShouldUseConstructorParametersArgs(parameterType);

// v26
var args = new ShouldUseConstructorParametersArgs
{
	ParameterType = parameterType,
};
```

**ValidateArgs**
```cs
// v25
var args = new ValidateArgs(field);

// v26
var args = new ValidateArgs
{
	Field = field,
};
```

