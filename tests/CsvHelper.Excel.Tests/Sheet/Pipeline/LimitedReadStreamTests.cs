using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// Tests of what <see cref="LimitedReadStream"/> gives the pipeline: reads that count nothing toward the workbook's total
/// but check the part's limit and the workbook, what the limit has left, and a charge that says instead of throwing.
/// </summary>
public sealed class LimitedReadStreamTests
{
	[Fact]
	public void ReadsUncountedWithinThePartsLimit()
	{
		using var archive = Archive();
		using var stream = new LimitedReadStream(new MemoryStream(new byte[100]), archive, "xl/styles.xml", 99, 1000);
		var buffer = new byte[60];

		// Uncounted toward the total, counted toward the part's limit, with the message a counted read gives.
		Assert.Equal(60, stream.ReadUncounted(buffer, 0, 60));
		Assert.Equal(0, archive.Guard.Total);
		Assert.Equal(30, stream.Read(buffer, 0, 30));
		Assert.Equal(30, archive.Guard.Total);
		var e = Assert.Throws<InvalidDataException>(() => stream.ReadUncounted(buffer, 0, 60));
		Assert.Equal("xl/styles.xml is larger than ExcelReaderOptions.MaxPartSize (99 bytes uncompressed).", e.Message);
		Assert.Equal(30, archive.Guard.Total);
		Assert.Throws<InvalidDataException>(() => stream.ReadUncounted(buffer, 0, 60));
		Assert.Throws<InvalidDataException>(() => stream.Read(buffer, 0, 60));
	}

	[Fact]
	public void ReadsUncountedOnlyWhileTheWorkbookIsOpen()
	{
		var archive = Archive();
		using var stream = new LimitedReadStream(new MemoryStream(new byte[100]), archive, "xl/worksheets/sheet1.xml", long.MaxValue, 1000);
		Assert.Equal(10, stream.ReadUncounted(new byte[10], 0, 10));
		archive.Dispose();
		Assert.Throws<ObjectDisposedException>(() => stream.ReadUncounted(new byte[10], 0, 10));
	}

	[Fact]
	public void SaysWhatTheLimitHasLeft()
	{
		using var archive = Archive();
		using var first = new LimitedReadStream(new MemoryStream(new byte[40]), archive, "xl/sharedStrings.xml", long.MaxValue, 1000);
		using var sheet = new LimitedReadStream(new MemoryStream(), archive, "xl/worksheets/sheet1.xml", long.MaxValue, 100);
		Assert.Equal(40, first.Read(new byte[40], 0, 40));

		// The workbook's total, against this call's limit.
		Assert.Equal(60, sheet.Remaining);
		Assert.Equal(960, first.Remaining);
		Assert.True(sheet.TryCharge(60));
		Assert.Equal(0, sheet.Remaining);

		// Past the limit, it says so, and counts the bytes all the same, as Charge does before it throws.
		Assert.False(sheet.TryCharge(5));
		Assert.Equal(-5, sheet.Remaining);
		Assert.Equal(105, archive.Guard.Total);
		Assert.Throws<InvalidDataException>(() => sheet.Charge(1));
		Assert.True(first.TryCharge(0));
	}

	/// <summary>An archive to give the streams a guard.</summary>
	/// <returns>The archive.</returns>
	private static PackageArchive Archive() =>
		SyncOrAsync.Result(PackageArchive.OpenAsync(TestPackage.Workbook("Data").ToStream(), false, Packages.Limits, false, CancellationToken.None));
}
