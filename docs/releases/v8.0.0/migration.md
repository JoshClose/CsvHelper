# Migrating from CsvHelper 7 to 8

CsvHelper 8 is a small release. `IgnoreReferences`, which only the `Configuration` class had, is on the
`IReaderConfiguration` and `IWriterConfiguration` interfaces too, and `IFieldReader` has a new method. If you use
`CsvReader`, `CsvWriter` and `Configuration` without implementing those interfaces, your code compiles unchanged. The
target frameworks don't change: .NET Standard 2.0 and .NET Framework 4.5.

## Ignoring references

`IgnoreReferences` makes auto mapping skip reference members: members whose type is a class mapped to columns of its
own. `csv.Configuration` is an `IReaderConfiguration` or `IWriterConfiguration`, which didn't have the setting, so in 7
you set it on a `Configuration` and passed that in. Now you can set it on the reader or writer.

```csharp before
var config = new Configuration { IgnoreReferences = true };
using var csv = new CsvReader(reader, config);
```

```csharp after
using var csv = new CsvReader(reader);
csv.Configuration.IgnoreReferences = true;
```

## Your own implementations

- **Configuration:** a class that implements `IReaderConfiguration` or `IWriterConfiguration` adds
  `bool IgnoreReferences { get; set; }`.
- **Field reader:** a class that implements `IFieldReader` adds `SetBufferPosition(int offset = 0)`, which moves the
  buffer position by the offset.

## Behavior changes

- **The object resolver is called later:** creating a `CsvReader` or `CsvWriter` no longer calls
  `ObjectResolver.Current`. The reader and writer create their `RecordManager` through it the first time you read or
  write a record; before, the reader created it without the resolver. Reading and writing fields by hand with
  `GetField<T>` and `WriteField<T>` doesn't call the resolver; `GetField<T, TConverter>` and
  `TryGetField<T, TConverter>` still create the converter through it.
- **Resolver fallback:** when a resolver's `CanResolve` returns `false` and it uses the fallback, CsvHelper now creates
  the object itself. Before, the fallback asked `ObjectResolver.Current`, which overflowed the stack when that was the
  same resolver. A resolver can now resolve only the types it cares about, such as
  `new ObjectResolver(type => type == typeof(Order), (type, args) => new Order())`.
