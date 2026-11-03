using System;

namespace CsvHelper;

/// <summary>
/// How the typed cells of a <see cref="CsvRecordSource"/> convert (see <see cref="CsvCells"/>): its date system, the
/// rounding of numbers for <see cref="decimal"/> and integer targets, and whether reading an error cell fails. A source
/// gives its options through <see cref="CsvRecordSource.CellOptions"/>; they are compared by value.
/// </summary>
internal sealed record CsvCellOptions
{
	private readonly int? significantDigits = 15;

	/// <summary>The options of a source that sets none: the 1900 date system, 15 significant digits, error cells read as text.</summary>
	public static CsvCellOptions Default { get; } = new();

	/// <summary>
	/// Whether serial dates count from 1904-01-01 (serial 0), as in workbooks of the 1904 date system, instead of Excel's
	/// 1900 system, where serial 1 is 1900-01-01 and serial 60 is a 1900-02-29 that never existed.
	/// </summary>
	public bool Date1904 { get; init; }

	/// <summary>
	/// The significant digits that <see cref="decimal"/> and integer targets round a number to, half away from zero, as
	/// Excel displays numbers (15); <see langword="null"/> reads the stored text exactly.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is not from 1 to 28, or null.</exception>
	public int? SignificantDigits
	{
		get => significantDigits;
		init
		{
			if (value is < 1 or > 28)
			{
				throw new ArgumentOutOfRangeException(nameof(SignificantDigits), value, "SignificantDigits must be from 1 to 28, or null.");
			}

			significantDigits = value;
		}
	}

	/// <summary>
	/// Whether reading an error cell fails for every target, strings included, instead of reading as its text
	/// (<c>#N/A</c>) for strings and failing for other targets.
	/// </summary>
	public bool ThrowOnErrorCells { get; init; }
}
