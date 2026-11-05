using System;
using System.Numerics;

namespace CsvHelper.Excel;

/// <summary>
/// What the sheet scanner keeps to fill merged cells: the merged ranges (<see cref="MergedCells"/>) that cover the rows it
/// is at, each with its top-left cell's field once the scanner has read it, so that every cell of a range reads as its
/// top-left cell, as Excel shows the range.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which ranges.</b> A range fills only the cells that are fields: in the layout's rows and columns, and shown. So a
/// range is used when it has more than one cell, its rows meet the area's, and some of its columns are fields. Those
/// columns are a run of consecutive fields, since the fields are the area's columns that are read, in order; its top-left
/// cell can be outside them, above or left of the area, or hidden.
/// </para>
/// <para>
/// <b>Rows.</b> A range is open from its first row to its last (<see cref="Advance"/>). Ranges are opened in the order of
/// their first rows and closed in the order of their last, so a sheet's rows cost the ranges they pass, not a check of
/// every range. Ranges that are open at once don't overlap, so their runs of fields don't either, and each is kept at its
/// run's first field: a set of a bit per field (<see cref="FieldBits"/>) finds the run a field is in, and the runs with a
/// value in field order.
/// </para>
/// <para>
/// <b>Values.</b> A range's top-left cell is in its first row: when the scanner reads that cell, it writes the cell's
/// field into the range's store (<see cref="MergedCapture"/>), as it would write it into a record. A range whose top-left
/// cell has no value, or whose first row the scanner doesn't read, fills with nothing; its other cells are still
/// read as it, so as nothing. The store holds a field per open range, and a range that closes frees its field.
/// </para>
/// </remarks>
internal sealed class MergedFill
{
	private readonly ExcelRange[] ranges;
	private readonly int[] ends;
	private readonly SheetLayout layout;
	// The first field of each open range's run; of those, the ones whose top-left cell has a value.
	private readonly FieldBits starts;
	private readonly FieldBits valued;
	// By an open range's first field: the range's index, its run's last field, and its top-left cell's field.
	private readonly int[] rangeAt;
	private readonly int[] lastAt;
	private readonly byte[]?[] values;
	private readonly int[] lengths;
	private readonly SheetCellKind[] kinds;
	// The ranges opened at the current row that start in it, in column order: those whose top-left cell can come.
	private readonly int[] starting;
	private int startingCount;
	private int startingNext;
	// The row opened up to (0 before the first), the next range to open, and the next to close in ends' order.
	private long at;
	private int next;
	private int closed;
	// The search for the ranges that start in a row: the next range to look at, and the last row asked about and its answer.
	private int probe;
	private long probeRow;
	private bool probeStarts;
	// The first field of the range whose top-left cell is being read.
	private int capturing = -1;

	/// <summary>Creates the fill state.</summary>
	/// <param name="cells">The sheet's merged ranges.</param>
	/// <param name="layout">The scanner's layout, final.</param>
	private MergedFill(MergedCells cells, SheetLayout layout)
	{
		ranges = cells.Ranges;
		ends = cells.Ends;
		this.layout = layout;
		var width = layout.Width;
		starts = new FieldBits(width);
		valued = new FieldBits(width);
		rangeAt = new int[width];
		lastAt = new int[width];
		values = new byte[width][];
		lengths = new int[width];
		kinds = new SheetCellKind[width];
		starting = new int[width];
	}

	/// <summary>Whether a range open at the current row has a value to fill with.</summary>
	public bool HasFills => valued.Any;

	/// <summary>Creates the fill state of a layout, if any of the ranges fills a cell of it.</summary>
	/// <param name="cells">The sheet's merged ranges.</param>
	/// <param name="layout">The scanner's layout, final.</param>
	/// <returns>The fill state; <see langword="null"/> when no range has a cell that is a field.</returns>
	public static MergedFill? Create(MergedCells cells, SheetLayout layout)
	{
		foreach (var range in cells.Ranges)
		{
			if (TryGetRun(range, layout, out _, out _))
			{
				return new MergedFill(cells, layout);
			}
		}

		return null;
	}

