namespace CsvHelper.Tests;

/// <summary>
/// <see cref="CsvRecordMapper{T}.FindFailingMembers"/>: every member of a record that fails, with its field and what it
/// threw, from generated mappers (members, constructor parameters, nested records, collections, checks, optional
/// columns and projections) and from the base class's, which hand-written mappers get.
/// </summary>
public class FindFailingMembersTests
{
	[Fact]
	public void FindsEveryFailingMemberWithItsFieldAndException()
	{
		var errors = Failing<Bill>("Id,Quantity,Price,Due,Lines\nA1,many,cheap,soon,0\n");

		Assert.Equal(
			[("Bill.Quantity", 1, typeof(FormatException)), ("Bill.Price", 2, typeof(FormatException)), ("Bill.Due", 3, typeof(FormatException)), ("Bill.Lines", 4, typeof(CsvValidationException))],
			errors.Select(e => (e.Member, e.FieldIndex, e.Exception.GetType())));
		Assert.Equal("Lines must be at least 1.", errors[3].Exception.Message);
	}

	[Fact]
	public void FindsTheFirstAsFindFailingMemberDoes()
	{
		using var reader = CsvReader.FromText("Id,Quantity,Price,Due,Lines\nA1,3,cheap,soon,1\n");
		var mapper = CsvRecordMapper.Create<Bill>(reader);
		Assert.True(reader.Read());
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecord(mapper));
		var errors = new List<CsvMemberError>();
		mapper.FindFailingMembers(reader, errors);

