using System.Runtime.ExceptionServices;
using System.Text;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// Tests of <see cref="SegmentStream"/>: the pieces the pipeline inflated, then the part's stream, each read charging what
/// no stream counted; the failed read's exception after them; and on disposal, the unread bytes charged, the buffers
/// returned and the part's stream disposed.
/// </summary>
public sealed class SegmentStreamTests
{
	[Fact]
	public void ReadsThePiecesThenThePart()
	{
		using var part = new Part("CCCCCCCCCC");
		using var stream = part.Stream(("AAAAA", 3), ("BBBB", 0));
		Assert.Equal(100, stream.Offset);

		Assert.Equal("AAAAABBBBCCCCCCCCCC", Encoding.ASCII.GetString(SegmentRun.ReadAll(stream, 4)));
		Assert.Equal(2 + 4 + 10, part.Charged);
		Assert.Equal(0, stream.Read(new byte[4], 0, 4));
		part.Pool.AssertClean();
	}

	[Fact]
	public void ChargesEachReadAsItReadsIt()
	{
		using var part = new Part("CC");
		using var stream = part.Stream(("AAAA", 3), ("BB", 0));
		var buffer = new byte[1];

		// The first three bytes were counted by the serial window, before the pipeline began.
		long[] charged = [0, 0, 0, 1, 2, 3, 4, 5];
		foreach (var total in charged)
		{
			Assert.Equal(1, stream.Read(buffer, 0, 1));
			Assert.Equal(total, part.Charged);
		}

		Assert.Equal(0, stream.Read(buffer, 0, 1));
		Assert.Equal(5, part.Charged);
		Assert.True(stream.Read(buffer, 0, 0) == 0);
	}

