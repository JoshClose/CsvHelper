using System.Buffers.Text;
using System.IO.Compression;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The writer's memory, as Phase 4's "Security and fuzzing" describes. With <see cref="ExcelStrings.Inline"/> strings, a
/// sheet of 32,767-character cells streams: what has been written but hasn't reached the stream stays within a flush, a
/// record and the zip's own buffers, however many rows go before, and on .NET writing more rows allocates nothing. A
/// million distinct <see cref="ExcelStrings.Shared"/> strings take the table's arena and its integers, at most twice the
/// strings' bytes and 32 bytes a string, and growing to it allocates at most twice that, in the table and through a whole
/// workbook.
/// </summary>
/// <param name="output">Shows the sizes measured.</param>
public class WriterSecurityTests(ITestOutputHelper output)
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The characters of the longest cell.</summary>
	private const int MaxCell = 32767;

	/// <summary>The distinct shared strings of the shared-strings tests.</summary>
	private const int Strings = 1_000_000;

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StreamsSheetsOfLongInlineCellsInABoundedBuffer(bool async)
	{
		// Four cells of 32,767 characters a row, of one and three bytes, of seven as written (CR as _x000D_) and of five
		// (& as &amp;): 512 KiB of XML a row, 32 MiB in all.
		const int rows = 64;
		var texts = new[] { new string('x', MaxCell), new string((char)0x20AC, MaxCell), new string('\r', MaxCell), new string('&', MaxCell) };
		var rowBytes = MaxCell * (1L + 3 + 7 + 5);
		var stream = new CountingStream();
		var lag = 0L;
		long allocated = 0;
		var options = new ExcelWriterOptions { Strings = ExcelStrings.Inline, CompressionLevel = CompressionLevel.NoCompression };
		var workbook = ExcelWorkbookWriter.Create(stream, options);
		var writer = workbook.AddSheet("Long");
		for (var r = 0; r < rows; r++)
		{
			if (r == rows / 8)
			{
				allocated = AllocatedBytes();
			}

			foreach (var text in texts)
			{
				writer.WriteField(text);
			}

			await Written.EndRecordAsync(writer, async);
			lag = Math.Max(lag, ((r + 1) * rowBytes) - stream.Count);
		}

		allocated = AllocatedBytes() - allocated;
		await Written.DisposeAsync(writer, async);
		if (async)
		{
			await workbook.DisposeAsync();
		}
		else
		{
			workbook.Dispose();
		}

		output.WriteLine($"{rows} rows of {rowBytes:N0} bytes of text: at most {lag:N0} bytes behind the stream; {allocated:N0} bytes allocated over the last {rows - (rows / 8)} rows; {stream.Count:N0} bytes in all.");
		Assert.True(stream.Count >= rows * rowBytes, $"{stream.Count:N0} bytes reached the stream, of {rows * rowBytes:N0}");

		// A flush (64 KiB), a record, and the zip's buffers, far from the 32 MiB that holding the rows would take.
		Assert.True(lag <= SheetSink.FlushSize + (2 * rowBytes) + MiB, $"{lag:N0} bytes behind");
#if NET
		if (!async)
		{
			Assert.True(allocated < MiB / 4, $"{allocated:N0} bytes allocated over the last rows");
		}
#endif
	}

	[Fact]
	public void KeepsAMillionDistinctSharedStringsInTheArena()
	{
		var table = new SharedStringTable();
		var item = new byte[32];
		long bytes = 0;
		var allocated = AllocatedBytes();
		for (var i = 0; i < Strings; i++)
		{
			var length = Item(i, item);
			if (table.IndexOf(item.AsSpan(0, length)) != i)
			{
				Assert.Fail($"String {i} has the index {table.IndexOf(item.AsSpan(0, length))}");
			}

			bytes += length;
		}

		allocated = AllocatedBytes() - allocated;
		output.WriteLine($"{Strings:N0} strings of {bytes:N0} bytes: the table holds {table.MemorySize:N0} bytes; growing it allocated {allocated:N0}.");
		Assert.Equal(Strings, table.Count);
		Assert.True(table.MemorySize <= (2 * bytes) + (32L * Strings), $"{table.MemorySize:N0} bytes for {bytes:N0} bytes of strings");
#if NET
		Assert.True(allocated <= (2 * table.MemorySize) + (64 << 10), $"{allocated:N0} bytes allocated for a table of {table.MemorySize:N0}");
#endif

		for (var i = 0; i < Strings; i += 99_991)
		{
			var length = Item(i, item);
			Assert.True(table[i].SequenceEqual(item.AsSpan(0, length)));
		}
	}

	[Fact]
	public void WritesAMillionDistinctSharedStringsInTheTablesMemory()
	{
		// The table the workbook's strings make, built alongside: the sink keeps each string's <t> element in it.
		var twin = new SharedStringTable();
		var item = new byte[32];
		long bytes = 0;
		for (var i = 0; i < Strings; i++)
		{
			var length = Item(i, item);
			twin.IndexOf(item.AsSpan(0, length));
			bytes += length;
		}

		var stream = new CountingStream();
		var allocated = AllocatedBytes();
		using (var workbook = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { Strings = ExcelStrings.Shared, Csv = new CsvWriterOptions { HasHeader = false } }))
		{
			using var writer = workbook.AddSheet("Strings");
			var text = new byte[16];
			text[0] = (byte)'s';
			for (var i = 0; i < Strings; i++)
			{
				Utf8Formatter.TryFormat(i, text.AsSpan(1), out var digits);
				writer.WriteField(new ReadOnlySpan<byte>(text, 0, 1 + digits));
				if (i % 1000 == 999)
				{
					writer.EndRecord();
				}
			}
		}

		allocated = AllocatedBytes() - allocated;
		output.WriteLine($"{Strings:N0} shared strings ({bytes:N0} bytes of XML, a table of {twin.MemorySize:N0}): {stream.Count:N0} bytes written, {allocated:N0} allocated.");
		Assert.Equal(Strings, twin.Count);
