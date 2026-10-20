using System.Data;
using System.Text;

namespace CsvHelper.Tests;

[CsvRecord]
public partial class Reading
{
	public string Name { get; set; } = "";

	public int Value { get; set; }

	[CsvColumn(Optional = true)]
	public double? Rate { get; set; }

	[CsvColumn(Optional = true)]
	public string? Note { get; set; }
}

/// <summary>Two columns with the same name; the second is declared first.</summary>
[CsvRecord]
public partial class Amounts
{
	public string Id { get; set; } = "";

	[CsvColumn("Amount", NameIndex = 1)]
	public decimal Gross { get; set; }

	[CsvColumn("Amount")]
	public decimal Net { get; set; }
}

[CsvRecord]
public partial class ThirdAmount
{
	[CsvColumn("Amount", NameIndex = 2)]
	public decimal Value { get; set; }

	public string Id { get; set; } = "";
}

[CsvRecord]
public partial class Paint
{
	public Color Color { get; set; }

	[CsvBooleanValues("J", "N")]
	public bool Glossy { get; set; }
}

/// <summary>Set by an object initializer only, so its records cannot be read into an existing instance.</summary>
[CsvRecord]
public partial class Settled
{
	public string Name { get; init; } = "";
}

/// <summary>
/// Record reading with the newer options: null values, skipped and failing records, header checks, refilled instances,
/// columns that share a name, values left out of exceptions, and the IDataReader adapter.
/// </summary>
public class RecordOptionTests
{
	private const string Readings = "Name,Value,Rate,Note\nA,1,NULL,NULL\nB,2,,\nC,3,1.5,x\n";

	private static CsvReader Reader(string csv, CsvReaderOptions? options = null) => CsvReader.FromText(csv, options);

	[Fact]
	public void NullValuesInRecords()
	{
		using var reader = Reader(Readings, new CsvReaderOptions { NullValues = ["NULL"] });
		var records = reader.GetRecords<Reading>().ToList();

		Assert.Equal([null, null, 1.5], records.Select(r => r.Rate));
		// A nullable string is null for a null value, "" for an empty field.
		Assert.Equal([null, "", "x"], records.Select(r => r.Note));
		// Without NullValues, "NULL" does not parse as a double.
		using var plain = Reader(Readings);
		Assert.Throws<CsvFormatException>(() => plain.GetRecords<Reading>().ToList());
	}

	[Fact]
	public void ShouldSkipRecordHidesRecordsFromTheMapper()
	{
		var options = new CsvReaderOptions { ShouldSkipRecord = row => row.GetString(0) == "Total" };
		using var reader = Reader("Name,Value\nA,1\nTotal,many\nB,2\n", options);

		Assert.Equal(["A1", "B2"], reader.GetRecords<Reading>().Select(r => r.Name + r.Value));
	}

	[Fact]
	public void OnRecordErrorSkipsRecordsThatDoNotMap()
	{
		var errors = new List<CsvRecordError>();
		var options = new CsvReaderOptions { OnRecordError = e => { errors.Add(e); return true; } };
		using var reader = Reader("Name,Value\nA,1\nB,x\nC,3\n", options);

		Assert.Equal(["A", "C"], reader.GetRecords<Reading>().Select(r => r.Name));
		var error = Assert.Single(errors);
		Assert.Equal(2, error.RowNumber);
		Assert.Equal(15, error.ByteOffset);
		Assert.Equal("B,x", error.RawRecord);
		// The handler gets the exception with the record's context; the parse error is its inner exception.
		var e = Assert.IsType<CsvFormatException>(error.Exception);
		Assert.IsType<FormatException>(e.InnerException);
		Assert.Equal("Reading.Value", e.Member);
		Assert.Equal(1, e.FieldIndex);
	}

	[Fact]
	public void OnRecordErrorCanRethrow()
	{
		var options = new CsvReaderOptions { OnRecordError = e => e.RowNumber != 3 };
		using var reader = Reader("Name,Value\nA,x\nB,2\nC,y\n", options);
		var names = new List<string>();

		var e = Assert.Throws<CsvFormatException>(() =>
		{
			foreach (var r in reader.GetRecords<Reading>()) names.Add(r.Name);
		});
		Assert.Equal(["B"], names);
		Assert.Equal(4, e.RecordNumber);
	}

