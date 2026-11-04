using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.Benchmarks;

/// <summary>
/// <see cref="OrderRecord"/>'s orders as a sheet's cells, each member in the type of cell Excel gives it: the header row
/// of member names, then an order per row.
/// </summary>
/// <remarks>
/// <para>
/// The typed Excel benchmark writes these cells with <see cref="ExcelTypedWorkbook"/>, and the corpus tool enters them in
/// Excel through COM, for the test that compares the two workbooks. The test project and the corpus tool link this file.
/// </para>
/// <list type="bullet">
/// <item><c>Id</c>, <c>Total</c>, <c>Weight</c>, <c>Quantity</c> and <c>Sku</c> are numbers in General;</item>
/// <item><c>OrderDate</c> and <c>ShippedAt</c> are serials in a date and time format, <c>ShippedAt</c> in UTC;</item>
/// <item><c>Paid</c> is a boolean;</item>
/// <item>the text, <c>CustomerId</c> included, is shared strings, and an empty <c>Notes</c> has no cell.</item>
/// </list>
/// <para>
/// A cell holds a time to the millisecond and no offset, so <see cref="Generate"/> gives the orders as the cells hold
/// them, which is what reading the sheet gives back.
/// </para>
/// </remarks>
internal static class ExcelOrders
{
	/// <summary>The sheet's name.</summary>
	public const string SheetName = "Orders";

	/// <summary>The format of the date columns, as Excel stores <c>yyyy-mm-dd hh:mm:ss</c>.</summary>
	public const string DateFormat = "yyyy\\-mm\\-dd\\ hh:mm:ss";

	/// <summary>The ticks in a millisecond.</summary>
	private const long TicksPerMillisecond = 10_000;

	/// <summary>The header row: the members' names, in the order of their columns.</summary>
	public static readonly string[] Header =
	[
		"Id", "CustomerId", "Customer", "OrderDate", "ShippedAt", "Total", "Weight", "Quantity", "Paid", "Status", "Sku", "Notes",
	];

	/// <summary>The number format of each column below the header: the dates' format, or <see langword="null"/> for General.</summary>
	public static readonly string?[] Formats = [null, null, null, DateFormat, DateFormat, null, null, null, null, null, null, null];

	/// <summary>Returns <see cref="OrderRecord.Generate"/>'s orders as the cells hold them.</summary>
	/// <param name="rows">The number of orders.</param>
	/// <returns>The orders, with <c>OrderDate</c> to the millisecond and <c>ShippedAt</c> in UTC to the millisecond.</returns>
	public static OrderRecord[] Generate(int rows)
	{
		var orders = OrderRecord.Generate(rows);
		foreach (var order in orders)
		{
			order.OrderDate = ToMillisecond(order.OrderDate);
			order.ShippedAt = new DateTimeOffset(ToMillisecond(order.ShippedAt.UtcDateTime), TimeSpan.Zero);
		}

		return orders;
	}

	/// <summary>Returns the sheet's rows: the header, then each order's cells.</summary>
	/// <param name="orders">The orders, as <see cref="Generate"/> gives them.</param>
	/// <returns>The rows, from row 1.</returns>
	public static List<object?[]> Rows(IReadOnlyList<OrderRecord> orders)
	{
		var rows = new List<object?[]>(orders.Count + 1) { Header.Cast<object?>().ToArray() };
		foreach (var order in orders)
		{
			rows.Add(Cells(order));
		}

		return rows;
	}

	/// <summary>Returns an order's cells: strings, doubles (dates as serials) and a bool.</summary>
	/// <param name="order">The order.</param>
	/// <returns>Its cells, by column.</returns>
	public static object?[] Cells(OrderRecord order) =>
	[
		(double)order.Id,
		order.CustomerId.ToString(),
		order.Customer,
		Serial(order.OrderDate),
		Serial(order.ShippedAt.UtcDateTime),
		(double)order.Total,
		order.Weight,
		(double)order.Quantity,
		order.Paid,
		order.Status,
		(double)order.Sku,
		order.Notes,
	];

	/// <summary>A date's serial in the 1900 date system, which for dates from March 1900 is the OLE Automation date.</summary>
	/// <param name="date">The date.</param>
	/// <returns>The serial.</returns>
	public static double Serial(DateTime date) => date.ToOADate();

	/// <summary>Drops the ticks of a date below the millisecond, as a serial keeps it.</summary>
	/// <param name="date">The date.</param>
	/// <returns>The date to the millisecond, of unspecified kind, as a cell reads.</returns>
	private static DateTime ToMillisecond(DateTime date) =>
		new(date.Ticks - (date.Ticks % TicksPerMillisecond), DateTimeKind.Unspecified);
}
