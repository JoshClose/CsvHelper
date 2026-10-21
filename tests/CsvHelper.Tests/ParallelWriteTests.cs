using System.Dynamic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

[CsvRecord]
public partial class Parcel
{
	public int Id { get; set; }

	public string Name { get; set; } = "";

	public double Weight { get; set; }

	public DateTime Sent { get; set; }

	public string? Note { get; set; }
}

/// <summary>
/// ParallelWriteRecords and ParallelWrite must write exactly what WriteRecords and a loop write, byte for byte, for every
/// option and destination, with inputs large enough for many batches; and stop where they would when something throws.
/// </summary>
public class ParallelWriteTests
{
	private static readonly int[] Degrees = [1, 3, -1];

	private static readonly int[] ParcelWidths = [8, 40, 12, 34, 40];

	private static Parcel[] Parcels(int count, int seed, bool plain)
	{
		string[] tricky = ["Contoso, Ltd", "say \"hi\"", "two\nlines", "cr\r\nlf", "#hash", "=1+2", "@SUM(A1)", "", " padded ", "naïve café", "日本語", "back\\slash"];
		string[] simple = ["Contoso", "Fabrikam", "#hash", "=1+2", "", " padded ", "naïve café", "日本語", "back\\slash", "tab\there"];
		var names = plain ? simple : tricky;
		var rng = new Random(seed);
		var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var result = new Parcel[count];
		for (var i = 0; i < count; i++)
		{
			result[i] = new Parcel
			{
				Id = i,
				Name = names[rng.Next(names.Length)] + (rng.Next(3) == 0 ? i.ToString(CultureInfo.InvariantCulture) : ""),
				Weight = Math.Round(rng.NextDouble() * 1000, rng.Next(4)),
				Sent = start.AddSeconds(rng.Next(0, 100_000_000)),
				Note = rng.Next(4) == 0 ? null : names[rng.Next(names.Length)],
			};
		}

		return result;
	}

