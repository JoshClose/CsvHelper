using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

[CsvRecord]
public partial class Gapped
{
	[CsvColumn(Index = 3)]
	public int Last { get; set; }

	[CsvColumn(Index = 0)]
	public int First { get; set; }
}

/// <summary>Read through a constructor parameter that sets no property, so it cannot be written.</summary>
[CsvRecord]
public partial class Unwritable
{
	public Unwritable(string name, int count) => Count = count + name.Length;

	public int Count { get; }
}

/// <summary>Two members read the same column, which the writer cannot write twice.</summary>
[CsvRecord]
public partial class SameIndex
{
	[CsvColumn(Index = 0)]
	public int A { get; set; }

	[CsvColumn(Index = 0)]
	public int B { get; set; }
}

[CsvRecord]
public partial class IndexedWithNested
{
	[CsvColumn(Index = 0)]
	public int A { get; set; }

	public GeoPoint Point { get; set; }
}

/// <summary>A nested record whose own index is a column of the whole row.</summary>
[CsvRecord]
public partial class NestsIndexed
{
	public string Name { get; set; } = "";

	public ByIndex Inner { get; set; } = new();
}

public sealed class PositiveConverter : ICsvConverter<int>
{
	public int Read(CsvReader reader, int index) => reader.GetInt32(index);

	public void Write(CsvWriter writer, int value)
	{
		if (value < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(value), value, "Not positive.");
		}

		writer.WriteField(value);
	}
}

[CsvRecord]
public partial class Guarded
{
	public string Name { get; set; } = "";

	[CsvConverter(typeof(PositiveConverter))]
	public int Value { get; set; }
}

public sealed class AddressWriter(string columnPrefix) : CsvRecordWriter<Address>(columnPrefix)
{
	public override int FieldCount => 2;

	public override void WriteHeader(CsvWriter writer)
	{
		WriteColumnName(writer, "Street");
		WriteColumnName(writer, "City");
	}

	public override void Write(CsvWriter writer, Address record)
	{
		writer.WriteField(record.Street);
		writer.WriteField(record.City);
	}
}

public sealed class CoordinatesWriter(string columnPrefix) : CsvRecordWriter<Coordinates>(columnPrefix)
{
	public override int FieldCount => 2;

	public override void WriteHeader(CsvWriter writer)
	{
		WriteColumnName(writer, "Lat");
		WriteColumnName(writer, "Lon");
	}

	public override void Write(CsvWriter writer, Coordinates record)
	{
		writer.WriteField(record.Lat);
		writer.WriteField(record.Lon);
	}
}

public sealed class TemperatureWriter() : CsvRecordWriter<Temperature>
{
	public override int FieldCount => 2;

	public override void WriteHeader(CsvWriter writer)
	{
		WriteColumnName(writer, "City");
		WriteColumnName(writer, "F");
	}

	public override void Write(CsvWriter writer, Temperature record)
	{
		writer.WriteField(record.City);
		writer.WriteField(record.Celsius * 9 / 5 + 32);
	}
}

/// <summary>Records of [CsvRecord] types written by the generated record writers: WriteRecords and its relatives.</summary>
public class RecordWriterTests
{
	static RecordWriterTests()
	{
		CsvRecordWriter.Register<Address>(prefix => new AddressWriter(prefix));
		CsvRecordWriter.Register<Coordinates>(prefix => new CoordinatesWriter(prefix));
		CsvRecordWriter.Register<Temperature>(_ => new TemperatureWriter());
	}

	private static string Write(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			write(writer);
		}

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private static List<T> Read<T>(string csv, CsvReaderOptions? options = null)
	{
		using var reader = CsvReader.FromText(csv, options);

		return reader.GetRecords<T>().ToList();
	}

	/// <summary>Writes the records, checks the text, and checks that reading it and writing again gives the same text.</summary>
	private static void AssertRoundTrip<T>(IEnumerable<T> records, string expected, CsvReaderOptions? readOptions = null, CsvWriterOptions? writeOptions = null)
	{
		var csv = Write(w => w.WriteRecords(records), writeOptions);
		Assert.Equal(expected, csv);
		var back = Read<T>(csv, readOptions);

		Assert.Equal(csv, Write(w => w.WriteRecords(back), writeOptions));
	}

