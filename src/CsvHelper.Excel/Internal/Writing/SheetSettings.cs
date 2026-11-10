using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet looks: its <see cref="ExcelSheetOptions"/>, resolved and checked once when it is added, and its
/// columns' <see cref="ExcelColumnOptions"/>, resolved by the header's names when the header record ends.
/// </summary>
/// <remarks>
/// <para>
/// The sheet's options are the ones given when it is added; else, for a sheet of a record type's records, the type's
/// <see cref="ExcelSheetAttribute"/>; else <see cref="ExcelWriterOptions.Sheets"/>. Each is taken whole. A column's are
/// its entry in <see cref="ExcelSheetOptions.Columns"/>, else its member's <see cref="ExcelColumnAttribute"/>, also whole.
/// </para>
/// <para>
/// The columns' options are copied when the sheet is added, and the copy is checked, so that options changed later (a
/// dictionary is mutable) can't reach the sheet unchecked. Names match ordinally, as the members' formats' do.
/// </para>
/// </remarks>
internal sealed class SheetSettings
{
	// The columns' options by header name, copied; null for none.
	private readonly Dictionary<string, ExcelColumnOptions>? columns;
	// The record type's members' formats and Excel options, or null.
	private readonly MemberFormats? members;

	/// <summary>Creates a sheet's settings.</summary>
	/// <param name="sheet">The sheet's options, checked.</param>
	/// <param name="columns">The columns' options, copied and checked, or <see langword="null"/>.</param>
	/// <param name="members">The record type's members' formats, or <see langword="null"/>.</param>
	/// <param name="hasHeader">Whether the sheet's first record is its header.</param>
	private SheetSettings(ExcelSheetOptions sheet, Dictionary<string, ExcelColumnOptions>? columns, MemberFormats? members, bool hasHeader)
	{
		Sheet = sheet;
		this.columns = columns;
		this.members = members;
		HasHeader = hasHeader;
	}

	/// <summary>
	/// The settings of the empty sheet that a workbook without sheets gets: nothing to show, so nothing shown, whatever
	/// <see cref="ExcelWriterOptions.Sheets"/> says.
	/// </summary>
	public static SheetSettings Empty { get; } = new(new ExcelSheetOptions { BoldHeader = false, FreezeHeader = false, AutoFit = 0 }, null, null, false);

	/// <summary>The sheet's options, as given: <see cref="ExcelSheetOptions.Columns"/> is read from a copy, through <see cref="ColumnsFor"/>.</summary>
	public ExcelSheetOptions Sheet { get; }

	/// <summary>Whether the sheet's first record is its header (<see cref="CsvWriterOptions.HasHeader"/>).</summary>
	public bool HasHeader { get; }

	/// <summary>
	/// Whether columns can have options: <see cref="ExcelSheetOptions.Columns"/> has some, or a member of the record type
	/// has an <see cref="ExcelColumnAttribute"/>. The header's names, or the record type's, then say which.
	/// </summary>
	public bool HasColumns => columns is not null || members is { HasExcelColumns: true };

	/// <summary>The title above the header, or <see langword="null"/>.</summary>
	public string? Title => Sheet.Title;

	/// <summary>Whether the header's cells are bold: with a header, and not in a table with a style, whose style sets its header.</summary>
	public bool BoldHeader => HasHeader && Sheet.BoldHeader && Sheet.Table?.Style is null;

	/// <summary>Whether the rows through the header stay in view: with a header only.</summary>
	public bool FreezeHeader => HasHeader && Sheet.FreezeHeader;

	/// <summary>How many columns stay in view.</summary>
	public int FreezeColumns => Sheet.FreezeColumns;

	/// <summary>Whether the sheet has its own filter: not with a table, whose buttons are the filter.</summary>
	public bool AutoFilter => Sheet.AutoFilter && Sheet.Table is null;

	/// <summary>The table, or <see langword="null"/>.</summary>
	public ExcelTableOptions? Table => Sheet.Table;

	/// <summary>How many records after the header auto-fit measures, or 0 for none.</summary>
	public int AutoFit => Sheet.AutoFit;

	/// <summary>The widest auto-fit makes a column, in characters.</summary>
	public double MaxAutoFitWidth => Sheet.MaxAutoFitWidth;