	private static IEnumerable<(string Name, CsvWriterOptions Options)> OptionSets()
	{
		yield return ("default", CsvWriterOptions.Default);
		yield return ("semicolon, LF, always quoted", new CsvWriterOptions { Separator = ';', NewLine = "\n", Quoting = CsvQuoting.Always });
		yield return ("ShouldQuote by record", new CsvWriterOptions { ShouldQuote = (field, type, _, record) => record % 3 == 0 || (type == typeof(string) && field.Length > 5) });
		yield return ("injection and comments", new CsvWriterOptions { Injection = CsvInjection.Escape, CommentPrefix = '#' });
		yield return ("no header, no field count check", new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false });
		yield return ("BOM and null value", new CsvWriterOptions { WriteByteOrderMark = true, NullValue = "NULL", Quoting = CsvQuoting.WhenNeededOrWhitespace });
		yield return ("escape mode", new CsvWriterOptions { Mode = CsvMode.Escape, NewLine = "\n" });
		yield return ("no escape, tabs", new CsvWriterOptions { Mode = CsvMode.NoEscape, Separator = '\t' });
		yield return ("fixed width", new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = ParcelWidths, CommentPrefix = '!' });
		yield return ("small buffer", new CsvWriterOptions { BufferSize = 256 });
		yield return ("German culture", new CsvWriterOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") });
	}

	[Fact]
	public async Task RecordsMatchWriteRecords()
	{
		foreach (var (name, options) in OptionSets())
		{
			var plain = options.Mode is CsvMode.FixedWidth or CsvMode.NoEscape;
			var parcels = Parcels(12_000, name.Length, plain);
			var expected = Write(w => w.WriteRecords(parcels), options);
			var expectedText = WriteText(w => w.WriteRecords(parcels), options);
			foreach (var degree in Degrees)
			{
				var context = $"{name}, degree {degree}";
				AssertSame(expected, Write(w => w.ParallelWriteRecords(parcels, degree), options), context);
				AssertSame(expected, Write(w => w.ParallelWriteRecords(parcels.ToList(), degree), options), $"{context}, list");
				AssertSame(expected, Write(w => w.ParallelWriteRecords(parcels.Select(p => p), degree), options), $"{context}, enumerable");
				AssertSame(expectedText, WriteText(w => w.ParallelWriteRecords(parcels, degree), options), $"{context}, text writer");
				AssertSame(expected, await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, degree), options), $"{context}, async");
				AssertSame(expected, await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options), $"{context}, async enumerable");
				AssertSame(expectedText, await WriteTextAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options), $"{context}, async text writer");
			}
		}
	}

	[Fact]
	public async Task RecordWritersOfYourOwnMatchWriteRecords()
	{
		var parcels = Parcels(10_000, 3, false);
		var recordWriter = new ParcelIdWriter();
		var expected = Write(w => w.WriteRecords(parcels, recordWriter));
		foreach (var degree in Degrees)
		{
			AssertSame(expected, Write(w => w.ParallelWriteRecords(parcels, recordWriter, degree)), $"degree {degree}");
			AssertSame(expected, await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, recordWriter, degree)), $"degree {degree}, async");
			AssertSame(expected, await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), recordWriter, degree)), $"degree {degree}, async enumerable");
		}
	}

	[Fact]
	public void SmallInputsMatchWriteRecords()
	{
		foreach (var count in new[] { 0, 1, 2, 63, 64, 65, 129, 500 })
		{
			var parcels = Parcels(count, count, false);
			foreach (var options in new[] { CsvWriterOptions.Default, new CsvWriterOptions { HasHeader = false } })
			{
				var expected = Capture(w => w.WriteRecords(parcels), options);
				var actual = Capture(w => w.ParallelWriteRecords(parcels, 4), options);
				Assert.Equal(expected.Text, actual.Text);
				Assert.Equal(expected.Records, actual.Records);
			}
		}
	}

	[Fact]
	public void ContinuesWhatWasWrittenBefore()
	{
		var parcels = Parcels(5_000, 11, false);
		var options = new CsvWriterOptions { CommentPrefix = '#' };
		var expected = Capture(Before(w => w.WriteRecords(parcels)), options);
		var actual = Capture(Before(w => w.ParallelWriteRecords(parcels)), options);
		Assert.Equal(expected.Text, actual.Text);
		Assert.Equal(expected.Records, actual.Records);
		Assert.Equal(5_002, actual.Records);

		// An open record takes the fields of the first record, as in WriteRecords.
		var open = Capture(w =>
		{
			w.WriteField("x");
			w.WriteRecords(parcels);
		}, new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false });
		var parallelOpen = Capture(w =>
		{
			w.WriteField("x");
			w.ParallelWriteRecords(parcels);
		}, new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false });
		Assert.Equal(open.Text, parallelOpen.Text);

		static Action<CsvWriter> Before(Action<CsvWriter> then) => w =>
		{
			w.WriteComment("parcels");
			w.WriteHeader<Parcel>();
			w.WriteRecord(new Parcel { Id = -1, Name = "first" });
			then(w);
		};
	}

	[Fact]
	public async Task CallbacksMatchALoop()
	{
		var parcels = Parcels(10_000, 5, false);
		var expected = Capture(w =>
		{
			w.WriteHeader<Parcel>();
			foreach (var parcel in parcels)
			{
				WriteItem(w, parcel);
			}
		});
		foreach (var degree in Degrees)
		{
			var actual = Capture(w =>
			{
				w.WriteHeader<Parcel>();
				w.ParallelWrite(parcels, WriteItem, degree);
			});
			Assert.Equal(expected.Text, actual.Text);
			Assert.Equal(expected.Records, actual.Records);
			var actualAsync = await WriteAsync(async w =>
			{
				w.WriteHeader<Parcel>();
				await w.ParallelWriteAsync(Yield(parcels), WriteItem, degree);
			});
			AssertSame(Encoding.UTF8.GetBytes(expected.Text), actualAsync, $"degree {degree}, async");
		}

		// Some items write no record, some two: a detail and a subtotal.
		static void WriteItem(CsvWriter writer, Parcel parcel)
		{
			if (parcel.Id % 5 == 0)
			{
				return;
			}

			writer.WriteRecord(parcel);
			if (parcel.Id % 7 == 0)
			{
				writer.WriteRecord(parcel.Id.ToString(CultureInfo.InvariantCulture), "Subtotal", (parcel.Weight * 2).ToString(CultureInfo.InvariantCulture), "", "");
			}
		}
	}

	[Fact]
	public void TheFirstRecordFixesTheFieldCountBeforeTheWorkersStart()
	{
		// Without a header, the items up to the first record are written first: the workers check against it.
		var items = Enumerable.Range(0, 20_000).ToArray();
		var options = new CsvWriterOptions { HasHeader = false };
		var expected = Capture(w =>
		{
			foreach (var item in items)
			{
				WriteItem(w, item);
			}
		}, options);
		var actual = Capture(w => w.ParallelWrite(items, WriteItem, 4), options);
		Assert.Equal(expected.Text, actual.Text);
		Assert.Equal(expected.Records, actual.Records);
		Assert.IsType<InvalidOperationException>(expected.Error);
		// The workers number records as if every item wrote one, so the record number differs.
		var e = Assert.IsType<InvalidOperationException>(actual.Error);
		Assert.Contains("Record 15001 has 3 fields but the first record has 2", e.Message);

		static void WriteItem(CsvWriter writer, int item)
		{
			if (item < 100)
			{
				return;
			}

			writer.WriteField(item);
			writer.WriteField("a");
			if (item == 15_000)
			{
				writer.WriteField("extra");
			}

			writer.EndRecord();
		}
	}

	[Fact]
	public async Task DynamicRecordsMatchWriteRecords()
	{
		var rows = new List<Dictionary<string, object?>>();
		for (var i = 0; i < 10_000; i++)
		{
			var row = new Dictionary<string, object?> { ["Id"] = i, ["Name"] = i % 3 == 0 ? "a, b" : "c" };
			if (i % 4 != 0)
			{
				row["Amount"] = i * 1.5m;
			}

			rows.Add(row);
		}

		// The first row has every column.
		rows[0]["Amount"] = 0m;
		var expected = Write(w => w.WriteRecords(rows));
		AssertSame(expected, Write(w => w.ParallelWriteRecords(rows, 3)), "dictionaries");
		AssertSame(expected, await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(rows), 3)), "dictionaries, async");

		var expandos = new List<ExpandoObject>();
		for (var i = 0; i < 5_000; i++)
		{
			dynamic expando = new ExpandoObject();
			expando.Name = "n" + i;
			expando.Total = i / 4.0;
			expandos.Add(expando);
		}

		AssertSame(Write(w => w.WriteRecords(expandos)), Write(w => w.ParallelWriteRecords(expandos)), "ExpandoObjects");

		// Records read as dynamic are written back as they were.
		var csv = Encoding.UTF8.GetString(Write(w => w.WriteRecords(Parcels(10_000, 9, false))));
		using var reader = CsvReader.FromText(csv);
		var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
		Assert.Equal(csv, Encoding.UTF8.GetString(Write(w => w.ParallelWriteRecords(records))));
	}

	[Fact]
	public async Task ARecordThatThrowsEndsTheOutputWhereWriteRecordsWould()
	{
		var items = Enumerable.Range(0, 20_000).Select(i => new Guarded { Name = "n" + i, Value = i == 13_579 ? -1 : i }).ToArray();
		var expected = Capture(w => w.WriteRecords(items));
		Assert.IsType<ArgumentOutOfRangeException>(expected.Error);
		foreach (var degree in Degrees)
		{
			var actual = Capture(w => w.ParallelWriteRecords(items, degree));
			Assert.IsType<ArgumentOutOfRangeException>(actual.Error);
			Assert.Equal(expected.Text, actual.Text);
			Assert.Equal(expected.Records, actual.Records);
			var actualAsync = await CaptureAsync(w => w.ParallelWriteRecordsAsync(Yield(items), degree));
			Assert.IsType<ArgumentOutOfRangeException>(actualAsync.Error);
			Assert.Equal(expected.Text, actualAsync.Text);
		}

		// A null record, and a record with the wrong field count, the same way.
		var withNull = Parcels(10_000, 1, false);
		withNull[7_777] = null!;
		var expectedNull = Capture(w => w.WriteRecords(withNull));
		var actualNull = Capture(w => w.ParallelWriteRecords(withNull));
		Assert.IsType<ArgumentException>(actualNull.Error);
		Assert.Equal(expectedNull.Text, actualNull.Text);

		var widths = Parcels(10_000, 2, true);
		widths[8_888].Name = new string('x', 41);
		var options = new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = ParcelWidths };
		var expectedWidth = Capture(w => w.WriteRecords(widths), options);
		var actualWidth = Capture(w => w.ParallelWriteRecords(widths), options);
		Assert.IsType<InvalidOperationException>(actualWidth.Error);
		Assert.Equal(expectedWidth.Error!.Message, actualWidth.Error!.Message);
		Assert.Equal(expectedWidth.Text, actualWidth.Text);
	}

	[Fact]
	public async Task AnExceptionFromTheItemsIsThrownAfterTheRecordsBeforeIt()
	{
		var expected = Capture(w => w.WriteRecords(Failing(15_000)));
		Assert.IsType<InvalidDataException>(expected.Error);
		var actual = Capture(w => w.ParallelWriteRecords(Failing(15_000)));
		Assert.IsType<InvalidDataException>(actual.Error);
		Assert.Equal(expected.Text, actual.Text);
		Assert.Equal(expected.Records, actual.Records);

		var actualAsync = await CaptureAsync(w => w.ParallelWriteRecordsAsync(Failing(15_000)));
		Assert.IsType<InvalidDataException>(actualAsync.Error);
		Assert.Equal(expected.Text, actualAsync.Text);

		// In the first batch, which is written on the calling thread.
		var early = Capture(w => w.ParallelWriteRecords(Failing(10)));
		Assert.IsType<InvalidDataException>(early.Error);
		Assert.Equal(Capture(w => w.WriteRecords(Failing(10))).Text, early.Text);

		static IEnumerable<Parcel> Failing(int at)
		{
			var parcels = Parcels(at, 4, false);
			foreach (var parcel in parcels)
			{
				yield return parcel;
			}

			throw new InvalidDataException("The source failed.");
		}
	}

	[Fact]
	public void ACallbackMustEndItsRecords()
	{
		var result = Capture(w => w.ParallelWrite(Enumerable.Range(0, 10_000), (writer, i) =>
		{
			writer.WriteField(i);
			if (i != 5_000)
			{
				writer.EndRecord();
			}
		}));
		var e = Assert.IsType<InvalidOperationException>(result.Error);
		Assert.Contains("must end the records it starts", e.Message);
		Assert.Equal(string.Concat(Enumerable.Range(0, 5_000).Select(i => $"{i}\r\n")), result.Text);
		Assert.Equal(5_000, result.Records);
	}

	[Fact]
	public void AWorkersWriterCannotFlushDisposeOrStartAParallelWrite()
	{
		var items = Enumerable.Range(0, 10_000).ToArray();
		var expected = Capture(w =>
		{
			foreach (var i in items)
			{
				w.WriteRecord(i.ToString(CultureInfo.InvariantCulture));
			}
		});
		var actual = Capture(w => w.ParallelWrite(items, (writer, i) =>
		{
			writer.WriteRecord(i.ToString(CultureInfo.InvariantCulture));
			writer.Flush();
			if (i > 0)
			{
				// Item 0, which fixes the field count, gets the writer of the call itself.
				writer.Dispose();
			}
		}));
		Assert.Null(actual.Error);
		Assert.Equal(expected.Text, actual.Text);

		var nested = Capture(w => w.ParallelWrite(items, (writer, i) => writer.ParallelWrite(new[] { i }, (inner, j) => inner.WriteRecord("x"))));
		Assert.IsType<InvalidOperationException>(nested.Error);
		Assert.Contains("cannot start a parallel write", nested.Error!.Message);
	}

	[Fact]
	public async Task AFailingDestinationStopsTheWorkers()
	{
		var parcels = Parcels(20_000, 6, false);
		var stream = new FailingStream(100_000);
		var writer = CsvWriter.Create(stream, leaveOpen: true);
		Assert.Throws<IOException>(() => writer.ParallelWriteRecords(parcels));
		Assert.True(stream.Length <= 100_000);

		var asyncStream = new FailingStream(100_000);
		var asyncWriter = CsvWriter.Create(asyncStream, leaveOpen: true);
		await Assert.ThrowsAsync<IOException>(async () => await asyncWriter.ParallelWriteRecordsAsync(parcels));
		Assert.True(asyncStream.Length <= 100_000);
	}

	[Fact]
	public async Task CanBeCanceled()
	{
		var parcels = Parcels(20_000, 8, false);
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		await using (var writer = CsvWriter.Create(new MemoryStream()))
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await writer.ParallelWriteRecordsAsync(parcels, cancellationToken: canceled.Token));
		}

		await using (var writer = CsvWriter.Create(new MemoryStream()))
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await writer.ParallelWriteRecordsAsync(Yield(parcels), cancellationToken: canceled.Token));
		}
	}

	[Fact]
	public void ChecksItsArguments()
	{
		using var writer = CsvWriter.Create(new MemoryStream());
		Assert.Throws<ArgumentNullException>(() => writer.ParallelWriteRecords<Parcel>(null!));
		Assert.Throws<ArgumentNullException>(() => writer.ParallelWriteRecords(new[] { new Parcel() }, null!));
		Assert.Throws<ArgumentNullException>(() => writer.ParallelWrite<int>([1], null!));
		Assert.Throws<ArgumentOutOfRangeException>(() => writer.ParallelWriteRecords(new Parcel[1], 0));
		Assert.Throws<ArgumentOutOfRangeException>(() => writer.ParallelWrite(new[] { 1 }, (w, i) => w.WriteRecord("x"), -2));
		Assert.Throws<InvalidOperationException>(() => writer.ParallelWriteRecords(new[] { new object() }));
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(() => writer.ParallelWriteRecords(new Parcel[1]));
	}

	private sealed class ParcelIdWriter : CsvRecordWriter<Parcel>
	{
		public override int FieldCount => 2;

		public override void WriteHeader(CsvWriter writer)
		{
			WriteColumnName(writer, "Id");
			WriteColumnName(writer, "Name");
		}

		public override void Write(CsvWriter writer, Parcel record)
		{
			writer.WriteField(record.Id);
			writer.WriteField(record.Name);
		}
	}

	/// <summary>A stream that throws once more than <c>limit</c> bytes have been written to it.</summary>
	private sealed class FailingStream(int limit) : MemoryStream
	{
		public override void Write(byte[] buffer, int offset, int count)
		{
			Check(count);
			base.Write(buffer, offset, count);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Check(count);

			return base.WriteAsync(buffer, offset, count, cancellationToken);
		}

#if NET
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			Check(buffer.Length);
			base.Write(buffer);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			Check(buffer.Length);

			return base.WriteAsync(buffer, cancellationToken);
		}
