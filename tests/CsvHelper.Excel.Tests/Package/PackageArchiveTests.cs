using System.IO.Compression;
using System.Text;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>The zip archive of a workbook: its input, signature, temporary copy, entries by part name and limits.</summary>
public class PackageArchiveTests
{
	private static readonly byte[] CompoundFile = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0, 0, 0, 0, 0];

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task OpensAZipArchive(bool async, bool seekable)
	{
		var bytes = TestPackage.Workbook("Data").ToArray();
		using var temp = new TempDirectory();

		using var archive = await Open(bytes, seekable, async, temp.Path);

		Assert.True(archive.Contains("xl/workbook.xml"));
		Assert.True(archive.Contains("[Content_Types].xml"));
		Assert.False(archive.Contains("xl/missing.xml"));
		Assert.Equal(6, archive.PartNames.Count());
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task ChecksTheSignature(bool async, bool seekable)
	{
		var empty = new MemoryStream();
		using (new ZipArchive(empty, ZipArchiveMode.Create, true))
		{
		}

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Open(empty.ToArray(), seekable, async));
		Assert.Equal("The input is an empty zip archive, which has no workbook.", e.Message);

		var notSupported = await Assert.ThrowsAsync<NotSupportedException>(() => Open(CompoundFile, seekable, async));
		Assert.StartsWith("The input is a compound file, as password-protected workbooks and Excel 97-2003 workbooks (.xls) are.", notSupported.Message);

		foreach (var bytes in new[] { Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><workbook/>"), [], [0x50, 0x4B], [0x50, 0x4B, 0x07, 0x08, 1, 2, 3, 4] })
		{
			e = await Assert.ThrowsAsync<InvalidDataException>(() => Open(bytes, seekable, async));
			Assert.Equal("The input is not a workbook: an .xlsx workbook is a zip archive, which starts with 'PK'.", e.Message);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsASeekableStreamFromItsStartAndLeavesItOpenIfAsked(bool async)
	{
		var stream = TestPackage.Workbook("Data").ToStream();
		stream.Position = stream.Length;

		var archive = await Packages.Run(async, a => PackageArchive.OpenAsync(stream, true, Packages.Limits, a, CancellationToken.None));
		archive.Dispose();
		Assert.True(stream.CanRead);

		archive = await Packages.Run(async, a => PackageArchive.OpenAsync(stream, false, Packages.Limits, a, CancellationToken.None));
		await archive.DisposeAsync();
		Assert.False(stream.CanRead);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task CopiesANonSeekableStreamToATemporaryFile(bool async, bool leaveOpen)
	{
		var bytes = TestPackage.Workbook("Data").ToArray();
		using var temp = new TempDirectory();
		var input = new TrickleStream(bytes, 1000);

		var archive = await Packages.Run(async, a => PackageArchive.OpenAsync(input, leaveOpen, Packages.Limits, a, CancellationToken.None, temp.Path));

		Assert.Equal(bytes.Length, input.Delivered);
		Assert.Equal(!leaveOpen, input.Disposed);
		Assert.Equal(bytes.Length, archive.Guard.Total);
		// Windows deletes the file when it closes; elsewhere it is deleted at once.
		Assert.Equal(Path.DirectorySeparatorChar == '\\' ? 1 : 0, temp.Files.Length);
		Assert.Contains("<sheetData/>", await ReadPartTextAsync(archive, "xl/worksheets/sheet1.xml", async));
		if (async)
		{
			await archive.DisposeAsync();
		}
		else
		{
			archive.Dispose();
		}

		Assert.Empty(temp.Files);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsCopyingAtTheLimitAndDeletesTheCopy(bool async)
	{
		var bytes = TestPackage.Workbook("Data").ToArray();
		using var temp = new TempDirectory();
		var input = new TrickleStream(bytes, 100);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() =>
			Packages.Run(async, a => PackageArchive.OpenAsync(input, false, new PartLimits(1000, 500), a, CancellationToken.None, temp.Path)));

		Assert.Equal("The input stream is longer than ExcelReaderOptions.MaxUncompressedSize (500 bytes), which bounds its copy to a temporary file too.", e.Message);
		// The copy stops at the read that passes the limit: the 8 bytes of the signature, then five reads of 100.
		Assert.Equal(508, input.Delivered);
		Assert.Empty(temp.Files);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task OpensAPathThatOthersCanReadWriteAndReplace(bool async)
	{
		using var temp = new TempDirectory();
		var path = Path.Combine(temp.Path, "book.xlsx");
		File.WriteAllBytes(path, TestPackage.Workbook("Data").ToArray());

		using var archive = await Packages.Run(async, a => PackageArchive.OpenAsync(path, Packages.Limits, a, CancellationToken.None));

		// What Excel does: open the file for writing, and save by replacing it with a new file.
		using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
		{
		}

		var saved = Path.Combine(temp.Path, "saved.xlsx");
		var backup = Path.Combine(temp.Path, "backup.xlsx");
		File.WriteAllBytes(saved, TestPackage.Workbook("Other").ToArray());
		File.Move(path, backup);
		File.Move(saved, path);
		File.Delete(backup);

		// The archive goes on reading the file it opened.
		Assert.Contains("name=\"Data\"", await ReadPartTextAsync(archive, "xl/workbook.xml", async));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AMissingPathThrows(bool async)
	{
		using var temp = new TempDirectory();

		await Assert.ThrowsAsync<FileNotFoundException>(() =>
			Packages.Run(async, a => PackageArchive.OpenAsync(Path.Combine(temp.Path, "none.xlsx"), Packages.Limits, a, CancellationToken.None)));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FindsPartsByNormalizedName(bool async)
	{
		var package = new TestPackage()
			.Set("xl\\worksheets\\sheet1.xml", "<a>backslashes</a>")
			.Set("/xl/styles.xml", "<a>leading slash</a>")
			.Set("XL/Workbook.XML", "<a>case</a>")
			.Set("xl/worksheets/sheet%202.xml", "<a>percent</a>")
			.Set("xl/media/", "")
			.Add("xl/dup.xml", "<a>first</a>")
			.Add("xl/dup.xml", "<a>second</a>");
		var bytes = package.ToArray();
		using (var raw = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
		{
			// The zip keeps the names as given, so the archive is what normalizes them.
			Assert.Contains("xl\\worksheets\\sheet1.xml", raw.Entries.Select(e => e.FullName));
			Assert.Contains("/xl/styles.xml", raw.Entries.Select(e => e.FullName));
		}

		using var archive = await Packages.Run(async, a => PackageArchive.OpenAsync(new MemoryStream(bytes), false, Packages.Limits, a, CancellationToken.None));

		Assert.Equal("<a>backslashes</a>", await ReadPartTextAsync(archive, "xl/worksheets/sheet1.xml", async));
		Assert.Equal("<a>leading slash</a>", await ReadPartTextAsync(archive, "xl/styles.xml", async));
		Assert.Equal("<a>case</a>", await ReadPartTextAsync(archive, "xl/workbook.xml", async));
		Assert.Equal("<a>percent</a>", await ReadPartTextAsync(archive, "xl/worksheets/sheet 2.xml", async));
		Assert.Equal("<a>first</a>", await ReadPartTextAsync(archive, "xl/dup.xml", async));
		Assert.False(archive.Contains("xl/media/"));
		Assert.False(archive.Contains("xl/media"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AMissingPartThrows(bool async)
	{
		using var archive = await TestPackage.Workbook("Data").OpenAsync(async);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadPartTextAsync(archive, "xl/sharedStrings.xml", async));
		Assert.Equal("The workbook has no part xl/sharedStrings.xml.", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsAHeldPartThatDeclaresMoreThanTheLimitBeforeInflatingIt(bool async)
	{
		var xml = "<a>" + new string('x', 1000) + "</a>";
		using var archive = await new TestPackage().Set("xl/workbook.xml", xml).OpenAsync(async);
		var limits = new PartLimits(1000, 1L << 30);

		var e = await Assert.ThrowsAsync<InvalidDataException>(() => Packages.Run(async, a => archive.OpenPartAsync("xl/workbook.xml", true, limits, a, CancellationToken.None)));
		Assert.Equal("xl/workbook.xml is larger than ExcelReaderOptions.MaxPartSize (1000 bytes uncompressed): its size in the zip is 1007 bytes.", e.Message);
		Assert.Equal(0, archive.Guard.Total);

		// A streamed part (a sheet) isn't bounded as a whole; and a held one at the limit reads.
		Assert.Equal(xml, await ReadPartTextAsync(archive, "xl/workbook.xml", async, held: false, maxPartSize: 1000));
		Assert.Equal(xml, await ReadPartTextAsync(archive, "xl/workbook.xml", async, held: true, maxPartSize: 1007));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsTheBytesOfAllPartsTowardTheLimitOfEachCall(bool async)
	{
		var package = new TestPackage().Set("a.xml", new string('a', 600), CompressionLevel.NoCompression).Set("b.xml", new string('b', 600));
		using var archive = await package.OpenAsync(async);

		await ReadPartTextAsync(archive, "a.xml", async, maxUncompressedSize: 1000);
		Assert.Equal(600, archive.Guard.Total);

		// The total so far counts against the limit of the next call, whatever that limit is.
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => ReadPartTextAsync(archive, "b.xml", async, maxUncompressedSize: 1000));
		Assert.Equal("Reading b.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize (1000 bytes uncompressed).", e.Message);
		Assert.Equal(new string('b', 600), await ReadPartTextAsync(archive, "b.xml", async, maxUncompressedSize: 10_000));
	}

	[Fact]
	public void CountsWhatAPartReallyInflatesTo()
	{
		// What a lying zip header would declare is not what counts: the stream counts the bytes it reads.
		using var archive = TestPackageArchive();
		using var held = new LimitedReadStream(new MemoryStream(new byte[100]), archive, "xl/styles.xml", 99, 1000);
		var buffer = new byte[60];

		Assert.Equal(60, held.Read(buffer, 0, 60));
		var e = Assert.Throws<InvalidDataException>(() => held.Read(buffer, 0, 60));
		Assert.Equal("xl/styles.xml is larger than ExcelReaderOptions.MaxPartSize (99 bytes uncompressed).", e.Message);

		// 60 counted so far, and a sheet of 1000 under a limit of 1000: the read that passes it throws.
		Assert.Equal(60, archive.Guard.Total);
		using var streamed = new LimitedReadStream(new MemoryStream(new byte[1000]), archive, "xl/worksheets/sheet1.xml", long.MaxValue, 1000);
		var reads = 0;
		e = Assert.Throws<InvalidDataException>(() =>
		{
			while (streamed.Read(new byte[300], 0, 300) > 0)
			{
				reads++;
			}
		});
		Assert.Equal(3, reads);
		Assert.Equal(1060, archive.Guard.Total);
		Assert.Equal("Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize (1000 bytes uncompressed).", e.Message);
	}

	[Fact]
	public void CountsBytesThatWereNotInflated()
	{
		using var archive = TestPackageArchive();
		using var sheet = new LimitedReadStream(new MemoryStream(), archive, "xl/worksheets/sheet1.xml", long.MaxValue, 100);

		sheet.Charge(100);
		var e = Assert.Throws<InvalidDataException>(() => sheet.Charge(1));
		Assert.Equal("Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize (100 bytes uncompressed).", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PartsOfADisposedArchiveThrow(bool async)
	{
		var archive = await TestPackage.Workbook("Data").OpenAsync(async);
		using var stream = await Packages.Run(async, a => archive.OpenPartAsync("xl/workbook.xml", true, Packages.Limits, a, CancellationToken.None));
		archive.Dispose();
		archive.Dispose();

		Assert.True(archive.IsDisposed);
		Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[10], 0, 10));
		await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.ReadAsync(new byte[10], 0, 10));
		await Assert.ThrowsAsync<ObjectDisposedException>(() => Packages.Run(async, a => archive.OpenPartAsync("xl/workbook.xml", true, Packages.Limits, a, CancellationToken.None)));
	}

	[Fact]
	public async Task ChecksItsArguments()
	{
		await Assert.ThrowsAsync<ArgumentNullException>(() => PackageArchive.OpenAsync((Stream)null!, false, Packages.Limits, false, CancellationToken.None).AsTask());
		await Assert.ThrowsAsync<ArgumentNullException>(() => PackageArchive.OpenAsync((string)null!, Packages.Limits, false, CancellationToken.None).AsTask());
		var writeOnly = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.DeleteOnClose);
		using (writeOnly)
		{
			await Assert.ThrowsAsync<ArgumentException>(() => PackageArchive.OpenAsync(writeOnly, false, Packages.Limits, false, CancellationToken.None).AsTask());
		}

		Assert.Throws<ArgumentOutOfRangeException>(() => new PartLimits(0, 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new PartLimits(1, -1));
	}

	/// <summary>Opens a package from bytes.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="seekable">Whether to read them from a seekable stream, rather than one that needs a temporary copy.</param>
	/// <param name="async">Whether to open asynchronously.</param>
	/// <param name="tempDirectory">The folder of the temporary copy.</param>
	/// <returns>The archive.</returns>
	private static Task<PackageArchive> Open(byte[] bytes, bool seekable, bool async, string? tempDirectory = null)
	{
		Stream input = seekable ? new MemoryStream(bytes) : new TrickleStream(bytes, 7);

		return Packages.Run(async, a => PackageArchive.OpenAsync(input, false, Packages.Limits, a, CancellationToken.None, tempDirectory));
	}

	/// <summary>An archive to give <see cref="LimitedReadStream"/>s a guard.</summary>
	/// <returns>The archive.</returns>
	private static PackageArchive TestPackageArchive() =>
		SyncOrAsync.Result(PackageArchive.OpenAsync(TestPackage.Workbook("Data").ToStream(), false, Packages.Limits, false, CancellationToken.None));

	/// <summary>Reads a part as UTF-8 text through the archive's limited stream.</summary>
	/// <param name="archive">The archive.</param>
	/// <param name="partName">The part.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="held">Whether the part is held in memory.</param>
	/// <param name="maxPartSize">The part limit.</param>
	/// <param name="maxUncompressedSize">The workbook limit.</param>
	/// <returns>The text.</returns>
	private static async Task<string> ReadPartTextAsync(
		PackageArchive archive, string partName, bool async, bool held = true, long maxPartSize = 1 << 20, long maxUncompressedSize = 1L << 30)
	{
		var limits = new PartLimits(maxPartSize, maxUncompressedSize);
		using var stream = await Packages.Run(async, a => archive.OpenPartAsync(partName, held, limits, a, CancellationToken.None));
		var output = new MemoryStream();
		var buffer = new byte[100];
		int count;
		while ((count = async ? await stream.ReadAsync(buffer, 0, buffer.Length) : stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			output.Write(buffer, 0, count);
		}

		return Encoding.UTF8.GetString(output.ToArray());
	}

	/// <summary>A new, empty folder for temporary files, deleted with whatever is in it when disposed.</summary>
	private sealed class TempDirectory : IDisposable
	{
		/// <summary>Creates the folder.</summary>
		public TempDirectory() => Directory.CreateDirectory(Path);

		/// <summary>The folder's path.</summary>
		public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CsvHelper.Excel.Tests." + Guid.NewGuid().ToString("N"));

		/// <summary>The files in the folder.</summary>
		public string[] Files => Directory.GetFiles(Path);

		/// <inheritdoc/>
		public void Dispose() => Directory.Delete(Path, true);
	}
}
