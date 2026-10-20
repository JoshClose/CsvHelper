using System.Collections;
using System.Globalization;
using System.Reflection;

namespace CsvHelper.Linq.Tests;

/// <summary>Runs queries against CSV and against LINQ to Objects, and compares what they return.</summary>
public static class TestUtil
{
	public static CsvQuery<Order> Orders(CsvQueryOptions? options = null) => CsvQueryable.FromText<Order>(Data.OrdersCsv, options);

	public static CsvQuery<Customer> Customers(CsvQueryOptions? options = null) => CsvQueryable.FromText<Customer>(Data.CustomersCsv, options);

	public static CsvQuery<Line> Lines(CsvQueryOptions? options = null) => CsvQueryable.FromText<Line>(Data.LinesCsv, options);

	/// <summary>
	/// Asserts that a query of orders returns the same over the CSV as over the orders in memory, or throws the same type
	/// of exception.
	/// </summary>
	public static void Same<TResult>(Func<IQueryable<Order>, TResult> query) =>
		Assert.Equal(Run(() => query(Data.Orders.AsQueryable())), Run(() => query(Orders())));

	/// <summary>Asserts that a query of orders, customers and lines returns the same over the CSV as in memory.</summary>
	public static void Same<TResult>(Func<IQueryable<Order>, IQueryable<Customer>, IQueryable<Line>, TResult> query) =>
		Assert.Equal(
			Run(() => query(Data.Orders.AsQueryable(), Data.CustomerList.AsQueryable(), Data.Lines.AsQueryable())),
			Run(() => query(Orders(), Customers(), Lines())));

	/// <summary>Runs a query and describes its result (enumerating it), or the type of exception it throws.</summary>
	public static string Run(Func<object?> query)
	{
		try
		{
			return Canon(query(), 0);
		}
		catch (Exception e)
		{
			return "throws " + e.GetType().Name;
		}
	}

	/// <summary>A description of a value that is the same for equal values: sequences, groups, tuples and objects by their members.</summary>
	public static string Canon(object? value, int depth)
	{
		if (depth > 8)
		{
			return "...";
		}

		switch (value)
		{
			case null:
				return "null";
			case string text:
				return "\"" + text + "\"";
			case IFormattable formattable:
				return formattable.ToString(null, CultureInfo.InvariantCulture);
			case bool or char:
				return value.ToString()!;
		}

		var type = value.GetType();
		var grouping = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGrouping<,>));
		if (grouping is not null)
		{
			var key = grouping.GetProperty("Key")!.GetValue(value);

			return "(" + Canon(key, depth + 1) + ": " + Items((IEnumerable)value, depth) + ")";
		}

		if (value is IEnumerable sequence)
		{
			return Items(sequence, depth);
		}

		var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0)
			.Select(p => p.Name + "=" + Canon(p.GetValue(value), depth + 1))
			.Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name + "=" + Canon(f.GetValue(value), depth + 1)));

		return "{" + string.Join(", ", members) + "}";
	}

	private static string Items(IEnumerable sequence, int depth) => "[" + string.Join(", ", sequence.Cast<object?>().Select(i => Canon(i, depth + 1))) + "]";
}

/// <summary>
/// A stream over bytes that counts what is read from it, and whether it was disposed. Its asynchronous reads call the
/// synchronous ones (as MemoryStream's do), which count.
/// </summary>
public sealed class CountingStream : MemoryStream
{
	public CountingStream(byte[] bytes)
		: base(bytes, false)
	{
	}

	public long BytesRead { get; private set; }

	public bool Disposed { get; private set; }

	public override int Read(byte[] buffer, int offset, int count)
	{
		var read = base.Read(buffer, offset, count);
		BytesRead += read;

		return read;
	}

#if NET
	public override int Read(Span<byte> buffer)
	{
		var read = base.Read(buffer);
		BytesRead += read;

		return read;
	}
#endif

	protected override void Dispose(bool disposing)
	{
		Disposed = true;
		base.Dispose(disposing);
	}
}
