using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The merged ranges of a worksheet, from its <c>mergeCells</c>, which comes after <c>sheetData</c>: so they are found by
/// reading the sheet's part through once, before a reader reads it again, when merged cells are filled
/// (<c>ExcelReaderOptions.FillMergedCells</c>). The scanner's fill state (<see cref="MergedFill"/>) reads them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reading.</b> The root must be <c>worksheet</c>, or there are no ranges (the scanner reports the root). Its children
/// are skipped, <c>sheetData</c> among them, up to the first <c>mergeCells</c>, whose <c>mergeCell</c> elements give a
/// range each in their <c>ref</c>; reading stops after <c>&lt;/mergeCells&gt;</c>, so nothing after it is inflated. Other
/// elements in <c>mergeCells</c>, and content in a <c>mergeCell</c>, are skipped, as Excel ignores them.
/// </para>
/// <para>
/// <b>Strictness.</b> Excel opens no file whose <c>mergeCell</c> has no <c>ref</c>, or a <c>ref</c> that isn't one block of
/// cells as a range's corners write it (white space around it, a sheet's name, a list, a cell past the grid), nor one
/// whose ranges overlap, even when one of them is a single cell: each throws <see cref="CsvFormatException"/> for the
/// sheet. Excel takes references in either case, with <c>$</c>, with their corners reversed, and as whole columns or
/// rows, and so does <see cref="AreaReference.TryParseArea(ReadOnlySpan{byte}, out ExcelRange)"/>.
/// </para>
/// <para>
/// <b>Limits.</b> The ranges are held while the sheet is read, so each counts <see cref="BytesPerRange"/> toward
/// <c>ExcelReaderOptions.MaxPartSize</c>; the read itself counts toward <c>ExcelReaderOptions.MaxUncompressedSize</c>,
/// as every part's stream does.
/// </para>
/// </remarks>
internal sealed class MergedCells
{
	/// <summary>The bytes each range counts toward <c>ExcelReaderOptions.MaxPartSize</c>: its bounds, and its place in the order the ranges end.</summary>
	public const int BytesPerRange = 20;

	/// <summary>The most bytes of a <c>ref</c> an error message quotes.</summary>
	private const int MaxQuoted = 32;

	/// <summary>Creates the ranges.</summary>
	/// <param name="ranges">The ranges, sorted by first row and then first column, none overlapping another.</param>
	/// <param name="ends">The index of each range in <paramref name="ranges"/>, in the order of their last rows.</param>
	private MergedCells(ExcelRange[] ranges, int[] ends)
	{
		Ranges = ranges;
		Ends = ends;
	}

	/// <summary>The ranges, one-cell ones among them, sorted by first row and then first column; none overlaps another.</summary>
	public ExcelRange[] Ranges { get; }

	/// <summary>The index of each range in <see cref="Ranges"/>, in the order of their last rows, for the fill to end them.</summary>
	public int[] Ends { get; }

	/// <summary>Finds the merged ranges of a worksheet, reading its part through.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="partName">The worksheet's part, normalized.</param>
	/// <param name="sheetName">The sheet's name, for errors.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the ranges, or <see langword="null"/> when the sheet has none.</returns>
	/// <exception cref="CsvFormatException">The part's XML can't be read, or its merged cells are not as Excel writes them.</exception>
	/// <exception cref="InvalidDataException">The part passes a limit, or can't be inflated.</exception>
	public static async ValueTask<MergedCells?> LoadAsync(PackageArchive archive, string partName, string sheetName, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var scanner = await archive.OpenXmlAsync(partName, false, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			return await ReadAsync(scanner, sheetName, limits.MaxPartSize, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}
	}

	/// <summary>Reads the merged ranges of a worksheet part, from its start; the caller disposes the scanner.</summary>
	/// <param name="scanner">The part's XML, at its start.</param>
	/// <param name="sheetName">The sheet's name, for errors.</param>
	/// <param name="maxSize">The most bytes the ranges can take: <c>ExcelReaderOptions.MaxPartSize</c>.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the ranges, or <see langword="null"/> when the sheet has none.</returns>
	/// <exception cref="CsvFormatException">The part's XML can't be read, or its merged cells are not as Excel writes them.</exception>
	/// <exception cref="InvalidDataException">The ranges take more than <paramref name="maxSize"/>, or the part passes a limit.</exception>
	public static async ValueTask<MergedCells?> ReadAsync(XmlScanner scanner, string sheetName, long maxSize, bool async, CancellationToken cancellationToken)
	{
		var location = new SheetLocation(sheetName);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("worksheet"u8))
			{
				return null;
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				if (!scanner.IsStartTag("mergeCells"u8))
				{
					// sheetData among them: its text is skipped as it comes, so the window holds a tag at a time.
					await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);

					continue;
				}

				var listOffset = scanner.Window.Offset(scanner.TokenStart);
				var ranges = new List<ExcelRange>();
				if (!scanner.IsEmptyElement)
				{
					var listDepth = scanner.ElementDepth;
					while (await scanner.ReadChildAsync(listDepth, async, cancellationToken).ConfigureAwait(false))
					{
						if (scanner.IsStartTag("mergeCell"u8))
						{
							if ((long)(ranges.Count + 1) * BytesPerRange > maxSize)
							{
								throw new InvalidDataException($"The merged cells of {scanner.Window.PartName} take more than ExcelReaderOptions.MaxPartSize ({maxSize} bytes).");
							}

							ranges.Add(ReadRange(scanner, location));
						}

						await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
					}
				}

				// Nothing after the list is read.
				return ranges.Count == 0 ? null : Create(ranges.ToArray(), location, scanner.Window.PartName, listOffset);
			}

