# Migrating from CsvHelper 24 to 25

CsvHelper 25 changes how you create the args of the configuration functions (`BadDataFoundArgs`,
`PrepareHeaderForMatchArgs` and the others): their constructors are gone, and you set their `init` properties instead.
Most code only receives these args in a callback, such as `args => args.Header.ToLower()`, and doesn't change. You are
affected if you create args yourself, for example to call your own `PrepareHeaderForMatch` in a test.
`BadDataFoundArgs` also gets the raw record. The target frameworks don't change.

If you go straight to 26, keep your constructor calls: 26 brings the constructors back and removes the `init`
properties (see [Migrating from 25 to 26](../v26.0.0/migration.md)).

## Delegate args

Each args struct had a constructor that took every member, as well as `init` properties. The constructors are
removed, so you create the args with an object initializer.

```csharp before
PrepareHeaderForMatch prepareHeader = args => args.Header.Trim().ToLower();

var header = prepareHeader(new PrepareHeaderForMatchArgs(" Name ", 0));
```

```csharp after
PrepareHeaderForMatch prepareHeader = args => args.Header.Trim().ToLower();

var header = prepareHeader(new PrepareHeaderForMatchArgs
{
    Header = " Name ",
    FieldIndex = 0,
});
```

| CsvHelper 24 | CsvHelper 25 |
|---|---|
| `new BadDataFoundArgs(field, context)` | `new BadDataFoundArgs { Field, RawRecord, Context }` |
| `new ConvertFromStringArgs(row)` | `new ConvertFromStringArgs { Row }` |
| `new ConvertToStringArgs<T>(value)` | `new ConvertToStringArgs<T> { Value }` |
| `new GetConstructorArgs(classType)` | `new GetConstructorArgs { ClassType }` |
| `new GetDynamicPropertyNameArgs(fieldIndex, context)` | `new GetDynamicPropertyNameArgs { FieldIndex, Context }` |
| `new HeaderValidatedArgs(invalidHeaders, context)` | `new HeaderValidatedArgs { InvalidHeaders, Context }` |
| `new MissingFieldFoundArgs(headerNames, index, context)` | `new MissingFieldFoundArgs { HeaderNames, Index, Context }` |
| `new PrepareHeaderForMatchArgs(header, fieldIndex)` | `new PrepareHeaderForMatchArgs { Header, FieldIndex }` |
| `new ReadingExceptionOccurredArgs(exception)` | `new ReadingExceptionOccurredArgs { Exception }` |
| `new ReferenceHeaderPrefixArgs(memberType, memberName)` | `new ReferenceHeaderPrefixArgs { MemberType, MemberName }` |
| `new ShouldQuoteArgs(field, fieldType, row)` | `new ShouldQuoteArgs { Field, FieldType, Row }` |
| `new ShouldSkipRecordArgs(record)` | `new ShouldSkipRecordArgs { Record }` |
| `new ShouldUseConstructorParametersArgs(parameterType)` | `new ShouldUseConstructorParametersArgs { ParameterType }` |
| `new ValidateArgs(field)` | `new ValidateArgs { Field }` |

Setting an `init` property needs C# 9 or later, so a project that creates args needs `LangVersion` 9 or later. Code
that only reads the args, such as `args.Field`, doesn't change.

## Bad data

`BadDataFoundArgs` has a new `RawRecord` property: the whole row, unedited, as `Field` is the whole field. Use it
instead of reading the parser in the callback.

```csharp before
var problems = new List<string>();
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args => problems.Add($"{args.Field} in {args.Context.Parser.RawRecord}"),
};
```

```csharp after
var problems = new List<string>();
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    BadDataFound = args => problems.Add($"{args.Field} in {args.RawRecord}"),
};
```

Don't read `args.Context.Parser[index]` or `args.Context.Parser.Record` in `BadDataFound`: in 24 that overflowed the
stack, and in 25 it throws a `ParserException` that tells you to use `Field` and `RawRecord`.