	/// <summary>
	/// Whether a used range has its top-left cell in a row: such a row is read, even one above the area or hidden, so that
	/// the range has its value.
	/// </summary>
	/// <param name="row">The row, from 1; the rows asked about ascend.</param>
	/// <returns>Whether a range starts in the row.</returns>
	public bool StartsAt(long row)
	{
		if (row == probeRow)
		{
			return probeStarts;
		}

		probeRow = row;
		while (probe < ranges.Length && ranges[probe].FirstRow < row)
		{
			probe++;
		}

		probeStarts = false;
		for (var i = probe; i < ranges.Length && ranges[i].FirstRow == row; i++)
		{
			if (TryGetRun(ranges[i], layout, out _, out _))
			{
				probeStarts = true;

				break;
			}
		}

		return probeStarts;
	}

	/// <summary>
	/// Moves to a row: closes the ranges that end before it, and opens the ranges that cover it. A range that starts in a
	/// row before it, which the scanner didn't read, opens without a value. Nothing changes when the row is the current one.
	/// </summary>
	/// <param name="row">The row, from 1, at or after the current one.</param>
	public void Advance(long row)
	{
		if (row <= at)
		{
			return;
		}

		at = row;
		startingCount = 0;
		startingNext = 0;
		while (closed < ends.Length && ranges[ends[closed]].LastRow < row)
		{
			Close(ends[closed]);
			closed++;
		}

		while (next < ranges.Length && ranges[next].FirstRow <= row)
		{
			var index = next++;
			var range = ranges[index];
			if (range.LastRow < row || !TryGetRun(range, layout, out var first, out var last))
			{
				continue;
			}

			starts.Set(first);
			valued.Clear(first);
			values[first] = null;
			rangeAt[first] = index;
			lastAt[first] = last;
			if (range.FirstRow == row)
			{
				starting[startingCount++] = index;
			}
		}
	}

	/// <summary>
	/// Whether a cell of the current row is the top-left cell of an open range, and so is read into its store rather than
	/// into a record (<see cref="MergedCapture"/>). The cells of a row are asked about in column order; a cell read again
	/// after a refill is asked about again.
	/// </summary>
	/// <param name="column">The cell's column, from 0.</param>
	/// <returns>Whether it is a range's top-left cell.</returns>
	public bool IsTopLeft(int column)
	{
		while (startingNext < startingCount && ranges[starting[startingNext]].FirstColumnIndex < column)
		{
			startingNext++;
		}

		if (startingNext == startingCount || ranges[starting[startingNext]].FirstColumnIndex != column)
		{
			return false;
		}

		TryGetRun(ranges[starting[startingNext]], layout, out capturing, out _);
		valued.Clear(capturing);

		return true;
	}

	/// <summary>Whether a field is in the run of an open range, so that its cell reads as the range's top-left cell.</summary>
	/// <param name="field">The field.</param>
	/// <returns>Whether an open range covers it.</returns>
	public bool Covers(int field)
	{
		var first = starts.Previous(field);

		return first >= 0 && lastAt[first] >= field;
	}

	/// <summary>The first field of the next run, from a field on, of an open range with a value to fill with.</summary>
	/// <param name="from">The field to look from.</param>
	/// <returns>The run's first field; -1 when there is none.</returns>
	public int NextFill(int from) => valued.Next(from);

	/// <summary>The last field of an open range's run.</summary>
	/// <param name="first">The run's first field.</param>
	/// <returns>The last field.</returns>
	public int LastField(int first) => lastAt[first];

	/// <summary>The value an open range fills with: its top-left cell's.</summary>
	/// <param name="first">The run's first field.</param>
	/// <returns>The value, as UTF-8.</returns>
	public ReadOnlySpan<byte> Value(int first) => values[first].AsSpan(0, lengths[first]);

