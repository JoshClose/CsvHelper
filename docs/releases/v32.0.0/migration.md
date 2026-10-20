# Migrating from CsvHelper 31 to 32

CsvHelper 32 makes reading and writing many records faster. The reader and writer get the delegate that creates or
writes a record once per record type, instead of looking it up for each record, and `dynamic` records are read into a
dictionary-backed object instead of an `ExpandoObject`. `GetRecords`, `WriteRecords` and the configuration don't
change. The breaking changes are in the record plumbing of `CsvHelper.Expressions`, which you only use if you derive
from `RecordWriter`, `RecordWriterFactory`, `RecordCreator` or `CsvWriter`, or call `RecordManager` yourself. If you
read `dynamic` records, see [Dynamic records](#dynamic-records). The target frameworks are the same as in 31.

## Dynamic records

`GetRecords<dynamic>()` and `GetRecord<dynamic>()` no longer give `ExpandoObject`s. Each record is an internal type
that implements `IDictionary<string, object>` and `IDynamicMetaObjectProvider`, so `record.Name` and a cast to
`IDictionary<string, object>` work as before, but a cast to `ExpandoObject` fails at run time.

```csharp before
using System.Dynamic;

using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
foreach (ExpandoObject record in csv.GetRecords<dynamic>())
{
    IDictionary<string, object> fields = record;
    Console.WriteLine(fields["Name"]);
}
```

```csharp after
using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
foreach (IDictionary<string, object> record in csv.GetRecords<dynamic>())
{
    Console.WriteLine(record["Name"]);
}
```

- **Use 32.0.1 or later.** In 32.0.0 the new object implements only the indexer of the dictionary: `Keys`, `Count`,
  `ContainsKey`, `TryGetValue`, `Add`, `Remove` and enumerating it throw `NotSupportedException`, so writing the
  records back out with `WriteRecords` fails too. 32.0.1 implements the whole interface.
- **A member the record doesn't have** throws a `CsvHelperException` ("does not contain a definition for ...")
  instead of a `RuntimeBinderException`.
- **A header with the same name twice** made 31 throw a `ReaderException`. 32 reads the record, and the name gets the
  value of the last of those columns.

## Record writers

This section is for you if you derive from `RecordWriter` or one of its subclasses (`ObjectRecordWriter`,
`DynamicRecordWriter`, `ExpandoObjectRecordWriter`, `PrimitiveRecordWriter`), or from `RecordWriterFactory`, and
register it through `ObjectResolver`. A record writer now creates its write delegate from the record's type instead of
from a record: `CreateWriteDelegate<T>(T record)` is `CreateWriteDelegate<T>(Type recordType)`, and
`RecordWriterFactory.MakeRecordWriter<T>(T record)` is `MakeRecordWriter(Type recordType)`. The delegates were already
cached by type in 31, so the record a writer was given was only the first one of its type.

```csharp before
public class LoggingRecordWriter : ObjectRecordWriter
{
    public LoggingRecordWriter(CsvWriter writer) : base(writer) { }

    protected override Action<T> CreateWriteDelegate<T>(T record)
    {
        var type = Writer.GetTypeForRecord(record);
        Console.WriteLine($"Writing {type.Name} records.");

        return base.CreateWriteDelegate(record);
    }
}

public class LoggingRecordWriterFactory : RecordWriterFactory
{
    private readonly LoggingRecordWriter loggingWriter;

    public LoggingRecordWriterFactory(CsvWriter writer) : base(writer)
    {
        loggingWriter = new LoggingRecordWriter(writer);
    }

    public override RecordWriter MakeRecordWriter<T>(T record)
    {
        var recordWriter = base.MakeRecordWriter(record);

        return recordWriter is ObjectRecordWriter ? loggingWriter : recordWriter;
    }
}

ObjectResolver.Current = new ObjectResolver(
    type => type == typeof(RecordWriterFactory),
    (type, args) => new LoggingRecordWriterFactory((CsvWriter)args[0]));
```

```csharp after
public class LoggingRecordWriter : ObjectRecordWriter
{
    public LoggingRecordWriter(CsvWriter writer) : base(writer) { }

    protected override Action<T> CreateWriteDelegate<T>(Type recordType)
    {
        Console.WriteLine($"Writing {recordType.Name} records.");

        return base.CreateWriteDelegate<T>(recordType);
    }
}

public class LoggingRecordWriterFactory : RecordWriterFactory
{
    private readonly LoggingRecordWriter loggingWriter;

    public LoggingRecordWriterFactory(CsvWriter writer) : base(writer)
    {
        loggingWriter = new LoggingRecordWriter(writer);
    }

    public override RecordWriter MakeRecordWriter(Type recordType)
    {
        var recordWriter = base.MakeRecordWriter(recordType);

        return recordWriter is ObjectRecordWriter ? loggingWriter : recordWriter;
    }
}

ObjectResolver.Current = new ObjectResolver(
    type => type == typeof(RecordWriterFactory),
    (type, args) => new LoggingRecordWriterFactory((CsvWriter)args[0]));
```

- **An override of the old method still compiles, but nothing calls it.** `RecordWriter` keeps
  `CreateWriteDelegate<T>(T record)` as a virtual method that calls the new one, so overriding it in a class derived
  from `ObjectRecordWriter` or another built-in writer compiles. The writer calls `CreateWriteDelegate<T>(Type)`, so
  move your code there. A class derived from `RecordWriter` itself must implement the new method, which is abstract.
- **The type is the one 31 computed from the record:** `typeof(T)`, or the record's runtime type when `T` is `object`.
- **The factory chooses by type:** `MakeRecordWriter` returns the `PrimitiveRecordWriter` for primitive types, the
  `ExpandoObjectRecordWriter` for any type that implements `IDictionary<string, object>` (in 31, only for
  `ExpandoObject` records), the `DynamicRecordWriter` for other `IDynamicMetaObjectProvider` types and the
  `ObjectRecordWriter` for the rest.
- **`CsvWriter.GetTypeForRecord<T>(T record)`** is `GetTypeInfoForRecord<T>(T record)` and returns a `RecordTypeInfo`:
  `RecordType` is the type, and `IsObject` is `true` when `T` is `object` and the type came from `record.GetType()`.
  Override that one in a `CsvWriter` subclass.

## Record manager and creators

`RecordManager`, `RecordCreator` and `RecordWriter` hand out delegates now, instead of creating or writing one record
per call. Get the delegate once and call it for each record, as the reader and writer do.

```csharp before
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}

using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
var readManager = new RecordManager(csvReader);
var writeManager = new RecordManager(csvWriter);

csvReader.Read();
csvReader.ReadHeader();
while (csvReader.Read())
{
    var person = readManager.Create<Person>();
    writeManager.Write(person);
    csvWriter.NextRecord();
}
```

```csharp after
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}

using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
var read = new RecordManager(csvReader).GetReadDelegate<Person>(typeof(Person));
var write = new RecordManager(csvWriter).GetWriteDelegate<Person>(new RecordTypeInfo(typeof(Person), false));

csvReader.Read();
csvReader.ReadHeader();
while (csvReader.Read())
{
    var person = read();
    write(person);
    csvWriter.NextRecord();
}
```

| Before | After | Notes |
|---|---|---|
| `RecordManager.Create<T>()`, `Create(Type)` | `RecordManager.GetReadDelegate<T>(Type recordType)` | Returns a `Func<T>` that reads the current row. |
| `RecordManager.Write<T>(T record)` | `RecordManager.GetWriteDelegate<T>(RecordTypeInfo typeInfo)` | Returns an `Action<T>` that writes a record to the current row. |
| `RecordCreator.Create<T>()`, `Create(Type)` | `RecordCreator.GetCreateRecordDelegate<T>(Type recordType)` | Was `protected virtual Delegate GetCreateRecordDelegate(Type)`; now public, generic and returns a `Func<T>`. |
| `RecordWriter.Write<T>(T record)` | `RecordWriter.GetWriteDelegate<T>(RecordTypeInfo typeInfo)` | Was `protected GetWriteDelegate<T>(T record)`; now public and virtual. |
| `RecordWriter.CreateWriteDelegate<T>(T record)` | `CreateWriteDelegate<T>(Type recordType)` | See [Record writers](#record-writers). |
| `RecordWriterFactory.MakeRecordWriter<T>(T record)` | `MakeRecordWriter(Type recordType)` | |
| `CsvWriter.GetTypeForRecord<T>(T record)` | `CsvWriter.GetTypeInfoForRecord<T>(T record)` | Returns a `RecordTypeInfo` instead of a `Type`. |

`RecordCreator.CreateCreateRecordDelegate(Type)`, the method that record creators implement, didn't change.
