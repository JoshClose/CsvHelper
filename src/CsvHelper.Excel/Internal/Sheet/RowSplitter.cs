using System;

namespace CsvHelper.Excel;

/// <summary>
/// Finds where <see cref="SegmentPipeline"/> cuts a sheet's <c>sheetData</c> into segments: at a row's start tag, the bytes
/// of the row start (<c>&lt;</c>, the prefix of <c>sheetData</c> and <c>row</c>, as <c>SheetScanner.RowStart</c> notes them)
/// followed by XML white space, <c>&gt;</c> or <c>/</c>.
/// </summary>
/// <remarks>
/// It knows nothing else of XML. A row start inside a comment or a CDATA section is cut at like any other: the scan of the
/// segment the markup begins in marks it (<see cref="SegmentMark.Markup"/>), and the sheet is read serially from there. One
/// inside an attribute value is malformed XML, which the serial read rejects.
/// </remarks>
internal sealed class RowSplitter
{
	private readonly byte[] rowStart;

	/// <summary>Creates the splitter.</summary>
	/// <param name="rowStart">The bytes a row's start tag begins with: <c>&lt;</c>, the prefix of <c>sheetData</c> and <c>row</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="rowStart"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="rowStart"/> doesn't start with <c>&lt;</c>, or has nothing after it.</exception>
	public RowSplitter(byte[] rowStart)
	{
		ArgumentNullException.ThrowIfNull(rowStart);
		if (rowStart.Length < 2 || rowStart[0] != (byte)'<')
		{
			throw new ArgumentException("A row start is '<' and the row's name.", nameof(rowStart));
		}

		this.rowStart = rowStart;
	}

	/// <summary>The bytes a row's start tag begins with.</summary>
	public ReadOnlySpan<byte> RowStart => rowStart;

	/// <summary>
	/// Finds the first row start at or after <paramref name="from"/> whose bytes, with the byte after them that ends the
	/// name, are all in <paramref name="bytes"/>.
	/// </summary>
	/// <param name="bytes">The bytes to search.</param>
	/// <param name="from">The first position a row start may be at.</param>
	/// <param name="resume">
	/// Where to search on from once more bytes follow <paramref name="bytes"/>: the row start found, or the first position
	/// whose bytes weren't all there to tell.
	/// </param>
	/// <returns>The row start's position, or -1 when there is none.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="from"/> is negative.</exception>
	public int Find(ReadOnlySpan<byte> bytes, int from, out int resume)
	{
		if (from < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(from), from, "The position can't be negative.");
		}

		var length = rowStart.Length;
		var start = from;
		while (true)
		{
			// A row start found in all but the last byte has the byte after it in the bytes.
			var room = bytes.Length - 1 - start;
			var at = room >= length ? bytes.Slice(start, room).IndexOf(rowStart) : -1;
			if (at < 0)
			{
				resume = Math.Max(start, bytes.Length - length);

				return -1;
			}

			at += start;
			if (EndsName(bytes[at + length]))
			{
				resume = at;

				return at;
			}

			start = at + 1;
		}
	}

	/// <summary>Whether a byte ends an element's name in a start tag: XML white space, <c>&gt;</c> or <c>/</c>.</summary>
	/// <param name="value">The byte after the name.</param>
	/// <returns>Whether the name ends there.</returns>
	private static bool EndsName(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>' or (byte)'/';
}
