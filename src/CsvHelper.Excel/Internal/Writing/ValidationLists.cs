using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The workbook's dropdown lists that don't go in their validations (<see cref="ColumnValidation.IsInline"/>): each
/// distinct one is a column of text cells in the lists sheet, very hidden, written after the workbook's other sheets, with
/// a hidden name over it that the validations refer to (Phase 7's decision 5).
/// </summary>
/// <remarks>
/// <para>
/// A sheet's validations are written when the sheet ends, before the workbook knows the lists sheet's name, which is
/// <c>Lists</c>, or the first of <c>Lists2</c>, <c>Lists3</c>, … that no sheet has once every sheet is added; so each list
/// has a name, <c>_CsvHelperList1</c> on, written with the workbook's names, which are the workbook's tables' too: a list
/// takes the first number whose name no table has, and a table can't then take it.
/// </para>
/// <para>
/// The sheet is protected without a password, so that its values aren't changed by a slip in VBA's editor, the only place
/// Excel shows a very hidden sheet; LibreOffice shows it as hidden (checked on 2026-11-14). Its cells are inline strings,
/// whatever <see cref="ExcelWriterOptions.Strings"/> says, since the sheet is written apart from the others. Excel matches a
/// value typed into a column with such a list ignoring case (checked).
/// </para>
/// </remarks>
internal sealed class ValidationLists
{
	/// <summary>The start of each list's name.</summary>
	public const string NamePrefix = "_CsvHelperList";

	/// <summary>The lists sheet's name, which a number follows when a sheet has it.</summary>
	public const string SheetStem = "Lists";

	/// <summary>The sheet protection of the lists sheet, without a password, as Excel writes it.</summary>
	private static readonly byte[] Protection = "<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/>"u8.ToArray();

	// The workbook's tables' names, which the lists' join.
	private readonly TableNames names;
	// Each list's index, by its values; and the lists, in the order of their columns.
	private readonly Dictionary<string[], int> indexes = new(ValuesComparer.Instance);
	private readonly List<(string Name, string[] Values)> lists = [];
	// The number of the next list's name.
	private int next = 1;

	/// <summary>Creates a workbook's lists.</summary>
	/// <param name="names">The workbook's tables' names, which the lists' names are added to.</param>
	public ValidationLists(TableNames names) => this.names = names;

	/// <summary>The number of lists, each a column of the lists sheet.</summary>
	public int Count => lists.Count;

	/// <summary>A list's name, its values the lists sheet's next column the first time they are asked for.</summary>
	/// <param name="values">The values, each once, of 1 to 255 characters.</param>
	/// <returns>The name, such as <c>_CsvHelperList1</c>.</returns>
	/// <exception cref="InvalidOperationException">The lists sheet has a list in each of Excel's 16,384 columns.</exception>
	public string NameOf(string[] values)
	{
		if (indexes.TryGetValue(values, out var index))
		{
			return lists[index].Name;
		}

		if (lists.Count == CellReference.MaxColumns)
		{
			throw new InvalidOperationException(
				$"The workbook's dropdown lists that don't fit in a validation go in its lists sheet, a list a column, and it has {CellReference.MaxColumns.ToString("N0", CultureInfo.InvariantCulture)}, Excel's columns: write fewer distinct long lists.");
		}

		string name;
		do
		{
			name = NamePrefix + (next++).ToString(CultureInfo.InvariantCulture);
		}
		while (names.Existing(name) is not null);

		names.Add(name);
		indexes.Add(values, lists.Count);
		lists.Add((name, values));

		return name;
	}

