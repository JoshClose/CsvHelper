using System.IO.Compression;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// A stream that fails mid-package: at a local header, in the middle of a streamed entry (a stored one's writes straight
/// through too), at a descriptor, at the directory, at the end records, at its flush or at its dispose; or that cancels a
/// write it was given. The stream's exception reaches the caller; from then on the writer writes nothing and throws
/// nothing, whatever is opened, written, closed or disposed, so that a dispose doesn't replace the stream's exception;
/// and every pooled buffer returns.
/// </summary>
public class ZipFailureTests
{
	/// <summary>Where the stream fails, synchronously and asynchronously.</summary>
	public static TheoryData<string, bool> Points()
	{
		var data = new TheoryData<string, bool>();
		foreach (var point in new[] { "local header", "streamed data", "descriptor", "whole data", "directory", "end records", "flush", "dispose" })
		{
			data.Add(point, false);
			data.Add(point, true);
		}

		return data;
	}

	/// <summary>
	/// Where a stored package's stream fails as its streamed entry is written, synchronously and asynchronously, leaving
	/// the stream open or not.
	/// </summary>
	/// <returns>The points, the modes, and whether the stream is left open.</returns>
	public static TheoryData<string, bool, bool> StoredPoints()
	{
		var data = new TheoryData<string, bool, bool>();
		foreach (var point in new[] { "streamed header", "held buffer", "held rest", "first write through", "later write through", "descriptor" })
		{
			foreach (var async in new[] { false, true })
			{
				data.Add(point, async, false);
				data.Add(point, async, true);
			}
		}

		return data;
	}

	/// <summary>
	/// Where an asynchronous write is canceled as it reaches the stream: a deflated entry's buffer, the buffer a stored
	/// entry held as it was streamed, and a stored entry's writes straight through; leaving the stream open or not.
	/// </summary>
	/// <returns>The points, and whether the stream is left open.</returns>
	public static TheoryData<string, bool> CancelPoints()
	{
		var data = new TheoryData<string, bool>();
		foreach (var point in new[] { "deflated buffer", "held buffer", "first write through", "later write through" })
		{
			data.Add(point, false);
			data.Add(point, true);
		}

		return data;
	}

	/// <summary>Where the stream fails under the workbook writer, synchronously and asynchronously.</summary>
	public static TheoryData<string, bool> WorkbookPoints()
	{
		var data = new TheoryData<string, bool>();
		foreach (var point in new[] { "first write", "middle write", "last write" })
		{
			data.Add(point, false);
			data.Add(point, true);
		}

		return data;
	}

	/// <summary>
	/// The package's calls after the stream fails: the entry left open written and closed, another opened (while the first
	/// is still open, too), written and closed, and the package disposed twice: nothing more reaches the stream, nothing
	/// throws, the stream is disposed and every buffer has returned.
	/// </summary>
	/// <param name="point">Where the stream fails.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Points))]
	public async Task WritesNothingMoreOnceTheStreamFails(string point, bool async)
	{
		var plain = new ZipTestStream(false);
		await WritePackageAsync(new PackageWriter(plain, leaveOpen: false, CompressionLevel.Fastest), async, _ => { });
		var sizes = plain.Writes.Select(w => w.Size).ToList();

		// Writes from 1: a.xml whole (header, data), b.bin streamed (header, its buffers, its last, the descriptor), c.xml
		// whole, the directory and the end records.
		var failAt = point switch
		{
			"local header" => 1,
			"whole data" => 2,
			"streamed data" => 5,
			"descriptor" => sizes.IndexOf(24) + 1,
			"directory" => sizes.Count - 1,
			"end records" => sizes.Count,
			_ => int.MaxValue,
		};
		var stream = new ZipTestStream(false) { FailAt = failAt, FailFlush = point == "flush", FailDispose = point == "dispose" };
		var package = new PackageWriter(stream, leaveOpen: false, CompressionLevel.Fastest);
		Stream? open = null;

		var caught = await Record(() => WritePackageAsync(package, async, entry => open = entry));

		Assert.Same(stream.Failure, caught);
		Assert.True(package.Zip.Faulted);
		Assert.Equal(0, package.Zip.RentedBuffers);
		Assert.Equal(failAt == int.MaxValue ? sizes.Count : failAt, stream.Attempts);
		await AssertWritesNothingMoreAsync(package, stream, open, async);
		Assert.True(stream.Disposed);
	}

