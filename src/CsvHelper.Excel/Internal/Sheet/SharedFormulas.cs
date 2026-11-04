using System;
using System.Collections.Generic;
using System.IO;

namespace CsvHelper.Excel;

/// <summary>
/// A sheet's shared formulas, by their index (<c>si</c>): the formula, row and column of the cell that defines each group,
/// so that the group's other cells get the formula moved to them (<see cref="FormulaShifter"/>).
/// </summary>
/// <remarks>
/// The groups are held while the sheet is read, so they count toward <c>ExcelReaderOptions.MaxPartSize</c>, as a part held
/// in memory does: each its formula's bytes and <see cref="GroupSize"/> more, and the room the moved formulas are written
/// in its size. Past the limit, <see cref="Define"/> and <see cref="TryGet"/> throw <see cref="InvalidDataException"/>.
/// </remarks>
internal sealed class SharedFormulas
{
	/// <summary>The bytes a group counts for besides its formula's: its entry, and its array's header.</summary>
	private const int GroupSize = 64;

	/// <summary>The room for moved formulas to start with.</summary>
	private const int InitialRoom = 256;

	private readonly Dictionary<int, Group> groups = [];
	private readonly string partName;
	private readonly long maxSize;
	// The bytes counted toward the limit: the groups' and the room's.
	private long size;
	// Where moved formulas are written, after an = at its start.
	private byte[] room = [];

	/// <summary>Creates the store of a sheet's shared formulas.</summary>
	/// <param name="partName">The sheet's part name, for the limit's error.</param>
	/// <param name="maxSize">The most bytes the groups and the room can take: <c>ExcelReaderOptions.MaxPartSize</c>.</param>
	public SharedFormulas(string partName, long maxSize)
	{
		this.partName = partName;
		this.maxSize = maxSize;
	}

	/// <summary>
	/// Defines a group: the formula of the cell that defines it, or none, for a group whose cells read their values. A later
	/// definition of the same index replaces it.
	/// </summary>
	/// <param name="index">The group's index (<c>si</c>).</param>
	/// <param name="formula">The formula, as UTF-8, without the <c>=</c>; empty for none.</param>
	/// <param name="row">The defining cell's row.</param>
	/// <param name="column">The defining cell's column, from 0.</param>
	/// <exception cref="InvalidDataException">The groups pass the limit.</exception>
	public void Define(int index, ReadOnlySpan<byte> formula, long row, int column)
	{
		var replaced = groups.TryGetValue(index, out var old) ? old.Formula.Length + GroupSize : 0;
		Charge(formula.Length + GroupSize - replaced);
		groups[index] = new Group(formula.ToArray(), (int)row, column);
	}

	/// <summary>Whether a cell defined a group, and whether with a formula.</summary>
	/// <param name="index">The group's index (<c>si</c>).</param>
	/// <param name="hasFormula">Whether the group has a formula; without one, its cells read their values.</param>
	/// <returns>Whether a cell defined the group.</returns>
	public bool TryFind(int index, out bool hasFormula)
	{
		var found = groups.TryGetValue(index, out var group);
		hasFormula = found && group.Formula.Length > 0;

		return found;
	}

	/// <summary>The formula of a cell of a group: the defining cell's, moved to the cell.</summary>
	/// <param name="index">The group's index (<c>si</c>).</param>
	/// <param name="row">The cell's row.</param>
	/// <param name="column">The cell's column, from 0.</param>
	/// <param name="formula">
	/// The formula, as UTF-8, with <c>=</c> before it, valid until the next call; empty when the group has none, so the
	/// cell reads its value.
	/// </param>
	/// <returns>Whether a cell defined the group.</returns>
	/// <exception cref="InvalidDataException">The room for the moved formula passes the limit.</exception>
	public bool TryGet(int index, long row, int column, out ReadOnlySpan<byte> formula)
	{
		formula = default;
		if (!groups.TryGetValue(index, out var group))
		{
			return false;
		}

		if (group.Formula.Length == 0)
		{
			return true;
		}

		// The room grows until the moved formula fits, as it does in MaxGrowth times the formula's length.
		var rows = (int)(row - group.Row);
		var columns = column - group.Column;
		var most = 1 + (long)FormulaShifter.MaxGrowth * group.Formula.Length;
		if (room.Length == 0)
		{
			Grow(Math.Min(Math.Max(InitialRoom, 1 + 2L * group.Formula.Length), most));
		}

		while (true)
		{
			if (FormulaShifter.TryShift(group.Formula, rows, columns, room.AsSpan(1), out var written))
			{
				formula = room.AsSpan(0, written + 1);

				return true;
			}

			Grow(Math.Max(Math.Min(2L * room.Length, most), room.Length + 1));
		}
	}

	/// <summary>Grows the room for moved formulas, with its <c>=</c>.</summary>
	/// <param name="length">The room's new length.</param>
	/// <exception cref="InvalidDataException">The room passes the limit.</exception>
	private void Grow(long length)
	{
		Charge(length - room.Length);
		room = new byte[length];
		room[0] = (byte)'=';
	}

	/// <summary>Counts bytes toward the limit.</summary>
	/// <param name="count">The bytes; negative for bytes freed.</param>
	/// <exception cref="InvalidDataException">The bytes counted pass the limit.</exception>
	private void Charge(long count)
	{
		if (count > maxSize - size)
		{
			throw new InvalidDataException($"The shared formulas of {partName} take more than ExcelReaderOptions.MaxPartSize ({maxSize} bytes).");
		}

		size += count;
	}

	/// <summary>A group: its defining cell's formula and place.</summary>
	private readonly struct Group
	{
		/// <summary>Creates the group.</summary>
		/// <param name="formula">The formula, as UTF-8, without the <c>=</c>; empty for none.</param>
		/// <param name="row">The defining cell's row.</param>
		/// <param name="column">The defining cell's column, from 0.</param>
		public Group(byte[] formula, int row, int column)
		{
			Formula = formula;
			Row = row;
			Column = column;
		}

		/// <summary>The formula, as UTF-8, without the <c>=</c>; empty for none.</summary>
		public byte[] Formula { get; }

		/// <summary>The defining cell's row.</summary>
		public int Row { get; }

		/// <summary>The defining cell's column, from 0.</summary>
		public int Column { get; }
	}
}
