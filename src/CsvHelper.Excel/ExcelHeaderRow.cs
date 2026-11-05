using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvHelper.Excel;

/// <summary>
/// Where a reader's header row is, when it isn't the first row read (<see cref="ExcelReaderOptions.HeaderRow"/>): a row
/// by its number (<see cref="At"/>), or the first row, of the first rows read, that has every column a record type
/// requires (<see cref="Find{T}(int)"/>) or every one of some names (<see cref="Find(IEnumerable{string}, int)"/>).
/// Immutable.
/// </summary>
/// <remarks>
/// <para>
/// Rows above the header row are skipped, and the columns are clipped to the header's: from its first cell with a value
/// to its last. So a report's title, logo and notes above its header, and notes beside its data, are not read, and field
/// 0 is the header's first column.
/// </para>
/// <para>
/// A search tries the rows that have a value, after hidden rows are skipped and merged cells filled when the reader's
/// options say so, as the reader would read them as its header: a typed cell names its column as its text (a date names
/// it <c>2024-01-15</c>), and the reader's header matching applies. Rows count by number, whether the file has them or
/// not.
/// </para>
/// </remarks>
public sealed class ExcelHeaderRow
{
	/// <summary>The default number of rows a search tries.</summary>
	private const int DefaultMaxRows = 20;

	// For a search: the columns a header row lacks, as a probe reader whose header is the row finds them.
	private readonly Func<CsvReader, IReadOnlyList<string>>? lacks;
	// For Find<T>: the type's name; for Find(names): the names.
	private readonly string? typeName;
	private readonly string[]? names;

	/// <summary>Creates a header row given by its number.</summary>
	/// <param name="row">The row, from 1.</param>
	private ExcelHeaderRow(int row)
	{
		Row = row;
		MaxRows = 1;
	}

	/// <summary>Creates a search for the header row.</summary>
	/// <param name="maxRows">The number of rows tried.</param>
	/// <param name="typeName">The record type's name, for <see cref="Find{T}(int)"/>; otherwise <see langword="null"/>.</param>
	/// <param name="names">The names, for <see cref="Find(IEnumerable{string}, int)"/>; otherwise <see langword="null"/>.</param>
	/// <param name="lacks">The columns a probe reader's header lacks.</param>
	private ExcelHeaderRow(int maxRows, string? typeName, string[]? names, Func<CsvReader, IReadOnlyList<string>> lacks)
	{
		MaxRows = maxRows;
		this.typeName = typeName;
		this.names = names;
		this.lacks = lacks;
	}

	/// <summary>The row given (<see cref="At"/>), from 1; 0 for a search.</summary>
	internal int Row { get; }

	/// <summary>The number of rows a search tries, from the first row read; 1 for a row given.</summary>
	internal int MaxRows { get; }

	/// <summary>
	/// What a search wants of a row, for messages: such as <c>the columns that Order requires</c> or
	/// <c>the columns 'Name', 'Total'</c>.
	/// </summary>
	internal string Wants => typeName is not null
		? $"the columns that {typeName} requires"
		: "the columns " + string.Join(", ", names!.Select(n => $"'{n}'"));

	/// <summary>
	/// The header row is a row of the sheet, by its number, as Excel numbers rows. It must be in the range or defined name
	/// read, if one is.
	/// </summary>
	/// <param name="row">The row's number: 1 to 1,048,576.</param>
	/// <returns>The header row.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="row"/> is not from 1 to 1,048,576.</exception>
	/// <remarks>
	/// The row is the header even when it is hidden and <see cref="ExcelReaderOptions.SkipHiddenRows"/> is set: it is named.
	/// Opening the reader throws <see cref="CsvFormatException"/> when the row has no value, and
	/// <see cref="ArgumentOutOfRangeException"/> when it isn't in the range or defined name read.
	/// </remarks>
	public static ExcelHeaderRow At(int row)
	{
		if (row < 1 || row > CellReference.MaxRow)
		{
			throw new ArgumentOutOfRangeException(nameof(row), row, $"A row is from 1 to {CellReference.MaxRow}.");
		}

		return new ExcelHeaderRow(row);
	}

