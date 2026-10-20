using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

public enum Color { Red, Green, Blue, DarkRed }

[Flags]
public enum Access { None = 0, Read = 1, Write = 2 }

/// <summary>Two names that differ only in case: matched exactly.</summary>
public enum Casing { Item, ITEM }

[CsvRecord]
public partial class Order
{
	public string Id { get; set; } = "";

	[CsvColumn("Unit Price")]
	public decimal Price { get; set; }

	[CsvColumn(Optional = true)]
	public int? Quantity { get; set; }

	[CsvColumn(Optional = true)]
	public string Note { get; set; } = "none";

	[CsvIgnore]
	public decimal Total { get; set; }

	public decimal Discounted => Price * 0.9m;
}

[CsvRecord]
public partial record Line(string Sku, [CsvColumn(Index = 2)] double Weight, int Count = 7);

[CsvRecord]
public partial struct Point
{
	public int X;
	public int Y;

	public long? Z { get; init; }
}

[CsvRecord]
public readonly partial record struct Pair(string Key, Guid Value);

[CsvRecord]
public partial class Person
{
	public required string Name { get; init; }

	[CsvColumn(Optional = true)]
	public required int Age { get; init; }
}

public class Entity
{
	public long Key { get; set; }

	internal string Kind { get; set; } = "";
}

[CsvRecord]
public partial class Derived : Entity
{
	public string Label { get; set; } = "";
}

[CsvRecord]
public partial class Values
{
	public bool Flag { get; set; }
	public float Single { get; set; }
	public double? Double { get; set; }
	public long Long { get; set; }
	public short Short { get; set; }
	public byte Byte { get; set; }
	public char Char { get; set; }
	public DateTime When { get; set; }
	public DateTimeOffset At { get; set; }
	public TimeSpan Span { get; set; }
	public Guid Guid { get; set; }
	public Color Color { get; set; }
	public Color? MaybeColor { get; set; }
	public Access Access { get; set; }
	public Casing Casing { get; set; }
	public Money Money { get; set; }
}

/// <summary>
/// A type parsed by its own Parse method: as <c>ISpanParsable</c> on .NET, from a string elsewhere. It is written with
/// <see cref="IFormattable"/>, with a format provider.
/// </summary>
public readonly struct Money : IFormattable
#if NET
	, ISpanParsable<Money>
#endif
{
	public Money(decimal amount) => Amount = amount;

	public decimal Amount { get; }

	public static Money Parse(string s, IFormatProvider? provider) => new(decimal.Parse(s.TrimStart('$'), NumberStyles.Number, provider));

	public string ToString(string? format, IFormatProvider? formatProvider) => "$" + Amount.ToString(format, formatProvider);

	public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

#if NET
	public static Money Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(decimal.Parse(s.TrimStart('$'), NumberStyles.Number, provider));

	public static bool TryParse(string? s, IFormatProvider? provider, out Money result) => TryParse(s.AsSpan(), provider, out result);

	public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Money result)
	{
		var ok = decimal.TryParse(s.TrimStart('$'), NumberStyles.Number, provider, out var amount);
		result = new Money(amount);

		return ok;
	}
#endif
}

[CsvRecord]
public partial class Chosen
{
	public Chosen()
	{
	}

	[CsvConstructor]
	public Chosen(string name) => Name = name + "!";

	public string Name { get; } = "";
}

[CsvRecord]
public partial class Hidden
{
	[CsvColumn("secret")]
	private string secret = "";

	public string Secret => secret;
}

[CsvRecord]
public partial class ByIndex
{
	[CsvColumn(Index = 1)]
	public string B { get; set; } = "";

	[CsvColumn(Index = 0)]
	public int A { get; set; }
}

public partial class Outer
{
	[CsvRecord]
	internal partial class Inner
	{
		public int A { get; set; }
	}
}

public class NotARecord
{
	public int A { get; set; }
}

public class RecordTests
{
	private const string Orders = "Id,Unit Price,Quantity,Note\nA1,1.50,3,first\nB2,2,,\n\"C,3\",0.25,10,\"say \"\"hi\"\"\"\n";

	[Fact]
	public void ReadsByHeaderName()
	{
		using var reader = CsvReader.FromText(Orders);
		var orders = reader.GetRecords<Order>().ToList();

		Assert.Equal(["A1", "B2", "C,3"], orders.Select(o => o.Id));
		Assert.Equal([1.50m, 2m, 0.25m], orders.Select(o => o.Price));
		Assert.Equal(new int?[] { 3, null, 10 }, orders.Select(o => o.Quantity));
		Assert.Equal(["first", "", "say \"hi\""], orders.Select(o => o.Note));
		Assert.All(orders, o => Assert.Equal(0m, o.Total));
	}

	[Fact]
	public void MatchesHeaderNamesIgnoringCaseAndWhitespace()
	{
		using var reader = CsvReader.FromText(" id ,UNIT PRICE\nx,4\n");
		var order = Assert.Single(reader.GetRecords<Order>());

		Assert.Equal("x", order.Id);
		Assert.Equal(4m, order.Price);
	}