	/// <summary>Resolves and checks a sheet's settings when it is added.</summary>
	/// <param name="options">The options given when it was added, or <see langword="null"/>.</param>
	/// <param name="recordType">The record type of a sheet of <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/>, or <see langword="null"/>.</param>
	/// <param name="members">The record type's members' formats (<see cref="MemberFormats.For(Type)"/>), or <see langword="null"/>.</param>
	/// <param name="writerOptions">The workbook's options, which are valid.</param>
	/// <returns>The settings.</returns>
	/// <exception cref="ArgumentException">The options given are not valid, or need a header that the sheet doesn't have.</exception>
	/// <exception cref="InvalidOperationException">
	/// The record type's <see cref="ExcelSheetAttribute"/> or <see cref="ExcelColumnAttribute"/>s are not valid, or
	/// <see cref="ExcelWriterOptions.Sheets"/>' columns were changed, after the writer was created, into ones that are not.
	/// </exception>
	public static SheetSettings Resolve(ExcelSheetOptions? options, Type? recordType, MemberFormats? members, ExcelWriterOptions writerOptions)
	{
		var hasHeader = writerOptions.Csv.HasHeader;
		if (members?.Problem is { } attributes)
		{
			throw new InvalidOperationException(attributes);
		}

		if (options is not null)
		{
			var copy = Copy(options.Columns);
			if ((options.Problem(copy) ?? options.HeaderProblem(hasHeader)) is { } problem)
			{
				throw new ArgumentException("ExcelSheetOptions: " + problem, nameof(options));
			}

			return new SheetSettings(options, copy, members, hasHeader);
		}

		if (members?.Sheet is { } sheet)
		{
			if (sheet.HeaderProblem(hasHeader) is { } problem)
			{
				throw new InvalidOperationException($"The [ExcelSheet] of {recordType?.Name}: {problem}");
			}

			return new SheetSettings(sheet, null, members, hasHeader);
		}

		var defaults = writerOptions.Sheets;
		var columns = Copy(defaults.Columns);
		if (defaults.Problem(columns) is { } changed)
		{
			throw new InvalidOperationException("ExcelWriterOptions.Sheets' columns were changed after the writer was created: " + changed);
		}

		return new SheetSettings(defaults, columns, members, hasHeader);
	}

	/// <summary>
	/// Each column's options, for the header's names: its entry in <see cref="ExcelSheetOptions.Columns"/> by its name,
	/// else its member's <see cref="ExcelColumnAttribute"/>. Called when the header record ends, or, without a header, when
	/// the sheet is added, with the names the record type's record writer writes in its header.
	/// </summary>
	/// <param name="names">The header's names, in column order, or <see langword="null"/> when the sheet has none.</param>
	/// <returns>Each column's options, or <see langword="null"/> for a column without; <see langword="null"/> when no column has any.</returns>
	/// <exception cref="InvalidOperationException">A name in <see cref="ExcelSheetOptions.Columns"/> is not one of the header's.</exception>
	public ExcelColumnOptions?[]? ColumnsFor(IReadOnlyList<string>? names)
	{
		names ??= [];
		var result = members?.ExcelColumns(names);
		if (columns is not null)
		{
			if (result is null || result.Length < names.Count)
			{
				var grown = new ExcelColumnOptions?[names.Count];
				result?.CopyTo(grown, 0);
				result = grown;
			}

			var matched = 0;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (var i = 0; i < names.Count; i++)
			{
				if (columns.TryGetValue(names[i], out var column))
				{
					result[i] = column;
					if (seen.Add(names[i]))
					{
						matched++;
					}
				}
			}

			if (matched < columns.Count)
			{
				var missing = string.Join(", ", columns.Keys.Where(k => !seen.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).Select(k => "\"" + k + "\""));
				if (!HasHeader && names.Count == 0)
				{
					throw new InvalidOperationException(
						$"ExcelSheetOptions.Columns names columns, and the sheet has no header to find them by (CsvWriterOptions.HasHeader is off): {missing}.");
				}

				var header = HasHeader ? "the header" : "the record type's header";

				throw new InvalidOperationException(
					$"ExcelSheetOptions.Columns names columns that {header} doesn't have: {missing}. Names match the header's ordinally.");
			}
		}

		return result is not null && Array.Exists(result, c => c is not null) ? result : null;
	}

	/// <summary>Copies columns' options, so that they can be checked once and can't change after.</summary>
	/// <param name="columns">The options, or <see langword="null"/>.</param>
	/// <returns>The copy, or <see langword="null"/> for none or an empty dictionary.</returns>
	private static Dictionary<string, ExcelColumnOptions>? Copy(IReadOnlyDictionary<string, ExcelColumnOptions>? columns)
	{
		if (columns is null || columns.Count == 0)
		{
			return null;
		}

		var copy = new Dictionary<string, ExcelColumnOptions>(columns.Count, StringComparer.Ordinal);
		foreach (var column in columns)
		{
			// Null options stay, for the check to report.
			copy[column.Key] = column.Value;
		}

		return copy;
	}
}
