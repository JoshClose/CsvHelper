using System;
using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// The workbook's cell formats as they were when a parallel write's view was frozen (<see cref="StyleRegistry.Snapshot"/>),
/// for the workers, which look styles up without adding any: a lookup that would add a cell format, a font or a custom
/// number format misses, and the worker's chunk is written again serially, where the registry adds it in the serial
/// write's order. So a parallel write's <c>styles.xml</c> is the serial write's. Immutable, so any number of threads read
/// it at once.
/// </summary>
internal sealed class StyleSnapshot
{
	// Copies of the registry's lookups, as StyleRegistry names them.
	private readonly Dictionary<CellXf, int> indexes;
	private readonly CellXf[] formats;
	private readonly int[] fixedIndexes;
	private readonly int[] unlockedIndexes;
	private readonly Dictionary<string, int> numberFormatIds;

	/// <summary>Creates a snapshot of copies of a registry's lookups (<see cref="StyleRegistry.Snapshot"/>).</summary>
	/// <param name="indexes">Each cell format's index.</param>
	/// <param name="formats">The cell formats, by index.</param>
	/// <param name="fixedIndexes">The cell format of each fixed format, 0 for one not used yet.</param>
	/// <param name="unlockedIndexes">The unlocked form of each cell format, by index, 0 for one not asked for yet.</param>
	/// <param name="numberFormatIds">The id of each number format code used.</param>
	public StyleSnapshot(Dictionary<CellXf, int> indexes, CellXf[] formats, int[] fixedIndexes, int[] unlockedIndexes, Dictionary<string, int> numberFormatIds)
	{
		this.indexes = indexes;
		this.formats = formats;
		this.fixedIndexes = fixedIndexes;
		this.unlockedIndexes = unlockedIndexes;
		this.numberFormatIds = numberFormatIds;
	}

	/// <summary>The number of cell formats, the default one included.</summary>
	public int Count => formats.Length;

	/// <summary>A cell format, by its index.</summary>
	/// <param name="index">The index, below <see cref="Count"/>.</param>
	/// <returns>The cell format.</returns>
	public CellXf this[int index] => formats[index];

	/// <summary>The cell format of one of the writer's own number formats, as <see cref="StyleRegistry.IndexOf(FixedFormat)"/> gives it, when it has one.</summary>
	/// <param name="format">The format.</param>
	/// <param name="index">Its index; 0 when it misses.</param>
	/// <returns>Whether the registry has it.</returns>
	public bool TryIndexOf(FixedFormat format, out int index)
	{
		index = fixedIndexes[(int)format];

		return index != 0 || TryIndexOf(StyleRegistry.Code(format), out index);
	}

	/// <summary>The cell format of a number format code in Calibri 11 without wrap, as <see cref="StyleRegistry.IndexOf(string)"/> gives it, when it has one.</summary>
	/// <param name="code">The code.</param>
	/// <param name="index">Its index; 0 when it misses (or for General).</param>
	/// <returns>Whether the registry has it: always for General or an empty code.</returns>
	public bool TryIndexOf(string code, out int index)
	{
		if (!TryNumberFormatId(code, out var id))
		{
			index = 0;

			return false;
		}

		return TryIndexOf(new CellXf(id, CellFont.Regular, false), out index);
	}

	/// <summary>The index of a cell format, as <see cref="StyleRegistry.IndexOf(CellXf)"/> gives it, when it has one.</summary>
	/// <param name="format">The cell format.</param>
	/// <param name="index">Its index; 0 when it misses.</param>
	/// <returns>Whether the registry has it.</returns>
	public bool TryIndexOf(CellXf format, out int index) => indexes.TryGetValue(format, out index);

	/// <summary>The index of a cell format's unlocked form, as <see cref="StyleRegistry.Unlocked"/> gives it, when it has one.</summary>
	/// <param name="index">The cell format's index, below <see cref="Count"/>.</param>
	/// <param name="unlocked">The unlocked form's index; 0 when it misses.</param>
	/// <returns>Whether the registry has it.</returns>
	public bool TryUnlocked(int index, out int unlocked)
	{
		unlocked = index < unlockedIndexes.Length ? unlockedIndexes[index] : 0;

		return unlocked != 0 || TryIndexOf(formats[index] with { Unlocked = true }, out unlocked);
	}

	/// <summary>
	/// The id of a number format code, as <see cref="StyleRegistry.NumberFormatId"/> gives it, when giving it adds nothing
	/// that the workbook writes: General, a code used before, or a built-in format's.
	/// </summary>
	/// <param name="code">The code.</param>
	/// <param name="id">Its id; -1 when it misses.</param>
	/// <returns>Whether it has one: not for a custom code not used yet.</returns>
	public bool TryNumberFormatId(string code, out int id)
	{
		if (code.Length == 0 || string.Equals(code, "General", StringComparison.OrdinalIgnoreCase))
		{
			id = 0;

			return true;
		}

		if (numberFormatIds.TryGetValue(code, out id))
		{
			return true;
		}

		id = StyleRegistry.BuiltInId(code);

		return id >= 0;
	}
}
