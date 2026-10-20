using System.Globalization;
using System.Text;

[assembly: CsvHelper.CsvConverter(typeof(CsvHelper.Tests.CentsConverter))]

namespace CsvHelper.Tests;

/// <summary>An amount in cents, which has no Parse method: the assembly's converter reads it.</summary>
/// <param name="Value">The number of cents.</param>
public readonly record struct Cents(long Value);

/// <summary>The converter that <c>[assembly: CsvConverter]</c> gives every <see cref="Cents"/> member: a decimal such as 12.34.</summary>
public sealed class CentsConverter : ICsvConverter<Cents>
{
	/// <inheritdoc/>
	public Cents Read(CsvReader reader, int index) => new((long)(reader.GetDecimal(index) * 100));

	/// <inheritdoc/>
	public void Write(CsvWriter writer, Cents value) => writer.WriteField(value.Value / 100m);
}

/// <summary>Reads whole cents, for a member whose own converter takes precedence over the assembly's.</summary>
public sealed class WholeCentsConverter : ICsvConverter<Cents>
{
	/// <inheritdoc/>
	public Cents Read(CsvReader reader, int index) => new(reader.GetInt64(index));

	/// <inheritdoc/>
	public void Write(CsvWriter writer, Cents value) => writer.WriteField(value.Value);
}

/// <summary>Members read by the assembly's converter, with several formats, and as URIs.</summary>
[CsvRecord]
public partial class Invoice
{
	public string Id { get; set; } = "";

	public Cents Total { get; set; }

	public Cents? Discount { get; set; }

	[CsvConverter(typeof(WholeCentsConverter))]
	public Cents Tax { get; set; }

	[CsvFormat("yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMdd")]
	public DateTime Date { get; set; }

#if NET
	[CsvFormat("dd.MM.yyyy", "yyyy-MM-dd")]
	public DateOnly Due { get; set; }
#endif

	public Uri? Link { get; set; }
}

/// <summary>A collection read from every column with its name, and one that may be missing.</summary>
[CsvRecord]
public partial class Series
{
	public string Name { get; set; } = "";

	[CsvColumn("Value")]
	public int[] Values { get; set; } = [];

	[CsvColumn("Note", Optional = true)]
	public List<string> Notes { get; set; } = [];
}

/// <summary>A collection read from a fixed range of columns, which can be written.</summary>
[CsvRecord]
public partial class Quarters
{
	[CsvColumn(Index = 0)]
	public int Year { get; set; }

	[CsvColumn(Index = 1, EndIndex = 4)]
	public decimal?[] Amounts { get; set; } = [];
}

/// <summary>A collection read from a column to the end of each record.</summary>
[CsvRecord]
public partial class Labeled
{
	[CsvColumn(Index = 0)]
	public string Id { get; set; } = "";

	[CsvColumn(Index = 1)]
	public IReadOnlyList<string> Tags { get; set; } = [];
}

/// <summary>A dictionary keyed by the header names of its columns.</summary>
[CsvRecord]
public partial class Survey
{
	[CsvColumn(Index = 0)]
	public string Respondent { get; set; } = "";

	[CsvColumn(Index = 1)]
	public Dictionary<string, int?> Answers { get; set; } = [];
}

/// <summary>A setter that rejects values: the record fails, but no member's field does.</summary>
[CsvRecord]
public partial class Strict
{
	private int level;

	public int Level
	{
		get => level;
		set => level = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The level cannot be negative.");
	}
}

/// <summary>
/// Type conversion of records: the context of conversion errors, the assembly's converters, several formats, URIs and
/// collections.
/// </summary>
public class RecordConversionTests
{
	[Fact]
	public void FailuresNameTheRecordFieldAndMember()
	{
		using var reader = CsvReader.FromText("Name,Value\nA,1\nB,x\n", new CsvReaderOptions { TrackLineNumbers = true });
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Reading>().ToList());

