using System.Globalization;
using System.Text;

namespace CsvHelper.Linq.Tests;

public enum Status { Open, Shipped, Cancelled }

[CsvRecord]
public partial class Order
{
	public int Id { get; set; }

	public string Customer { get; set; } = "";

	public Status Status { get; set; }

	public decimal Total { get; set; }

	public int Quantity { get; set; }

	public DateTime Placed { get; set; }

	[CsvColumn(Optional = true)]
	public string? Note { get; set; }

	/// <summary>Computed from <see cref="Total"/>: not a column.</summary>
	public decimal Discounted => Total * 0.9m;

	public override string ToString() => $"Order {Id}";
}

[CsvRecord]
public partial record Customer(string Name, string Country, int Rank);

[CsvRecord]
public partial record Line(int OrderId, string Sku, int Count, decimal Price);

/// <summary>A record whose constructor rejects what a projection gives the members it leaves out.</summary>
[CsvRecord]
public partial record Checked
{
	public Checked(string code, int value)
	{
		if (code.Length == 0)
		{
			throw new ArgumentException("A code is required.", nameof(code));
		}

		Code = code;
		Value = value;
	}

	public string Code { get; }

	public int Value { get; }
}

/// <summary>The CSV of the tests: orders, customers and lines that refer to each other, made the same way every time.</summary>
public static class Data
{
	public static readonly string[] Customers = ["Ada", "Bob", "Cy", "Dee", "Eve", "Fay", "Gus"];

	public static string OrdersCsv { get; } = MakeOrders(60);

	public static string CustomersCsv { get; } =
		"Name,Country,Rank\nAda,NO,1\nBob,SE,2\nCy,NO,3\nDee,DK,4\nEve,SE,5\nZed,FI,6\n";

	public static string LinesCsv { get; } = MakeLines();

	/// <summary>The orders, read with GetRecords: what LINQ to Objects queries in the comparisons.</summary>
	public static List<Order> Orders { get; } = Read<Order>(OrdersCsv);

	public static List<Customer> CustomerList { get; } = Read<Customer>(CustomersCsv);

	public static List<Line> Lines { get; } = Read<Line>(LinesCsv);

	public static List<T> Read<T>(string csv)
	{
		using var reader = CsvReader.FromText(csv);

		return reader.GetRecords<T>().ToList();
	}

	private static string MakeOrders(int count)
	{
		var text = new StringBuilder("Id,Customer,Status,Total,Quantity,Placed,Note\n");
		for (var i = 1; i <= count; i++)
		{
			var customer = Customers[(i * 5) % Customers.Length];
			var status = (Status)(i % 3);
			var total = ((i * 37) % 250 + i / 7m).ToString(CultureInfo.InvariantCulture);
			var placed = new DateTime(2026, 1, 1).AddDays(i * 3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var note = i % 4 == 0 ? "\"rush, please\"" : i % 5 == 0 ? "" : "n" + i;
			text.Append(i).Append(',').Append(customer).Append(',').Append(status).Append(',').Append(total).Append(',')
				.Append(i % 9 + 1).Append(',').Append(placed).Append(',').Append(note).Append('\n');
		}

		return text.ToString();
	}

	private static string MakeLines()
	{
		var text = new StringBuilder("OrderId,Sku,Count,Price\n");
		for (var i = 1; i <= 90; i++)
		{
			text.Append(i % 45 + 1).Append(",SKU-").Append(i % 11).Append(',').Append(i % 4 + 1).Append(',')
				.Append(((i * 13) % 40 + 0.5m).ToString(CultureInfo.InvariantCulture)).Append('\n');
		}

		return text.ToString();
	}
}
