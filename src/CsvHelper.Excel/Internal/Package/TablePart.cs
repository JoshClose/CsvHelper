using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// An Excel table's part (<c>xl/tables/table1.xml</c>), which a worksheet's <c>table</c> relationship names: the table's
/// name, its block of cells, its header and totals rows, and its columns' names.
/// </summary>
/// <remarks>
/// <para>
/// The name is <c>displayName</c>, which formulas use (<c>=ROWS(Sales)</c>), else <c>name</c>; Excel writes the two alike.
/// <c>ref</c> holds the header row, the data rows and the totals row. <c>headerRowCount</c> is 1 unless the header row is
/// hidden (0); <c>totalsRowCount</c> is 0 unless the table shows totals (1). The columns' names are decoded for their
/// <c>_xHHHH_</c> escapes, as Excel escapes a line break in one.
/// </para>
/// <para>
/// A table without a name or a <c>ref</c>, a <c>ref</c> that is not a block, a count that is not one, or header and totals
/// rows that are more than the <c>ref</c>'s rows throw <see cref="InvalidDataException"/>, as the package's other
/// attributes do.
/// </para>
/// </remarks>
internal sealed class TablePart
{
	/// <summary>Creates the table.</summary>
	/// <param name="partName">The table's part.</param>
	/// <param name="name">The table's name.</param>
	/// <param name="range">The table's cells.</param>
	/// <param name="headerRowCount">The number of header rows.</param>
	/// <param name="totalsRowCount">The number of totals rows.</param>
	/// <param name="columns">The columns' names.</param>
	private TablePart(string partName, string name, ExcelRange range, int headerRowCount, int totalsRowCount, string[] columns)
	{
		PartName = partName;
		Name = name;
		Range = range;
		HeaderRowCount = headerRowCount;
		TotalsRowCount = totalsRowCount;
		Columns = new ReadOnlyCollection<string>(columns);
	}

	/// <summary>The table's part, such as <c>xl/tables/table1.xml</c>.</summary>
	public string PartName { get; }

	/// <summary>The table's name: its <c>displayName</c>, else its <c>name</c>.</summary>
	public string Name { get; }

	/// <summary>The table's cells (<c>ref</c>): its header, data and totals rows.</summary>
	public ExcelRange Range { get; }

	/// <summary>The number of header rows at the top of <see cref="Range"/>: 1, or 0 when the header row is hidden.</summary>
	public int HeaderRowCount { get; }

	/// <summary>The number of totals rows at the bottom of <see cref="Range"/>: 0, or 1 when the table shows totals.</summary>
	public int TotalsRowCount { get; }

	/// <summary>The columns' names, in order.</summary>
	public IReadOnlyList<string> Columns { get; }

	/// <summary>Reads a table part.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="partName">The table's part, normalized; it must exist.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the table.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, passes a limit, or describes no table.</exception>
	public static async ValueTask<TablePart> LoadAsync(PackageArchive archive, string partName, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var scanner = await archive.OpenXmlAsync(partName, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("table"u8))
			{
				throw scanner.Error("its root element is not table");
			}

			string? displayName = null;
			string? name = null;
			ExcelRange? range = null;
			var headerRowCount = 1;
			var totalsRowCount = 0;
			var attributes = scanner.Attributes;
			while (attributes.MoveNext())
			{
				if (attributes.IsName("displayName"u8))
				{
					displayName = attributes.DecodeValue();
				}
				else if (attributes.IsName("name"u8))
				{
					name = attributes.DecodeValue();
				}
				else if (attributes.IsName("ref"u8))
				{
					range = AreaReference.TryParseArea(attributes.DecodeValue().AsSpan(), out var value)
						? value
						: throw attributes.InvalidValue("a range of cells, such as A1:D10");
				}
				else if (attributes.IsName("headerRowCount"u8))
				{
					headerRowCount = ReadCount(attributes);
				}
				else if (attributes.IsName("totalsRowCount"u8))
				{
					totalsRowCount = ReadCount(attributes);
				}
			}

			var tableName = string.IsNullOrEmpty(displayName) ? name : displayName;
			if (string.IsNullOrEmpty(tableName))
			{
				throw scanner.Error("the table has no name");
			}

			if (range is null)
			{
				throw scanner.Error("the table has no ref");
			}

			if ((long)headerRowCount + totalsRowCount > range.Value.RowCount)
			{
				throw scanner.Error(
					$"its header and totals rows, {(long)headerRowCount + totalsRowCount}, are more than the {range.Value.RowCount} rows of its ref, {range.Value}");
			}

			var columns = new List<string>();
			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				if (scanner.IsStartTag("tableColumns"u8))
				{
					var listDepth = scanner.ElementDepth;
					while (await scanner.ReadChildAsync(listDepth, async, cancellationToken).ConfigureAwait(false))
					{
						if (scanner.IsStartTag("tableColumn"u8))
						{
							columns.Add(ReadColumnName(scanner));
						}

						await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
					}

					continue;
				}

				// autoFilter, sortState, tableStyleInfo, extLst.
				await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
			}

			return new TablePart(partName, tableName!, range.Value, headerRowCount, totalsRowCount, [.. columns]);
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}
	}

	/// <summary>Reads a row count: an integer 0 or more.</summary>
	/// <param name="attributes">The attributes, at the count.</param>
	/// <returns>The count.</returns>
	/// <exception cref="InvalidDataException">The value is not an integer 0 or more.</exception>
	private static int ReadCount(XmlAttributes attributes)
	{
		var count = attributes.GetInt32();
		if (count < 0)
		{
			throw attributes.InvalidValue("a number of rows (0 or more)");
		}

		return count;
	}

	/// <summary>Reads a <c>tableColumn</c>'s name, with its XML and its <c>_xHHHH_</c> escapes decoded.</summary>
	/// <param name="scanner">The scanner, at the element's start tag.</param>
	/// <returns>The name.</returns>
	/// <exception cref="InvalidDataException">The column has no name, or its name can't be decoded.</exception>
	private static string ReadColumnName(XmlScanner scanner)
	{
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("name"u8))
			{
				var name = new byte[attributes.Value.Length];
				var length = XmlText.DecodeEscapes(name.AsSpan(0, attributes.DecodeValue(name)));

				return Encoding.UTF8.GetString(name, 0, length);
			}
		}

		throw scanner.Error("a table column has no name");
	}
}