			return null;
		}
		catch (InvalidDataException e) when (XmlError.TryGet(e, out var offset, out var detail))
		{
			throw Error(location, scanner.Window.PartName, offset, detail, e);
		}
	}

	/// <summary>Reads a <c>mergeCell</c>'s <c>ref</c>.</summary>
	/// <param name="scanner">The scanner, at the <c>mergeCell</c>'s start tag.</param>
	/// <param name="location">The sheet's locations, for errors.</param>
	/// <returns>The range.</returns>
	/// <exception cref="CsvFormatException">The <c>mergeCell</c> has no <c>ref</c>, or one that isn't a block of cells.</exception>
	private static ExcelRange ReadRange(XmlScanner scanner, SheetLocation location)
	{
		var offset = scanner.Window.Offset(scanner.TokenStart);
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (!attributes.IsName("ref"u8))
			{
				continue;
			}

			var value = attributes.Value;
			if (value.IndexOf((byte)'&') >= 0)
			{
				var decoded = new byte[value.Length];
				value = decoded.AsSpan(0, attributes.DecodeValue(decoded));
			}

			// Excel can't open a ref with white space around it, which the parser would trim.
			var padded = value.Length > 0 && (XmlScanner.IsWhitespace(value[0]) || XmlScanner.IsWhitespace(value[value.Length - 1]));
			if (padded || !AreaReference.TryParseArea(value, out var range))
			{
				var text = Encoding.UTF8.GetString(value);
				var quoted = "\"" + (text.Length > MaxQuoted ? text.Substring(0, MaxQuoted) + "..." : text) + "\"";

				throw Error(location, scanner.Window.PartName, offset, $"the attribute ref={quoted} of a mergeCell is not a block of cells, such as A1:B2");
			}

			return range;
		}

		throw Error(location, scanner.Window.PartName, offset, "a mergeCell has no ref");
	}

	/// <summary>Sorts the ranges, checks that none overlaps another, and orders them by their last rows.</summary>
	/// <param name="ranges">The ranges, in the file's order.</param>
	/// <param name="location">The sheet's locations, for errors.</param>
	/// <param name="partName">The part's name, for errors.</param>
	/// <param name="listOffset">The offset of <c>mergeCells</c> in the part, for errors.</param>
	/// <returns>The ranges.</returns>
	/// <exception cref="CsvFormatException">Two ranges overlap.</exception>
	private static MergedCells Create(ExcelRange[] ranges, SheetLocation location, string partName, long listOffset)
	{
		if (!IsSorted(ranges))
		{
			Array.Sort(ranges, CompareStarts);
		}

		var ends = new int[ranges.Length];
		for (var i = 0; i < ends.Length; i++)
		{
			ends[i] = i;
		}

		// Stable for equal last rows, which keeps the check's expiry and the fill's ends in the same order.
		Array.Sort(ends, (x, y) => ranges[x].LastRow != ranges[y].LastRow ? ranges[x].LastRow.CompareTo(ranges[y].LastRow) : x.CompareTo(y));

		// A sweep down the rows: the columns of the ranges still open at each range's first row, which it must not touch.
		var open = new ulong[CellReference.MaxColumns / 64];
		var expired = 0;
		for (var i = 0; i < ranges.Length; i++)
		{
			var range = ranges[i];
			while (ranges[ends[expired]].LastRow < range.FirstRow)
			{
				// Open ranges don't overlap, so clearing one's columns clears only its own.
				Mark(open, ranges[ends[expired]], false);
				expired++;
			}

			if (AnyMarked(open, range.FirstColumnIndex, range.LastColumnIndex))
			{
				var other = Array.FindIndex(ranges, 0, i, r => r.LastRow >= range.FirstRow && r.FirstColumnIndex <= range.LastColumnIndex && r.LastColumnIndex >= range.FirstColumnIndex);

				throw Error(location, partName, listOffset, $"its merged cells {ranges[other]} and {range} overlap");
			}

			Mark(open, range, true);
		}

		return new MergedCells(ranges, ends);
	}

	/// <summary>Whether the ranges are sorted by first row and then first column, as Excel mostly writes them.</summary>
	/// <param name="ranges">The ranges.</param>
	/// <returns>Whether they are sorted.</returns>
	private static bool IsSorted(ExcelRange[] ranges)
	{
		for (var i = 1; i < ranges.Length; i++)
		{
			if (CompareStarts(ranges[i - 1], ranges[i]) > 0)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Orders ranges by first row, then first column.</summary>
	/// <param name="x">A range.</param>
	/// <param name="y">Another range.</param>
	/// <returns>Less than 0, 0, or more than 0, as <paramref name="x"/> comes before, with, or after <paramref name="y"/>.</returns>
	private static int CompareStarts(ExcelRange x, ExcelRange y) =>
		x.FirstRow != y.FirstRow ? x.FirstRow.CompareTo(y.FirstRow) : x.FirstColumnIndex.CompareTo(y.FirstColumnIndex);

	/// <summary>Marks or clears a range's columns in a set of a bit per column.</summary>
	/// <param name="bits">The set.</param>
	/// <param name="range">The range.</param>
	/// <param name="value">Whether to mark them.</param>
	private static void Mark(ulong[] bits, ExcelRange range, bool value)
	{
		var first = range.FirstColumnIndex;
		var last = range.LastColumnIndex;
		for (var word = first >> 6; word <= last >> 6; word++)
		{
			var mask = WordMask(word, first, last);
			bits[word] = value ? bits[word] | mask : bits[word] & ~mask;
		}
	}

	/// <summary>Whether any column from <paramref name="first"/> to <paramref name="last"/> is marked.</summary>
	/// <param name="bits">The set.</param>
	/// <param name="first">The first column, from 0.</param>
	/// <param name="last">The last column, from 0.</param>
	/// <returns>Whether one is marked.</returns>
	private static bool AnyMarked(ulong[] bits, int first, int last)
	{
		for (var word = first >> 6; word <= last >> 6; word++)
		{
			if ((bits[word] & WordMask(word, first, last)) != 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>The bits of a word that fall from <paramref name="first"/> to <paramref name="last"/>.</summary>
	/// <param name="word">The word.</param>
	/// <param name="first">The first column, from 0.</param>
	/// <param name="last">The last column, from 0.</param>
	/// <returns>The mask.</returns>
	private static ulong WordMask(int word, int first, int last)
	{
		var low = word == first >> 6 ? ulong.MaxValue << (first & 63) : ulong.MaxValue;
		var high = word == last >> 6 ? ulong.MaxValue >> (63 - (last & 63)) : ulong.MaxValue;

		return low & high;
	}

	/// <summary>Creates the exception for merged cells that can't be read, for the sheet.</summary>
	/// <param name="location">The sheet's locations.</param>
	/// <param name="partName">The part's name.</param>
	/// <param name="offset">The offset in the part of what can't be read.</param>
	/// <param name="detail">What is wrong, as a clause.</param>
	/// <param name="inner">The XML's error, if it is one.</param>
	/// <returns>The exception, to throw.</returns>
	private static CsvFormatException Error(SheetLocation location, string partName, long offset, string detail, Exception? inner = null) =>
		new($"Sheet {location.QuotedName} can't be read at byte {offset} of {partName}: {detail}.", 0, 0, inner);
}
