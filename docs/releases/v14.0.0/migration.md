# Migrating from CsvHelper 13 to 14

CsvHelper 14 lets you dispose a `CsvWriter` asynchronously, with `await using`, so that the last records are written
with `FlushAsync` instead of the blocking `Flush` that `Dispose` calls. Most code needs no change. Code that
implements `ISerializer` or `IWriter` needs a `DisposeAsync` method, and the package depends on
System.Threading.Tasks.Extensions on .NET Framework 4.5 and 4.7 and .NET Standard 2.0.

## Disposing a writer

Use `await using` where you write asynchronously:

```csharp before
public class Foo
{
    public int Id { get; set; }
}

using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    await csv.WriteRecordsAsync(new List<Foo> { new Foo { Id = 1 } });
}
```

```csharp after
public class Foo
{
    public int Id { get; set; }
}

await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    await csv.WriteRecordsAsync(new List<Foo> { new Foo { Id = 1 } });
}
```

`DisposeAsync` flushes the writer with `FlushAsync`, then disposes the `TextWriter` (unless `leaveOpen` is `true`),
with `DisposeAsync` on .NET Standard 2.1 and with `FlushAsync` and `Dispose` elsewhere. `CsvWriter`, `CsvSerializer`
and `WritingContext` have it in the .NET Standard and .NET Framework 4.7 builds, not in the .NET Framework 4.5 build.

## Implementing ISerializer or IWriter

`ISerializer` extends `IAsyncDisposable` in the .NET Standard and .NET Framework 4.7 builds, and `IWriter` in the
.NET Standard 2.1 and .NET Framework 4.7 builds. An implementation needs a `DisposeAsync` method:

```csharp before
public class UpperCaseSerializer : ISerializer
{
    private readonly CsvSerializer inner;

    public UpperCaseSerializer(TextWriter textWriter)
    {
        inner = new CsvSerializer(textWriter, CultureInfo.InvariantCulture);
    }

    public WritingContext Context => inner.Context;

    public ISerializerConfiguration Configuration => inner.Configuration;

    public void Write(string[] fields) => inner.Write(fields.Select(f => f?.ToUpperInvariant()).ToArray());

    public Task WriteAsync(string[] fields) => inner.WriteAsync(fields.Select(f => f?.ToUpperInvariant()).ToArray());

    public void WriteLine() => inner.WriteLine();

    public Task WriteLineAsync() => inner.WriteLineAsync();

    public void Dispose() => inner.Dispose();
}
```

```csharp after
public class UpperCaseSerializer : ISerializer
{
    private readonly CsvSerializer inner;

    public UpperCaseSerializer(TextWriter textWriter)
    {
        inner = new CsvSerializer(textWriter, CultureInfo.InvariantCulture);
    }

    public WritingContext Context => inner.Context;

    public ISerializerConfiguration Configuration => inner.Configuration;

    public void Write(string[] fields) => inner.Write(fields.Select(f => f?.ToUpperInvariant()).ToArray());

    public Task WriteAsync(string[] fields) => inner.WriteAsync(fields.Select(f => f?.ToUpperInvariant()).ToArray());

    public void WriteLine() => inner.WriteLine();

    public Task WriteLineAsync() => inner.WriteLineAsync();

    public void Dispose() => inner.Dispose();

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
```
