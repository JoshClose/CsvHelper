using System.Buffers.Binary;
using System.IO.Compression;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// The structure check itself (<see cref="ZipStructure"/>): a package from the own writer passes, and each rule broken in
/// it by a byte or two fails with the rule named; <see cref="ZipArchive"/>'s shape fails; and WindowsBase opens the
/// package on .NET Framework, a file closed again before the check returns.
/// </summary>
public class ZipStructureTests
{
	/// <summary>Rules broken in a valid package, each by patching its bytes, and the rule's words in the failure.</summary>
	public static TheoryData<string, string> Breaks()
	{
		return new TheoryData<string, string>
		{
			{ "local version", "versions needed agree" },
			{ "local sizes all ones", "are 0, not 0xFFFFFFFF" },
			{ "unsigned descriptor", "every descriptor is signed" },
			{ "made by", "made by 0x002D" },
			{ "central date", "date 0x0021" },
			{ "whole at version 45", "45 for a streamed entry or one with a Zip64 extra, else 20" },
			{ "UTF-8 flag", "no UTF-8 flag" },
			{ "entries on this disk", "the entries on this disk are the entries" },
			{ "end comment", "without a comment" },
		};
	}

	/// <summary>A package of the own writer, with a whole and a streamed entry, passes.</summary>
	[Fact]
	public void PassesTheOwnWritersPackage()
	{
		ZipStructure.AssertValid(Package());
		using var stream = new MemoryStream(Package());
		ZipStructure.AssertValid(stream);
	}

	/// <summary>Each rule broken fails, naming the rule.</summary>
	/// <param name="name">The break.</param>
	/// <param name="rule">Words of the rule's message.</param>
	[Theory]
	[MemberData(nameof(Breaks))]
	public void FailsEachBrokenRule(string name, string rule)
	{
		var package = Package();
		var zip = ZipStructure.Read(package);
		var whole = zip.Entries[0];
		var streamed = zip.Entries[1];
		switch (name)
		{
			case "local version":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)streamed.Local.Position + 4), 20);
				break;
			case "local sizes all ones":
				BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan((int)streamed.Local.Position + 18), 0xFFFFFFFF);
				break;
			case "unsigned descriptor":
				BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan((int)streamed.Descriptor!.Position), 0);
				break;
			case "made by":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Central.Position + 4), 0x0014);
				break;
			case "central date":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Central.Position + 14), 0x0022);
				break;
			case "whole at version 45":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Local.Position + 4), 45);
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Central.Position + 6), 45);
				break;
			case "UTF-8 flag":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Local.Position + 6), (ushort)(whole.Local.Flags | 0x0800));
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)whole.Central.Position + 8), (ushort)(whole.Central.Flags | 0x0800));
				break;
			case "entries on this disk":
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)zip.End.Position + 8), 2);
				break;
			default:
				package = [.. package, 0x21];
				BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)zip.End.Position + 20), 1);
				break;
		}

		var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ZipStructure.AssertValid(package));

		Assert.Contains(rule, failure.Message);
	}

	/// <summary>
	/// The rules of patched entries and full Zip64 extras broken, each in a package of the own writer that keeps them: a
	/// stored entry past one buffer, written to a stream that seeks, whose local CRC is still the placeholder; and an
	/// entry whose sizes overflow with its offset in its central field rather than all ones in the Zip64 extra.
	/// </summary>
	/// <param name="name">The break.</param>
	/// <param name="rule">Words of the rule's message.</param>
	[Theory]
	[InlineData("unpatched CRC", "a whole or patched entry's local header holds its CRC and sizes")]
	[InlineData("offset outside the extra", "all three are 0xFFFFFFFF when any reaches Max32")]
	public void FailsABrokenPatchOrExtraRule(string name, string rule)
	{
		var limits = name == "unpatched CRC" ? ZipLimits.Default : new ZipLimits(5_000, 0xFFFF);
		var package = ZipPackages.Write([new ZipPart("a.bin", ZipPackages.RandomBytes(name == "unpatched CRC" ? 100_000 : 6_000, 3))], CompressionLevel.NoCompression, limits);
		var entry = ZipStructure.Read(package).Entries[0];
		ZipStructure.AssertValid(package, limits.Max32, limits.Max16);
		if (name == "unpatched CRC")
		{
			Assert.True(entry.Patched);
			BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan((int)entry.Local.Position + 14), 0);
		}
		else
		{
			Assert.Equal((0L, 28), (entry.Offset, entry.Central.Extra.Length));
			BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan((int)entry.Central.Position + 42), 0);
		}

		var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ZipStructure.AssertValid(package, limits.Max32, limits.Max16));

		Assert.Contains(rule, failure.Message);
	}

	/// <summary>
	/// <see cref="ZipArchive"/>'s own packages don't take the own writer's shape (version made by, and on a stream that
	/// can't seek, 4-byte descriptors at version 2.0), so the check fails them. .NET Framework's can't write to a stream
	/// that can't seek.
	/// </summary>
	/// <param name="seekable">Whether <see cref="ZipArchive"/> writes to a stream that seeks.</param>
	[Theory]
	[InlineData(true)]
#if NET
	[InlineData(false)]
#endif
	public void FailsZipArchivesShape(bool seekable)
	{
		var memory = new MemoryStream();
		using (var archive = new ZipArchive(seekable ? memory : new Writing.ForwardOnlyStream(memory), ZipArchiveMode.Create, true))
		{
			using var entry = archive.CreateEntry("a.bin", CompressionLevel.Fastest).Open();
			entry.Write(ZipPackages.RandomBytes(100_000, 1), 0, 100_000);
		}

		Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ZipStructure.AssertValid(memory.ToArray()));
	}

	/// <summary>
	/// On .NET Framework WindowsBase opens the package and reads its parts, and refuses a streamed entry whose central
	/// version needed (2.0) disagrees with its local one (4.5), as POI's SXSSF writes them; elsewhere the check does
	/// nothing. A file checked is closed when the check returns, so that it can be deleted.
	/// </summary>
	[Fact]
	public void OpensPackagesWithWindowsBaseOnDotNetFramework()
	{
		var package = Package();
		var streamed = ZipStructure.Read(package).Entries[1];
		BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan((int)streamed.Central.Position + 6), 20);
		var path = Path.Combine(Path.GetTempPath(), $"csvhelper-zipstructure-{Guid.NewGuid():N}.xlsx");
		File.WriteAllBytes(path, Package());

		ZipStructure.AssertPackagingReads(path);
		File.Delete(path);
#if NETFRAMEWORK
		Assert.ThrowsAny<Exception>(() => ZipStructure.AssertPackagingReads(package));
#else
		ZipStructure.AssertPackagingReads(package);
#endif
		Assert.False(File.Exists(path));
	}

	/// <summary>A package of the own writer: a whole entry, a streamed one and the content types.</summary>
	/// <returns>The package.</returns>
	private static byte[] Package() => ZipPackages.Write([new ZipPart("a.xml", ZipWriterTests.Bytes(1_000, 1)), new ZipPart("b.bin", ZipPackages.RandomBytes(100_000, 2))], CompressionLevel.Fastest);
}
