using BenchmarkDotNet.Attributes;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CsvHelper.Benchmarks;

public class BenchmarkWriteRecords
{
    private const int entryCount = 10000;
    private readonly MemoryStream stream = new();
    private List<Simple> records = new();
    public class Simple
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var random = new Random(42); // Pick a known seed to keep things consistent
        var chars = new char[10];
        string getRandomString()
        {
            for (int i = 0; i < 10; ++i)
                chars[i] = (char)random.Next('a', 'z' + 1);
            return new string (chars);
        }

        this.records = new List<Simple>(entryCount);
        for (int i = 0; i < entryCount; ++i)
        {
            this.records.Add(new Simple() { Id = random.Next(), Name = getRandomString() });
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.stream.Dispose();
    }

    [Benchmark]
    public void WriteRecords()
    {
        this.stream.Position = 0;
        this.stream.SetLength(0);
        using var streamWriter = new StreamWriter(this.stream, null, -1, true);
        using var csv = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, true);
        csv.WriteRecords(this.records);
    }
}