	/// <summary>Whether a name is one of the lists'.</summary>
	/// <param name="name">The name, as the workbook has it.</param>
	/// <returns>Whether it is.</returns>
	public bool IsList(string name)
	{
		foreach (var list in lists)
		{
			if (string.Equals(list.Name, name, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Writes the lists sheet: each list a column, from row 1, of inline text cells, in rows as Excel writes them; and its
	/// protection, without a password.
	/// </summary>
	/// <param name="package">The package.</param>
	/// <param name="partName">The sheet's part.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the part is written.</returns>
	public async ValueTask WriteSheetAsync(PackageWriter package, string partName, bool async, CancellationToken cancellationToken)
	{
		using var xml = new XmlOutput(SheetSink.FlushSize + 4096);
		xml.Append(PackageParts.Declaration);
		xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheetData>"u8);
		var entry = await package.OpenPartAsync(partName, async, cancellationToken).ConfigureAwait(false);
		try
		{
			// The lists with a value in the row, in column order: each row drops those that ended in the row before.
			var active = new List<int>(lists.Count);
			var longest = 0;
			for (var i = 0; i < lists.Count; i++)
			{
				active.Add(i);
				longest = Math.Max(longest, lists[i].Values.Length);
			}

			for (var row = 1; row <= longest; row++)
			{
				xml.Append("<row r=\""u8);
				xml.AppendNumber(row);
				xml.Append("\">"u8);
				var ended = false;
				foreach (var i in active)
				{
					var values = lists[i].Values;
					xml.Append("<c r=\""u8);
					xml.AppendReference(SheetSink.ColumnName(i), row);
					xml.Append("\" t=\"inlineStr\"><is>"u8);
					SheetSink.AppendElement(xml, Encoding.UTF8.GetBytes(values[row - 1]));
					xml.Append("</is></c>"u8);
					ended |= values.Length == row;
				}

				xml.Append("</row>"u8);
				if (ended)
				{
					active.RemoveAll(i => lists[i].Values.Length == row);
				}

				if (xml.Length >= SheetSink.FlushSize)
				{
					await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
					xml.Truncate(0);
				}
			}

			xml.Append("</sheetData>"u8);
			xml.Append(Protection);
			xml.Append(SheetSink.PageMargins);
			xml.Append("</worksheet>"u8);
			await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await PackageWriter.CloseAsync(entry, async).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Writes each list's hidden name, over its column of the lists sheet (<c>Lists!$A$1:$A$3</c>), in the workbook's
	/// <c>definedNames</c>.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="sheetName">The lists sheet's name.</param>
	public void AppendNames(XmlOutput xml, string sheetName)
	{
		var sheet = SheetLocation.Quote(sheetName) + "!$";
		for (var i = 0; i < lists.Count; i++)
		{
			var (name, values) = lists[i];
			var column = CellReference.ColumnName(i);
			xml.Append("<definedName name=\""u8);
			xml.Append(Encoding.ASCII.GetBytes(name));
			xml.Append("\" hidden=\"1\">"u8);
			xml.AppendText(Encoding.UTF8.GetBytes(sheet + column + "$1:$" + column + "$" + values.Length.ToString(CultureInfo.InvariantCulture)));
			xml.Append("</definedName>"u8);
		}
	}

	/// <summary>Compares lists by their values, ordinally, in order.</summary>
	private sealed class ValuesComparer : IEqualityComparer<string[]>
	{
		/// <summary>The comparer.</summary>
		public static readonly ValuesComparer Instance = new();

		/// <summary>Whether two lists have the same values, in the same order.</summary>
		/// <param name="x">A list.</param>
		/// <param name="y">Another.</param>
		/// <returns>Whether they have.</returns>
		public bool Equals(string[]? x, string[]? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x is null || y is null || x.Length != y.Length)
			{
				return false;
			}

			for (var i = 0; i < x.Length; i++)
			{
				if (!string.Equals(x[i], y[i], StringComparison.Ordinal))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>A hash of a list's values.</summary>
		/// <param name="values">The list.</param>
		/// <returns>The hash.</returns>
		public int GetHashCode(string[] values)
		{
			var hash = values.Length;
			foreach (var value in values)
			{
				hash = unchecked((hash * 31) + StringComparer.Ordinal.GetHashCode(value));
			}

			return hash;
		}
	}
}
