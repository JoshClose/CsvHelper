using BenchmarkDotNet.Attributes;

using CsvHelper.Configuration;

using System;
using System.Globalization;
using System.IO;

namespace CsvHelper.Benchmarks;

public class BenchmarkShouldQuote
{
    private const int FieldCount = 10000;
    private readonly StringWriter stringWriter = new();
    private string[] fields = null!;
    private static readonly Type StringType = typeof(string);
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Pre-compute a mix of typical fields. Real CsvWriter is created per iteration below.
        var random = new Random(42);
        this.fields = new string[FieldCount];
        var chars = new char[10];
        for (int i = 0; i < FieldCount; i++)
        {
            string value;
            var pick = i % 10;
            if (pick == 0)
            {
                // Field that should be quoted (contains comma)
                value = "a,b,c";
            }
            else if (pick == 1)
            {
                // Field that should be quoted (contains quote)
                value = "he said \"hi\"";
            }
            else if (pick < 6)
            {
                // Random lowercase strings (no special chars)
                for (int j = 0; j < 10; j++)
                {
                    chars[j] = (char)random.Next('a', 'z' + 1);
                }

                value = new string (chars);
            }
            else
            {
                // Integer-as-string
                value = random.Next().ToString(CultureInfo.InvariantCulture);
            }

            this.fields[i] = value;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.stringWriter.Dispose();
    }

    [Benchmark]
    public void ShouldQuote()
    {
        using var csv = new CsvWriter(this.stringWriter, CultureInfo.InvariantCulture, true);
        int quotedCount = 0;
        var f = this.fields;
        for (int i = 0; i < f.Length; i++)
        {
            var args = new ShouldQuoteArgs(f[i], StringType, csv);
            if (ConfigurationFunctions.ShouldQuote(args))
            {
                quotedCount++;
            }
        }

        _ = quotedCount;
    }
}
