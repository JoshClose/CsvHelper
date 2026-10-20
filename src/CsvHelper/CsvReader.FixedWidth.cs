using System;
using System.Runtime.CompilerServices;

namespace CsvHelper;

// FixedWidth mode. Each line is a record with a field per column. The index keeps its usual shape, so that records,
// compaction and parallel batches work unchanged; only its entries mean something else. For a line [S, E) (E is its CR
// or LF) the scanner writes, when it finds the line break:
//
//   one entry per column after the first: the byte position where the column starts, clamped to E;
//   the terminator entry E + 1, and after a CR followed by LF the LF entry, as in the other modes.
//
// So ends[fieldBase] is S, ends[fieldBase + fieldCount] is E + 1 and the record has one field per column. Every entry is
// flagged, so field access always goes through FixedWidthField, which knows that a column ends where the next one starts
// (not one byte before, as in the other modes) and where the last one ends.
public sealed partial class CsvReader
{
	/// <summary>
	/// Scans fixed-width lines from <paramref name="pos"/> up to <paramref name="end"/>. Returns <paramref name="end"/>,
	/// or the position of a line break that the index has no room for. The text after the last line break stays
	/// pending: its start is the last index entry.
	/// </summary>
	private int ScanFixed(int pos, int end)
	{
		// A line writes an entry per column but the first, its terminator and, after CR, an LF entry.
		var need = fixedStarts.Length;
		if (carry.PrevCr != 0 && pos < end)
		{
			// The scan before ended with a CR: an LF next makes CR LF one line break.
			if (buffer[pos] == (byte)'\n')
			{
				ends[endsCount++] = (pos + 1) | int.MinValue;
				rowEnds[rowCount++] = (endsCount - 1) | int.MinValue;
				pos++;
			}
			else
			{
				rowEnds[rowCount++] = endsCount - 1;
			}

			carry.PrevCr = 0;
		}

		while (pos < end)
		{
			var k = buffer.AsSpan(pos, end - pos).IndexOfAny((byte)'\r', (byte)'\n');
			if (k < 0) return end;
			var i = pos + k;
			if (ends.Length - EndsSlack - endsCount < need || rowEnds.Length - rowCount < 2) return i;
			WriteFixedColumns(ends[endsCount - 1] & int.MaxValue, i);
			ends[endsCount++] = (i + 1) | int.MinValue;
			pos = i + 1;
			if (buffer[i] == (byte)'\n')
			{
				rowEnds[rowCount++] = endsCount - 1;
			}
			else if (pos == end)
			{
				// Whether an LF follows is decided by the next scan.
				carry.PrevCr = 1;
			}
			else if (buffer[pos] == (byte)'\n')
			{
				ends[endsCount++] = (pos + 1) | int.MinValue;
				rowEnds[rowCount++] = (endsCount - 1) | int.MinValue;
				pos++;
			}
			else
			{
				rowEnds[rowCount++] = endsCount - 1;
			}
		}

		return pos;
	}

	/// <summary>Writes the entries of the columns after the first of the line [<paramref name="start"/>, <paramref name="end"/>).</summary>
	private void WriteFixedColumns(int start, int end)
	{
		var starts = fixedStarts;
		var columns = starts.Length - 2;
		if (columns == 0) return;
		var length = end - start;
		var ends = this.ends;
		var count = endsCount;
		// An ASCII line has a byte per character.
		var checkedLength = Math.Min(length, starts[columns]);
		if (FixedWidth.AsciiPrefixLength(buffer.AsSpan(start, checkedLength)) == checkedLength)
		{
			for (var k = 1; k <= columns; k++) ends[count++] = (start + Math.Min(starts[k], length)) | int.MinValue;
		}
		else
		{
			var offsets = ends.AsSpan(count, columns);
			FixedWidth.Offsets(buffer.AsSpan(start, length), starts.AsSpan(1, columns), offsets);
			for (var k = 0; k < columns; k++) offsets[k] = (start + offsets[k]) | int.MinValue;
			count += columns;
		}

		endsCount = count;
	}

	/// <summary>
	/// The bytes of a fixed-width field, from the index entries that <see cref="GetSpan(int)"/> read:
	/// <paramref name="start"/> is where the column starts, and <paramref name="start"/> + <paramref name="length"/> + 1
	/// where the next column starts, or past the end of the line for the last column.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private ReadOnlySpan<byte> FixedWidthField(int start, int length, bool trimPadding)
	{
		var end = start + length + 1;
		var lineEnd = (ends[fieldBase + fieldCount] & int.MaxValue) - 1;
		if (end > lineEnd)
		{
			// The last column: it ends after the width of the line, counted from the start of the line (a surrogate
			// pair can push the start of the column one character later).
			var lineStart = ends[fieldBase] & int.MaxValue;
			end = lineStart + FixedWidth.Offset(buffer.AsSpan(lineStart, lineEnd - lineStart), fixedTotal);
		}

		var field = new ReadOnlySpan<byte>(buffer, start, end - start);

		return trimPadding ? TrimWhitespace(field) : field;
	}
}
