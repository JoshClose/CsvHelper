using System;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

[MemoryDiagnoser]
public class BenchmarkEnumerateRecordsSingleInstance
{
    private const int entryCount = 2000;
    private readonly MemoryStream stream = new();

    public class SimpleWithValueType
    {
        public int Id { get; set; }
        public bool Flag1 { get; set; }
        public bool Flag2 { get; set; }
    }

    [GlobalSetup]
    public void GlobalSetupSimple()
    {
        using var streamWriter = new StreamWriter(this.stream, null, -1, true);
        using var writer = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, true);
        var random = new Random(43); // Different seed for variety

        writer.WriteHeader(typeof(SimpleWithValueType));
        writer.NextRecord();
        for (int i = 0; i < entryCount; ++i)
        {
            writer.WriteRecord(new SimpleWithValueType()
            {
                Id = random.Next(),
                Flag1 = random.Next(2) == 0,
                Flag2 = random.Next(2) == 0
            });
            writer.NextRecord();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanupSimple()
    {
        this.stream.Dispose();
    }

    [Benchmark]
    public void EnumerateRecordsSingleInstance()
    {
        this.stream.Position = 0;
        using var streamReader = new StreamReader(this.stream, null, true, -1, true);
        using var csv = new CsvReader(streamReader, CultureInfo.InvariantCulture, true);
        var instance = new SimpleWithValueType();
        foreach (var record in csv.EnumerateRecords(instance))
        {
            _ = record;
        }
    }
}