#if NET
		// The table's growth, and the writer's buffers and zip, which don't grow with the strings.
		Assert.True(allocated <= (2 * twin.MemorySize) + (8 * MiB), $"{allocated:N0} bytes allocated for a table of {twin.MemorySize:N0}");
#endif
	}

	/// <summary>A string's <c>t</c> element as the sink keeps it: <c>&lt;t&gt;s</c>, the number, <c>&lt;/t&gt;</c>.</summary>
	/// <param name="number">The string's number.</param>
	/// <param name="item">Where it goes.</param>
	/// <returns>Its length.</returns>
	private static int Item(int number, byte[] item)
	{
		"<t>s"u8.CopyTo(item);
		Utf8Formatter.TryFormat(number, item.AsSpan(4), out var digits);
		"</t>"u8.CopyTo(item.AsSpan(4 + digits));

		return 8 + digits;
	}

	/// <summary>The bytes this thread has allocated, on .NET; 0 on .NET Framework, which doesn't count them.</summary>
	/// <returns>The bytes.</returns>
	private static long AllocatedBytes()
	{
#if NET
		return GC.GetAllocatedBytesForCurrentThread();
#else
		return 0;
#endif
	}

	/// <summary>A stream that counts the bytes written to it and drops them, and can't seek, as a network stream can't.</summary>
	private sealed class CountingStream : Stream
	{
		/// <summary>The bytes written.</summary>
		public long Count { get; private set; }

		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => true;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush()
		{
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => Count += count;

#if NET
		/// <inheritdoc/>
		public override void Write(ReadOnlySpan<byte> buffer) => Count += buffer.Length;
#endif

		/// <inheritdoc/>
		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			Count += count;

			return Task.CompletedTask;
		}
	}
}