	[Fact]
	public void OptionalColumnsMayBeMissing()
	{
		using var reader = CsvReader.FromText("Unit Price,Id\n5,a\n");
		var order = Assert.Single(reader.GetRecords<Order>());

		Assert.Equal("a", order.Id);
		Assert.Null(order.Quantity);
		Assert.Equal("none", order.Note);   // keeps its initializer
	}

	[Fact]
	public void MissingRequiredColumnsAreReportedTogether()
	{
		using var reader = CsvReader.FromText("Quantity\n1\n");
		var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Order>());

		Assert.Contains("Order.Id ('Id')", e.Message);
		Assert.Contains("Order.Price ('Unit Price')", e.Message);
		Assert.DoesNotContain("Quantity", e.Message);
	}

	[Fact]
	public void PositionalRecordUsesItsConstructor()
	{
		using var reader = CsvReader.FromText("sku,count,weight\nA,2,1.5\n");

		// Weight reads column 2 by index; Count is matched to "count" ignoring case.
		Assert.Equal(new Line("A", 1.5, 2), Assert.Single(reader.GetRecords<Line>()));
		// An empty field is not a missing column.
		using var empty = CsvReader.FromText("sku,count,weight\nB,,2.5\n");
		Assert.Throws<CsvFormatException>(() => empty.GetRecords<Line>().ToList());
	}

	[Fact]
	public void ConstructorParametersWithDefaultsAreOptional()
	{
		using var reader = CsvReader.FromText("Sku,X,Weight\nA,_,1.5\n");
		var line = Assert.Single(reader.GetRecords<Line>());

		Assert.Equal(new Line("A", 1.5, 7), line);
	}

	[Fact]
	public void ReadsStructsFieldsAndInitProperties()
	{
		using var reader = CsvReader.FromText("X,Y,Z\n1,2,3\n4,5,\n");
		var points = reader.GetRecords<Point>().ToList();

		Assert.Equal(new (int, int, long?)[] { (1, 2, 3), (4, 5, null) }, points.Select(p => (p.X, p.Y, p.Z)));
	}

	[Fact]
	public void ReadsRecordStructs()
	{
		var guid = Guid.NewGuid();
		using var reader = CsvReader.FromText($"Key,Value\nk,{guid}\n");

		Assert.Equal(new Pair("k", guid), Assert.Single(reader.GetRecords<Pair>()));
	}

	[Fact]
	public void ReadsRequiredMembers()
	{
		using var reader = CsvReader.FromText("Name\nAda\n");
		var person = Assert.Single(reader.GetRecords<Person>());

		Assert.Equal("Ada", person.Name);
		Assert.Equal(0, person.Age);
	}

	[Fact]
	public void ReadsBaseClassMembers()
	{
		using var reader = CsvReader.FromText("Key,Kind,Label\n42,k,l\n");
		var d = Assert.Single(reader.GetRecords<Derived>());

		Assert.Equal((42L, "k", "l"), (d.Key, d.Kind, d.Label));
	}

	[Fact]
	public void ReadsEveryValueType()
	{
		var guid = Guid.NewGuid();
		var csv = "Flag,Single,Double,Long,Short,Byte,Char,When,At,Span,Guid,Color,MaybeColor,Access,Casing,Money\n" +
				  $"yes,1.25,,-9000000000,-5,200,x,2024-02-29T12:30:00,2024-02-29T12:30:00+02:00,01:02:03,{guid},Blue,,\"Read, Write\",ITEM,$12.50\n" +
				  $"0,-0.5,2.5,1,1,1,y,2024-01-01,2024-01-01T00:00:00Z,1.00:00:00,{guid},darkred,green,3,Item,7\n";
		using var reader = CsvReader.FromText(csv);
		var values = reader.GetRecords<Values>().ToList();
		var v = values[0];

		Assert.True(v.Flag);
		Assert.Equal(1.25f, v.Single);
		Assert.Null(v.Double);
		Assert.Equal(-9_000_000_000L, v.Long);
		Assert.Equal((short)-5, v.Short);
		Assert.Equal((byte)200, v.Byte);
		Assert.Equal('x', v.Char);
		Assert.Equal(new DateTime(2024, 2, 29, 12, 30, 0), v.When);
		Assert.Equal(new DateTimeOffset(2024, 2, 29, 12, 30, 0, TimeSpan.FromHours(2)), v.At);
		Assert.Equal(new TimeSpan(1, 2, 3), v.Span);
		Assert.Equal(guid, v.Guid);
		Assert.Equal(Color.Blue, v.Color);
		Assert.Null(v.MaybeColor);
		Assert.Equal(Access.Read | Access.Write, v.Access);
		Assert.Equal(Casing.ITEM, v.Casing);
		Assert.Equal(12.50m, v.Money.Amount);

		v = values[1];
		Assert.False(v.Flag);
		Assert.Equal(2.5, v.Double);
		Assert.Equal(TimeSpan.FromDays(1), v.Span);
		Assert.Equal(Color.DarkRed, v.Color);
		Assert.Equal(Color.Green, v.MaybeColor);
		Assert.Equal(Access.Read | Access.Write, v.Access);
		Assert.Equal(Casing.Item, v.Casing);
		Assert.Equal(7m, v.Money.Amount);
	}

	[Theory]
	[InlineData("Red", Color.Red)]
	[InlineData("RED", Color.Red)]
	[InlineData("dArKrEd", Color.DarkRed)]
	[InlineData("\"Green\"", Color.Green)]
	[InlineData(" Blue", Color.Blue)]
	[InlineData("2", Color.Blue)]
	public void MatchesEnumNames(string field, Color expected)
	{
		using var reader = CsvReader.FromText("Color\n" + field + "\n", new CsvReaderOptions { HasHeader = true });

		Assert.Equal(expected, Assert.Single(reader.GetRecords<ColorOnly>()).Color);
	}

	[Fact]
	public void RejectsUnknownEnumNames()
	{
		using var reader = CsvReader.FromText("Color\nPurple\n");
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<ColorOnly>().ToList());

		Assert.Contains("Purple", e.Message);
	}

	[Fact]
	public void UsesTheFormatProvider()
	{
		var options = new CsvReaderOptions { FormatProvider = new CultureInfo("de-DE"), Separator = ';' };
		using var reader = CsvReader.FromText("Id;Unit Price;Money\na;1,5;$2,25\n", options);
		var order = Assert.Single(reader.GetRecords<Order>());

		Assert.Equal(1.5m, order.Price);
		using var values = CsvReader.FromText("Money\n$2,25\n", options);
		Assert.Equal(2.25m, Assert.Single(values.GetRecords<MoneyOnly>()).Money.Amount);
	}

	[Fact]
	public void UsesTheMarkedConstructorAndPrivateMembers()
	{
		using var reader = CsvReader.FromText("name,secret\nn,s\n");
		Assert.Equal("n!", Assert.Single(reader.GetRecords<Chosen>()).Name);
		using var hidden = CsvReader.FromText("name,secret\nn,s\n");
		Assert.Equal("s", Assert.Single(hidden.GetRecords<Hidden>()).Secret);
	}

	[Fact]
	public void ReadsByIndexWithoutHeader()
	{
		using var reader = CsvReader.FromText("1,a\n2,b\n", new CsvReaderOptions { HasHeader = false });

		Assert.Equal([(1, "a"), (2, "b")], reader.GetRecords<ByIndex>().Select(r => (r.A, r.B)));
	}

	[Fact]
	public void WithoutHeaderNamedColumnsAreAnError()
	{
		using var reader = CsvReader.FromText("1,a\n", new CsvReaderOptions { HasHeader = false });
		var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Order>());

		Assert.Contains("no header", e.Message);
	}

	[Fact]
	public void ReadsNestedTypes()
	{
		using var reader = CsvReader.FromText("A\n5\n");

		Assert.Equal(5, Assert.Single(reader.GetRecords<Outer.Inner>()).A);
	}

	[Fact]
	public void GetRecordReusesItsMapper()
	{
		using var reader = CsvReader.FromText(Orders);
		var ids = new List<string>();
		while (reader.Read())
		{
			ids.Add(reader.GetRecord<Order>().Id);
			Assert.Equal(reader.GetString(0), reader.GetRecord<Order>().Id);
		}

		Assert.Equal(["A1", "B2", "C,3"], ids);
	}

	[Fact]
	public async Task ReadsAsync()
	{
		await using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(Orders)));
		var ids = new List<string>();
		await foreach (var order in reader.GetRecordsAsync<Order>()) ids.Add(order.Id);

		Assert.Equal(["A1", "B2", "C,3"], ids);
	}

	[Fact]
	public void ParallelEnumerateMatchesGetRecords()
	{
		var sb = new StringBuilder("Id,Unit Price,Quantity,Note\n");
		for (var i = 0; i < 200_000; i++) sb.Append($"id{i},{i % 1000}.{i % 100:D2},{(i % 7 == 0 ? "" : (i % 50).ToString(CultureInfo.InvariantCulture))},n{i % 13}\n");
		var text = sb.ToString();
		using var sequential = CsvReader.FromText(text);
		var expected = sequential.GetRecords<Order>().Select(Key).ToList();
		using var parallel = CsvReader.FromText(text);
		var actual = parallel.ParallelEnumerate<Order>(4).Select(Key).ToList();

		Assert.Equal(200_000, expected.Count);
		Assert.Equal(expected, actual);
		using var selector = CsvReader.FromText(text);
		Assert.Equal(expected, selector.ParallelEnumerate(r => Key(r.GetRecord<Order>()), 3).ToList());

		static (string, decimal, int?, string) Key(Order o) => (o.Id, o.Price, o.Quantity, o.Note);
	}

	[Fact]
	public void UnregisteredTypesThrow()
	{
		using var reader = CsvReader.FromText("A\n1\n");
		var e = Assert.Throws<InvalidOperationException>(() => reader.GetRecords<NotARecord>());

		Assert.Contains("[CsvRecord]", e.Message);
	}
}

[CsvRecord]
public partial class ColorOnly
{
	public Color Color { get; set; }
}

[CsvRecord]
public partial class MoneyOnly
{
	public Money Money { get; set; }
}
