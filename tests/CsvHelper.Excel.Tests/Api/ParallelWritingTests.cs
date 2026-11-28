using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Security;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order of every kind of cell the parallel writing tests write: text, numbers, a date, a boolean, a duration and a link.</summary>
[CsvRecord]
public partial class ParallelOrder
{
	/// <summary>The order's number: its index among its orders, plus 1.</summary>
	public int Id { get; set; }

	/// <summary>Text: some that starts like a number, some with XML's characters, a line break or letters past ASCII.</summary>
	public string Name { get; set; } = "";

	/// <summary>A decimal.</summary>
	public decimal Total { get; set; }

	/// <summary>A double.</summary>
	public double Rate { get; set; }

	/// <summary>A date: at midnight, in whole seconds or with milliseconds, which are three formats.</summary>
	public DateTime Placed { get; set; }

	/// <summary>A boolean.</summary>
	public bool Paid { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A link, or no cell.</summary>
	public Uri? Site { get; set; }

	/// <summary>Text, empty, or no cell.</summary>
	public string? Note { get; set; }
}

/// <summary>An order whose name's getter throws the exception it was given, as user code that a record writer calls may.</summary>
[CsvRecord]
public partial class GuardedOrder
{
	/// <summary>The name.</summary>
	private string name = "";

	/// <summary>The exception the name's getter throws, or <see langword="null"/>.</summary>
	private Exception? failure;

	/// <summary>The order's number.</summary>
	public int Id { get; set; }

	/// <summary>The name, unless the order was made to fail (<see cref="Fail"/>).</summary>
	public string Name
	{
		get => failure is null ? name : throw failure;
		set => name = value;
	}

	/// <summary>Makes the name's getter throw an exception.</summary>
	/// <param name="exception">The exception.</param>
	internal void Fail(Exception exception) => failure = exception;
}

/// <summary>How the parallel writing tests write a sheet's orders: a parallel method, and the serial write it must match.</summary>
public enum ParallelMethod
{
	/// <summary><see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/>, against <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/>.</summary>
	Records,

	/// <summary>
	/// <see cref="CsvWriter.ParallelWriteRecordsAsync{T}(IEnumerable{T}, int, CancellationToken)"/> of an array, against
	/// <see cref="CsvWriter.WriteRecordsAsync{T}(IEnumerable{T}, CancellationToken)"/>.
	/// </summary>
	RecordsAsync,

	/// <summary>
	/// <see cref="CsvWriter.ParallelWriteRecordsAsync{T}(IAsyncEnumerable{T}, int, CancellationToken)"/> of an asynchronous
	/// sequence, against <see cref="CsvWriter.WriteRecordsAsync{T}(IAsyncEnumerable{T}, CancellationToken)"/>.
	/// </summary>
	RecordsSequence,

	/// <summary>
	/// The header, then <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> of a callback
	/// that writes an order, against a <see langword="foreach"/> loop that calls it with the sheet's writer.
	/// </summary>
	Callback,

	/// <summary>
	/// The header, then <see cref="CsvWriter.ParallelWriteAsync{T}(IEnumerable{T}, Action{CsvWriter, T}, int, CancellationToken)"/>
	/// of an array, against the same loop.
	/// </summary>
	CallbackAsync,