		Assert.Equal(["Bill.Price", "Bill.Due"], errors.Select(m => m.Member));
		Assert.Equal(e.Member, errors[0].Member);
		Assert.Equal(e.FieldIndex, errors[0].FieldIndex);
		Assert.Equal(e.InnerException!.GetType(), errors[0].Exception.GetType());
	}

	[Fact]
	public void FindsNoneInARecordThatReads()
	{
		Assert.Empty(Failing<Bill>("Id,Quantity,Price,Due,Lines\nA1,3,1.50,2024-01-15,2\n"));
	}

	[Fact]
	public void TellsOverflowFromFormat()
	{
		var errors = Failing<Bill>("Id,Quantity,Price,Due,Lines\nA1,99999999999,1.50,2024-01-15,-1\n");

		Assert.Equal([("Bill.Quantity", typeof(OverflowException)), ("Bill.Lines", typeof(CsvValidationException))], errors.Select(e => (e.Member, e.Exception.GetType())));
	}

	[Fact]
	public void ReadsAnOptionalColumnOnlyWhenTheHeaderHasIt()
	{
		Assert.Equal(["Bill.Quantity"], Failing<Bill>("Id,Quantity,Price,Due,Lines\nA1,x,1,2024-01-15,1\n").Select(e => e.Member));
		Assert.Equal(["Bill.Quantity", "Bill.Discount"], Failing<Bill>("Id,Quantity,Price,Due,Lines,Discount\nA1,x,1,2024-01-15,1,y\n").Select(e => e.Member));
	}

	[Fact]
	public void FindsConstructorParameters()
	{
		var errors = Failing<Measure>("Name,Value,Count\nm,abc,xyz\n");

		Assert.Equal([("Measure.Value", 1), ("Measure.Count", 2)], errors.Select(e => (e.Member, e.FieldIndex)));
	}

	[Fact]
	public void FindsANestedRecordsOwnMembers()
	{
		var errors = Failing<Payment>("Id,Paid Amount,Paid Currency,Count\nP1,lots,EUR,many\n");

		Assert.Equal([("PaidAmount.Amount", 1), ("Payment.Count", 3)], errors.Select(e => (e.Member, e.FieldIndex)));
	}

	[Fact]
	public void FindsACollectionsFirstFailingField()
	{
		var errors = Failing<Readings>("Station,V1,V2,V3,Count\nS1,1,x,y,z\n");

		Assert.Equal([("Readings.Values", 2), ("Readings.Count", 4)], errors.Select(e => (e.Member, e.FieldIndex)));
		Assert.IsType<FormatException>(errors[0].Exception);
	}

	[Fact]
	public void AProjectionFindsOnlyItsMembers()
	{
		using var reader = CsvReader.FromText("Id,Quantity,Price,Due,Lines\nA1,many,cheap,soon,0\n");
		var mapper = CsvRecordMapper.CreateProjection<Bill>(reader, ["Price", "Due"]);
		Assert.True(reader.Read());
		var errors = new List<CsvMemberError>();
		mapper.FindFailingMembers(reader, errors);

		Assert.Equal([("Bill.Price", 2), ("Bill.Due", 3)], errors.Select(e => (e.Member, e.FieldIndex)));
	}

	[Fact]
	public void AProjectionSkipsNestedRecordsAndCollectionsItDoesntSelect()
	{
		// A member's column is -1 when it isn't selected; a nested record's and a collection's steps need the selection.
		using var payments = CsvReader.FromText("Id,Paid Amount,Paid Currency,Count\nP1,lots,EUR,many\n");
		var payment = CsvRecordMapper.CreateProjection<Payment>(payments, ["Count"]);
		Assert.True(payments.Read());
		var errors = new List<CsvMemberError>();
		payment.FindFailingMembers(payments, errors);
		Assert.Equal(["Payment.Count"], errors.Select(e => e.Member));

		using var readings = CsvReader.FromText("Station,V1,V2,V3,Count\nS1,1,x,y,z\n");
		var reading = CsvRecordMapper.CreateProjection<Readings>(readings, ["Count"]);
		Assert.True(readings.Read());
		errors.Clear();
		reading.FindFailingMembers(readings, errors);
		Assert.Equal(["Readings.Count"], errors.Select(e => e.Member));
	}

	[Fact]
	public void TheBaseClassAddsWhatFindFailingMemberFinds()
	{
		using var reader = CsvReader.FromText("Level\nhigh\n");
		Assert.True(reader.Read());
		var errors = new List<CsvMemberError>();

		new LevelMapper("Gauge.Level").FindFailingMembers(reader, errors);
		var error = Assert.Single(errors);
		Assert.Equal(("Gauge.Level", 0), (error.Member, error.FieldIndex));
		Assert.IsType<FormatException>(error.Exception);

		// A mapper that doesn't say which member failed adds nothing.
		errors.Clear();
		new LevelMapper(null).FindFailingMembers(reader, errors);
		Assert.Empty(errors);
	}

	[Fact]
	public void TheBaseClassAddsNothingForARecordThatReads()
	{
		using var reader = CsvReader.FromText("Level\n7\n");
		Assert.True(reader.Read());
		var errors = new List<CsvMemberError>();
		new LevelMapper("Gauge.Level").FindFailingMembers(reader, errors);

		Assert.Empty(errors);
	}

	[Fact]
	public void TheBaseClassChecksItsArguments()
	{
		using var reader = CsvReader.FromText("Level\n7\n");
		var mapper = new LevelMapper("Gauge.Level");

		Assert.Throws<ArgumentNullException>(() => mapper.FindFailingMembers(null!, []));
		Assert.Throws<ArgumentNullException>(() => mapper.FindFailingMembers(reader, null!));
	}

	[Fact]
	public void FindsEveryFailingMemberOfEachRecord()
	{
		// Each record that fails, and each of its members that does.
		using var reader = CsvReader.FromText("Id,Quantity,Price,Due,Lines\nA1,3,1.50,2024-01-15,2\nA2,x,1.50,2024-01-15,y\nA3,4,2,soon,1\n");
		var mapper = CsvRecordMapper.Create<Bill>(reader);
		var bills = new List<Bill>();
		var errors = new List<CsvMemberError>();
		while (reader.Read())
		{
			try
			{
				bills.Add(reader.GetRecord(mapper));
			}
			catch (CsvFormatException)
			{
				mapper.FindFailingMembers(reader, errors);
			}
		}

		Assert.Equal(["A1"], bills.Select(i => i.Id));
		Assert.Equal(["Bill.Quantity", "Bill.Lines", "Bill.Due"], errors.Select(e => e.Member));
	}

	[Fact]
	public void TheGuidesExampleFindsBothMembers()
	{
		// docs/pages/records.md, "Every member that fails".
		using var reader = CsvReader.FromText("Id,Unit Price,Quantity\nA-1,9.99,3\nA-2,abc,x\n");
		var mapper = CsvRecordMapper.Create<Order>(reader);
		var orders = new List<Order>();
		var errors = new List<CsvMemberError>();
		while (reader.Read())
		{
			try
			{
				orders.Add(reader.GetRecord(mapper));
			}
			catch (CsvFormatException)
			{
				mapper.FindFailingMembers(reader, errors);
			}
		}

		Assert.Equal(["A-1"], orders.Select(o => o.Id));
		Assert.Equal([("Order.Price", 1), ("Order.Quantity", 2)], errors.Select(e => (e.Member, e.FieldIndex)));
		Assert.All(errors, e => Assert.IsType<FormatException>(e.Exception));
	}

	/// <summary>The failing members of the first record of a CSV text.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="csv">The CSV text, with a header.</param>
	/// <returns>The failing members.</returns>
	private static List<CsvMemberError> Failing<T>(string csv)
	{
		using var reader = CsvReader.FromText(csv);
		var mapper = CsvRecordMapper.Create<T>(reader);
		Assert.True(reader.Read());
		var errors = new List<CsvMemberError>();
		mapper.FindFailingMembers(reader, errors);

		return errors;
	}

	/// <summary>A hand-written mapper of one number, which names its member when it fails, or doesn't.</summary>
	/// <param name="member">The member <see cref="FindFailingMember"/> names, or <see langword="null"/> for none.</param>
	private sealed class LevelMapper(string? member) : CsvRecordMapper<int>
	{
		public override int Read(CsvReader reader) => reader.Parse<int>(0);

		public override string? FindFailingMember(CsvReader reader, out int fieldIndex)
		{
			fieldIndex = member is null ? -1 : 0;

			return member;
		}
	}
}

