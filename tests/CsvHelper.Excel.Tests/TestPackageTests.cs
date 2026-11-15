using System.IO.Compression;

namespace CsvHelper.Excel.Tests;

/// <summary>
/// The test packages themselves: tests compare a workbook built at one moment with one built later, so a package's
/// bytes mustn't depend on when it's built.
/// </summary>
public class TestPackageTests
{
	[Fact]
	public void EveryEntryHasTheFixedDate()
	{
		var package = TestPackage.Workbook("A", "B").SetStreamed("xl/media/big.bin", s => s.WriteByte(1), CompressionLevel.NoCompression);

		using var archive = new ZipArchive(package.ToStream(), ZipArchiveMode.Read);
		Assert.NotEmpty(archive.Entries);
		Assert.All(archive.Entries, e => Assert.Equal(TestPackage.EntryDate.DateTime, e.LastWriteTime.DateTime));
		Assert.Equal(package.ToArray(), package.ToArray());
	}
}
