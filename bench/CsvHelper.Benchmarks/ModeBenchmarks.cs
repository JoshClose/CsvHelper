using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;
using SylvanReader = Sylvan.Data.Csv.CsvDataReader;
using SylvanWriter = Sylvan.Data.Csv.CsvDataWriter;

namespace CsvHelper.Benchmarks;

/// <summary>The value of column <c>i</c> of a fixed-width line, trimmed, for the hand-written baseline.</summary>
internal readonly struct LineSource(string line, int[] starts) : IColumnSource
{
	public string Get(int index) => FixedWidthRead.Column(line, starts, index).ToString();
}

/// <summary>
/// The package assets data set in Escape mode (a backslash before commas and backslashes, nothing quoted). Sep has no
/// escape mode, so Sylvan's Escaped style is the baseline.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class EscapeRead : ReadBenchmarkBase
{
	private static readonly CsvReaderOptions CsvHelperOptions = new() { Mode = CsvMode.Escape };
	private static readonly CsvDataReaderOptions SylvanOptions = new() { CsvStyle = CsvStyle.Escaped, Escape = '\\' };

	protected override string Generate() => DataGenerator.PackageAssetsEscaped();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Sylvan_Rows()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long Sylvan_Cols()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sylvan_Records()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var list = new List<PackageAsset>();
		var source = new SylvanSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>
/// The package assets data set tab separated, with nothing quoted or escaped, read without quote handling (Sep's
/// DisableQuotesParsing). Sylvan has no such mode: it reads the same data in its standard mode.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class NoEscapeRead : ReadBenchmarkBase
{
	private static readonly SepReaderOptions SepOptions = Sep.New('\t').Reader(o => o with { DisableQuotesParsing = true });
	private static readonly CsvReaderOptions CsvHelperOptions = new() { Mode = CsvMode.NoEscape };
	private static readonly CsvDataReaderOptions SylvanOptions = new() { Delimiter = '\t' };

	protected override string Generate() => DataGenerator.PackageAssetsTsv();

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int Sep_Rows()
	{
		using var reader = SepOptions.From(NewStream());
		var n = 0;
		foreach (var _ in reader) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int Sylvan_Rows()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long Sep_Cols()
	{
		using var reader = SepOptions.From(NewStream());
		var total = 0L;
		foreach (var row in reader)
		{
			for (var i = 0; i < row.ColCount; i++) total += row[i].Span.Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long Sylvan_Cols()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetFieldSpan(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sep_Records()
	{
		using var reader = SepOptions.From(NewStream());
		var list = new List<PackageAsset>();
		foreach (var row in reader)
		{
			var source = new SepSource(row);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = CsvReader.Create(NewStream(), CsvHelperOptions);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> Sylvan_Records()
	{
		using var reader = SylvanReader.Create(new StreamReader(NewStream()), SylvanOptions);
		var list = new List<PackageAsset>();
		var source = new SylvanSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>
/// The package assets data set in fixed-width columns padded with spaces. Sep and Sylvan do not read fixed widths, so
/// the baseline is the usual hand-written loop: <see cref="StreamReader.ReadLine"/>, then a trimmed slice per column.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FixedWidthRead : ReadBenchmarkBase
{
	private CsvReaderOptions csvHelperOptions = new();
	private int[] starts = [];

	protected override string Generate()
	{
		var text = DataGenerator.PackageAssetsFixedWidth(out var widths);
		csvHelperOptions = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = widths };
		starts = new int[widths.Length + 1];
		for (var i = 0; i < widths.Length; i++) starts[i + 1] = starts[i] + widths[i];

		return text;
	}

	/// <summary>Column <paramref name="index"/> of a line, trimmed (a short line has empty columns at its end).</summary>
	internal static ReadOnlySpan<char> Column(string line, int[] starts, int index)
	{
		var start = Math.Min(starts[index], line.Length);

		return line.AsSpan(start, Math.Min(starts[index + 1], line.Length) - start).Trim();
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Rows")]
	public int ReadLine_Rows()
	{
		using var reader = new StreamReader(NewStream());
		reader.ReadLine();
		var n = 0;
		while (reader.ReadLine() is not null) n++;

		return n;
	}

	[Benchmark]
	[BenchmarkCategory("Rows")]
	public int CsvHelper_Rows()
	{
		using var reader = CsvReader.Create(NewStream(), csvHelperOptions);
		var n = 0;
		while (reader.Read()) n++;

		return n;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Cols")]
	public long ReadLine_Cols()
	{
		using var reader = new StreamReader(NewStream());
		reader.ReadLine();
		var total = 0L;
		var columns = starts.Length - 1;
		while (reader.ReadLine() is { } line)
		{
			for (var i = 0; i < columns; i++) total += Column(line, starts, i).Length;
		}

		return total;
	}

	[Benchmark]
	[BenchmarkCategory("Cols")]
	public long CsvHelper_Cols()
	{
		using var reader = CsvReader.Create(NewStream(), csvHelperOptions);
		var total = 0L;
		while (reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++) total += reader.GetSpan(i).Length;
		}

		return total;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> ReadLine_Records()
	{
		using var reader = new StreamReader(NewStream());
		reader.ReadLine();
		var list = new List<PackageAsset>();
		while (reader.ReadLine() is { } line)
		{
			var source = new LineSource(line, starts);
			list.Add(PackageAsset.Create(ref source));
		}

		return list;
	}

	[Benchmark]
	[BenchmarkCategory("Records")]
	public List<PackageAsset> CsvHelper_Records()
	{
		using var reader = CsvReader.Create(NewStream(), csvHelperOptions);
		var list = new List<PackageAsset>();
		var source = new CsvHelperSource(reader);
		while (reader.Read()) list.Add(PackageAsset.Create(ref source));

		return list;
	}
}

/// <summary>Writes the package assets records (with Windows paths) in Escape mode: a backslash before commas and backslashes.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class EscapeWrite : WriteBenchmarkBase
{
	private static readonly CsvWriterOptions CsvHelperOptions = new() { Mode = CsvMode.Escape };
	private static readonly CsvDataWriterOptions SylvanEscape = new() { Style = CsvStyle.Escaped, Escape = '\\', NewLine = "\r\n" };
	private string[] header = [];
	private string[][] rows = [];

	[GlobalSetup]
	public void Setup()
	{
		var records = DataGenerator.PackageAssetRecordsWindowsPaths();
		header = records[0];
		rows = records.Skip(1).ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long Sylvan_Write()
	{
		var stream = ResetOutput();
		using (var textWriter = new StreamWriter(stream, leaveOpen: true))
		using (var writer = SylvanWriter.Create(textWriter, SylvanEscape))
		{
			writer.Write(new StringRowsDataReader(header, rows));
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, CsvHelperOptions, leaveOpen: true))
		{
			writer.WriteHeader(header);
			foreach (var r in rows)
			{
				for (var i = 0; i < r.Length; i++) writer.WriteField(r[i]);
				writer.EndRecord();
			}
		}

		return stream.Length;
	}
}

/// <summary>Writes the package assets records tab separated, as they are (NoEscape mode).</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class NoEscapeWrite : WriteBenchmarkBase
{
	private static readonly CsvWriterOptions CsvHelperOptions = new() { Mode = CsvMode.NoEscape, Separator = '\t' };
	// Sep writes values as they are unless Escape is set.
	private static readonly SepWriterOptions SepTsv = Sep.New('\t').Writer();
	private string[] header = [];
	private string[][] rows = [];

	[GlobalSetup]
	public void Setup()
	{
		var records = DataGenerator.PackageAssetRecords();
		header = records[0];
		rows = records.Skip(1).ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long Sep_Write()
	{
		var stream = ResetOutput();
		using (var writer = SepTsv.To(stream, leaveOpen: true))
		{
			var header = this.header;
			foreach (var r in rows)
			{
				using var row = writer.NewRow();
				for (var i = 0; i < r.Length; i++) row[header[i]].Set(r[i]);
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, CsvHelperOptions, leaveOpen: true))
		{
			writer.WriteHeader(header);
			foreach (var r in rows)
			{
				for (var i = 0; i < r.Length; i++) writer.WriteField(r[i]);
				writer.EndRecord();
			}
		}

		return stream.Length;
	}
}

/// <summary>Writes the package assets records in fixed-width columns, against a <see cref="string.PadRight(int)"/> loop.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FixedWidthWrite : WriteBenchmarkBase
{
	private CsvWriterOptions csvHelperOptions = new();
	private int[] widths = [];
	private string[] header = [];
	private string[][] rows = [];

	[GlobalSetup]
	public void Setup()
	{
		var records = DataGenerator.PackageAssetRecords();
		widths = DataGenerator.FixedWidths(records);
		csvHelperOptions = new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = widths };
		header = records[0];
		rows = records.Skip(1).ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long PadRight_Write()
	{
		var stream = ResetOutput();
		using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
		{
			Write(writer, header);
			foreach (var r in rows) Write(writer, r);
		}

		return stream.Length;

		void Write(StreamWriter writer, string[] record)
		{
			for (var i = 0; i < record.Length; i++) writer.Write(record[i].PadRight(widths[i]));
			writer.Write("\r\n");
		}
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, csvHelperOptions, leaveOpen: true))
		{
			writer.WriteHeader(header);
			foreach (var r in rows)
			{
				for (var i = 0; i < r.Length; i++) writer.WriteField(r[i]);
				writer.EndRecord();
			}
		}

		return stream.Length;
	}
}
