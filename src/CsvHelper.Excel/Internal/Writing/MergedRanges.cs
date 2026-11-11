using System;
using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// The ranges merged in a sheet being written (<see cref="ExcelWriterExtensions.MergeCells"/>), in the order added, with an
/// index that finds the one a new range overlaps, since Excel refuses a workbook whose merged ranges overlap.
/// </summary>
/// <remarks>
/// <para>
/// The index is a set of levels, each of a power of two ranges sorted by first row, with a tree of the greatest last row
/// over them; adding a range merges full levels into the next, as a binary counter carries. A search looks in each level
/// at the ranges that start at or above the new range's last row, and goes down the tree only where one ends at or below
/// its first. So a sheet of <c>n</c> ranges costs <c>O(n log n)</c> to add them, and each search <c>O((log n)^2)</c> and the
/// ranges whose rows meet the new range's, whatever their heights: a merged label down a whole column beside a range
/// merged in each row costs each search the label and that row's ranges.
/// </para>
/// <para>
/// The ranges in the index don't overlap, so a cell is in one at most.
/// </para>
/// </remarks>
internal sealed class MergedRanges
{
	// The ranges in the order added, as the sheet writes them.
	private readonly List<ExcelRange> added = [];
	// Level i holds 2^i ranges, or is null.
	private Level?[] levels = new Level?[4];

	/// <summary>The number of ranges.</summary>
	public int Count => added.Count;

	/// <summary>The ranges, in the order added.</summary>
	public IReadOnlyList<ExcelRange> Ranges => added;

	/// <summary>Finds a range that a range overlaps.</summary>
	/// <param name="range">The range.</param>
	/// <returns>A range of the sheet's that has a cell of <paramref name="range"/>, or <see langword="null"/>.</returns>
	public ExcelRange? Overlapping(ExcelRange range)
	{
		foreach (var level in levels)
		{
			if (level?.Overlapping(range) is { } found)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>Adds a range, which overlaps none of the sheet's (<see cref="Overlapping"/>).</summary>
	/// <param name="range">The range.</param>
	public void Add(ExcelRange range)
	{
		added.Add(range);
		ExcelRange[] carry = [range];
		for (var i = 0; ; i++)
		{
			if (i == levels.Length)
			{
				Array.Resize(ref levels, levels.Length * 2);
			}

			if (levels[i] is not { } level)
			{
				levels[i] = new Level(carry);

				return;
			}

			carry = Merge(level.Ranges, carry);
			levels[i] = null;
		}
	}

	/// <summary>
	/// Whether a cell's width counts nothing in auto-fit, as in Excel's AutoFit: it is in a range merged across two columns
	/// or more. A range merged down one column counts each of its cells, those below its first row too, which Excel keeps
	/// though it shows the first alone (checked through COM on 2026-11-10).
	/// </summary>
	/// <param name="row">The cell's row, from 1.</param>
	/// <param name="column">The cell's column, from 0.</param>
	/// <returns>Whether it counts nothing.</returns>
	public bool HidesWidth(long row, int column)
	{
		if (row > CellReference.MaxRow)
		{
			return false;
		}

		var cell = new ExcelRange((int)row, column + 1, (int)row, column + 1);

		return Overlapping(cell) is { ColumnCount: > 1 };
	}

	/// <summary>Merges two arrays of ranges sorted by first row into one.</summary>
	/// <param name="left">The ranges of a level.</param>
	/// <param name="right">The ranges carried.</param>
	/// <returns>The ranges of both, sorted by first row.</returns>
	private static ExcelRange[] Merge(ExcelRange[] left, ExcelRange[] right)
	{
		var merged = new ExcelRange[left.Length + right.Length];
		var i = 0;
		var j = 0;
		var k = 0;
		while (i < left.Length && j < right.Length)
		{
			merged[k++] = left[i].FirstRow <= right[j].FirstRow ? left[i++] : right[j++];
		}

		while (i < left.Length)
		{
			merged[k++] = left[i++];
		}

		while (j < right.Length)
		{
			merged[k++] = right[j++];
		}

		return merged;
	}

	/// <summary>A level of the index: ranges sorted by first row, and a tree of the greatest last row over them.</summary>
	private sealed class Level
	{
		// The number of leaves of the tree, a power of two; and each node's greatest last row (0 for none), the root at 1.
		private readonly int leaves;
		private readonly int[] maxLastRow;

		/// <summary>Creates a level.</summary>
		/// <param name="ranges">The ranges, sorted by first row.</param>
		public Level(ExcelRange[] ranges)
		{
			Ranges = ranges;
			leaves = 1;
			while (leaves < ranges.Length)
			{
				leaves *= 2;
			}

			maxLastRow = new int[leaves * 2];
			for (var i = 0; i < ranges.Length; i++)
			{
				maxLastRow[leaves + i] = ranges[i].LastRow;
			}

			for (var node = leaves - 1; node >= 1; node--)
			{
				maxLastRow[node] = Math.Max(maxLastRow[node * 2], maxLastRow[(node * 2) + 1]);
			}
		}

		/// <summary>The ranges, sorted by first row.</summary>
		public ExcelRange[] Ranges { get; }

		/// <summary>Finds a range of the level's that a range overlaps.</summary>
		/// <param name="range">The range.</param>
		/// <returns>The range found, or <see langword="null"/>.</returns>
		public ExcelRange? Overlapping(ExcelRange range)
		{
			// The ranges that start at or above the range's last row: the first `end` of them.
			var low = 0;
			var high = Ranges.Length;
			while (low < high)
			{
				var middle = (low + high) / 2;
				if (Ranges[middle].FirstRow <= range.LastRow)
				{
					low = middle + 1;
				}
				else
				{
					high = middle;
				}
			}

			return low == 0 ? null : Find(1, 0, leaves, low, range);
		}

		/// <summary>Finds, under a node, a range among the first ones that a range overlaps.</summary>
		/// <param name="node">The node.</param>
		/// <param name="start">The first leaf under it.</param>
		/// <param name="count">The leaves under it.</param>
		/// <param name="end">The number of ranges to look at, from the first.</param>
		/// <param name="range">The range.</param>
		/// <returns>The range found, or <see langword="null"/>.</returns>
		private ExcelRange? Find(int node, int start, int count, int end, ExcelRange range)
		{
			if (start >= end || maxLastRow[node] < range.FirstRow)
			{
				return null;
			}

			if (count == 1)
			{
				var candidate = Ranges[start];

				return candidate.FirstColumn <= range.LastColumn && range.FirstColumn <= candidate.LastColumn ? candidate : null;
			}

			var half = count / 2;

			return Find(node * 2, start, half, end, range) ?? Find((node * 2) + 1, start + half, half, end, range);
		}
	}
}