	/// <summary>The kind of the value an open range fills with.</summary>
	/// <param name="first">The run's first field.</param>
	/// <returns>The kind.</returns>
	public SheetCellKind Kind(int first) => kinds[first];

	/// <summary>
	/// Whether a run's first field is, in a row, the field of its range's top-left cell itself: the one fill whose bytes
	/// the cell's XML counted when it was read, as any cell's are.
	/// </summary>
	/// <param name="first">The run's first field.</param>
	/// <param name="row">The row being written.</param>
	/// <returns>Whether the field is the top-left cell's own.</returns>
	public bool IsOwnCell(int first, long row)
	{
		var range = ranges[rangeAt[first]];

		return range.FirstRow == row && layout.FieldOf(range.FirstColumnIndex) == first;
	}

	/// <summary>The last row that an open range with a value covers, for the rows to fill after the file's last.</summary>
	/// <returns>The row; 0 when no open range has a value.</returns>
	public long LastFilledRow()
	{
		long last = 0;
		for (var first = valued.Next(0); first >= 0; first = valued.Next(lastAt[first] + 1))
		{
			last = Math.Max(last, ranges[rangeAt[first]].LastRow);
		}

		return last;
	}

	/// <summary>Room for the value of the top-left cell being read, for <see cref="MergedCapture"/>.</summary>
	/// <param name="sizeHint">The most bytes the value can take.</param>
	/// <returns>At least <paramref name="sizeHint"/> bytes.</returns>
	public Span<byte> CaptureSpan(int sizeHint)
	{
		var room = values[capturing];
		if (room is null || room.Length < sizeHint)
		{
			room = new byte[Math.Max(sizeHint, 16)];
			values[capturing] = room;
		}

		return room;
	}

	/// <summary>Keeps the value of the top-left cell being read, written at the start of <see cref="CaptureSpan"/>'s room.</summary>
	/// <param name="length">The value's bytes.</param>
	/// <param name="kind">Its kind.</param>
	public void Commit(int length, SheetCellKind kind)
	{
		lengths[capturing] = length;
		kinds[capturing] = kind;
		valued.Set(capturing);
	}

	/// <summary>The run of fields a range fills, if it is used: more than one cell, its rows meeting the area's, and a column that is a field.</summary>
	/// <param name="range">The range.</param>
	/// <param name="layout">The scanner's layout.</param>
	/// <param name="first">The run's first field.</param>
	/// <param name="last">The run's last field.</param>
	/// <returns>Whether the range is used.</returns>
	private static bool TryGetRun(ExcelRange range, SheetLayout layout, out int first, out int last)
	{
		first = -1;
		last = -1;
		if ((range.RowCount == 1 && range.ColumnCount == 1) || range.LastRow < layout.FirstRow || range.FirstRow > layout.LastRow)
		{
			return false;
		}

		return layout.TryGetFields(range.FirstColumnIndex, range.LastColumnIndex, out first, out last);
	}

	/// <summary>Closes a range, if it is open, freeing its field.</summary>
	/// <param name="index">The range's index.</param>
	private void Close(int index)
	{
		if (TryGetRun(ranges[index], layout, out var first, out _) && starts.Get(first) && rangeAt[first] == index)
		{
			starts.Clear(first);
			valued.Clear(first);
			values[first] = null;
		}
	}

	/// <summary>
	/// A set of a bit per field, up to 16,384, that finds the next set bit after a field and the last before it in a few
	/// words: a word per 64 fields, and a summary bit per word.
	/// </summary>
	private sealed class FieldBits
	{
		private readonly ulong[] words;
		private readonly ulong[] summary;

		/// <summary>Creates an empty set.</summary>
		/// <param name="count">The number of fields.</param>
		public FieldBits(int count)
		{
			words = new ulong[(count + 63) >> 6];
			summary = new ulong[(words.Length + 63) >> 6];
		}

		/// <summary>Whether any bit is set.</summary>
		public bool Any
		{
			get
			{
				foreach (var word in summary)
				{
					if (word != 0)
					{
						return true;
					}
				}

				return false;
			}
		}

