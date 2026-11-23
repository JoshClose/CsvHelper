using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>A part of a test package: its name and bytes.</summary>
/// <param name="Name">The part's name.</param>
/// <param name="Bytes">Its bytes.</param>
internal sealed record ZipPart(string Name, byte[] Bytes);

/// <summary>
/// Packages written through <see cref="PackageWriter"/> for the zip writer's tests, with a <c>[Content_Types].xml</c>
/// last so that WindowsBase opens them, and read back through <see cref="ZipArchive"/>.
/// </summary>
internal static class ZipPackages
{
	/// <summary>The content types of the test packages' parts.</summary>
	public static readonly byte[] ContentTypes = Encoding.UTF8.GetBytes(
		"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
		+ "<Default Extension=\"bin\" ContentType=\"application/octet-stream\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");

	/// <summary>The inputs found whose data is exactly a length at a level (<see cref="InputOfData"/>).</summary>
	private static readonly ConcurrentDictionary<(CompressionLevel Level, int Length), byte[]> Inputs = new();

	/// <summary>Every level: deflated at each, and stored.</summary>
	public static IEnumerable<CompressionLevel> Levels
	{
		get
		{
			yield return CompressionLevel.Optimal;
			yield return CompressionLevel.Fastest;
			yield return CompressionLevel.NoCompression;
#if NET
			yield return CompressionLevel.SmallestSize;
#endif
		}
	}

	/// <summary>
	/// Writes a package: each part opened, written in pieces, and closed, then <c>[Content_Types].xml</c>, and the
	/// package disposed, synchronously (each call's task completed when it returns) or asynchronously. Every pooled buffer
	/// has returned at the end.
	/// </summary>
	/// <param name="parts">The parts.</param>
	/// <param name="level">The compression.</param>
	/// <param name="stream">The stream written to, left open.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="piece">The most bytes of a write to an entry.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for the default ones.</param>
	/// <returns>The bytes the stream holds.</returns>
	public static async Task<byte[]> WriteAsync(IEnumerable<ZipPart> parts, CompressionLevel level, ZipTestStream stream, bool async, int piece = int.MaxValue, ZipLimits? limits = null)
	{
		var package = new PackageWriter(stream, leaveOpen: true, level, limits ?? ZipLimits.Default);
		foreach (var part in parts.Append(new ZipPart("[Content_Types].xml", ContentTypes)))
		{
			var entry = await Packages.Run(async, a => package.OpenPartAsync(part.Name, a, CancellationToken.None));
			for (var at = 0; at < part.Bytes.Length; at += piece)
			{
				await WriteAsync(entry, part.Bytes, at, Math.Min(piece, part.Bytes.Length - at), async);
			}

			await RunAsync(async, a => PackageWriter.CloseAsync(entry, a));
		}

		await RunAsync(async, package.DisposeAsync);
		Assert.Equal(0, package.Zip.RentedBuffers);

		return stream.ToArray();
	}

	/// <summary>Writes a package to memory, seekable, synchronously.</summary>
	/// <param name="parts">The parts.</param>
	/// <param name="level">The compression.</param>
	/// <param name="limits">The limits, or <see langword="null"/> for the default ones.</param>
	/// <returns>The package.</returns>
	public static byte[] Write(IEnumerable<ZipPart> parts, CompressionLevel level, ZipLimits? limits = null)
	{
		var task = WriteAsync(parts, level, new ZipTestStream(), false, int.MaxValue, limits);
		Assert.True(task.IsCompleted);

		return task.GetAwaiter().GetResult();
	}

	/// <summary>
	/// Checks that <see cref="ZipArchive"/> reads a package's entries back, in order, each with the bytes written, and
	/// <c>[Content_Types].xml</c> last.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="parts">The parts written, before <c>[Content_Types].xml</c>.</param>
	public static void AssertReadsBack(byte[] package, IReadOnlyList<ZipPart> parts)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		Assert.Equal(parts.Select(p => p.Name).Append("[Content_Types].xml"), archive.Entries.Select(e => e.FullName));
		foreach (var part in parts)
		{
			var entry = archive.GetEntry(part.Name)!;
			Assert.Equal(part.Bytes.Length, entry.Length);
			Assert.True(part.Bytes.AsSpan().SequenceEqual(ZipWriterTests.ReadEntry(archive, part.Name)), $"{part.Name}: the bytes read back differ from those written.");
		}
	}

	/// <summary>
	/// An input whose data, deflated at a level (or stored), is exactly so many bytes: a prefix of seeded random bytes,
	/// found by bisection over the writer's own output, since deflate's output grows with its input there.
	/// </summary>
	/// <param name="level">The level.</param>
	/// <param name="length">The data's length.</param>
	/// <returns>The input.</returns>
	public static byte[] InputOfData(CompressionLevel level, int length)
	{
		return Inputs.GetOrAdd((level, length), key =>
		{
			if (key.Level == CompressionLevel.NoCompression)
			{
				return RandomBytes(key.Length, 1);
			}

			for (var seed = 1; seed <= 20; seed++)
			{
				var random = RandomBytes(key.Length + 4096, seed);
				long Data(int n) => ZipStructure.Read(Write([new ZipPart("p.bin", random.AsSpan(0, n).ToArray())], key.Level)).Entries[0].CompressedSize;
				var low = key.Length / 2;
				var high = random.Length;
				while (low < high)
				{
					var middle = (low + high) / 2;
					if (Data(middle) >= key.Length)
					{
						high = middle;
					}
					else
					{
						low = middle + 1;
					}
				}

				if (Data(low) == key.Length)
				{
					return random.AsSpan(0, low).ToArray();
				}
			}

			throw new InvalidOperationException($"No input deflates to {key.Length} bytes at {key.Level}.");
		});
	}

	/// <summary>Seeded random bytes, which don't deflate.</summary>
	/// <param name="length">How many.</param>
	/// <param name="seed">The seed.</param>
	/// <returns>The bytes.</returns>
	public static byte[] RandomBytes(int length, int seed)
	{
		var bytes = new byte[length];
		new Random(seed).NextBytes(bytes);

		return bytes;
	}

	/// <summary>Writes bytes to an entry's stream, synchronously or not.</summary>
	/// <param name="entry">The entry's stream.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="offset">The first.</param>
	/// <param name="count">How many.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when they are written.</returns>
	public static async Task WriteAsync(Stream entry, byte[] bytes, int offset, int count, bool async)
	{
		if (async)
		{
			await entry.WriteAsync(bytes, offset, count);
		}
		else
		{
			entry.Write(bytes, offset, count);
		}
	}

	/// <summary>
	/// Runs code written for both modes: asynchronously, or synchronously, where its task must have completed when it
	/// returns.
	/// </summary>
	/// <param name="async">Whether to run asynchronously.</param>
	/// <param name="run">The code, given the mode.</param>
	/// <returns>A task that completes when it has run.</returns>
	public static async Task RunAsync(bool async, Func<bool, ValueTask> run)
	{
		if (async)
		{
			await run(true);
		}
		else
		{
			SyncOrAsync.Wait(run(false));
		}
	}
}