	[Fact]
	public async Task OnRecordErrorInEveryEnumeration()
	{
		const string csv = "Name,Value\nA,1\nB,x\nC,3\n";
		var count = 0;
		var options = new CsvReaderOptions { OnRecordError = _ => { Interlocked.Increment(ref count); return true; } };

		using (var reader = Reader(csv, options))
		{
			var names = new List<string>();
			await foreach (var r in reader.GetRecordsAsync<Reading>()) names.Add(r.Name);
			Assert.Equal(["A", "C"], names);
		}

		using (var reader = Reader(csv, options))
		{
			Assert.Equal(["A", "C"], reader.EnumerateRecords(new Reading()).Select(r => r.Name));
		}

		using (var reader = Reader(csv, options))
		{
			var names = new List<string>();
			await foreach (var r in reader.EnumerateRecordsAsync(new Reading())) names.Add(r.Name);
			Assert.Equal(["A", "C"], names);
		}

		Assert.Equal(3, count);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public void OnRecordErrorInParallel(int degree)
	{
		var csv = "Name,Value\n" + string.Concat(Enumerable.Range(0, 50_000).Select(i => i % 1000 == 999 ? $"n{i},bad\n" : $"n{i},{i}\n"));
		var errors = new System.Collections.Concurrent.ConcurrentBag<long>();
		var options = new CsvReaderOptions { OnRecordError = e => { errors.Add(e.RowNumber); return true; } };
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(csv)), options);