	/// <summary>
	/// The header, then <see cref="CsvWriter.ParallelWriteAsync{T}(IAsyncEnumerable{T}, Action{CsvWriter, T}, int, CancellationToken)"/>
	/// of an asynchronous sequence, against an <see langword="await foreach"/> loop that calls the callback.
	/// </summary>
	CallbackSequence,
}

/// <summary>A workbook a test wrote, and what its writer counted, read after the writer was disposed.</summary>
/// <param name="Bytes">The package, checked (<see cref="Written.Checked(byte[])"/>).</param>
/// <param name="ChunksAppended">The workers' chunks its sheets appended (<see cref="ExcelWorkbookWriter.ChunksAppended"/>).</param>
/// <param name="ChunksRedone">The workers' chunks its sheets refused, whose batches were written again serially (<see cref="ExcelWorkbookWriter.ChunksRedone"/>).</param>
/// <param name="RentedBuffers">The pooled buffers its zip writer still held: 0 once it is disposed.</param>
/// <param name="HeldSegments">The segments its entries still held: 0 once it is disposed.</param>
/// <param name="Threads">
/// The threads its chunked parts deflated on (<see cref="ZipWriter.Threads"/>): <see cref="ExcelWriterOptions.CompressionThreads"/>
/// resolved, or more when the suites run chunked (<see cref="ExcelWorkbookWriter.TestThreads"/>).
/// </param>
/// <param name="SheetNames">Its sheets' names, in tab order.</param>
internal sealed record ParallelBook(byte[] Bytes, long ChunksAppended, long ChunksRedone, int RentedBuffers, int HeldSegments, int Threads, IReadOnlyList<string> SheetNames);

/// <summary>A sheet's write: the record count its writer reached, and the exception that ended the write, if one did.</summary>
/// <param name="RecordCount">The sheet's writer's <see cref="CsvWriter.RecordCount"/> after the write.</param>
/// <param name="Error">The exception, or <see langword="null"/>.</param>
internal sealed record SheetWrite(long RecordCount, Exception? Error);

/// <summary>
/// What the tests of parallel writing and parallel deflate share: orders to write, workbooks written with their writers'
/// counters, sheets written serially or by a parallel method, and the comparisons of packages part by part (their entries
/// inflated) and byte for byte.
/// </summary>
internal static class ParallelWrites
{
	/// <summary>Core's <c>CsvWriter.DiscardRecord</c>, which the tests reach by reflection: core's internals aren't visible to them.</summary>
	private static readonly MethodInfo DiscardRecord = typeof(CsvWriter).GetMethod("DiscardRecord", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!;

	/// <summary>
	/// Writes an item's records serially, as a parallel write writes its serial items (core's <c>WriteSerialItem</c>): an
	/// item that throws has its unfinished record dropped, as <c>WriteRecords</c> drops one, and the exception goes on. A
	/// plain loop would leave the record open, and disposing the writer would end it with the cells written before the
	/// throw, which the parallel write doesn't have.
	/// </summary>
	/// <typeparam name="T">The items' type.</typeparam>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="write">Writes the item's records.</param>
	/// <param name="item">The item.</param>
	internal static void WriteSerialItem<T>(CsvWriter sheet, Action<CsvWriter, T> write, T item)
	{
		try
		{
			write(sheet, item);
		}
		catch
		{
			DiscardRecord.Invoke(sheet, null);

			throw;
		}
	}

	/// <summary>The first order's date.</summary>
	private static readonly DateTime First = new(2024, 1, 1);

	/// <summary>Two letters past ASCII, Japanese for "Japan".</summary>
	private static readonly string Japanese = WriterFuzz.Units(0x65E5, 0x672C);

	/// <summary>Orders of every kind of cell, each kind's styles among the first few, so that a sheet's first rows register them.</summary>
	/// <param name="count">The number of orders.</param>
	/// <returns>The orders, numbered from 1.</returns>
	public static ParallelOrder[] Orders(int count) => [.. Enumerable.Range(0, count).Select(Order)];

	/// <summary>
	/// Orders whose cells take the styles of the first order only: dates at midnight, durations in whole seconds, and no
	/// links. A test changes one of them to bring a style late.
	/// </summary>
	/// <param name="count">The number of orders.</param>
	/// <returns>The orders, numbered from 1.</returns>
	public static ParallelOrder[] PlainOrders(int count) =>
	[
		.. Enumerable.Range(0, count).Select(i => new ParallelOrder
		{
			Id = i + 1,
			Name = "name " + Number(i),
			Total = i * 1.25m,
			Rate = i / 3.0,
			Placed = First.AddDays(i % 1000),
			Paid = i % 2 == 0,
			Took = TimeSpan.FromSeconds(i * 37L),
			Note = i % 3 == 0 ? null : "note " + Number(i),
		}),
	];

	/// <summary>Writes a workbook into memory, disposes its writer as the mode says, checks the package, and reads the writer's counters.</summary>
	/// <param name="write">Writes the sheets; its flag says whether to write asynchronously.</param>
	/// <param name="async">Whether to write and dispose asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="seekable">Whether the stream seeks; one that doesn't writes forward only (<see cref="ForwardOnlyStream"/>).</param>
	/// <returns>The workbook and its writer's counters.</returns>
	public static async Task<ParallelBook> WorkbookAsync(Func<ExcelWorkbookWriter, bool, Task> write, bool async, ExcelWriterOptions? options = null, bool seekable = true)
	{
		using var memory = new MemoryStream();
		using var forward = new ForwardOnlyStream(memory);
		var book = ExcelWorkbookWriter.Create(seekable ? memory : forward, options, leaveOpen: true);
		await write(book, async);
		if (async)
		{
			await book.DisposeAsync();
		}
		else
		{
			book.Dispose();
		}

		var zip = book.Package.Zip;

		return new ParallelBook(Written.Checked(memory.ToArray()), book.ChunksAppended, book.ChunksRedone, zip.RentedBuffers, zip.HeldSegments, zip.Threads, [.. book.SheetNames]);
	}

	/// <summary>
	/// Adds a sheet of <see cref="ParallelOrder"/> and writes orders into it, serially (degree 0) or by a parallel method,
	/// catching the exception that ends the write; then disposes the sheet's writer as the method says.
	/// </summary>
	/// <param name="book">The workbook's writer.</param>
	/// <param name="orders">The orders.</param>
	/// <param name="method">The parallel method, whose serial write degree 0 makes.</param>
	/// <param name="degree">The degree of parallelism, or 0 for the serial write.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <param name="write">The callbacks' writing of an order, or <see langword="null"/> for <see cref="CsvWriter.WriteRecord{T}(T)"/>.</param>
	/// <param name="name">The sheet's name.</param>
	/// <returns>A task whose result is the sheet's record count and the exception, if one ended the write.</returns>
	public static async Task<SheetWrite> WriteOrdersAsync(
		ExcelWorkbookWriter book, IReadOnlyList<ParallelOrder> orders, ParallelMethod method, int degree, ExcelSheetOptions? look = null, Action<CsvWriter, ParallelOrder>? write = null, string name = "Data")
	{
		var sheet = book.AddSheet<ParallelOrder>(name, look);
		Exception? error = null;
		try
		{
			await WriteAsync(sheet, orders, method, degree, write ?? WriteOrder);
		}
		catch (Exception e)
		{
			error = e;
		}

		var count = sheet.RecordCount;
		await Written.DisposeAsync(sheet, IsAsync(method));

		return new SheetWrite(count, error);
	}

	/// <summary>
	/// Adds a sheet and writes records into it with <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> or its
	/// asynchronous form (degree 0), or with <see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/> or its
	/// asynchronous form, catching the exception that ends the write; then disposes the sheet's writer as the mode says.
	/// </summary>
	/// <typeparam name="T">The records' type: a <see cref="CsvRecordAttribute"/> type, or a dynamic record's.</typeparam>
	/// <param name="book">The workbook's writer.</param>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="degree">The degree of parallelism, or 0 for the serial write.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <param name="typed">Whether the sheet is added for <typeparamref name="T"/> (<see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/>), or without a type, as dynamic records' sheets are.</param>
	/// <returns>A task whose result is the sheet's record count and the exception, if one ended the write.</returns>
	public static async Task<SheetWrite> WriteRecordsAsync<T>(ExcelWorkbookWriter book, IReadOnlyList<T> records, bool async, int degree, ExcelSheetOptions? look = null, bool typed = true)
	{
		var sheet = typed ? book.AddSheet<T>("Data", look) : book.AddSheet("Data", look);
		Exception? error = null;
		try
		{
			if (!async)
			{
				if (degree == 0)
				{
					sheet.WriteRecords(records);
				}
				else
				{
					sheet.ParallelWriteRecords(records, degree);
				}
			}
			else if (degree == 0)
			{
				await sheet.WriteRecordsAsync(records);
			}
			else
			{
				await sheet.ParallelWriteRecordsAsync(records, degree);
			}
		}
		catch (Exception e)
		{
			error = e;
		}

		var count = sheet.RecordCount;
		await Written.DisposeAsync(sheet, async);

		return new SheetWrite(count, error);
	}

	/// <summary>Awaits a sheet's write, and rethrows the exception that ended it, if one did.</summary>
	/// <param name="write">The write, from <see cref="WriteOrdersAsync"/> or <see cref="WriteRecordsAsync{T}"/>.</param>
	/// <returns>A task that completes when the write has, and fails with its exception when one ended it.</returns>
	public static async Task Rethrow(Task<SheetWrite> write)
	{
		if ((await write).Error is { } error)
		{
			ExceptionDispatchInfo.Capture(error).Throw();
		}
	}

	/// <summary>Whether a method writes asynchronously.</summary>
	/// <param name="method">The method.</param>
	/// <returns>Whether it does.</returns>
	public static bool IsAsync(ParallelMethod method) => method is not (ParallelMethod.Records or ParallelMethod.Callback);

	/// <summary>Writes an order as a record: the callbacks' default.</summary>
	/// <param name="writer">The writer the callback got.</param>
	/// <param name="order">The order.</param>
	public static void WriteOrder(CsvWriter writer, ParallelOrder order) => writer.WriteRecord(order);

	/// <summary>Items as an asynchronous sequence, which yields to the scheduler now and then.</summary>
	/// <typeparam name="T">The items' type.</typeparam>
	/// <param name="items">The items.</param>
	/// <returns>The sequence.</returns>
	public static async IAsyncEnumerable<T> Yield<T>(IEnumerable<T> items)
	{
		var i = 0;
		foreach (var item in items)
		{
			if (i++ % 100 == 0)
			{
				await Task.Yield();
			}

			yield return item;
		}
	}

	/// <summary>A package's parts, inflated: each entry's name and bytes, in the zip's order.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <returns>The parts.</returns>
	public static List<(string Name, byte[] Bytes)> Parts(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		var parts = new List<(string, byte[])>();
		foreach (var entry in archive.Entries)
		{
			using var stream = entry.Open();
			using var part = new MemoryStream();
			stream.CopyTo(part);
			parts.Add((entry.FullName, part.ToArray()));
		}

		return parts;
	}

	/// <summary>How a package's parts differ from another's, inflated: their names, or the first part that differs, and where.</summary>
	/// <param name="expected">The serial write's package.</param>
	/// <param name="actual">The other write's.</param>
	/// <returns>The difference, or <see langword="null"/> when the parts are the same.</returns>
	public static string? PartsDifference(byte[] expected, byte[] actual)
	{
		var (serial, other) = (Parts(expected), Parts(actual));
		var names = string.Join(", ", serial.Select(p => p.Name));
		var otherNames = string.Join(", ", other.Select(p => p.Name));
		if (names != otherNames)
		{
			return $"the parts are {otherNames}, where the serial write's are {names}";
		}

		for (var i = 0; i < serial.Count; i++)
		{
			if (BytesDifference(serial[i].Bytes, other[i].Bytes) is { } difference)
			{
				return $"{serial[i].Name} {difference}";
			}
		}

		return null;
	}

	/// <summary>How bytes differ from others: their lengths, and the text around the first byte that differs.</summary>
	/// <param name="expected">The serial write's bytes.</param>
	/// <param name="actual">The other write's.</param>
	/// <returns>The difference, or <see langword="null"/> when they are the same.</returns>
	public static string? BytesDifference(byte[] expected, byte[] actual)
	{
		var length = Math.Min(expected.Length, actual.Length);
		var at = 0;
		while (at < length && expected[at] == actual[at])
		{
			at++;
		}

		if (at == length && expected.Length == actual.Length)
		{
			return null;
		}

		return $"has {Count(actual.Length)} bytes, the serial write's {Count(expected.Length)}, and differs from byte {Count(at)}: {Around(actual, at)} where the serial write has {Around(expected, at)}";
	}

	/// <summary>Fails unless two packages have the same parts, inflated.</summary>
	/// <param name="expected">The serial write's package.</param>
	/// <param name="actual">The other write's.</param>
	public static void AssertSameParts(byte[] expected, byte[] actual)
	{
		var difference = PartsDifference(expected, actual);
		Assert.True(difference is null, "The package's " + difference);
	}

	/// <summary>Fails unless two packages have the same bytes.</summary>
	/// <param name="expected">The serial write's package.</param>
	/// <param name="actual">The other write's.</param>
	public static void AssertSameBytes(byte[] expected, byte[] actual)
	{
		var difference = BytesDifference(expected, actual);
		Assert.True(difference is null, "The package " + difference);
	}

	/// <summary>
	/// On net8.0 and net10.0, fails unless two workbooks whose chunked parts both deflated on threads have the same bytes:
	/// a chunked part's bytes depend on its XML alone there. On the other builds it checks nothing: a parallel write's
	/// bytes differ from a serial write's there, though its parts don't.
	/// </summary>
	/// <param name="serial">The serial write's workbook.</param>
	/// <param name="parallel">The other write's.</param>
	public static void AssertSameBytesWhenChunked(ParallelBook serial, ParallelBook parallel)
	{
#if NET
		if (serial.Threads > 1 && parallel.Threads > 1)
		{
			AssertSameBytes(serial.Bytes, parallel.Bytes);
		}
#endif
	}

	/// <summary>Fails unless a workbook's zip writer returned every buffer and segment it took.</summary>
	/// <param name="book">The workbook.</param>
	public static void AssertReleased(ParallelBook book)
	{
		Assert.Equal(0, book.RentedBuffers);
		Assert.Equal(0, book.HeldSegments);
	}

	/// <summary>Writes orders into a sheet's writer, serially (degree 0) or by a parallel method.</summary>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="orders">The orders.</param>
	/// <param name="method">The parallel method, whose serial write degree 0 makes.</param>
	/// <param name="degree">The degree of parallelism, or 0 for the serial write.</param>
	/// <param name="write">The callbacks' writing of an order.</param>
	/// <returns>A task that completes when the orders are written.</returns>
	private static async Task WriteAsync(CsvWriter sheet, IReadOnlyList<ParallelOrder> orders, ParallelMethod method, int degree, Action<CsvWriter, ParallelOrder> write)
	{
		var serial = degree == 0;
		if (method == ParallelMethod.Records)
		{
			if (serial)
			{
				sheet.WriteRecords(orders);
			}
			else
			{
				sheet.ParallelWriteRecords(orders, degree);
			}

			return;
		}

		if (method == ParallelMethod.RecordsAsync)
		{
			await (serial ? sheet.WriteRecordsAsync(orders) : sheet.ParallelWriteRecordsAsync(orders, degree));

			return;
		}

		if (method == ParallelMethod.RecordsSequence)
		{
			await (serial ? sheet.WriteRecordsAsync(Yield(orders)) : sheet.ParallelWriteRecordsAsync(Yield(orders), degree));

			return;
		}

		// The callbacks write the records alone: the header first, on the sheet's writer.
		sheet.WriteHeader<ParallelOrder>();
		if (serial && method == ParallelMethod.CallbackSequence)
		{
			await foreach (var order in Yield(orders))
			{
				WriteSerialItem(sheet, write, order);
			}
		}
		else if (serial)
		{
			foreach (var order in orders)
			{
				WriteSerialItem(sheet, write, order);
			}
		}
		else if (method == ParallelMethod.Callback)
		{
			sheet.ParallelWrite(orders, write, degree);
		}
		else if (method == ParallelMethod.CallbackAsync)
		{
			await sheet.ParallelWriteAsync(orders, write, degree);
		}
		else
		{
			await sheet.ParallelWriteAsync(Yield(orders), write, degree);
		}
	}

	/// <summary>An order of every kind of cell.</summary>
	/// <param name="index">Its index among the orders.</param>
	/// <returns>The order.</returns>
	private static ParallelOrder Order(int index) => new()
	{
		Id = index + 1,
		Name = (index % 5) switch
		{
			0 => "00" + Number(index),
			1 => "a & b <c> " + Number(index),
			2 => "two\nlines " + Number(index),
			3 => Japanese + Number(index),
			_ => "name " + Number(index),
		},
		Total = index % 4 == 0 ? -index / 8m : index * 1.25m,
		Rate = index / 3.0,
		Placed = (index % 3) switch
		{
			0 => First.AddDays(index % 1000),
			1 => First.AddSeconds(index * 61L),
			_ => First.AddMilliseconds(index * 7L + 1),
		},
		Paid = index % 2 == 0,
		Took = TimeSpan.FromSeconds(index * 37L),
		Site = (index % 4) switch
		{
			0 => new Uri("https://example.com/orders/" + Number(index)),
			1 => new Uri("mailto:o" + Number(index) + "@example.com"),
			_ => null,
		},
		Note = (index % 6) switch
		{
			0 => null,
			1 => "",
			_ => "note " + Number(index),
		},
	};

	/// <summary>A number's text, in the invariant culture.</summary>
	/// <param name="number">The number.</param>
	/// <returns>Its text.</returns>
	private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

	/// <summary>A count for a message, with its thousands grouped.</summary>
	/// <param name="count">The count.</param>
	/// <returns>Its text.</returns>
	private static string Count(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

	/// <summary>The text around a byte, for a message.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="at">The byte's index.</param>
	/// <returns>Up to 80 bytes around it, as UTF-8 shown with its other characters escaped.</returns>
	private static string Around(byte[] bytes, int at)
	{
		var start = Math.Max(0, Math.Min(at, bytes.Length) - 40);
		var length = Math.Min(bytes.Length - start, 80);

		return WriterFuzz.Show(Encoding.UTF8.GetString(bytes, start, length));
	}
}

/// <summary>
/// The parallel write methods on a sheet's <see cref="CsvWriter"/> (Phase 9's step 10):
/// <see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/>, <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/>
/// and their asynchronous forms, which threw <see cref="NotSupportedException"/> before. Each sheet has the serial
/// write's parts, inflated, and on net8.0 and net10.0 the bytes of a serial write whose parts deflate on threads too
/// (<see cref="ExcelWriterOptions.CompressionThreads"/> 2 or more); the workers wrote chunks of it
/// (<see cref="ExcelWorkbookWriter.ChunksAppended"/>); and every buffer and segment of the zip returns. Across a batch:
/// <see cref="ExcelRowLimit.NewSheet"/> and <see cref="ExcelRowLimit.Throw"/> at a few hundred rows, links past the cap
/// as text, and a style that comes late, whose batch is written again serially, its callback called twice. Errors: the
/// sheet's own (a long text, a long number, an early date, a field past XFD) are the serial write's, with their cells;
/// user code's are its own exception objects, after the rows before them. Shared strings are written serially, and they
/// and the lists sheet, written as the package closes, deflate on the write's threads;
/// <see cref="ExcelSheetOptions.Protect"/>, dynamic records, a sheet serial writes began, cancellation, and
/// <see cref="ExcelWriterExtensions.MergeCells"/> from a callback, which refuses.
/// </summary>
/// <remarks>
/// Core writes a source that ends within its first batch, 64 items, on the calling thread, and the sheet takes workers only
/// once auto-fit's 100 records are written (and under <see cref="ExcelSheetOptions.Protect"/> its first 64 KB), so the
/// sheets have 1,000 records or more, and what fails is at record 1,000 or after.
/// </remarks>
public class ParallelWritingTests
{
	/// <summary>The index of the order a test makes fail, or bring a late style: past the serial prefix and the first batch.</summary>
	private const int Late = 1000;

	/// <summary>A report length large enough that core's batches hold one item each (<see cref="ExcelWorkbookWriter.ParallelChunkLength"/>).</summary>
	private const int OneItemBatches = 1 << 20;

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>Each parallel method at degrees 1, 3 and -1.</summary>
	public static TheoryData<ParallelMethod, int> Methods
	{
		get
		{
			var data = new TheoryData<ParallelMethod, int>();
			foreach (var method in (ParallelMethod[])Enum.GetValues(typeof(ParallelMethod)))
			{
				foreach (var degree in new[] { 1, 3, -1 })
				{
					data.Add(method, degree);
				}
			}

			return data;
		}
	}

	/// <summary>The sheet's own errors, sync and async: a cell's text too long, a number of too many digits, a date before 1900, a field past XFD.</summary>
	public static TheoryData<string, bool> SheetErrors => new()
	{
		{ "long text", false },
		{ "long text", true },
		{ "long number", false },
		{ "long number", true },
		{ "early date", false },
		{ "early date", true },
		{ "past XFD", false },
		{ "past XFD", true },
	};

	/// <summary>The styles that come late, sync and async: a date with milliseconds, and the sheet's first link.</summary>
	public static TheoryData<string, bool> LateStyles => new()
	{
		{ "milliseconds", false },
		{ "milliseconds", true },
		{ "link", false },
		{ "link", true },
	};

	[Theory]
	[MemberData(nameof(Methods))]
	public async Task WritesTheSerialWritesSheet(ParallelMethod method, int degree)
	{
		var orders = ParallelWrites.Orders(1500);
		var async = ParallelWrites.IsAsync(method);
		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;

		var serial = await ParallelWrites.WorkbookAsync(async (book, _) => serialWrite = await ParallelWrites.WriteOrdersAsync(book, orders, method, 0), async);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, _) => parallelWrite = await ParallelWrites.WriteOrdersAsync(book, orders, method, degree), async);

		Assert.Null(serialWrite!.Error);
		Assert.Null(parallelWrite!.Error);
		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Equal(0, serial.ChunksAppended);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheet.");
		ParallelWrites.AssertReleased(serial);
		ParallelWrites.AssertReleased(parallel);
		Assert.Equal(Written.ReadOne(serial.Bytes), Written.ReadOne(parallel.Bytes));
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(OneItemBatches, false)]
	[InlineData(OneItemBatches, true)]
	[InlineData(1, true)]
	public async Task WritesTheBytesOfAChunkedSerialWrite(int chunkLength, bool async)
	{
		// Three sheets, the first and the last written in parallel, the middle serially; chunks of 4 KB, and batches of
		// the XML's length, of one item, or of 4,096.
		var orders = ParallelWrites.Orders(3000);
		var options = new ExcelWriterOptions { CompressionThreads = 2 };
		async Task Write(ExcelWorkbookWriter book, bool a, int degree)
		{
			book.ChunkSize = 4096;
			book.ParallelChunkLength = chunkLength;
			await ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, a ? ParallelMethod.RecordsAsync : ParallelMethod.Records, degree, name: "Records"));
			await ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, a ? ParallelMethod.RecordsAsync : ParallelMethod.Records, 0, name: "Serial"));
			await ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, a ? ParallelMethod.CallbackAsync : ParallelMethod.Callback, degree, name: "Callback"));
		}

		var serial = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 0), async, options);
		var parallel = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 4), async, options);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertSameBytesWhenChunked(serial, parallel);
		Assert.True(parallel.ChunksAppended >= 2, $"The workers wrote {parallel.ChunksAppended} chunks of two sheets.");
		ParallelWrites.AssertReleased(serial);
		ParallelWrites.AssertReleased(parallel);
	}

	[Fact]
	public async Task ChunksItsSheetAsCompressionThreadsDo()
	{
		// With CompressionThreads 1, a parallel write at degree 2 chunks its sheet from its start, on two threads, as
		// CompressionThreads 2 chunks it; the parts written whole deflate on the writing thread either way.
		var orders = ParallelWrites.Orders(6000);
		var serial = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, ParallelMethod.Records, 0)), false, new ExcelWriterOptions { CompressionThreads = 2 });
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, ParallelMethod.Records, 2)), false);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
#if NET
		ParallelWrites.AssertSameBytes(serial.Bytes, parallel.Bytes);