		Assert.IsType<FormatException>(e.InnerException);
		Assert.Equal($"Cannot read Reading.Value from field 1 'Value' of record 3 (line 3): {e.InnerException!.Message}", e.Message);
		Assert.Equal("Reading.Value", e.Member);
		Assert.Equal(1, e.FieldIndex);
		Assert.Equal(3, e.RecordNumber);
		Assert.Equal(3, e.LineNumber);
		Assert.Equal(15, e.ByteOffset);
	}

	[Fact]
	public void OverflowsAreTheInnerException()
	{
		using var reader = CsvReader.FromText("Name,Value\nA,99999999999\n");
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Reading>().ToList());

		Assert.IsType<OverflowException>(e.InnerException);
		Assert.Equal("Reading.Value", e.Member);
	}

	[Fact]
	public void FailuresWithoutAHeaderNameTheFieldByIndex()
	{
		using var reader = CsvReader.FromText("2024,1,2,x,4\n", new CsvReaderOptions { HasHeader = false });
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Quarters>().ToList());

		Assert.StartsWith("Cannot read Quarters.Amounts from field 3 of record 1: ", e.Message);
		Assert.Equal(3, e.FieldIndex);
	}

	[Fact]
	public void NestedRecordsNameTheirOwnMember()
	{
		const string csv = "Id,From City,From Geo Lat,From Geo Lon,To City,To Geo Lat,To Geo Lon\nS1,Oslo,59.9,10.7,Rome,north,12.5\n";
		using var reader = CsvReader.FromText(csv);
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Shipment>().ToList());

		Assert.Equal("GeoPoint.Lat", e.Member);
		Assert.Equal(5, e.FieldIndex);
		Assert.StartsWith("Cannot read GeoPoint.Lat from field 5 'To Geo Lat' of record 2: ", e.Message);
	}

	[Fact]
	public void HandWrittenMappersAndSettersNameTheRecordOnly()
	{
		// A nested record with a hand-written mapper is named as the member that holds it.
		using (var reader = CsvReader.FromText("Name,Home Street,Home City,Lat,Lon\nAnn,Main St,Oslo,x,1\n"))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Customer>().ToList());
			Assert.Equal("Customer.Location", e.Member);
			Assert.Equal(-1, e.FieldIndex);
		}

		// A setter that throws is not a field that fails.
		using (var reader = CsvReader.FromText("Level\n1\n-1\n"))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Strict>().ToList());
			Assert.IsType<ArgumentOutOfRangeException>(e.InnerException);
			Assert.Null(e.Member);
			Assert.Equal(-1, e.FieldIndex);
			Assert.Equal($"Cannot read record 3 as Strict: {e.InnerException!.Message}", e.Message);
		}
	}

	[Fact]
	public void EveryRecordMethodAddsTheContext()
	{
		const string csv = "Name,Value\nA,1\nB,x\n";
		static void Check(CsvFormatException e)
		{
			Assert.Equal("Reading.Value", e.Member);
			Assert.Equal(3, e.RecordNumber);
		}

		using (var reader = CsvReader.FromText(csv))
		{
			reader.Read();
			reader.Read();
			Check(Assert.Throws<CsvFormatException>(() => reader.GetRecord<Reading>()));
			Check(Assert.Throws<CsvFormatException>(() => reader.GetRecord(new Reading())));
		}

		using (var reader = CsvReader.FromText(csv))
		{
			Check(Assert.Throws<CsvFormatException>(() => reader.EnumerateRecords(new Reading()).Count()));
		}

		using (var reader = CsvReader.FromText(csv))
		{
			Check(Assert.Throws<CsvFormatException>(() => reader.ParallelEnumerate<Reading>(2).ToList()));
		}

		using (var reader = CsvReader.FromText(csv))
		{
			Check(Assert.Throws<CsvFormatException>(() => Drain(reader.GetRecordsAsync<Reading>()).GetAwaiter().GetResult()));
		}

		using (var reader = CsvReader.FromText(csv))
		{
			Check(Assert.Throws<CsvFormatException>(() => Drain(reader.EnumerateRecordsAsync(new Reading())).GetAwaiter().GetResult()));
		}

		static async Task Drain<T>(IAsyncEnumerable<T> records)
		{
			await foreach (var _ in records)
			{
			}
		}
	}

	[Fact]
	public void FailuresInParallelNameTheirRecord()
	{
		var csv = new StringBuilder("Name,Value\n");
		for (var i = 0; i < 20000; i++)
		{
			csv.Append("N,").Append(i == 15000 ? "bad" : i.ToString(CultureInfo.InvariantCulture)).Append('\n');
		}

		using var reader = CsvReader.FromText(csv.ToString(), new CsvReaderOptions { TrackLineNumbers = true });
		var e = Assert.Throws<CsvFormatException>(() => reader.ParallelEnumerate<Reading>(4).ToList());

		Assert.Equal(15002, e.RecordNumber);
		Assert.Equal(15002, e.LineNumber);
		Assert.Equal("Reading.Value", e.Member);
		Assert.Equal(1, e.FieldIndex);
	}

	[Fact]
	public void AssemblyConvertersFormatsAndUris()
	{
		const string csv = "Id,Total,Discount,Tax,Date,Due,Link\n" +
						   "A,12.34,,250,2024-03-01,01.04.2024,https://example.com/a?x=1\n" +
						   "B,0.5,1.25,7,15/03/2024,2024-04-15,docs/readme.md\n" +
						   "C,1,0.1,0,20240320,02.05.2024,\n";
		using var reader = CsvReader.FromText(csv);
		var invoices = reader.GetRecords<Invoice>().ToList();

		Assert.Equal([1234L, 50L, 100L], invoices.Select(i => i.Total.Value));
		Assert.Equal([null, 125L, 10L], invoices.Select(i => i.Discount?.Value));
		// The member's own converter takes precedence.
		Assert.Equal([250L, 7L, 0L], invoices.Select(i => i.Tax.Value));
		Assert.Equal([new DateTime(2024, 3, 1), new DateTime(2024, 3, 15), new DateTime(2024, 3, 20)], invoices.Select(i => i.Date));
#if NET
		Assert.Equal([new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 15), new DateOnly(2024, 5, 2)], invoices.Select(i => i.Due));
