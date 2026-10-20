using System.Globalization;

namespace CsvHelper.Tests;

/// <summary>A type as another library would declare it: no CsvHelper attributes, a constructor parameter, an init member.</summary>
public sealed class Supplier
{
	public Supplier(string code) => Code = code;

	public string Code { get; }

	public string Name { get; set; } = "";

	public decimal Rating { get; init; }

	public DateTime Since { get; set; }

	public string? Notes { get; set; }
}

/// <summary>Maps <see cref="Supplier"/> with the attributes of its members' stand-ins.</summary>
[CsvRecordMap(typeof(Supplier))]
[CsvOptions(Separator = ';')]
internal partial class SupplierMap
{
	[CsvColumn("Supplier Code")]
	public string Code { get; set; } = "";

	[CsvColumn("Full Name", "Name")]
	public string Name { get; set; } = "";

	[CsvValidate(nameof(IsRating))]
	public decimal Rating { get; set; }

	[CsvFormat("yyyyMMdd")]
	public DateTime Since { get; set; }

	[CsvIgnore]
	public string? Notes { get; set; }

	private static bool IsRating(decimal rating) => rating is >= 0 and <= 5;
}

/// <summary>Members checked by methods of the type.</summary>
[CsvRecord]
public partial class Signup
{
	[CsvValidate(nameof(IsName), Message = "A name is required.")]
	public string Name { get; set; } = "";

	[CsvValidate(nameof(IsAge))]
	public int Age { get; set; }

	[CsvColumn(Optional = true)]
	[CsvValidate(nameof(IsEmail))]
	public string? Email { get; set; }

	private static bool IsName(string name) => name.Trim().Length > 0;

	private static bool IsAge(int age) => age is >= 0 and <= 150;

	private static bool IsEmail(string? email) => email is not null && email.IndexOf('@') > 0;
}

/// <summary>Members with a fixed value, which the header does not need.</summary>
[CsvRecord]
public partial class Versioned
{
	public string Name { get; set; } = "";

	[CsvConstant("v2")]
	public string Schema { get; set; } = "";

	[CsvConstant(null)]
	public int? Batch { get; set; } = 7;
}

/// <summary>A constructor parameter with a fixed value.</summary>
[CsvRecord]
public partial record Leveled(string Name, [CsvConstant(5)] int Level);

/// <summary>Members that every record type of an application inherits.</summary>
public class Audited
{
	public DateTime Created { get; set; }

	public string CreatedBy { get; set; } = "";
}

/// <summary>Reads and writes only its own members.</summary>
[CsvRecord]
[CsvIgnoreBase]
public partial class Memo : Audited
{
	public string Text { get; set; } = "";
}

/// <summary>A file dialect kept on the record type.</summary>
[CsvRecord]
[CsvOptions(Separator = ';', HasHeader = false, Culture = "de-DE", NullValues = new[] { "NULL", "-" }, NewLine = "\n", CommentPrefix = '#')]
public partial class EuroPrice
{
	[CsvColumn(Index = 0)]
	public string Item { get; set; } = "";

	[CsvColumn(Index = 1)]
	public decimal Price { get; set; }

	[CsvColumn(Index = 2)]
	public decimal? Discount { get; set; }
}

/// <summary>A fixed-width layout whose options come from the type.</summary>
[CsvRecord]
[CsvOptions(Mode = CsvMode.FixedWidth, HasHeader = false, TrueValues = new[] { "J" }, FalseValues = new[] { "N" })]
public partial class FixedRow
{
	[CsvColumn(Index = 0, Width = 4)]
	public string Code { get; set; } = "";

	[CsvColumn(Index = 1, Width = 3)]
	public int Count { get; set; }

	[CsvColumn(Index = 2, Width = 1)]
	public bool Active { get; set; }
}