#endif
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[InlineData(1, false)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	public async Task GoesOnInASheetSerialWritesBegan(int threads, bool async)
	{
		// 2,000 orders written serially, past a flush, so that the sheet's entry is open when the parallel write begins:
		// with CompressionThreads 1 it switches to chunks there, and with 2 it was chunked already.
		var orders = ParallelWrites.Orders(4000);
		var (first, rest) = (orders.Take(2000).ToList(), orders.Skip(2000).ToList());
		async Task Write(ExcelWorkbookWriter book, bool a, int degree)
		{
			var sheet = book.AddSheet<ParallelOrder>("Data");
			if (a)
			{
				await sheet.WriteRecordsAsync(first);
				await (degree == 0 ? sheet.WriteRecordsAsync(rest) : sheet.ParallelWriteRecordsAsync(rest, degree));
			}
			else
			{
				sheet.WriteRecords(first);
				if (degree == 0)
				{
					sheet.WriteRecords(rest);
				}
				else
				{
					sheet.ParallelWriteRecords(rest, degree);
				}
			}

			await Written.DisposeAsync(sheet, a);
		}

		var options = new ExcelWriterOptions { CompressionThreads = threads };
		var serial = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 0), async, options);
		var parallel = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 4), async, options);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertSameBytesWhenChunked(serial, parallel);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheet.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[InlineData(false, 0)]
	[InlineData(true, 0)]
	[InlineData(false, OneItemBatches)]
	[InlineData(true, 1)]
	public async Task GoesOnInNewSheetsAcrossABatch(bool async, int chunkLength)
	{
		// 250 rows a sheet: the header and 249 orders; a batch across a sheet's end is written again serially.
		var orders = ParallelWrites.Orders(1000);
		var options = new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet };
		Task Write(ExcelWorkbookWriter book, bool a, int degree)
		{
			book.MaxRows = 250;
			book.ParallelChunkLength = chunkLength;

			return ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, a ? ParallelMethod.RecordsAsync : ParallelMethod.Records, degree));
		}

		var serial = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 0), async, options);
		var parallel = await ParallelWrites.WorkbookAsync((book, a) => Write(book, a, 3), async, options);

		Assert.Equal(["Data", "Data (2)", "Data (3)", "Data (4)", "Data (5)"], parallel.SheetNames);
		Assert.Equal(serial.SheetNames, parallel.SheetNames);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheets.");
		Assert.True(parallel.ChunksRedone >= 1, "No batch across a sheet's end was written again.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsAtTheLastRowAsTheSerialWriteDoes(bool async)
	{
		var orders = ParallelWrites.Orders(1000);
		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;
		async Task<SheetWrite> Write(ExcelWorkbookWriter book, bool a, int degree)
		{
			book.MaxRows = 250;

			return await ParallelWrites.WriteOrdersAsync(book, orders, a ? ParallelMethod.RecordsAsync : ParallelMethod.Records, degree);
		}

		var serial = await ParallelWrites.WorkbookAsync(async (book, a) => serialWrite = await Write(book, a, 0), async);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, a) => parallelWrite = await Write(book, a, 3), async);

		var error = Assert.IsType<InvalidOperationException>(parallelWrite!.Error);
		Assert.Equal("The sheet Data is full at row 250, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet.", error.Message);
		Assert.Equal(serialWrite!.Error!.Message, error.Message);
		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[InlineData(ExcelRowLimit.Throw, false)]
	[InlineData(ExcelRowLimit.Throw, true)]
	[InlineData(ExcelRowLimit.NewSheet, false)]
	[InlineData(ExcelRowLimit.NewSheet, true)]
	public async Task MeetsTheRowLimitBeforeAFailureOnTheRecordPastIt(ExcelRowLimit limit, bool async)
	{
		// The first record past the sheet's last row fails after its first field reached the sheet (its name's getter
		// throws): the serial write meets the row limit at that field first, so it throws the sheet's error, or starts the
		// next sheet before the failure; the parallel write's batch that stopped there is written again serially, and does
		// the same.
		var orders = Enumerable.Range(0, 1500).Select(i => new GuardedOrder { Id = i + 1, Name = "name " + i.ToString(CultureInfo.InvariantCulture) }).ToArray();
		orders[249].Fail(new FormatException("The name can't be read."));
		var options = ExcelWriterOptions.Default with { RowLimit = limit };
		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;
		async Task<SheetWrite> Write(ExcelWorkbookWriter book, bool a, int degree)
		{
			book.MaxRows = 250;

			return await ParallelWrites.WriteRecordsAsync(book, orders, a, degree, new ExcelSheetOptions { AutoFit = 0 });
		}

		var serial = await ParallelWrites.WorkbookAsync(async (book, a) => serialWrite = await Write(book, a, 0), async, options);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, a) => parallelWrite = await Write(book, a, 4), async, options);

		Assert.NotNull(serialWrite!.Error);
		Assert.IsType(serialWrite.Error.GetType(), parallelWrite!.Error);
		Assert.Equal(serialWrite.Error.Message, parallelWrite.Error!.Message);
		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.True(parallel.ChunksRedone >= 1, "The batch that stopped past the last row wasn't written again.");
	}

	[Theory]
	[MemberData(nameof(SheetErrors))]
	public async Task ThrowsTheSheetsOwnErrorAsTheSerialWriteDoes(string kind, bool async)
	{
		// The worker that meets the error flags its batch, which is written again serially: the serial write's
		// exception, with its type, message and cell, after the rows before it.
		var orders = ParallelWrites.Orders(1500);
		var failing = orders[Late];
		var method = async ? ParallelMethod.RecordsAsync : ParallelMethod.Records;
		Action<CsvWriter, ParallelOrder>? write = null;
		var options = ExcelWriterOptions.Default;
		if (kind == "long text")
		{
			failing.Name = new string('x', 40_000);
		}
		else if (kind == "long number")
		{
			failing.Total = 12345678901234567m;
			options = options with { LongNumbers = ExcelLongNumbers.Throw };
		}
		else if (kind == "early date")
		{
			failing.Placed = new DateTime(1899, 12, 31);
			options = options with { OutOfRangeDates = ExcelOutOfRangeDates.Throw };
		}
		else
		{
			method = async ? ParallelMethod.CallbackAsync : ParallelMethod.Callback;
			options = options with { Csv = new CsvWriterOptions { ValidateFieldCount = false } };
			write = (writer, order) =>
			{
				if (order.Id != failing.Id)
				{
					writer.WriteRecord(order);

					return;
				}

				for (var i = 0; i <= 16_384; i++)
				{
					writer.WriteField(i);
				}

				writer.EndRecord();
			};
		}

		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;
		var serial = await ParallelWrites.WorkbookAsync(async (book, _) => serialWrite = await ParallelWrites.WriteOrdersAsync(book, orders, method, 0, write: write), async, options);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, _) => parallelWrite = await ParallelWrites.WriteOrdersAsync(book, orders, method, 4, write: write), async, options);

		var expected = Assert.IsType<InvalidOperationException>(serialWrite!.Error);
		var error = Assert.IsType<InvalidOperationException>(parallelWrite!.Error);
		Assert.Equal(expected.Message, error.Message);
		if (kind == "long text")
		{
			Assert.Equal("Cell Data!B1002 has 40,000 characters, and Excel opens no cell of more than 32,767.", error.Message);
		}

		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.True(parallel.ChunksRedone >= 1, "The batch of the record that failed wasn't written again.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsTheCallbacksExceptionAfterTheRowsBeforeIt(bool async)
	{
		// The callback fails on a worker's writer, halfway through a record: the call throws that exception object, and
		// the sheet has the records before it.
		var orders = ParallelWrites.Orders(1500);
		var thrown = new InvalidOperationException("The callback failed.");
		void Write(CsvWriter writer, ParallelOrder order)
		{
			if (order.Id == Late + 1)
			{
				writer.WriteField("half a record");

				throw thrown;
			}

			writer.WriteRecord(order);
		}

		var method = async ? ParallelMethod.CallbackAsync : ParallelMethod.Callback;
		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;
		var serial = await ParallelWrites.WorkbookAsync(async (book, _) => serialWrite = await ParallelWrites.WriteOrdersAsync(book, orders.Take(Late).ToList(), method, 0), async);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, _) => parallelWrite = await ParallelWrites.WriteOrdersAsync(book, orders, method, 4, write: Write), async);

		Assert.Null(serialWrite!.Error);
		Assert.Same(thrown, parallelWrite!.Error);
		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsARecordsExceptionAfterTheRowsBeforeIt(bool async)
	{
		// A member's getter fails as the generated record writer reads it on a worker.
		var orders = Enumerable.Range(0, 1500).Select(i => new GuardedOrder { Id = i + 1, Name = "name " + i.ToString(CultureInfo.InvariantCulture) }).ToArray();
		var thrown = new InvalidOperationException("The getter failed.");
		orders[Late].Fail(thrown);
		SheetWrite? serialWrite = null;
		SheetWrite? parallelWrite = null;

		var serial = await ParallelWrites.WorkbookAsync(async (book, a) => serialWrite = await ParallelWrites.WriteRecordsAsync(book, orders, a, 0), async);
		var parallel = await ParallelWrites.WorkbookAsync(async (book, a) => parallelWrite = await ParallelWrites.WriteRecordsAsync(book, orders, a, 4), async);

		Assert.Same(thrown, serialWrite!.Error);
		Assert.Same(thrown, parallelWrite!.Error);
		Assert.Equal(serialWrite.RecordCount, parallelWrite.RecordCount);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Equal(Late + 1, Written.ReadOne(parallel.Bytes).Count);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesSharedStringsSerially(bool async)
	{
		// A worker can't know a shared string's index: the sheet is never ready for workers, and core writes every record
		// on the calling thread.
		var orders = ParallelWrites.Orders(1500);
		var options = new ExcelWriterOptions { Strings = ExcelStrings.Shared };
		var method = async ? ParallelMethod.RecordsAsync : ParallelMethod.Records;
		var serial = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 0)), async, options);
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 4)), async, options);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Equal(0, parallel.ChunksAppended);
		Assert.Equal(0, parallel.ChunksRedone);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DeflatesTheSharedStringsOnItsThreads(bool async)
	{
		// The shared strings are written as the package closes, after the parallel write has ended: they deflate on its
		// threads all the same, as CompressionThreads of its degree deflates them, so the package has that write's bytes. In
		// chunks of 4 KB, so that on .NET 8 too, whose chunks deflate at the level of a part deflated whole, a part deflated
		// whole on the writing thread would have other bytes.
		var orders = ParallelWrites.Orders(1500);
		var method = async ? ParallelMethod.RecordsAsync : ParallelMethod.Records;
		Task Write(ExcelWorkbookWriter book, int degree)
		{
			book.ChunkSize = 4096;

			return ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, degree));
		}

		var serial = await ParallelWrites.WorkbookAsync((book, _) => Write(book, 0), async, new ExcelWriterOptions { Strings = ExcelStrings.Shared, CompressionThreads = 3 });
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => Write(book, 3), async, new ExcelWriterOptions { Strings = ExcelStrings.Shared });

		Assert.True(Written.Entry(parallel.Bytes, "xl/sharedStrings.xml").Length > 4096 * 4, "The shared strings fill fewer than four chunks.");
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertSameBytes(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DeflatesTheListsSheetOnItsThreads(bool async)
	{
		// A list too long for its validation is written to the lists sheet, as the package closes, after the parallel write
		// has ended: it deflates on the write's threads all the same, so the package has the bytes of CompressionThreads of
		// its degree. In chunks of 4 KB, as above.
		var orders = ParallelWrites.Orders(1500);
		var look = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["Name"] = new ExcelColumnOptions { List = [.. Enumerable.Range(0, 2000).Select(i => "value " + i.ToString(CultureInfo.InvariantCulture))] },
			},
		};
		var method = async ? ParallelMethod.RecordsAsync : ParallelMethod.Records;
		Task Write(ExcelWorkbookWriter book, int degree)
		{
			book.ChunkSize = 4096;

			return ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, degree, look));
		}

		var serial = await ParallelWrites.WorkbookAsync((book, _) => Write(book, 0), async, new ExcelWriterOptions { CompressionThreads = 3 });
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => Write(book, 3), async);

		Assert.True(Written.Entry(parallel.Bytes, "xl/worksheets/sheet2.xml").Length > 4096 * 4, "The lists sheet fills fewer than four chunks.");
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertSameBytes(serial.Bytes, parallel.Bytes);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheet.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(LateStyles))]
	public async Task WritesTheBatchOfALateStyleAgainSerially(string late, bool async)
	{
		// One order past the first batches needs a style the workers' view lacks: its batch is written again serially,
		// which registers the style where the serial write does, and the view is frozen again, so that later batches go on
		// in the workers. The callback runs again for each item of that batch, and once for every other item.
		var orders = ParallelWrites.PlainOrders(1500);
		if (late == "milliseconds")
		{
			orders[Late].Placed = new DateTime(2024, 5, 6, 7, 8, 9, 123);
		}
		else
		{
			orders[Late].Site = new Uri("https://example.com/late");
		}

		var calls = new int[orders.Length];
		void Counted(CsvWriter writer, ParallelOrder order)
		{
			Interlocked.Increment(ref calls[order.Id - 1]);
			writer.WriteRecord(order);
		}

		var method = async ? ParallelMethod.CallbackAsync : ParallelMethod.Callback;
		var serial = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 0)), async);
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 4, write: Counted)), async);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Equal(1, parallel.ChunksRedone);
		Assert.True(parallel.ChunksAppended >= 2, $"The workers wrote {parallel.ChunksAppended} chunks, before and after the late style.");
		Assert.Equal(2, calls[Late]);
		Assert.All(calls, c => Assert.InRange(c, 1, 2));
		var twice = Enumerable.Range(0, calls.Length).Where(i => calls[i] == 2).ToList();
		Assert.Equal(twice.Count, twice[twice.Count - 1] - twice[0] + 1);
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesAProtectedSheet(bool async)
	{
		// Under Protect the sheet takes workers once its first 64 KB, whose start registers the unlocked columns' styles,
		// are written: styles.xml is the serial write's.
		var orders = ParallelWrites.Orders(3000);
		var look = new ExcelSheetOptions { Protect = true };
		var method = async ? ParallelMethod.RecordsAsync : ParallelMethod.Records;
		var serial = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 0, look)), async);
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, method, 4, look)), async);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Contains("<sheetProtection ", Written.Entry(parallel.Bytes, "xl/worksheets/sheet1.xml"));
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the protected sheet.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesDynamicRecords(bool async)
	{
		// The first record fixes the columns on the calling thread; the rest go to the workers.
		var rows = Enumerable.Range(0, 1500).Select(i => new Dictionary<string, object?>
		{
			["Id"] = i + 1,
			["Name"] = "name " + i.ToString(CultureInfo.InvariantCulture),
			["When"] = new DateTime(2024, 1, 1).AddHours(i),
			["Site"] = i % 3 == 0 ? new Uri("https://example.com/" + i.ToString(CultureInfo.InvariantCulture)) : null,
		}).ToList();
		var serial = await ParallelWrites.WorkbookAsync((book, a) => ParallelWrites.Rethrow(ParallelWrites.WriteRecordsAsync(book, rows, a, 0, typed: false)), async);
		var parallel = await ParallelWrites.WorkbookAsync((book, a) => ParallelWrites.Rethrow(ParallelWrites.WriteRecordsAsync(book, rows, a, 4, typed: false)), async);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheet.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Fact]
	public async Task LeavesTheLinksPastTheCapAsText()
	{
		// 70 links more than Excel opens in a sheet: a batch past the cap is written again serially, and its URIs are text.
		var sites = Enumerable.Range(0, SheetLinks.MaxLinks + 70).Select(i => new LinkedSite { Name = "s" + i.ToString(CultureInfo.InvariantCulture), Site = new Uri("https://example.com/" + i.ToString(CultureInfo.InvariantCulture)) }).ToArray();
		var options = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFit = 0 } };
		var serial = await ParallelWrites.WorkbookAsync((book, a) => ParallelWrites.Rethrow(ParallelWrites.WriteRecordsAsync(book, sites, a, 0)), false, options);
		var parallel = await ParallelWrites.WorkbookAsync((book, a) => ParallelWrites.Rethrow(ParallelWrites.WriteRecordsAsync(book, sites, a, 4)), false, options);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Equal(SheetLinks.MaxLinks, Regex.Matches(Written.Entry(parallel.Bytes, "xl/worksheets/sheet1.xml"), "<hyperlink ").Count);
		Assert.True(parallel.ChunksAppended >= 1, "The workers wrote no chunk of the sheet.");
		Assert.True(parallel.ChunksRedone >= 1, "No batch past the cap was written again.");
		ParallelWrites.AssertReleased(parallel);
	}

	[Fact]
	public async Task RefusesToMergeCellsFromACallback()
	{
		// A worker's writer can't merge cells: the callback's exception says so and what to do instead, the sheet has the
		// records before it, and the sheet's own writer merges after the call.
		var orders = ParallelWrites.Orders(1500);
		Exception? error = null;
		async Task Write(ExcelWorkbookWriter book, bool inParallel)
		{
			var sheet = book.AddSheet<ParallelOrder>("Data");
			sheet.WriteHeader<ParallelOrder>();
			if (inParallel)
			{
				try
				{
					sheet.ParallelWrite(orders, (writer, order) =>
					{
						if (order.Id == Late + 1)
						{
							writer.MergeCells("A2:B2");
						}

						writer.WriteRecord(order);
					}, 4);
				}
				catch (Exception e)
				{
					error = e;
				}
			}
			else
			{
				foreach (var order in orders.Take(Late))
				{
					sheet.WriteRecord(order);
				}
			}

			sheet.MergeCells("A2:B2");
			await Written.DisposeAsync(sheet, false);
		}

		var serial = await ParallelWrites.WorkbookAsync((book, _) => Write(book, false), false);
		var parallel = await ParallelWrites.WorkbookAsync((book, _) => Write(book, true), false);

		var refused = Assert.IsType<NotSupportedException>(error);
		Assert.Contains("ParallelWrite", refused.Message);
		Assert.DoesNotContain("writes CSV", refused.Message);
		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		Assert.Contains("<mergeCell ref=\"A2:B2\"/>", Written.Entry(parallel.Bytes, "xl/worksheets/sheet1.xml"));
		ParallelWrites.AssertReleased(parallel);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsWhenCanceled(bool callback)
	{
		// The source cancels the token as it reaches order 3,000 of 6,000. The call throws; the sheet's writer and the
		// workbook's dispose without throwing; every buffer and segment returns; and unless the cancellation reached the
		// stream, which faults the package, the sheet holds the header and orders from the first, in order.
		var orders = ParallelWrites.Orders(6000);
		var all = Written.ReadOne((await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, ParallelMethod.RecordsAsync, 0)), true)).Bytes);
		using var canceling = new CancellationTokenSource();
		using var stream = new MemoryStream();
		var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true);
		var sheet = workbook.AddSheet<ParallelOrder>("Data");
		Exception? error = null;
		try
		{
			if (callback)
			{
				sheet.WriteHeader<ParallelOrder>();
				await sheet.ParallelWriteAsync(CancelAt(orders, 3000, canceling), ParallelWrites.WriteOrder, 4, canceling.Token);
			}
			else
			{
				await sheet.ParallelWriteRecordsAsync(CancelAt(orders, 3000, canceling), 4, canceling.Token);
			}
		}
		catch (Exception e)
		{
			error = e;
		}

		Assert.IsAssignableFrom<OperationCanceledException>(error);
		await sheet.DisposeAsync();
		await workbook.DisposeAsync();
		Assert.Equal(0, workbook.Package.Zip.RentedBuffers);
		Assert.Equal(0, workbook.Package.Zip.HeldSegments);
		if (!workbook.Package.Zip.Faulted)
		{
			var rows = Written.ReadOne(Written.Checked(stream.ToArray()));
			Assert.InRange(rows.Count, 1, 3001);
			Assert.Equal(all.Take(rows.Count), rows);
		}
	}

	[Fact]
	public async Task ChecksItsArguments()
	{
		// Core's checks, on a sheet's writer; the sheet is as it was after them.
		var orders = ParallelWrites.Orders(1500);
		var serial = await ParallelWrites.WorkbookAsync((book, _) => ParallelWrites.Rethrow(ParallelWrites.WriteOrdersAsync(book, orders, ParallelMethod.Records, 0)), false);
		var parallel = await ParallelWrites.WorkbookAsync((book, _) =>
		{
			using var sheet = book.AddSheet<ParallelOrder>("Data");
			Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ParallelWriteRecords(orders, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ParallelWrite(orders, ParallelWrites.WriteOrder, -2));
			Assert.Throws<ArgumentNullException>(() => sheet.ParallelWriteRecords<ParallelOrder>(null!));
			Assert.Throws<ArgumentNullException>(() => sheet.ParallelWrite(orders, null!));
			sheet.ParallelWriteRecords(orders, 2);

			return Task.CompletedTask;
		}, false);

		ParallelWrites.AssertSameParts(serial.Bytes, parallel.Bytes);
		ParallelWrites.AssertReleased(parallel);
	}

	/// <summary>Orders as an asynchronous sequence that cancels a token as it reaches one of them, and then throws.</summary>
	/// <param name="orders">The orders.</param>
	/// <param name="count">The number of orders it yields before it cancels.</param>
	/// <param name="source">The token's source.</param>
	/// <returns>The sequence.</returns>
	private static async IAsyncEnumerable<ParallelOrder> CancelAt(IReadOnlyList<ParallelOrder> orders, int count, CancellationTokenSource source)
	{
		for (var i = 0; i < orders.Count; i++)
		{
			if (i == count)
			{
				source.Cancel();
				source.Token.ThrowIfCancellationRequested();
			}

			if (i % 100 == 0)
			{
				await Task.Yield();
			}

			yield return orders[i];
		}
	}
}
