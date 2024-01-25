# Migrating from version 29 to 30

## BadDataException constructor

```cs
// 29
throw new BadDataException(context);

// 30
throw new BadDataException(field, rawRecord, context);
```

## IParserConfiguration

Any class that implements `IParserConfiguration` will need to add property `double MaxFixFieldSize { get; }`.
Any class that implements `IParserConfiguration` will need to add property `bool LeaveOpen { get; }`.

## IWriterConfiguration

ixFieldSize { get; }`.
Any class that implements `IWriterConfiguration` will need to add property `bool LeaveO

## ValidateArgs

```cs
// 29
var args = new ValidateArgs(field);

// 30
var args = new ValidateArgs(field, row);
```