		/// <summary>Whether a field's bit is set.</summary>
		/// <param name="field">The field.</param>
		/// <returns>Whether it is set.</returns>
		public bool Get(int field) => (words[field >> 6] & (1UL << (field & 63))) != 0;

		/// <summary>Sets a field's bit.</summary>
		/// <param name="field">The field.</param>
		public void Set(int field)
		{
			var word = field >> 6;
			words[word] |= 1UL << (field & 63);
			summary[word >> 6] |= 1UL << (word & 63);
		}

		/// <summary>Clears a field's bit.</summary>
		/// <param name="field">The field.</param>
		public void Clear(int field)
		{
			var word = field >> 6;
			words[word] &= ~(1UL << (field & 63));
			if (words[word] == 0)
			{
				summary[word >> 6] &= ~(1UL << (word & 63));
			}
		}

		/// <summary>The first set field at or after a field.</summary>
		/// <param name="from">The field to look from; past the last, there is none.</param>
		/// <returns>The field; -1 when there is none.</returns>
		public int Next(int from)
		{
			var word = from >> 6;
			if (word >= words.Length)
			{
				return -1;
			}

			var bits = words[word] & (ulong.MaxValue << (from & 63));
			if (bits != 0)
			{
				return (word << 6) + BitOperations.TrailingZeroCount(bits);
			}

			// The next word with a bit, through the summary.
			for (var start = word + 1; start < words.Length;)
			{
				var group = start >> 6;
				var marks = summary[group] & (ulong.MaxValue << (start & 63));
				if (marks != 0)
				{
					var found = (group << 6) + BitOperations.TrailingZeroCount(marks);

					return (found << 6) + BitOperations.TrailingZeroCount(words[found]);
				}

				start = (group + 1) << 6;
			}

			return -1;
		}

		/// <summary>The last set field at or before a field.</summary>
		/// <param name="at">The field to look back from.</param>
		/// <returns>The field; -1 when there is none.</returns>
		public int Previous(int at)
		{
			var word = at >> 6;
			var bits = words[word] & (ulong.MaxValue >> (63 - (at & 63)));
			if (bits != 0)
			{
				return (word << 6) + 63 - BitOperations.LeadingZeroCount(bits);
			}

			// The last word before it with a bit, through the summary.
			for (var end = word - 1; end >= 0;)
			{
				var group = end >> 6;
				var marks = summary[group] & (ulong.MaxValue >> (63 - (end & 63)));
				if (marks != 0)
				{
					var found = (group << 6) + 63 - BitOperations.LeadingZeroCount(marks);

					return (found << 6) + 63 - BitOperations.LeadingZeroCount(words[found]);
				}

				end = (group << 6) - 1;
			}

			return -1;
		}
	}
}

/// <summary>
/// The <see cref="IRecordTarget"/> that reads a merged range's top-left cell into the range's store
/// (<see cref="MergedFill"/>), through the scanner's code for writing a cell's field, so that the fill is the field the
/// cell would be. It takes one field, and no records.
/// </summary>
internal readonly struct MergedCapture : IRecordTarget
{
	private readonly MergedFill fill;

	/// <summary>Creates the target.</summary>
	/// <param name="fill">The fill state, at the top-left cell being read (<see cref="MergedFill.IsTopLeft"/>).</param>
	public MergedCapture(MergedFill fill) => this.fill = fill;

	/// <inheritdoc/>
	public bool IsFull => false;

	/// <inheritdoc/>
	public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
	{
		utf8.CopyTo(fill.CaptureSpan(utf8.Length));
		fill.Commit(utf8.Length, kind);
	}

	/// <inheritdoc/>
	public Span<byte> GetFieldSpan(int sizeHint) => fill.CaptureSpan(sizeHint);

	/// <inheritdoc/>
	public void CommitField(int length, SheetCellKind kind) => fill.Commit(length, kind);

	/// <inheritdoc/>
	public void EndRecord(long row)
	{
	}
}