#endif
		Assert.Equal(new Uri("https://example.com/a?x=1"), invoices[0].Link);
		Assert.False(invoices[1].Link!.IsAbsoluteUri);
		Assert.Equal("docs/readme.md", invoices[1].Link!.OriginalString);
		Assert.Null(invoices[2].Link);

		// Written back with the converters, the first format and the URIs as they were read.
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords(invoices);
		}

#if NET
		const string expected = "Id,Total,Discount,Tax,Date,Due,Link\r\n" +
								"A,12.34,,250,2024-03-01,01.04.2024,https://example.com/a?x=1\r\n" +
								"B,0.5,1.25,7,2024-03-15,15.04.2024,docs/readme.md\r\n" +
								"C,1,0.1,0,2024-03-20,02.05.2024,\r\n";
#else
		const string expected = "Id,Total,Discount,Tax,Date,Link\r\n" +
								"A,12.34,,250,2024-03-01,https://example.com/a?x=1\r\n" +
								"B,0.5,1.25,7,2024-03-15,docs/readme.md\r\n" +
								"C,1,0.1,0,2024-03-20,\r\n";
#endif
		Assert.Equal(expected, output.ToString());
	}

	[Fact]
	public void DatesInNoneOfTheFormatsFail()
	{
		using var reader = CsvReader.FromText("Id,Total,Discount,Tax,Date,Due,Link\nA,1,,1,03-01-2024,01.04.2024,\n");
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Invoice>().ToList());

		Assert.Equal("Invoice.Date", e.Member);
	}

	[Fact]
	public void ReaderGettersTakeSeveralFormats()
	{
		using var reader = CsvReader.FromText("2024-03-01,01/04/2024 10:00 +02:00\n", new CsvReaderOptions { HasHeader = false });
		reader.Read();

		Assert.Equal(new DateTime(2024, 3, 1), reader.GetDateTime(0, ["dd/MM/yyyy", "yyyy-MM-dd"]));
		Assert.Equal(new DateTime(2024, 3, 1), reader.GetDateTime(0, ["yyyy-MM-dd"], CultureInfo.InvariantCulture));
		Assert.Equal(new DateTimeOffset(2024, 4, 1, 10, 0, 0, TimeSpan.FromHours(2)), reader.GetDateTimeOffset(1, ["yyyy-MM-dd", "dd/MM/yyyy HH:mm zzz"]));
		Assert.Throws<FormatException>(() => reader.GetDateTime(0, ["dd/MM/yyyy", "yyyyMMdd"]));
		Assert.Throws<ArgumentNullException>(() => reader.GetDateTime(0, (string[])null!));
	}

	[Fact]
	public void CollectionsReadEveryColumnWithTheirName()
	{
		using var reader = CsvReader.FromText("Name,Value,Value,Value\nA,1,2,3\nB,4,5,6\n");
		var series = reader.GetRecords<Series>().ToList();

		Assert.Equal([1, 2, 3], series[0].Values);
		Assert.Equal([4, 5, 6], series[1].Values);
		// An optional collection without columns is empty.
		Assert.Empty(series[0].Notes);

		using var missing = CsvReader.FromText("Name,Note\nA,x\n");
		Assert.Contains("Series.Values ('Value')", Assert.Throws<KeyNotFoundException>(() => missing.GetRecords<Series>()).Message);

		using var failing = CsvReader.FromText("Name,Value,Value,Value\nA,1,x,3\n");
		var e = Assert.Throws<CsvFormatException>(() => failing.GetRecords<Series>().ToList());
		Assert.Equal("Series.Values", e.Member);
		Assert.Equal(2, e.FieldIndex);
	}

	[Fact]
	public void CollectionsReadRangesOfColumns()
	{
		using var reader = CsvReader.FromText("Year,Q1,Q2,Q3,Q4\n2024,1.5,2,,4\n2025,1\n");
		var quarters = reader.GetRecords<Quarters>().ToList();

		Assert.Equal([1.5m, 2m, null, 4m], quarters[0].Amounts);
		// The fields a short record lacks read as empty.
		Assert.Equal([1m, null, null, null], quarters[1].Amounts);

		using var labeled = CsvReader.FromText("Id\nA,x,y\nB\nC,z\n");
		Assert.Equal(["x,y", "", "z"], labeled.GetRecords<Labeled>().Select(l => string.Join(",", l.Tags)));
	}

	[Fact]
	public void CollectionsOfAFixedRangeAreWritten()
	{
		Quarters[] quarters = [new() { Year = 2024, Amounts = [1.5m, null, 3m, 4m] }, new() { Year = 2025, Amounts = [1m] }];
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords(quarters);
		}

		Assert.Equal("Year,Amounts,Amounts,Amounts,Amounts\r\n2024,1.5,,3,4\r\n2025,1,,,\r\n", output.ToString());
		using var reader = CsvReader.FromText(output.ToString());
		Assert.Equal([1m, null, null, null], reader.GetRecords<Quarters>().Last().Amounts);
	}

	[Fact]
	public void DictionariesAreKeyedByHeaderNames()
	{
		using var reader = CsvReader.FromText("Respondent,Q1,Q2,Q3\nann,5,,3\n");
		var survey = Assert.Single(reader.GetRecords<Survey>());

		Assert.Equal(["Q1", "Q2", "Q3"], survey.Answers.Keys);
		Assert.Equal([5, null, 3], survey.Answers.Values);

		using var noHeader = CsvReader.FromText("ann,5\n", new CsvReaderOptions { HasHeader = false });
		Assert.Contains("no header", Assert.Throws<InvalidOperationException>(() => noHeader.GetRecords<Survey>()).Message);
		using var writer = CsvWriter.Create(new StringWriter());
		Assert.Contains("dictionary", Assert.Throws<InvalidOperationException>(() => writer.WriteRecords(new[] { survey })).Message);
		Assert.Contains("fixed number of columns", Assert.Throws<InvalidOperationException>(() => writer.WriteRecords(new[] { new Series() })).Message);
	}
}
