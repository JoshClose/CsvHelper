using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The cell styles of a workbook, from its styles part: the number format of each cell format (<c>cellXfs/xf</c>, which a
/// cell's <c>s</c> attribute indexes), the custom number formats (<c>numFmts</c>), and from them the kind of a number cell
/// in each style. It is immutable once loaded, so the readers of a workbook share it.
/// </summary>
/// <remarks>
/// <para>
/// A workbook without a styles part has only the General format, as does an <c>s</c> past the cell formats: that cell
/// gets the first cell format's.
/// </para>
/// <para>
/// A style's number kind comes from its format (<see cref="NumberFormats"/>): a code that <c>numFmts</c> defines, even
/// for a built-in id, as Excel reads it, else the built-in format of the id. A cell format's <c>applyNumberFormat</c>
/// is ignored, as Excel ignores it.
/// </para>
/// </remarks>
internal sealed class StyleTable
{
	private readonly int[] numberFormatIds;
	private readonly Dictionary<int, string> formatCodes;
	private readonly SheetCellKind[] numberKinds;

	/// <summary>Creates the table.</summary>
	/// <param name="numberFormatIds">The number format of each cell format, in order.</param>
	/// <param name="formatCodes">The custom number formats, by id.</param>
	private StyleTable(int[] numberFormatIds, Dictionary<int, string> formatCodes)
	{
		this.numberFormatIds = numberFormatIds;
		this.formatCodes = formatCodes;
		numberKinds = new SheetCellKind[numberFormatIds.Length];
		var kinds = new Dictionary<int, SheetCellKind>();
		for (var i = 0; i < numberFormatIds.Length; i++)
		{
			var id = numberFormatIds[i];
			if (!kinds.TryGetValue(id, out var kind))
			{
				kind = formatCodes.TryGetValue(id, out var code) ? NumberFormats.Classify(code) : NumberFormats.BuiltIn(id);
				kinds.Add(id, kind);
			}

			numberKinds[i] = kind;
		}
	}

	/// <summary>The styles of a workbook without a styles part: only General.</summary>
	public static StyleTable Empty { get; } = new([], []);

	/// <summary>The number of cell formats.</summary>
	public int Count => numberFormatIds.Length;

	/// <summary>The number format of a cell style: 0 (General) without styles.</summary>
	/// <param name="style">The cell's <c>s</c>: an index into the cell formats.</param>
	/// <returns>The number format id, built-in (below 164, by Excel's convention) or custom.</returns>
	public int NumberFormatId(int style) => numberFormatIds.Length == 0 ? 0 : numberFormatIds[Clamp(style)];

	/// <summary>The code of a custom number format, such as <c>yyyy-mm-dd;@</c>.</summary>
	/// <param name="numberFormatId">The number format id.</param>
	/// <returns>The code, or <see langword="null"/> when the styles part defines no format with that id (a built-in one).</returns>
	public string? FormatCode(int numberFormatId) => formatCodes.TryGetValue(numberFormatId, out var code) ? code : null;

	/// <summary>The kind of a number cell in a style.</summary>
	/// <param name="style">The cell's <c>s</c>: an index into the cell formats.</param>
	/// <returns>
	/// <see cref="SheetCellKind.DateNumber"/> for a date or time format, <see cref="SheetCellKind.Duration"/> for elapsed
	/// time, otherwise <see cref="SheetCellKind.Number"/>.
	/// </returns>
	public SheetCellKind NumberKind(int style) => numberKinds.Length == 0 ? SheetCellKind.Number : numberKinds[Clamp(style)];

	/// <summary>Reads a workbook's styles part.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="partName">The styles part; <see langword="null"/>, or a part that is missing, gives <see cref="Empty"/>.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the styles.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, or passes a limit.</exception>
	public static async ValueTask<StyleTable> LoadAsync(PackageArchive archive, string? partName, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		// Styles only tell dates from numbers: a workbook that points at a missing styles part still reads.
		if (partName is null || !archive.Contains(partName))
		{
			return Empty;
		}

		var numberFormatIds = new List<int>();
		var formatCodes = new Dictionary<int, string>();
		var scanner = await archive.OpenXmlAsync(partName, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await scanner.ReadRootAsync(async, cancellationToken).ConfigureAwait(false);
			if (!scanner.IsStartTag("styleSheet"u8))
			{
				throw scanner.Error("its root element is not styleSheet");
			}

			var depth = scanner.ElementDepth;
			while (await scanner.ReadChildAsync(depth, async, cancellationToken).ConfigureAwait(false))
			{
				var isFormats = scanner.IsStartTag("numFmts"u8);
				if (!isFormats && !scanner.IsStartTag("cellXfs"u8))
				{
					// fonts, fills, borders, cellStyleXfs (whose xf elements are not the cells'), cellStyles, dxfs, ...
					await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);

					continue;
				}

				var listDepth = scanner.ElementDepth;
				while (await scanner.ReadChildAsync(listDepth, async, cancellationToken).ConfigureAwait(false))
				{
					if (isFormats && scanner.IsStartTag("numFmt"u8))
					{
						ReadNumberFormat(scanner, formatCodes);
					}
					else if (!isFormats && scanner.IsStartTag("xf"u8))
					{
						numberFormatIds.Add(ReadNumberFormatId(scanner));
					}

					await scanner.SkipElementAsync(async, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}

		return new StyleTable([.. numberFormatIds], formatCodes);
	}

	/// <summary>Reads a <c>numFmt</c> element: its id and code. The first of two with one id counts.</summary>
	/// <param name="scanner">The scanner, at the element's start tag.</param>
	/// <param name="formatCodes">The custom number formats, by id.</param>
	/// <exception cref="InvalidDataException">The id is not a number 0 or more.</exception>
	private static void ReadNumberFormat(XmlScanner scanner, Dictionary<int, string> formatCodes)
	{
		int? id = null;
		string? code = null;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("numFmtId"u8))
			{
				id = ReadId(attributes);
			}
			else if (attributes.IsName("formatCode"u8))
			{
				code = attributes.DecodeValue();
			}
		}

		if (id is not null && code is not null)
		{
			formatCodes.TryAdd(id.Value, code);
		}
	}

	/// <summary>Reads the number format of a cell format (<c>xf/@numFmtId</c>): 0 (General) when it has none.</summary>
	/// <param name="scanner">The scanner, at the element's start tag.</param>
	/// <returns>The number format id.</returns>
	/// <exception cref="InvalidDataException">The id is not a number 0 or more.</exception>
	private static int ReadNumberFormatId(XmlScanner scanner)
	{
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("numFmtId"u8))
			{
				return ReadId(attributes);
			}
		}

		return 0;
	}

	/// <summary>Reads a number format id: an integer 0 or more.</summary>
	/// <param name="attributes">The attributes, at the id.</param>
	/// <returns>The id.</returns>
	/// <exception cref="InvalidDataException">The value is not an integer 0 or more.</exception>
	private static int ReadId(XmlAttributes attributes)
	{
		var id = attributes.GetInt32();
		if (id < 0)
		{
			throw attributes.InvalidValue("a number format id (0 or more)");
		}

		return id;
	}

	/// <summary>The cell format a style index means: an index past the cell formats means the first.</summary>
	/// <param name="style">The style index.</param>
	/// <returns>The index of the cell format.</returns>
	private int Clamp(int style) => (uint)style < (uint)numberFormatIds.Length ? style : 0;
}
