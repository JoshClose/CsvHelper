using System.Buffers.Binary;
using System.Text;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Patches the headers of a zip archive's bytes, so that tests can make them lie: sizes in the central directory and the
/// local headers, and entries whose records point at another entry's data. It reads the archives
/// <see cref="TestPackage.ToArray"/> writes: no Zip64, no data descriptors, and no archive comment.
/// </summary>
internal static class ZipBytes
{
	/// <summary>The signature of a central directory record.</summary>
	private const uint CentralSignature = 0x02014b50;

	/// <summary>The signature of a local file header.</summary>
	private const uint LocalSignature = 0x04034b50;

	/// <summary>The signature of the end of central directory record.</summary>
	private const uint EndSignature = 0x06054b50;

	/// <summary>The size of the end of central directory record, without a comment.</summary>
	private const int EndSize = 22;

	/// <summary>Sets an entry's uncompressed size in its central directory record and its local header.</summary>
	/// <param name="zip">The archive's bytes, changed in place.</param>
	/// <param name="name">The entry's name.</param>
	/// <param name="size">The size to declare.</param>
	public static void SetUncompressedSize(byte[] zip, string name, uint size)
	{
		var (central, local) = Find(zip, name);
		BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(central + 24), size);
		BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(local + 22), size);
	}

	/// <summary>Sets an entry's compressed size in its central directory record and its local header.</summary>
	/// <param name="zip">The archive's bytes, changed in place.</param>
	/// <param name="name">The entry's name.</param>
	/// <param name="size">The size to declare.</param>
	public static void SetCompressedSize(byte[] zip, string name, uint size)
	{
		var (central, local) = Find(zip, name);
		BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(central + 20), size);
		BinaryPrimitives.WriteUInt32LittleEndian(zip.AsSpan(local + 18), size);
	}

	/// <summary>Sets the length of the extra field an entry's central directory record declares, without adding one.</summary>
	/// <param name="zip">The archive's bytes, changed in place.</param>
	/// <param name="name">The entry's name.</param>
	/// <param name="length">The length to declare.</param>
	public static void SetCentralExtraLength(byte[] zip, string name, ushort length) =>
		BinaryPrimitives.WriteUInt16LittleEndian(zip.AsSpan(Find(zip, name).Central + 30), length);

	/// <summary>Renames an entry in its central directory record only, to a name of the same length in bytes.</summary>
	/// <param name="zip">The archive's bytes, changed in place.</param>
	/// <param name="name">The entry's name.</param>
	/// <param name="newName">The new name.</param>
	public static void SetCentralName(byte[] zip, string name, string newName)
	{
		var bytes = Encoding.UTF8.GetBytes(newName);
		Assert.Equal(Encoding.UTF8.GetByteCount(name), bytes.Length);
		bytes.CopyTo(zip.AsSpan(Find(zip, name).Central + 46));
	}

	/// <summary>Where an entry's compressed data is: after its local header, for the compressed size it declares.</summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>The data's offset and length.</returns>
	public static (long Start, long Length) DataRange(byte[] zip, string name)
	{
		var (central, local) = Find(zip, name);
		var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 26));
		var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 28));

		return (local + 30 + nameLength + extraLength, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(central + 20)));
	}

	/// <summary>The uncompressed size an entry's central directory record declares.</summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>The size.</returns>
	public static uint UncompressedSize(byte[] zip, string name) => BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(Find(zip, name).Central + 24));

	/// <summary>The compressed size an entry's central directory record declares.</summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>The size.</returns>
	public static uint CompressedSize(byte[] zip, string name) => BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(Find(zip, name).Central + 20));

	/// <summary>
	/// Points an entry's central directory record at another entry's data: its compression method, CRC, sizes and local
	/// header offset become the other's, while its name stays its own. Both entries then read the same compressed bytes.
	/// </summary>
	/// <param name="zip">The archive's bytes, changed in place.</param>
	/// <param name="name">The entry to point elsewhere.</param>
	/// <param name="target">The entry whose data it gets.</param>
	public static void PointAt(byte[] zip, string name, string target)
	{
		var central = Find(zip, name).Central;
		var source = Find(zip, target).Central;

		// The compression method at 10, then the time and date, the CRC (16), the sizes (20, 24) and, at 42, the offset.
		zip.AsSpan(source + 10, 2).CopyTo(zip.AsSpan(central + 10));
		zip.AsSpan(source + 16, 12).CopyTo(zip.AsSpan(central + 16));
		zip.AsSpan(source + 42, 4).CopyTo(zip.AsSpan(central + 42));
	}

	/// <summary>The offsets of an entry's central directory record and local header.</summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>The two offsets.</returns>
	private static (int Central, int Local) Find(byte[] zip, string name)
	{
		var end = zip.Length - EndSize;
		Assert.Equal(EndSignature, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end)));
		var count = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(end + 10));
		var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end + 16));
		for (var i = 0; i < count; i++)
		{
			Assert.Equal(CentralSignature, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset)));
			var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 28));
			var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 30));
			var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 32));
			if (Encoding.UTF8.GetString(zip, offset + 46, nameLength) == name)
			{
				var local = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset + 42));
				Assert.Equal(LocalSignature, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(local)));
				Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 6)) & 8);

				return (offset, local);
			}

			offset += 46 + nameLength + extraLength + commentLength;
		}

		throw new InvalidOperationException($"The zip has no entry {name}.");
	}
}