	[Fact]
	public async Task ReadsAsynchronouslyAsItReadsSynchronously()
	{
		using var part = new Part("CCCCCC");
		using var stream = part.Stream(("AAAAA", 2), ("BBB", 0));
		var output = new MemoryStream();
		var buffer = new byte[3];
		int count;
		while ((count = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
		{
			output.Write(buffer, 0, count);
		}

		Assert.Equal("AAAAABBBCCCCCC", Encoding.ASCII.GetString(output.ToArray()));
		Assert.Equal(3 + 3 + 6, part.Charged);

#if NET
		using var other = new Part("CCCCCC");
		using var memory = other.Stream(("AAAAA", 2), ("BBB", 0));
		output.SetLength(0);
		while ((count = await memory.ReadAsync(buffer.AsMemory())) > 0)
		{
			output.Write(buffer, 0, count);
		}

		Assert.Equal("AAAAABBBCCCCCC", Encoding.ASCII.GetString(output.ToArray()));
		Assert.Equal(12, other.Charged);
		var canceled = new CancellationTokenSource();
		canceled.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => memory.ReadAsync(buffer.AsMemory(), canceled.Token).AsTask());
#endif
	}

	[Fact]
	public void ThrowsPastTheLimitAfterTheRead()
	{
		// The read that passes the limit throws, after its bytes are copied, as the part's own reads do.
		using var part = new Part("", limit: 4);
		using var stream = part.Stream(("AAAAAA", 0));
		var buffer = new byte[6];
		Assert.Equal(4, stream.Read(buffer, 0, 4));
		var e = Assert.Throws<InvalidDataException>(() => stream.Read(buffer, 4, 2));
		Assert.Equal($"Reading {SegmentRun.PartName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize (4 bytes uncompressed).", e.Message);
		Assert.Equal("AAAAAA", Encoding.ASCII.GetString(buffer));
	}

	[Fact]
	public void RethrowsTheFailedReadAfterThePieces()
	{
		using var part = new Part("CCCC");
		var failure = new InvalidDataException("The deflate stream is corrupt.");
		using var stream = part.Stream([("AAA", 0), ("BB", 0)], error: failure);

		Assert.Equal("AAABB", Encoding.ASCII.GetString(ReadSome(stream, 5)));
		Assert.Same(failure, Assert.Throws<InvalidDataException>(() => stream.Read(new byte[4], 0, 4)));
		Assert.Same(failure, Assert.Throws<InvalidDataException>(() => stream.Read(new byte[4], 0, 4)));
		Assert.Equal(0, part.Inner.Reads);
	}

	[Fact]
	public void EndsWhereThePartEnded()
	{
		using var part = new Part("CCCC");
		using var stream = part.Stream([("AAA", 0)], ended: true);
		Assert.Equal("AAA", Encoding.ASCII.GetString(SegmentRun.ReadAll(stream)));
		Assert.Equal(0, part.Inner.Reads);
	}

	[Fact]
	public void DisposingChargesWhatNoReadReached()
	{
		using var part = new Part("CCCC");
		var stream = part.Stream(("AAAAA", 2), ("BBBB", 1), ("DD", 0));
		Assert.Equal("AAAA", Encoding.ASCII.GetString(ReadSome(stream, 4)));
		Assert.Equal(2, part.Charged);

		// The rest of the pieces is charged, but for the bytes counted before; the part's own bytes were never inflated.
		stream.Dispose();
		Assert.Equal(1 + 3 + 2 + 2, part.Charged);
		Assert.True(part.Inner.Disposed);
		part.Pool.AssertClean();
		stream.Dispose();
		Assert.Equal(8, part.Charged);
		part.Pool.AssertClean();
		Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
	}

	[Fact]
	public void IsReadByAnXmlWindow()
	{
		// The window the serial read goes on with reads it as it reads a part: refills of its room, at the part's offsets.
		var random = new Random(5);
		var data = PipelineSheets.Part(random, 400);
		var from = PipelineSheets.RowStarts(data, Encoding.ASCII.GetBytes("<row"))[5];
		using var part = new Part(Encoding.ASCII.GetString(data, 4000, data.Length - 4000));
		var pieces = new List<(string, int)>();
		for (var at = from; at < 4000;)
		{
			var length = Math.Min(random.Next(1, 700), 4000 - at);
			pieces.Add((Encoding.ASCII.GetString(data, at, length), 0));
			at += length;
		}

		var window = new XmlWindow(part.Stream([.. pieces]), SegmentRun.PartName, 1 << 20, 16);
		var output = new List<byte>();
		window.Read();
		while (true)
		{
			output.AddRange(window.Buffer.Take(window.End));
			if (window.IsEof)
			{
				break;
			}

			window.Compact(window.End);
			window.Read();
		}

		window.Dispose();
		Assert.Equal(data.Skip(from), output);
		Assert.Equal(data.Length - from, part.Charged);
		Assert.True(part.Inner.Disposed);
		part.Pool.AssertClean();
	}

	/// <summary>Reads a number of bytes from a stream, in reads of one byte to the most it gives.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="length">The number of bytes.</param>
	/// <returns>The bytes.</returns>
	private static byte[] ReadSome(Stream stream, int length)
	{
		var output = new byte[length];
		var read = 0;
		while (read < length)
		{
			var count = stream.Read(output, read, length - read);
			Assert.True(count > 0);
			read += count;
		}

		return output;
	}

	/// <summary>A part's stream after the pieces, with a guard and a checking pool for the pieces' buffers.</summary>
	private sealed class Part : IDisposable
	{
		private readonly PackageArchive archive;
		private readonly LimitedReadStream stream;

		/// <summary>Creates the part.</summary>
		/// <param name="rest">The part's bytes after the pieces.</param>
		/// <param name="limit">The workbook's limit.</param>
		public Part(string rest, long limit = 1L << 40)
		{
			archive = SyncOrAsync.Result(PackageArchive.OpenAsync(TestPackage.Workbook("Data").ToStream(), false, Packages.Limits, false, CancellationToken.None));
			Inner = new GatedStream(Encoding.ASCII.GetBytes(rest));
			stream = new LimitedReadStream(Inner, archive, SegmentRun.PartName, long.MaxValue, limit);
		}

		/// <summary>The part's inner stream.</summary>
		public GatedStream Inner { get; }

		/// <summary>The pool the pieces' buffers come from.</summary>
		public CheckingPool Pool { get; } = new();

		/// <summary>The workbook's total.</summary>
		public long Charged => archive.Guard.Total;

		/// <summary>A stream of pieces, from offset 100, then the part.</summary>
		/// <param name="pieces">Each piece's text, and its bytes counted already.</param>
		/// <returns>The stream.</returns>
		public SegmentStream Stream(params (string Text, int Counted)[] pieces) => Stream(pieces, null, false);

		/// <summary>A stream of pieces, from offset 100, then the part, or an error, or nothing.</summary>
		/// <param name="pieces">Each piece's text, and its bytes counted already.</param>
		/// <param name="error">The exception after the pieces, or <see langword="null"/>.</param>
		/// <param name="ended">Whether nothing follows the pieces.</param>
		/// <returns>The stream.</returns>
		public SegmentStream Stream((string Text, int Counted)[] pieces, Exception? error = null, bool ended = false)
		{
			var made = pieces.Select(p =>
			{
				var buffer = Pool.Rent(p.Text.Length);
				Encoding.ASCII.GetBytes(p.Text, 0, p.Text.Length, buffer, 0);

				return new SegmentStream.Piece(buffer, p.Text.Length, p.Counted);
			}).ToArray();

			return new SegmentStream(stream, Pool, made, 100, error is null ? null : ExceptionDispatchInfo.Capture(error), ended);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			stream.Dispose();
			archive.Dispose();
		}
	}
}