/// <summary>An invoice of members of several types, an optional column and a check.</summary>
[CsvRecord]
public partial class Bill
{
	public string Id { get; set; } = "";

	public int Quantity { get; set; }

	public decimal Price { get; set; }

	public DateTime Due { get; set; }

	[CsvValidate(nameof(IsPositive), Message = "Lines must be at least 1.")]
	public int Lines { get; set; }

	[CsvColumn(Optional = true)]
	public int? Discount { get; set; }

	private static bool IsPositive(int value) => value > 0;
}

/// <summary>A record of constructor parameters.</summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value.</param>
/// <param name="Count">The count.</param>
[CsvRecord]
public partial record Measure(string Name, double Value, int Count);

/// <summary>An amount in a currency, nested in <see cref="Payment"/>.</summary>
/// <param name="Amount">The amount.</param>
/// <param name="Currency">The currency.</param>
[CsvRecord]
public partial record struct PaidAmount(decimal Amount, string Currency);

/// <summary>A payment, with a nested record.</summary>
[CsvRecord]
public partial class Payment
{
	public string Id { get; set; } = "";

	[CsvColumn(Prefix = "Paid ")]
	public PaidAmount Paid { get; set; }

	public int Count { get; set; }
}

/// <summary>A station's readings: a collection of columns by index, and a member after it.</summary>
[CsvRecord]
public partial class Readings
{
	public string Station { get; set; } = "";

	[CsvColumn(Index = 1, EndIndex = 3)]
	public int[] Values { get; set; } = [];

	public int Count { get; set; }
}