/// <summary>Class mapping without attributes on the type, field validation, constants, base members and options on the type.</summary>
public class RecordMapTests
{
	[Fact]
	public void MapsTypesThatCannotBeAnnotated()
	{
		var options = CsvReaderOptions.For<Supplier>();
		using var reader = CsvReader.FromText("Supplier Code;Name;Rating;Since;Notes\nS1;Acme;4.5;20200115;secret\n", options);
		var supplier = Assert.Single(reader.GetRecords<Supplier>());

		Assert.Equal("S1", supplier.Code);
		Assert.Equal("Acme", supplier.Name);
		Assert.Equal(4.5m, supplier.Rating);
		Assert.Equal(new DateTime(2020, 1, 15), supplier.Since);
		// [CsvIgnore] on the stand-in.
		Assert.Null(supplier.Notes);

		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output, CsvWriterOptions.For<Supplier>()))
		{
			writer.WriteRecords([supplier]);
		}

		Assert.Equal("Supplier Code;Full Name;Rating;Since\r\nS1;Acme;4.5;20200115\r\n", output.ToString());
	}

	[Fact]
	public void MapsCheckTheirMembers()
	{
		using var reader = CsvReader.FromText("Supplier Code;Full Name;Rating;Since\nS1;Acme;9;20200115\n", CsvReaderOptions.For<Supplier>());
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Supplier>().ToList());

		Assert.Equal("Supplier.Rating", e.Member);
		Assert.Equal("'9' is not valid for Supplier.Rating.", Assert.IsType<CsvValidationException>(e.InnerException).Message);
	}

	[Fact]
	public void ValidatesMembers()
	{
		using (var reader = CsvReader.FromText("Name,Age,Email\nAnn,30,ann@example.com\n"))
		{
			Assert.Equal(30, Assert.Single(reader.GetRecords<Signup>()).Age);
		}

		// Without its optional column, Email is not checked.
		using (var reader = CsvReader.FromText("Name,Age\nAnn,30\n"))
		{
			Assert.Null(Assert.Single(reader.GetRecords<Signup>()).Email);
		}

		using (var reader = CsvReader.FromText("Name,Age\nAnn,200\n"))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Signup>().ToList());
			Assert.Equal(("Signup.Age", 1), (e.Member!, e.FieldIndex));
			Assert.Equal("'200' is not valid for Signup.Age.", Assert.IsType<CsvValidationException>(e.InnerException).Message);
			Assert.Equal("Cannot read Signup.Age from field 1 'Age' of record 2: '200' is not valid for Signup.Age.", e.Message);
		}

		using (var reader = CsvReader.FromText("Name,Age\nAnn,200\n", new CsvReaderOptions { IncludeValuesInExceptions = false }))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Signup>().ToList());
			Assert.DoesNotContain("200", e.Message);
			Assert.Contains("IncludeValuesInExceptions", e.Message);
		}

		using (var reader = CsvReader.FromText("Name,Age,Email\n ,30,x\n"))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Signup>().ToList());
			Assert.Equal("A name is required.", e.InnerException!.Message);
		}

		// A record that fails its check can be skipped.
		var options = new CsvReaderOptions { OnRecordError = _ => true };
		using (var reader = CsvReader.FromText("Name,Age,Email\nAnn,30,ann@x\nBen,30,nope\nCid,40,cid@x\n", options))
		{
			Assert.Equal(["Ann", "Cid"], reader.GetRecords<Signup>().Select(s => s.Name));
		}
	}

	[Fact]
	public void ConstantsAreReadAndWrittenWithoutAColumn()
	{
		using var reader = CsvReader.FromText("Name\nA\n");
		var record = Assert.Single(reader.GetRecords<Versioned>());

		Assert.Equal("v2", record.Schema);
		// The constant replaces the member's own initial value.
		Assert.Null(record.Batch);

		// The constant is written whatever the member holds.
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords([new Versioned { Name = "B", Schema = "v1", Batch = 3 }]);
		}

		Assert.Equal("Name,Schema,Batch\r\nB,v2,\r\n", output.ToString());

		using var leveled = CsvReader.FromText("Name\nA\n");
		Assert.Equal(new Leveled("A", 5), Assert.Single(leveled.GetRecords<Leveled>()));
		using var written = new StringWriter();
		using (var writer = CsvWriter.Create(written))
		{
			writer.WriteRecords([new Leveled("A", 1)]);
		}

		Assert.Equal("Name,Level\r\nA,5\r\n", written.ToString());
	}

	[Fact]
	public void IgnoreBaseLeavesOutInheritedMembers()
	{
		using var reader = CsvReader.FromText("Text\nhello\n");

		Assert.Equal("hello", Assert.Single(reader.GetRecords<Memo>()).Text);
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output))
		{
			writer.WriteRecords([new Memo { Text = "hi", CreatedBy = "me" }]);
		}

		Assert.Equal("Text\r\nhi\r\n", output.ToString());
	}

	[Fact]
	public void OptionsComeFromTheRecordType()
	{
		var options = CsvReaderOptions.For<EuroPrice>();

		Assert.Same(options, CsvReaderOptions.For<EuroPrice>());
		Assert.Equal(';', options.Separator);
		Assert.False(options.HasHeader);
		Assert.Equal('#', options.CommentPrefix);
		Assert.Equal("de-DE", ((CultureInfo)options.FormatProvider).Name);
		Assert.Equal(["NULL", "-"], options.NullValues!);

		using var reader = CsvReader.FromText("# prices\nTea;1,50;NULL\nCake;2,25;0,5\n", options);
		var prices = reader.GetRecords<EuroPrice>().ToList();
		Assert.Equal([1.50m, 2.25m], prices.Select(p => p.Price));
		Assert.Equal([null, 0.5m], prices.Select(p => p.Discount));

		var writerOptions = CsvWriterOptions.For<EuroPrice>();
		Assert.Equal("NULL", writerOptions.NullValue);
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output, writerOptions))
		{
			// No header: the options say the files have none.
			writer.WriteRecords(prices);
		}

		Assert.Equal("Tea;1,50;NULL\nCake;2,25;0,5\n", output.ToString());

		// Types without [CsvOptions] get the defaults.
		Assert.Same(CsvReaderOptions.Default, CsvReaderOptions.For<Reading>());
		Assert.Same(CsvWriterOptions.Default, CsvWriterOptions.For<Reading>());
	}

	[Fact]
	public void FixedWidthOptionsTakeTheTypesLayout()
	{
		var options = CsvReaderOptions.For<FixedRow>();

		Assert.Equal(CsvMode.FixedWidth, options.Mode);
		Assert.Equal([4, 3, 1], options.FieldWidths!);
		Assert.Equal(["J"], options.TrueValues!);
		using var reader = CsvReader.FromText("AB12  5J\nCD   12N\n", options);
		var rows = reader.GetRecords<FixedRow>().ToList();
		Assert.Equal([5, 12], rows.Select(r => r.Count));
		Assert.Equal([true, false], rows.Select(r => r.Active));

		var writerOptions = CsvWriterOptions.For<FixedRow>();
		Assert.Equal("J", writerOptions.TrueValue);
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output, writerOptions))
		{
			writer.WriteRecords(rows);
		}

		Assert.Equal("AB125  J\r\nCD  12 N\r\n", output.ToString());
	}

	[Fact]
	public void WriterOptionsCanLeaveOutTheHeader()
	{
		using var output = new StringWriter();
		using (var writer = CsvWriter.Create(output, new CsvWriterOptions { HasHeader = false }))
		{
			writer.WriteRecords([new Memo { Text = "a" }]);
		}

		Assert.Equal("a\r\n", output.ToString());
	}
}