	/// <summary>
	/// A stored package's stream that fails as its streamed entry is written: at its local header, at the full buffer and
	/// the rest the entry held as it was streamed, at a write that goes straight through and at a later one, and at its
	/// descriptor. As for a deflated entry's buffer, the stream's exception reaches the caller, and from then on nothing
	/// more is written or thrown; every buffer returns, and the stream is disposed unless it was left open.
	/// </summary>
	/// <param name="point">Where the stream fails.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="leaveOpen">Whether the package leaves the stream open.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(StoredPoints))]
	public async Task WritesNothingMoreOnceAStoredEntrysWriteFails(string point, bool async, bool leaveOpen)
	{
		// Writes from 1: a.xml whole (header, data); b.bin's header and, as its third piece of 30,000 bytes passes one
		// buffer, the full buffer and the 24,464 bytes held; then each piece straight through, and the descriptor.
		var (failAt, size) = point switch
		{
			"streamed header" => (3, 30 + 5 + 20),
			"held buffer" => (4, 65_536),
			"held rest" => (5, 24_464),
			"first write through" => (6, 30_000),
			"later write through" => (9, 30_000),
			_ => (13, 24),
		};
		var stream = new ZipTestStream(false) { FailAt = failAt };
		var package = new PackageWriter(stream, leaveOpen, CompressionLevel.NoCompression);
		Stream? open = null;

		var caught = await Record(() => WritePackageAsync(package, async, entry => open = entry));

		Assert.Same(stream.Failure, caught);
		Assert.True(package.Zip.Faulted);
		Assert.Equal(0, package.Zip.RentedBuffers);
		Assert.Equal(failAt, stream.Attempts);
		await AssertWritesNothingMoreAsync(package, stream, open, async);
		Assert.Equal(!leaveOpen, stream.Disposed);
		Assert.Equal(size, await SizeOfWriteAsync(CompressionLevel.NoCompression, failAt));
	}

	/// <summary>
	/// An asynchronous write canceled while the stream writes it, after the entry has taken its bytes: the stream's
	/// <see cref="OperationCanceledException"/> reaches the caller and faults the writer, as any failure of the stream
	/// does, since the entry can't take its bytes back; from then on nothing more is written or thrown, every buffer
	/// returns, and the stream is disposed unless it was left open. A stored entry's write straight through behaves as a
	/// deflated entry's buffer does, and as the buffer a stored entry held does.
	/// </summary>
	/// <param name="point">The write canceled.</param>
	/// <param name="leaveOpen">Whether the package leaves the stream open.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(CancelPoints))]
	public async Task FaultsTheWriterWhenTheStreamCancelsAWrite(string point, bool leaveOpen)
	{
		// Writes from 1: a.xml whole (header, data); b.bin's header and its first buffer, deflated or held; and stored,
		// the rest it held, then each piece straight through.
		var (level, cancelAt, size) = point switch
		{
			"deflated buffer" => (CompressionLevel.Fastest, 4, 65_536),
			"held buffer" => (CompressionLevel.NoCompression, 4, 65_536),
			"first write through" => (CompressionLevel.NoCompression, 6, 30_000),
			_ => (CompressionLevel.NoCompression, 9, 30_000),
		};
		using var source = new CancellationTokenSource();
		var stream = new ZipTestStream(false) { Canceled = source, CancelAt = cancelAt };
		var package = new PackageWriter(stream, leaveOpen, level);
		Stream? open = null;

		var caught = await Record(() => WritePackageAsync(package, true, entry => open = entry, source.Token));

		var canceled = Assert.IsAssignableFrom<OperationCanceledException>(caught);
		Assert.Equal(source.Token, canceled.CancellationToken);
		Assert.True(package.Zip.Faulted);
		Assert.Equal(0, package.Zip.RentedBuffers);
		Assert.Equal(cancelAt - 1, stream.Attempts);
		Assert.NotNull(open);
		await AssertWritesNothingMoreAsync(package, stream, open, true);
		Assert.Equal(!leaveOpen, stream.Disposed);
		Assert.Equal(size, await SizeOfWriteAsync(level, cancelAt));
	}

	/// <summary>
	/// A stream that fails under the workbook writer, at its first write (a sheet's streamed header), in the middle of a
	/// sheet and at the end records: the stream's exception is the one the caller gets, once, from the call that failed;
	/// writing another sheet after it and disposing the workbook writer throw nothing more and write nothing more; the
	/// stream is disposed.
	/// </summary>
	/// <param name="point">Where the stream fails.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(WorkbookPoints))]
	public async Task TheWorkbookWritersDisposeThrowsNothingMore(string point, bool async)
	{
		var plain = new ZipTestStream(false);
		await WriteWorkbookAsync(plain, async);
		var writes = plain.Writes.Count;
		var failAt = point switch
		{
			"first write" => 1,
			"middle write" => writes / 2,
			_ => writes,
		};
		var stream = new ZipTestStream(false) { FailAt = failAt };

		var (inside, outside) = await WriteWorkbookAsync(stream, async);

		Assert.Same(stream.Failure, inside ?? outside);
		Assert.True(inside is null || outside is null, $"Two exceptions reached the caller: {inside} and {outside}.");
		Assert.Equal(failAt, stream.Attempts);
		Assert.True(stream.Disposed);
	}