	private static readonly Order[] Orders =
	[
		new() { Id = "A1", Price = 1.50m, Quantity = 3, Note = "first", Total = 99 },
		new() { Id = "B2", Price = 2m, Quantity = null, Note = "" },
		new() { Id = "C,3", Price = 0.25m, Quantity = 10, Note = "say \"hi\"" },
	];

	[Fact]
	public void WritesTheHeaderAndTheRecords() =>
		AssertRoundTrip(Orders, "Id,Unit Price,Quantity,Note\r\nA1,1.50,3,first\r\nB2,2,,\r\n\"C,3\",0.25,10,\"say \"\"hi\"\"\"\r\n");

	[Fact]
	public void WritesTheHeaderOfAType()
	{
		Assert.Equal("Id,Unit Price,Quantity,Note\r\n", Write(w => w.WriteHeader<Order>()));
		var e = Assert.Throws<InvalidOperationException>(() => Write(w =>
		{
			w.WriteHeader<Order>();
			w.WriteHeader<Line>();
		}));
		Assert.Contains("has 3 fields but the first record has 4", e.Message);
	}

	[Fact]
	public void WritesTheHeaderWhenNothingWasWritten()
	{
		var csv = Write(w =>
		{
			w.WriteComment("orders");
			w.WriteRecords(Orders.Take(1));
			w.WriteRecords(Orders.Skip(1).Take(1));
		}, new CsvWriterOptions { CommentPrefix = '#' });
		Assert.Equal("#orders\r\nId,Unit Price,Quantity,Note\r\nA1,1.50,3,first\r\nB2,2,,\r\n", csv);

		var withoutHeader = Write(w =>
		{
			foreach (var order in Orders.Take(2))
			{
				w.WriteRecord(order);
			}
		});
		Assert.Equal("A1,1.50,3,first\r\nB2,2,,\r\n", withoutHeader);

		var ownHeader = Write(w =>
		{
			w.WriteHeader("id", "price", "qty", "note");
			w.WriteRecords(Orders.Take(1));
		});
		Assert.Equal("id,price,qty,note\r\nA1,1.50,3,first\r\n", ownHeader);
	}

	[Fact]
	public void WritesConstructorParametersStructsAndBaseMembers()
	{
		AssertRoundTrip([new Line("A", 1.5, 2), new Line("B,b", -0.25, 7)], "Sku,Count,Weight\r\nA,2,1.5\r\n\"B,b\",7,-0.25\r\n");
		AssertRoundTrip([new Point { X = 1, Y = 2, Z = 3 }, new Point { X = 4, Y = 5 }], "X,Y,Z\r\n1,2,3\r\n4,5,\r\n");
		AssertRoundTrip([new Pair("k", Guid.Empty)], "Key,Value\r\nk,00000000-0000-0000-0000-000000000000\r\n");
		AssertRoundTrip([new Person { Name = "Ann", Age = 3 }], "Name,Age\r\nAnn,3\r\n");
		AssertRoundTrip([new Derived { Key = 5, Kind = "k", Label = "l" }], "Key,Kind,Label\r\n5,k,l\r\n");
		AssertRoundTrip([new ByIndex { A = 1, B = "b" }], "A,B\r\n1,b\r\n");
		AssertRoundTrip([new Outer.Inner { A = 1 }], "A\r\n1\r\n");

		using var reader = CsvReader.FromText("secret\nx\n");
		var hidden = reader.GetRecords<Hidden>().ToList();
		Assert.Equal("secret\r\nx\r\n", Write(w => w.WriteRecords(hidden)));
		Assert.Equal("name\r\nx!\r\n", Write(w => w.WriteRecords([new Chosen("x")])));
	}