	/// <summary>
	/// The header row is the first row, of the first <paramref name="maxRows"/> rows read, that has every column
	/// <typeparamref name="T"/> requires, as <see cref="CsvReader.GetRecords{T}"/> would find them there.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="CsvRecordAttribute"/> type the rows are read as. Dynamic records require no columns, so for
	/// <c>dynamic</c> the header is the first row with a value.
	/// </typeparam>
	/// <param name="maxRows">
	/// The number of rows tried, from the first row read (the sheet's first, or the range's or defined name's): 1 to
	/// 1,048,576. Rows count by number, whether the file has them or not.
	/// </param>
	/// <returns>The header row.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRows"/> is not from 1 to 1,048,576.</exception>
	/// <remarks>
	/// <para>
	/// A row has the columns when <typeparamref name="T"/>'s mapper finds them in it (<see cref="CsvReader.TryValidateHeader{T}(out IReadOnlyList{string})"/>),
	/// so alternate names, nested records, <see cref="CsvReaderOptions.NormalizeHeaderName"/> and the header's matching
	/// all apply, and optional members are not required.
	/// </para>
	/// <para>
	/// Opening the reader throws <see cref="KeyNotFoundException"/> when no row has the columns, naming the rows tried and
	/// the row that came closest with what it lacks; and <see cref="InvalidOperationException"/> when
	/// <typeparamref name="T"/> has no mapper, as <see cref="CsvReader.GetRecords{T}"/> does. A type with few required
	/// columns can find them in a title row; <see cref="Find(IEnumerable{string}, int)"/> or <see cref="At"/> are the
	/// remedies then.
	/// </para>
	/// </remarks>
	public static ExcelHeaderRow Find<T>(int maxRows = DefaultMaxRows)
	{
		CheckMaxRows(maxRows);

		return new ExcelHeaderRow(maxRows, typeof(T).Name, null, static probe => probe.TryValidateHeader<T>(out var missing) ? [] : missing);
	}

	/// <summary>
	/// The header row is the first row, of the first <paramref name="maxRows"/> rows read, that has every one of
	/// <paramref name="names"/>, each matched as <see cref="CsvReader.TryGetFieldIndex(string, out int)"/> matches it: for
	/// dynamic records, and for reading fields by name.
	/// </summary>
	/// <param name="names">The names, which are copied; none for the first row with a value.</param>
	/// <param name="maxRows">
	/// The number of rows tried, from the first row read (the sheet's first, or the range's or defined name's): 1 to
	/// 1,048,576. Rows count by number, whether the file has them or not.
	/// </param>
	/// <returns>The header row.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A name is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRows"/> is not from 1 to 1,048,576.</exception>
	/// <remarks>
	/// Opening the reader throws <see cref="KeyNotFoundException"/> when no row has the names, naming the rows tried and
	/// the row that came closest with the names it lacks.
	/// </remarks>
	public static ExcelHeaderRow Find(IEnumerable<string> names, int maxRows = DefaultMaxRows)
	{
		ArgumentNullException.ThrowIfNull(names);
		var copy = names.ToArray();
		if (Array.IndexOf(copy, null) >= 0)
		{
			throw new ArgumentException("A name can't be null.", nameof(names));
		}

		CheckMaxRows(maxRows);

		return new ExcelHeaderRow(maxRows, null, copy, probe => [.. copy.Where(n => !probe.TryGetFieldIndex(n, out _)).Select(n => $"'{n}'")]);
	}

	/// <summary>The header row as the code that makes it, such as <c>ExcelHeaderRow.At(4)</c> or <c>ExcelHeaderRow.Find&lt;Order&gt;(20)</c>.</summary>
	/// <returns>The text.</returns>
	public override string ToString()
	{
		var maxRows = MaxRows.ToString(CultureInfo.InvariantCulture);
		if (lacks is null)
		{
			return $"ExcelHeaderRow.At({Row.ToString(CultureInfo.InvariantCulture)})";
		}

		return typeName is not null
			? $"ExcelHeaderRow.Find<{typeName}>({maxRows})"
			: $"ExcelHeaderRow.Find([{string.Join(", ", names!.Select(n => $"\"{n}\""))}], {maxRows})";
	}

	/// <summary>The columns that a probe reader's header lacks, for a search.</summary>
	/// <param name="probe">A reader whose header is the row tried.</param>
	/// <returns>The columns lacking, as the messages name them; empty when the row is the header.</returns>
	/// <exception cref="InvalidOperationException">The record type has no mapper.</exception>
	internal IReadOnlyList<string> Lacks(CsvReader probe) => lacks!(probe);

	/// <summary>Checks the number of rows a search tries.</summary>
	/// <param name="maxRows">The number.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRows"/> is not from 1 to 1,048,576.</exception>
	private static void CheckMaxRows(int maxRows)
	{
		if (maxRows < 1 || maxRows > CellReference.MaxRow)
		{
			throw new ArgumentOutOfRangeException(nameof(maxRows), maxRows, $"maxRows must be from 1 to {CellReference.MaxRow}.");
		}
	}
}
