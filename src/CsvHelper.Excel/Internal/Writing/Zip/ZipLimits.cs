using System;

namespace CsvHelper.Excel;

/// <summary>
/// Where a package's fields overflow into Zip64's (<see cref="ZipWriter"/>): a size or offset at or past
/// <see cref="Max32"/> is written <c>0xFFFFFFFF</c>, its value in a Zip64 extra field or end record, and so are entries
/// at or past <see cref="Max16"/>. The defaults (<see cref="Default"/>) are the zip format's own. Tests lower them, so that
/// small packages carry every Zip64 field, which is valid: a Zip64 field may hold a value that would have fit.
/// </summary>
internal sealed class ZipLimits
{
	/// <summary>Creates limits.</summary>
	/// <param name="max32">The first size or offset that overflows: from 1 to <c>0xFFFFFFFF</c>.</param>
	/// <param name="max16">The first number of entries that overflows: from 1 to <c>0xFFFF</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException">A limit is out of its range.</exception>
	public ZipLimits(long max32, long max16)
	{
		if (max32 is < 1 or > 0xFFFFFFFF)
		{
			throw new ArgumentOutOfRangeException(nameof(max32), max32, "A 32-bit field's limit is from 1 to 0xFFFFFFFF.");
		}

		if (max16 is < 1 or > 0xFFFF)
		{
			throw new ArgumentOutOfRangeException(nameof(max16), max16, "A 16-bit field's limit is from 1 to 0xFFFF.");
		}

		Max32 = max32;
		Max16 = max16;
	}

	/// <summary>The zip format's limits: <c>0xFFFFFFFF</c> and <c>0xFFFF</c>, the values that mean "see Zip64".</summary>
	public static ZipLimits Default => new(0xFFFFFFFF, 0xFFFF);

	/// <summary>The first size or offset that a 32-bit field doesn't hold.</summary>
	public long Max32 { get; }

	/// <summary>The first number of entries that a 16-bit field doesn't hold.</summary>
	public long Max16 { get; }
}