		Assert.Equal(Enumerable.Range(0, 50_000).Where(i => i % 1000 != 999), reader.ParallelEnumerate<Reading>(degree).Select(r => r.Value));
		Assert.Equal(Enumerable.Range(0, 50).Select(i => (long)i * 1000 + 1000), errors.OrderBy(r => r));
	}

	[Fact]
	public void ValidateHeader()
	{
		using (var reader = Reader("Name,Rate\nA,1\n"))
		{
			Assert.False(reader.TryValidateHeader<Reading>(out var missing));
			Assert.Equal(["Reading.Value ('Value')"], missing);
			Assert.Contains("Reading.Value", Assert.Throws<KeyNotFoundException>(() => reader.ValidateHeader<Reading>()).Message);
		}

		using (var reader = Reader("Name,Value\nA,1\n"))
		{
			Assert.True(reader.TryValidateHeader<Reading>(out var missing));
			Assert.Empty(missing);
			reader.ValidateHeader<Reading>();
			// Checking reads no record.
			Assert.Equal("A", Assert.Single(reader.GetRecords<Reading>()).Name);
		}
	}

	[Fact]
	public void EnumerateRecordsRefillsOneInstance()
	{
		var record = new Reading { Rate = 9 };
		using var reader = Reader("Name,Value,Note\nA,1,x\nB,2,\n");
		var seen = new List<string>();
		foreach (var r in reader.EnumerateRecords(record))
		{
			Assert.Same(record, r);
			seen.Add($"{r.Name}{r.Value}{r.Note}{r.Rate}");
		}

		// The optional Rate column is missing, so the instance keeps its value.
		Assert.Equal(["A1x9", "B29"], seen);
	}

	[Fact]
	public async Task EnumerateRecordsAsyncRefillsOneInstance()
	{
		var record = new Reading();
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(Readings.Replace("NULL", "")), 5, 3, yieldAsync: true));
		var seen = new List<string>();
		await foreach (var r in reader.EnumerateRecordsAsync(record))
		{
			Assert.Same(record, r);
			seen.Add(r.Name + r.Value + r.Rate);
		}

		Assert.Equal(["A1", "B2", "C31.5"], seen);
	}

	[Fact]
	public void GetRecordIntoAnInstance()
	{
		var record = new Reading();
		using var reader = Reader("Name,Value\nA,1\n");
		reader.Read();
		reader.GetRecord(record);

		Assert.Equal("A1", record.Name + record.Value);
	}

	[Fact]
	public void TypesWithInitAccessorsCannotBeRefilled()
	{
		using var reader = Reader("Name\nA\n", new CsvReaderOptions { OnRecordError = _ => true });

		Assert.Contains("existing instance", Assert.Throws<NotSupportedException>(() => reader.EnumerateRecords(new Settled()).ToList()).Message);
	}

	[Fact]
	public void ColumnsThatShareAName()
	{
		using (var reader = Reader("Id,Amount,Amount\nx,1.5,2.5\n"))
		{
			var record = Assert.Single(reader.GetRecords<Amounts>());
			Assert.Equal((1.5m, 2.5m), (record.Net, record.Gross));
		}

		using (var reader = Reader("Id,Amount\nx,1.5\n"))
		{
			Assert.Contains("Amounts.Gross ('Amount' number 2)", Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Amounts>()).Message);
		}
	}

	[Fact]
	public void ColumnsThatShareANameAreWrittenInOrder()
	{
		Assert.Equal("Id,Amount,Amount\r\nx,1.5,2.5\r\n", Write([new Amounts { Id = "x", Net = 1.5m, Gross = 2.5m }]));
		// The columns before the one a member reads are written empty.
		var csv = Write([new ThirdAmount { Value = 3.5m, Id = "x" }]);
		Assert.Equal("Amount,Amount,Amount,Id\r\n,,3.5,x\r\n", csv);
		using var reader = Reader(csv);
		Assert.Equal(3.5m, Assert.Single(reader.GetRecords<ThirdAmount>()).Value);

		static string Write<T>(T[] records)
		{
			var text = new StringWriter();
			using (var writer = CsvWriter.Create(text))
			{
				writer.WriteRecords(records);
			}

			return text.ToString();
		}
	}

	[Fact]
	public void NormalizeHeaderNameInRecords()
	{
		using var reader = Reader("id,unit_price\nA,2.5\n", new CsvReaderOptions { NormalizeHeaderName = n => n.Replace('_', ' ') });

		Assert.Equal(2.5m, Assert.Single(reader.GetRecords<Order>()).Price);
	}

	[Fact]
	public void GeneratedParsersCanLeaveValuesOut()
	{
		foreach (var csv in new[] { "Color,Glossy\nPurple,J\n", "Color,Glossy\nRed,Purple\n" })
		{
			using (var reader = Reader(csv))
			{
				Assert.Contains("Purple", Assert.Throws<CsvFormatException>(() => reader.GetRecords<Paint>().ToList()).Message);
			}

			using (var reader = Reader(csv, new CsvReaderOptions { IncludeValuesInExceptions = false }))
			{
				var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Paint>().ToList());
				Assert.DoesNotContain("Purple", e.Message);
				Assert.Contains("IncludeValuesInExceptions", e.Message);
			}
		}
	}

	[Fact]
	public void DataReaderLoadsADataTable()
	{
		using var reader = Reader("Name,Value\nA,1\nB,NULL\n", new CsvReaderOptions { NullValues = ["NULL"] });
		using var data = new CsvDataReader(reader);
		var table = new DataTable();
		table.Load(data);

		Assert.Equal(["Name", "Value"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
		Assert.All(table.Columns.Cast<DataColumn>(), c => Assert.Equal(typeof(string), c.DataType));
		Assert.Equal("A", table.Rows[0]["Name"]);
		Assert.Equal("1", table.Rows[0]["Value"]);
		Assert.Equal(DBNull.Value, table.Rows[1]["Value"]);
		Assert.True(data.IsClosed);
	}

	[Fact]
	public void DataReaderMembers()
	{
		using var reader = Reader("Name,Value,When\nAnn,42,2024-05-06\n");
		using var data = new CsvDataReader(reader, leaveOpen: true);

		Assert.Equal(3, data.FieldCount);
		Assert.Equal(1, data.GetOrdinal("value"));
		Assert.Throws<IndexOutOfRangeException>(() => data.GetOrdinal("missing"));
		Assert.True(data.Read());
		Assert.Equal(42, data.GetInt32(1));
		Assert.Equal((short)42, data.GetInt16(1));
		Assert.Equal(new DateTime(2024, 5, 6), data.GetDateTime(2));
		Assert.Equal("Ann", data["Name"]);
		Assert.Equal(typeof(string), data.GetFieldType(0));
		var values = new object[3];
		Assert.Equal(3, data.GetValues(values));
		Assert.Equal(["Ann", "42", "2024-05-06"], values);
		var chars = new char[2];
		Assert.Equal(2, data.GetChars(0, 1, chars, 0, 2));
		Assert.Equal("nn", new string(chars));
		Assert.Equal(3, data.GetChars(0, 0, null, 0, 0));
		Assert.Equal(["Name", "Value", "When"], data.GetSchemaTable().Rows.Cast<DataRow>().Select(r => (string)r["ColumnName"]));
		Assert.False(data.Read());
		Assert.False(data.NextResult());
		data.Close();
		// leaveOpen keeps the CsvReader usable.
		Assert.Equal(["Name", "Value", "When"], reader.Headers);
	}

	[Fact]
	public void DataReaderWithoutHeader()
	{
		using var reader = Reader("a,b\nc,d\n", new CsvReaderOptions { HasHeader = false });
		using var data = new CsvDataReader(reader);

		Assert.Equal(["Column1", "Column2"], Enumerable.Range(0, data.FieldCount).Select(data.GetName));
		var rows = new List<string>();
		while (data.Read()) rows.Add(data.GetString(0) + data.GetString(1));
		Assert.Equal(["ab", "cd"], rows);
	}
}