	[Fact]
	public void WritesValuesOfEveryType()
	{
		var values = new Values
		{
			Flag = true,
			Single = 1.5f,
			Double = null,
			Long = -1,
			Short = 2,
			Byte = 3,
			Char = 'c',
			When = new DateTime(2024, 3, 1, 10, 30, 0, DateTimeKind.Utc),
			At = new DateTimeOffset(2024, 3, 1, 10, 30, 0, TimeSpan.FromHours(2)),
			Span = TimeSpan.FromMinutes(90),
			Guid = Guid.Empty,
			Color = Color.Blue,
			MaybeColor = Color.DarkRed,
			Access = Access.Read | Access.Write,
			Casing = Casing.ITEM,
			Money = new Money(9.5m),
		};

		AssertRoundTrip([values],
			"Flag,Single,Double,Long,Short,Byte,Char,When,At,Span,Guid,Color,MaybeColor,Access,Casing,Money\r\n" +
			"True,1.5,,-1,2,3,c,2024-03-01T10:30:00.0000000Z,2024-03-01T10:30:00.0000000+02:00,01:30:00,00000000-0000-0000-0000-000000000000," +
			"Blue,DarkRed,\"Read, Write\",ITEM,$9.5\r\n");
	}

	[Fact]
	public void WritesWithTheAttributesOfTheMembers()
	{
#if NET
		var dated = new Dated
		{
			Day = new DateTime(2024, 4, 1),
			At = new DateTimeOffset(2024, 4, 1, 13, 45, 0, TimeSpan.FromHours(2)),
			Span = new TimeSpan(1, 30, 0),
			Due = new DateOnly(2024, 12, 24),
		};
		AssertRoundTrip([dated], "Day,At,Maybe,Span,Due\r\n20240401,202404011345+02:00,,01:30,24.12.2024\r\n");
#else
		var dated = new Dated { Day = new DateTime(2024, 4, 1), At = new DateTimeOffset(2024, 4, 1, 13, 45, 0, TimeSpan.FromHours(2)), Span = new TimeSpan(1, 30, 0) };
		AssertRoundTrip([dated], "Day,At,Maybe,Span\r\n20240401,202404011345+02:00,,01:30\r\n");
#endif
		AssertRoundTrip([new Nullables(), new Nullables { Name = "a", Count = 1, Empty = "" }], "Name,Count,Empty\r\nNULL,NULL,\r\na,1,\r\n");
		AssertRoundTrip([new Switches { German = true }, new Switches { German = false, Power = true }], "German,Power\r\nJa,\r\nNein,on\r\n");
		AssertRoundTrip([new Renamed { Price = 1.5m, Quantity = 2 }], "Unit Price,Qty\r\n1.5,2\r\n");
		AssertRoundTrip([new Ticket { Status = Status.OnHold }, new Ticket { Status = Status.Closed, Previous = Status.Active }], "Status,Previous\r\non hold,\r\nClosed,Active\r\n");
		AssertRoundTrip([new Link { Target = new Uri("https://example.com/a,b"), Code = 255 }], "Target,Code,MaybeCode\r\n\"https://example.com/a,b\",ff,\r\n");
		var mixed = new Mixed { Invariant = 1.5m, German = 1234.5m, Date = new DateTime(2024, 3, 3), Money = new Money(9.5m) };
		AssertRoundTrip([mixed], "Invariant,German,Date,Money\r\n1.5,\"1234,5\",3. März 2024,\"$9,5\"\r\n");
	}

	[Fact]
	public void NullMembersWithADefaultReadBackAsTheDefault()
	{
		var csv = Write(w => w.WriteRecords([new Defaults { Count = 0, Note = null, Ratio = 1.5, Status = Status.Closed, Maybe = null }]));
		Assert.Equal("Count,Note,Ratio,Status,Maybe\r\n0,,1.5,Closed,\r\n", csv);

		// An empty field is what [CsvDefault] replaces: null cannot come back.
		Assert.Equal(5, Assert.Single(Read<Defaults>(csv)).Maybe);
	}

	[Fact]
	public void WritesNestedRecords()
	{
		var shipment = new Shipment
		{
			Id = "S1",
			From = new Place { City = "Oslo", Point = new GeoPoint(59.9, 10.7) },
			To = new Place { City = "Rome", Point = new GeoPoint(41.9, 12.5) },
		};
		AssertRoundTrip([shipment],
			"Id,From City,From Geo Lat,From Geo Lon,To City,To Geo Lat,To Geo Lon\r\nS1,Oslo,59.9,10.7,Rome,41.9,12.5\r\n");

		shipment.To = null!;
		Assert.Equal("S1,Oslo,59.9,10.7,,,\r\n", Write(w => w.WriteRecord(shipment)));
		var writer = CsvRecordWriter.Create<Shipment>();
		Assert.Equal(7, writer.FieldCount);
		Assert.Equal(",,,,,,\r\n", Write(w =>
		{
			writer.WriteEmpty(w);
			w.EndRecord();
		}));
	}

