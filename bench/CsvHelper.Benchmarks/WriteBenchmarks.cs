using System.Collections;
using System.Data.Common;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using nietras.SeparatedValues;
using SylvanWriter = Sylvan.Data.Csv.CsvDataWriter;

namespace CsvHelper.Benchmarks;

public abstract class WriteBenchmarkBase
{
	public long DataBytes { get; protected set; }

	protected readonly MemoryStream Output = new(64 << 20);

	/// <summary>The bytes written by the last benchmark call.</summary>
	public ReadOnlySpan<byte> Written => Output.GetBuffer().AsSpan(0, (int)Output.Length);

	protected MemoryStream ResetOutput()
	{
		Output.Position = 0;
		Output.SetLength(0);

		return Output;
	}

	// Sep writes ';' separated values without quoting by default; use ',' and enable escaping so every
	// library writes the same valid CSV.
	protected static readonly Sylvan.Data.Csv.CsvDataWriterOptions SylvanOptions = new() { NewLine = "\r\n" };

	protected static readonly SepWriterOptions SepEscape = Sep.New(',').Writer(o => o with { Escape = true });
}

/// <summary>Writes the package assets records (25 short text fields per record).</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PackageAssetsWrite : WriteBenchmarkBase
{
	private string[] header = [];
	private string[][] rows = [];
	private PackageAsset[] assets = [];

	[GlobalSetup]
	public void Setup()
	{
		using var reader = CsvReader.FromText(DataGenerator.PackageAssets());
		header = reader.Headers.ToArray();
		var rows = new List<string[]>();
		while (reader.Read()) rows.Add(TestRecord(reader));
		this.rows = rows.ToArray();
		assets = this.rows.Select(r =>
		{
			var source = new ArraySource(r);

			return PackageAsset.Create(ref source);
		}).ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	/// <summary>The columns of a record as an <see cref="IColumnSource"/>.</summary>
	private readonly struct ArraySource(string[] record) : IColumnSource
	{
		public string Get(int index) => record[index];
	}

	private static string[] TestRecord(CsvReader reader)
	{
		var record = new string[reader.FieldCount];
		for (var i = 0; i < record.Length; i++) record[i] = reader.GetString(i);

		return record;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long Sep_Write()
	{
		var stream = ResetOutput();
		using (var writer = SepEscape.To(stream, leaveOpen: true))
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
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
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

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Sylvan_Write()
	{
		var stream = ResetOutput();
		using (var textWriter = new StreamWriter(stream, leaveOpen: true))
		using (var writer = SylvanWriter.Create(textWriter, SylvanOptions))
		{
			writer.Write(new StringRowsDataReader(header, rows));
		}

		return stream.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("WriteAsync")]
	public async Task<long> Sep_WriteAsync()
	{
		var stream = ResetOutput();
		await using (var writer = SepEscape.To(stream, leaveOpen: true))
		{
			var header = this.header;
			foreach (var r in rows)
			{
				await using var row = writer.NewRow(CancellationToken.None);
				for (var i = 0; i < r.Length; i++) row[header[i]].Set(r[i]);
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("WriteAsync")]
	public async Task<long> CsvHelper_WriteAsync()
	{
		var stream = ResetOutput();
		await using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteHeader(header);
			foreach (var r in rows)
			{
				for (var i = 0; i < r.Length; i++) writer.WriteField(r[i]);
				await writer.EndRecordAsync();
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("WriteAsync")]
	public async Task<long> Sylvan_WriteAsync()
	{
		var stream = ResetOutput();
		await using (var textWriter = new StreamWriter(stream, leaveOpen: true))
		await using (var writer = SylvanWriter.Create(textWriter, SylvanOptions))
		{
			await writer.WriteAsync(new StringRowsDataReader(header, rows));
		}

		return stream.Length;
	}

	/// <summary>Records written field by field: the code the record writer generated for [CsvRecord] should match.</summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("WriteRecords")]
	public long CsvHelper_WriteRecordsByHand()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteHeader(header);
			foreach (var a in assets)
			{
				writer.WriteField(a.ScanId);
				writer.WriteField(a.ScanTimestamp);
				writer.WriteField(a.Id);
				writer.WriteField(a.Version);
				writer.WriteField(a.Created);
				writer.WriteField(a.ResultType);
				writer.WriteField(a.PatternSet);
				writer.WriteField(a.PropertyAnyValue);
				writer.WriteField(a.PropertyCodeLanguage);
				writer.WriteField(a.PropertyTargetFrameworkMoniker);
				writer.WriteField(a.PropertyLocale);
				writer.WriteField(a.PropertyManagedAssembly);
				writer.WriteField(a.PropertyMSBuild);
				writer.WriteField(a.PropertyRuntimeIdentifier);
				writer.WriteField(a.PropertySatelliteAssembly);
				writer.WriteField(a.Path);
				writer.WriteField(a.FileName);
				writer.WriteField(a.FileExtension);
				writer.WriteField(a.TopLevelFolder);
				writer.WriteField(a.RoundTripTargetFrameworkMoniker);
				writer.WriteField(a.FrameworkName);
				writer.WriteField(a.FrameworkVersion);
				writer.WriteField(a.FrameworkProfile);
				writer.WriteField(a.PlatformName);
				writer.WriteField(a.PlatformVersion);
				writer.EndRecord();
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("WriteRecords")]
	public long CsvHelper_WriteRecords()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteRecords(assets);
		}

		return stream.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("WriteRecordsAsync")]
	public async Task<long> CsvHelper_WriteRecordsAsync()
	{
		var stream = ResetOutput();
		await using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			await writer.WriteRecordsAsync(assets);
		}

		return stream.Length;
	}
}

/// <summary>Writes 3 text fields and 25 floats per record (number formatting).</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FloatsWrite : WriteBenchmarkBase
{
	private string[] header = [];
	private string[][] text = [];
	private float[][] floats = [];

	[GlobalSetup]
	public void Setup()
	{
		using var reader = CsvReader.FromText(DataGenerator.Floats());
		header = reader.Headers.ToArray();
		var text = new List<string[]>();
		var floats = new List<float[]>();
		while (reader.Read())
		{
			text.Add([reader.GetString(0), reader.GetString(1), reader.GetString(2)]);
			var f = new float[DataGenerator.FloatColumns];
			for (var i = 0; i < f.Length; i++) f[i] = reader.GetSingle(3 + i);
			floats.Add(f);
		}

		this.text = text.ToArray();
		this.floats = floats.ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long Sep_Write()
	{
		var stream = ResetOutput();
		using (var writer = SepEscape.To(stream, leaveOpen: true))
		{
			var header = this.header;
			for (var r = 0; r < text.Length; r++)
			{
				using var row = writer.NewRow();
				var t = text[r];
				row[header[0]].Set(t[0]);
				row[header[1]].Set(t[1]);
				row[header[2]].Set(t[2]);
				var f = floats[r];
				for (var i = 0; i < f.Length; i++) row[header[3 + i]].Format(f[i]);
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long CsvHelper_Write()
	{
		var stream = ResetOutput();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteHeader(header);
			for (var r = 0; r < text.Length; r++)
			{
				var t = text[r];
				writer.WriteField(t[0]);
				writer.WriteField(t[1]);
				writer.WriteField(t[2]);
				var f = floats[r];
				for (var i = 0; i < f.Length; i++) writer.WriteField(f[i]);
				writer.EndRecord();
			}
		}

		return stream.Length;
	}

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Sylvan_Write()
	{
		var stream = ResetOutput();
		using (var textWriter = new StreamWriter(stream, leaveOpen: true))
		using (var writer = SylvanWriter.Create(textWriter, SylvanOptions))
		{
			writer.Write(new FloatRowsDataReader(header, text, floats));
		}

		return stream.Length;
	}
}

/// <summary>Writes text that needs quoting: separators, quotes and line breaks inside fields.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class QuotedWrite : WriteBenchmarkBase
{
	private string[] header = [];
	private string[][] rows = [];

	[GlobalSetup]
	public void Setup()
	{
		using var reader = CsvReader.FromText(DataGenerator.Quoted());
		header = reader.Headers.ToArray();
		var rows = new List<string[]>();
		while (reader.Read())
		{
			var record = new string[reader.FieldCount];
			for (var i = 0; i < record.Length; i++) record[i] = reader.GetString(i);
			rows.Add(record);
		}

		this.rows = rows.ToArray();
		CsvHelper_Write();
		DataBytes = Output.Length;
	}

	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Write")]
	public long Sep_Write()
	{
		var stream = ResetOutput();
		using (var writer = SepEscape.To(stream, leaveOpen: true))
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
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
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

	[Benchmark]
	[BenchmarkCategory("Write")]
	public long Sylvan_Write()
	{
		var stream = ResetOutput();
		using (var textWriter = new StreamWriter(stream, leaveOpen: true))
		using (var writer = SylvanWriter.Create(textWriter, SylvanOptions))
		{
			writer.Write(new StringRowsDataReader(header, rows));
		}

		return stream.Length;
	}
}

/// <summary>A minimal forward-only <see cref="DbDataReader"/> over in-memory rows, for Sylvan's writer.</summary>
public abstract class ArrayDataReaderBase(string[] names, int rowCount) : DbDataReader, IDbColumnSchemaGenerator
{
	protected int Row = -1;

	public System.Collections.ObjectModel.ReadOnlyCollection<DbColumn> GetColumnSchema() =>
		names.Select((n, i) => (DbColumn)new Column(n, i, GetFieldType(i))).ToList().AsReadOnly();

	private sealed class Column : DbColumn
	{
		public Column(string name, int ordinal, Type type)
		{
			ColumnName = name;
			ColumnOrdinal = ordinal;
			DataType = type;
			AllowDBNull = false;
		}
	}

	public override int FieldCount => names.Length;
	public override bool HasRows => rowCount > 0;
	public override bool IsClosed => false;
	public override int RecordsAffected => -1;
	public override int Depth => 0;
	public override bool Read() => ++Row < rowCount;
	public override bool NextResult() => false;
	public override string GetName(int ordinal) => names[ordinal];
	public override int GetOrdinal(string name) => Array.IndexOf(names, name);
	public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
	public override bool IsDBNull(int ordinal) => false;
	public override object this[int ordinal] => GetValue(ordinal);
	public override object this[string name] => GetValue(GetOrdinal(name));
	public override int GetValues(object[] values)
	{
		var n = Math.Min(values.Length, FieldCount);
		for (var i = 0; i < n; i++) values[i] = GetValue(i);

		return n;
	}

	public override IEnumerator GetEnumerator() => throw new NotSupportedException();
	public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
	public override byte GetByte(int ordinal) => throw new NotSupportedException();
	public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
	public override char GetChar(int ordinal) => throw new NotSupportedException();
	public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
	public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
	public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
	public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
	public override short GetInt16(int ordinal) => throw new NotSupportedException();
	public override int GetInt32(int ordinal) => throw new NotSupportedException();
	public override long GetInt64(int ordinal) => throw new NotSupportedException();
	public override double GetDouble(int ordinal) => GetFloat(ordinal);
	public override float GetFloat(int ordinal) => throw new NotSupportedException();
}

public sealed class StringRowsDataReader(string[] names, string[][] rows) : ArrayDataReaderBase(names, rows.Length)
{
	public override Type GetFieldType(int ordinal) => typeof(string);
	public override string GetString(int ordinal) => rows[Row][ordinal];
	public override object GetValue(int ordinal) => rows[Row][ordinal];
}

public sealed class FloatRowsDataReader(string[] names, string[][] text, float[][] floats) : ArrayDataReaderBase(names, text.Length)
{
	public override Type GetFieldType(int ordinal) => ordinal < 3 ? typeof(string) : typeof(float);
	public override string GetString(int ordinal) => text[Row][ordinal];
	public override float GetFloat(int ordinal) => floats[Row][ordinal - 3];
	public override object GetValue(int ordinal) => ordinal < 3 ? text[Row][ordinal] : floats[Row][ordinal - 3];
}