	/// <summary>
	/// The package's calls after the stream fails: the entry left open written and closed, another opened (while the first
	/// is still open, too), written and closed, and the package disposed twice: nothing more reaches the stream, nothing
	/// throws, and every buffer has returned.
	/// </summary>
	/// <param name="package">The package, faulted.</param>
	/// <param name="stream">Its stream.</param>
	/// <param name="open">The entry left open, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the checks are done.</returns>
	private static async Task AssertWritesNothingMoreAsync(PackageWriter package, ZipTestStream stream, Stream? open, bool async)
	{
		var attempts = stream.Attempts;
		var bytes = ZipPackages.RandomBytes(200_000, 2);
		var after = await package.OpenPartAsync("after.xml", async, CancellationToken.None);
		await ZipPackages.WriteAsync(after, bytes, 0, bytes.Length, async);
		if (open is not null)
		{
			await ZipPackages.WriteAsync(open, bytes, 0, bytes.Length, async);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(open, a));
		}

		await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(after, a));
		await ZipPackages.RunAsync(async, package.DisposeAsync);
		await ZipPackages.RunAsync(async, package.DisposeAsync);
		Assert.Equal(attempts, stream.Attempts);
		Assert.Equal(0, package.Zip.RentedBuffers);
	}

	/// <summary>
	/// The size of a write of the test package (<see cref="WritePackageAsync"/>) written whole, asynchronously, to a stream
	/// that doesn't fail: so that a test's point is the write it means.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <param name="write">The write, from 1.</param>
	/// <returns>Its size.</returns>
	private static async Task<int> SizeOfWriteAsync(CompressionLevel level, int write)
	{
		var plain = new ZipTestStream(false);
		await WritePackageAsync(new PackageWriter(plain, leaveOpen: false, level), true, _ => { });

		return plain.Writes[write - 1].Size;
	}

	/// <summary>
	/// Writes a test package: <c>a.xml</c> whole, <c>b.bin</c> streamed in pieces, <c>c.xml</c> whole, then disposes it.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="opened">Told of each entry opened, and of <see langword="null"/> before it is closed.</param>
	/// <param name="cancellationToken">Cancels the asynchronous writes.</param>
	/// <returns>A task that completes when the package is disposed.</returns>
	private static async Task WritePackageAsync(PackageWriter package, bool async, Action<Stream?> opened, CancellationToken cancellationToken = default)
	{
		foreach (var (name, bytes) in new[] { ("a.xml", ZipWriterTests.Bytes(5_000, 1)), ("b.bin", ZipPackages.RandomBytes(300_000, 3)), ("c.xml", ZipWriterTests.Bytes(3_000, 4)) })
		{
			var entry = await Packages.Run(async, a => package.OpenPartAsync(name, a, CancellationToken.None));
			opened(entry);
			for (var at = 0; at < bytes.Length; at += 30_000)
			{
				await ZipPackages.WriteAsync(entry, bytes, at, Math.Min(30_000, bytes.Length - at), async, cancellationToken);
			}

			opened(null);
			await ZipPackages.RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		}

		await ZipPackages.RunAsync(async, package.DisposeAsync);
	}

	/// <summary>
	/// Writes a workbook in a <c>using</c> block: a sheet of 20,000 records, whose exception is caught inside the block,
	/// then a small sheet, then the dispose.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The exception caught inside the block, and the one the block threw.</returns>
	private static async Task<(Exception? Inside, Exception? Outside)> WriteWorkbookAsync(ZipTestStream stream, bool async)
	{
		var records = Enumerable.Range(0, 20_000).Select(i => new WrittenText { Name = $"name {i}", Note = $"note {i * 7919 % 10007}" }).ToArray();
		var few = new[] { new WrittenText { Name = "a" } };
		Exception? inside = null;
		var outside = await Record(async () =>
		{
			if (async)
			{
				await using var book = ExcelWorkbookWriter.Create(stream);
				inside = await Record(() => book.WriteSheetAsync("Orders", records).AsTask());
				await book.WriteSheetAsync("More", few);
			}
			else
			{
				using var book = ExcelWorkbookWriter.Create(stream);
				inside = await Record(() =>
				{
					book.WriteSheet("Orders", records);

					return Task.CompletedTask;
				});
				book.WriteSheet("More", few);
			}
		});

		return (inside, outside);
	}

	/// <summary>Runs code, returning the exception it throws.</summary>
	/// <param name="run">The code.</param>
	/// <returns>The exception, or <see langword="null"/>.</returns>
	private static async Task<Exception?> Record(Func<Task> run)
	{
		try
		{
			await run();

			return null;
		}
		catch (Exception e)
		{
			return e;
		}
	}
}
