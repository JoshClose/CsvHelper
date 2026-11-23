using System.IO.Compression;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// A stream that fails mid-package: at a local header, in the middle of a streamed entry, at a descriptor, at the
/// directory, at the end records, at its flush or at its dispose. The stream's exception reaches the caller; from then on
/// the writer writes nothing and throws nothing, whatever is opened, written, closed or disposed, so that a dispose
/// doesn't replace the stream's exception; and every pooled buffer returns.
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
		var attempts = stream.Attempts;
		Assert.Equal(failAt == int.MaxValue ? sizes.Count : failAt, attempts);
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
		Assert.True(stream.Disposed);
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
	/// Writes a test package: <c>a.xml</c> whole, <c>b.bin</c> streamed in pieces, <c>c.xml</c> whole, then disposes it.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="opened">Told of each entry opened, and of <see langword="null"/> before it is closed.</param>
	/// <returns>A task that completes when the package is disposed.</returns>
	private static async Task WritePackageAsync(PackageWriter package, bool async, Action<Stream?> opened)
	{
		foreach (var (name, bytes) in new[] { ("a.xml", ZipWriterTests.Bytes(5_000, 1)), ("b.bin", ZipPackages.RandomBytes(300_000, 3)), ("c.xml", ZipWriterTests.Bytes(3_000, 4)) })
		{
			var entry = await Packages.Run(async, a => package.OpenPartAsync(name, a, CancellationToken.None));
			opened(entry);
			for (var at = 0; at < bytes.Length; at += 30_000)
			{
				await ZipPackages.WriteAsync(entry, bytes, at, Math.Min(30_000, bytes.Length - at), async);
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