#endif

		private void Check(int count)
		{
			if (Length + count > limit)
			{
				throw new IOException("The disk is full.");
			}
		}
	}

	private static byte[] Write(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			write(writer);
		}

		return stream.ToArray();
	}

	private static string WriteText(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, options))
		{
			write(writer);
		}

		return text.ToString();
	}

	private static async Task<byte[]> WriteAsync(Func<CsvWriter, ValueTask> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		await using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			await write(writer);
		}

		return stream.ToArray();
	}

	private static async Task<string> WriteTextAsync(Func<CsvWriter, ValueTask> write, CsvWriterOptions? options = null)
	{
		var text = new StringWriter();
		await using (var writer = CsvWriter.Create(text, options))
		{
			await write(writer);
		}

		return text.ToString();
	}

	/// <summary>Writes, catching an exception; returns the output, the record count and the exception.</summary>
	private static (string Text, long Records, Exception? Error) Capture(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		Exception? error = null;
		long records;
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true))
		{
			try
			{
				write(writer);
			}
			catch (Exception ex)
			{
				error = ex;
			}

			records = writer.RecordCount;
		}

		return (Encoding.UTF8.GetString(stream.ToArray()), records, error);
	}

	private static async Task<(string Text, long Records, Exception? Error)> CaptureAsync(Func<CsvWriter, ValueTask> write)
	{
		var stream = new MemoryStream();
		Exception? error = null;
		long records;
		await using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			try
			{
				await write(writer);
			}
			catch (Exception ex)
			{
				error = ex;
			}

			records = writer.RecordCount;
		}

		return (Encoding.UTF8.GetString(stream.ToArray()), records, error);
	}

	private static async IAsyncEnumerable<T> Yield<T>(IEnumerable<T> items, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var i = 0;
		foreach (var item in items)
		{
			if (i++ % 1000 == 0)
			{
				await Task.Yield();
			}

			cancellationToken.ThrowIfCancellationRequested();

			yield return item;
		}
	}

	private static void AssertSame(byte[] expected, byte[] actual, string context)
	{
		var length = Math.Min(expected.Length, actual.Length);
		var at = 0;
		while (at < length && expected[at] == actual[at])
		{
			at++;
		}

		Assert.True(at == length && expected.Length == actual.Length,
			$"{context}: the output differs at byte {at} of {expected.Length} (got {actual.Length} bytes).");
	}

	private static void AssertSame(string expected, string actual, string context)
	{
		var length = Math.Min(expected.Length, actual.Length);
		var at = 0;
		while (at < length && expected[at] == actual[at])
		{
			at++;
		}

		Assert.True(at == length && expected.Length == actual.Length,
			$"{context}: the output differs at char {at} of {expected.Length} (got {actual.Length} chars).");
	}
}
