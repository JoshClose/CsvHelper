using System;

namespace CsvHelper.Benchmarks;

/// <summary>A typical business record: dates, a decimal, a Guid, numbers and some text.</summary>
/// <remarks>
/// The parallel write benchmarks write these orders, and the typed Excel benchmark reads them (see
/// <see cref="ExcelOrders"/>). The test project and the corpus tool link this file, so it uses nothing of the benchmarks,
/// and of CsvHelper only <see cref="CsvRecordAttribute"/>.
/// </remarks>
[CsvRecord]
public sealed partial class OrderRecord
{
	/// <summary>The order's number, from 1.</summary>
	public int Id;

	/// <summary>The customer's id.</summary>
	public Guid CustomerId;

	/// <summary>The customer's name.</summary>
	public string Customer = "";

	/// <summary>When the order was placed, in UTC.</summary>
	public DateTime OrderDate;

	/// <summary>When the order shipped, with the offset of where it shipped from.</summary>
	public DateTimeOffset ShippedAt;

	/// <summary>The order's total, to the cent.</summary>
	public decimal Total;

	/// <summary>The parcel's weight.</summary>
	public double Weight;

	/// <summary>The number of items.</summary>
	public int Quantity;

	/// <summary>Whether the order is paid.</summary>
	public bool Paid;

	/// <summary>The order's status.</summary>
	public string Status = "";

	/// <summary>The stock keeping unit, a 10-digit number.</summary>
	public long Sku;

	/// <summary>The delivery notes, often empty.</summary>
	public string Notes = "";

	/// <summary>Deterministic orders.</summary>
	/// <param name="rows">The number of orders.</param>
	/// <param name="seed">The seed of the random numbers.</param>
	/// <returns>The orders, numbered from 1.</returns>
	public static OrderRecord[] Generate(int rows, int seed = 42)
	{
		var rng = new Random(seed);
		string[] customers = ["Contoso Ltd", "Fabrikam, Inc.", "Northwind Traders", "Adventure Works", "Tailspin Toys", "Wide World Importers"];
		string[] statuses = ["Pending", "Shipped", "Delivered", "Cancelled", "Returned"];
		string[] notes = ["", "", "", "Leave at the door", "Call before delivery", "Gift wrap, no invoice", "Fragile: \"glass\""];
		var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var guid = new byte[16];
		var result = new OrderRecord[rows];
		for (var i = 0; i < rows; i++)
		{
			rng.NextBytes(guid);
			var date = start.AddSeconds(rng.Next(0, 5 * 365 * 24 * 3600)).AddTicks(rng.Next(10_000_000));
			result[i] = new OrderRecord
			{
				Id = i + 1,
				CustomerId = new Guid(guid),
				Customer = customers[rng.Next(customers.Length)],
				OrderDate = date,
				ShippedAt = new DateTimeOffset(date.AddHours(rng.Next(1, 96)).Ticks, TimeSpan.FromHours(rng.Next(-8, 9))),
				Total = rng.Next(100, 10_000_000) / 100m,
				Weight = rng.NextDouble() * 50,
				Quantity = rng.Next(1, 100),
				Paid = rng.Next(4) != 0,
				Status = statuses[rng.Next(statuses.Length)],
				Sku = rng.NextInt64(1_000_000_000, 9_999_999_999),
				Notes = notes[rng.Next(notes.Length)],
			};
		}

		return result;
	}
}