	[Fact]
	public void WritesWithHandWrittenRecordWriters()
	{
		var customer = new Customer("Ann", new Address { Street = "Main 1", City = "Oslo" }) { Location = new Coordinates(1.5, 2.5) };
		AssertRoundTrip([customer], "Name,Home Street,Home City,Lat,Lon\r\nAnn,Main 1,Oslo,1.5,2.5\r\n");
		Assert.Equal("Name,At Lat,At Lon\r\nx,1,2\r\n", Write(w => w.WriteRecords([new Visit { Name = "x", Location = new Coordinates(1, 2) }])));
		Assert.Equal("City,F\r\nOslo,212\r\n", Write(w => w.WriteRecords([new Temperature("Oslo", 100)])));
	}

	[Fact]
	public void WritesColumnsWithIndexesAtTheirIndex() =>
		AssertRoundTrip([new Gapped { First = 1, Last = 2 }], "First,,,Last\r\n1,,,2\r\n");

	[Fact]
	public void TypesThatCannotBeWrittenSayWhy()
	{
		var unwritable = Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteRecords([new Unwritable("a", 1)])));
		Assert.Equal("Records of Unwritable cannot be written: the constructor parameter 'name' has no property or field to write it from.", unwritable.Message);
		var sameIndex = Assert.Throws<InvalidOperationException>(() => CsvRecordWriter.Create<SameIndex>());
		Assert.Contains("'SameIndex.A' and 'SameIndex.B' both have column index 0", sameIndex.Message);
		var mixed = Assert.Throws<InvalidOperationException>(() => CsvRecordWriter.Create<IndexedWithNested>());
		Assert.Contains("both columns with an index", mixed.Message);
		var nested = Assert.Throws<InvalidOperationException>(() => CsvRecordWriter.Create<NestsIndexed>());
		Assert.Contains("'NestsIndexed.Inner'", nested.Message);
		var none = Assert.Throws<InvalidOperationException>(() => Write(w => w.WriteRecord(new NotARecord())));
		Assert.Contains("NotARecord has no CSV record writer", none.Message);

		// Reading them is fine.
		using var reader = CsvReader.FromText("name,count\nab,1\n");
		Assert.Equal(3, Assert.Single(reader.GetRecords<Unwritable>()).Count);
	}

	[Fact]
	public void NullRecordsAreRejected()
	{
		Assert.Throws<ArgumentNullException>(() => Write(w => w.WriteRecord<Order>(null!)));
		var csv = Write(w =>
		{
			var e = Assert.Throws<ArgumentException>(() => w.WriteRecords([Orders[0], null!]));
			Assert.Equal("records", e.ParamName);
		});
		Assert.Equal("Id,Unit Price,Quantity,Note\r\nA1,1.50,3,first\r\n", csv);
	}

	[Fact]
	public void ARecordThatFailsIsDiscarded()
	{
		var csv = Write(w =>
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => w.WriteRecords([new Guarded { Name = "a", Value = 1 }, new Guarded { Name = "b", Value = -1 }]));
			w.WriteRecord(new Guarded { Name = "c", Value = 3 });
		});

		Assert.Equal("Name,Value\r\na,1\r\nc,3\r\n", csv);
	}

	[Fact]
	public void WritesFixedWidthRecords()
	{
		var options = new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = CsvRecordMapper.GetFieldWidths<Account>() };
		var accounts = new[]
		{
			new Account { Id = 1, Name = "Anna", Opened = new DateTime(2024, 1, 15), Balance = 1250.00m },
			new Account { Id = 22, Name = "Benedikt", Opened = new DateTime(2023, 12, 31), Balance = -3.75m },
		};
		var csv = Write(w =>
		{
			foreach (var account in accounts)
			{
				w.WriteRecord(account);
			}
		}, options);
		Assert.Equal("1     Anna      202401151250.00  \r\n22    Benedikt  20231231-3.75    \r\n", csv);

		var back = Read<Account>(csv, new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = options.FieldWidths, HasHeader = false });
		Assert.Equal([1250.00m, -3.75m], back.Select(a => a.Balance));
		Assert.Equal(["Anna", "Benedikt"], back.Select(a => a.Name));
	}

	[Fact]
	public async Task WritesAsynchronously()
	{
		var expected = Write(w => w.WriteRecords(Orders));
		var stream = new MemoryStream();
		await using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { BufferSize = 256 }, leaveOpen: true))
		{
			await writer.WriteRecordsAsync(Orders.Concat(Orders).Concat(Orders));
		}

		Assert.Equal(expected + string.Concat(Enumerable.Repeat(expected.Substring(expected.IndexOf('\n') + 1), 2)), Encoding.UTF8.GetString(stream.ToArray()));

		var asyncStream = new MemoryStream();
		await using (var writer = CsvWriter.Create(asyncStream, leaveOpen: true))
		{
			await writer.WriteRecordsAsync(Yield(Orders));
			await writer.WriteRecordAsync(Orders[0]);
		}

		Assert.Equal(expected + "A1,1.50,3,first\r\n", Encoding.UTF8.GetString(asyncStream.ToArray()));

		// An array, a list and any other sequence take different loops.
		var arrayStream = new MemoryStream();
		await using (var writer = CsvWriter.Create(arrayStream, new CsvWriterOptions { BufferSize = 256 }, leaveOpen: true))
		{
			await writer.WriteRecordsAsync(Orders);
			await writer.WriteRecordsAsync(Orders.ToList());
			writer.WriteRecords(Orders.ToList());
		}

		Assert.Equal(expected + string.Concat(Enumerable.Repeat(expected.Substring(expected.IndexOf('\n') + 1), 2)), Encoding.UTF8.GetString(arrayStream.ToArray()));

		var failing = new MemoryStream();
		await using (var writer = CsvWriter.Create(failing, leaveOpen: true))
		{
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
				await writer.WriteRecordsAsync(new[] { new Guarded { Name = "a", Value = 1 }, new Guarded { Name = "b", Value = -1 } }));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
				await writer.WriteRecordsAsync(Yield(new[] { new Guarded { Name = "c", Value = -2 } })));
			await writer.WriteRecordAsync(new Guarded { Name = "d", Value = 4 });
		}

		Assert.Equal("Name,Value\r\na,1\r\nd,4\r\n", Encoding.UTF8.GetString(failing.ToArray()));
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		await using var other = CsvWriter.Create(new MemoryStream());
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await other.WriteRecordsAsync(Yield(Orders), canceled.Token));
	}

	private static async IAsyncEnumerable<T> Yield<T>(IEnumerable<T> items, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();

			yield return item;
		}
	}

	[Fact]
	public void RecordsUseTheWriterOptions()
	{
		var options = new CsvWriterOptions
		{
			Separator = ';',
			NewLine = "\n",
			CommentPrefix = '#',
			ShouldQuote = (_, type, _, _) => type == typeof(string),
			Injection = CsvInjection.Escape,
		};
		var csv = Write(w => w.WriteRecords([new Order { Id = "#1", Price = 1.5m, Quantity = 2, Note = "=2+3" }]), options);

		Assert.Equal("\"Id\";\"Unit Price\";\"Quantity\";\"Note\"\n\"#1\";1.5;2;\"'=2+3\"\n", csv);
	}

	[Fact]
	public void RecordWritersCanBeReplaced()
	{
		try
		{
			CsvRecordWriter.Register<Order>(_ => new OrderIdWriter());
			Assert.Equal("Id\r\nA1\r\n", Write(w => w.WriteRecords(Orders.Take(1))));
		}
		finally
		{
			CsvRecordWriter.Register<Order>(Order.CreateCsvWriter);
		}

		Assert.Throws<ArgumentNullException>(() => CsvRecordWriter.Register<Order>(null!));
		Assert.Throws<ArgumentNullException>(() => CsvRecordWriter.Create<Order>(null!));
	}

	private sealed class OrderIdWriter : CsvRecordWriter<Order>
	{
		public override int FieldCount => 1;

		public override void WriteHeader(CsvWriter writer) => WriteColumnName(writer, "Id");

		public override void Write(CsvWriter writer, Order record) => writer.WriteField(record.Id);
	}
}
