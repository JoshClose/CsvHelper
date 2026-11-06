using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// An Excel workbook opened to read: an <c>.xlsx</c>, <c>.xlsm</c>, <c>.xltx</c> or <c>.xltm</c> file (or an <c>.xlam</c>),
/// its sheets, tables and defined names, and a <see cref="CsvReader"/> over any of its worksheets, or over a range, table or
/// defined name of one, which reads the rows as records.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reading.</b> <see cref="OpenReader(ExcelReaderOptions?)"/> returns a plain <see cref="CsvReader"/>: records, dynamic
/// records, <see cref="CsvDataReader"/> and CsvHelper.Linq work as for CSV. Field <c>i</c> of a record is column <c>i</c>
/// of its row (<c>A</c> is 0), or of a block of cells that <see cref="ExcelReaderOptions.Range"/>,
/// <see cref="ExcelReaderOptions.Table"/> or <see cref="ExcelReaderOptions.DefinedName"/> chooses (its first column is 0),
/// or of the columns of a header row below a report's title (<see cref="ExcelReaderOptions.HeaderRow"/>). Empty cells are
/// empty fields, and rows are padded to the first record's width. Cells read by their types: numbers,
/// dates, booleans, errors and text. Errors in a sheet name their cell, such as <c>Orders!C14</c>, in
/// <see cref="CsvFormatException.Location"/>.
/// </para>
/// <para>
/// <b>Ownership.</b> A reader holds its sheet's stream, not the workbook, and disposing the reader leaves the workbook
/// open. After the workbook is disposed, a reader's next fill throws <see cref="ObjectDisposedException"/>.
/// <see cref="ExcelReader"/>'s readers own their workbook instead.
/// </para>
/// <para>
/// <b>Threading.</b> A workbook is not thread-safe. Several readers of one workbook can be open and used in turn on one
/// thread, but not from several threads at once. <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>
/// works, since its fills run one at a time.
/// </para>
/// <para>
/// <b>Files.</b> A path is opened so that others can read, write and delete the file, so a workbook that is open in Excel
/// can be read, and Excel can still save it; the workbook goes on reading the copy it opened. A stream that can seek is
/// read from its start; one that can't is copied to a temporary file first.
/// </para>
/// </remarks>
public sealed class ExcelWorkbook : IDisposable, IAsyncDisposable
{
	private readonly PackageArchive archive;
	private readonly WorkbookPart part;
	private readonly ExcelReaderOptions options;
	private readonly bool fromStream;
	private StyleTable? styles;
	private SharedStrings? strings;

	/// <summary>Creates the workbook.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="part">The workbook part.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="fromStream">Whether the package reads the caller's stream.</param>
	private ExcelWorkbook(PackageArchive archive, WorkbookPart part, ExcelReaderOptions options, bool fromStream)
	{
		this.archive = archive;
		this.part = part;
		this.options = options;
		this.fromStream = fromStream;
		Sheets = new ReadOnlyCollection<ExcelSheet>([.. part.Sheets.Select(s => new ExcelSheet(this, s))]);
		Tables = new ReadOnlyCollection<ExcelTable>([.. Sheets.SelectMany(s => s.Tables)]);

		// Each name finds its block's sheet in one lookup, not a search of every sheet: a million names in a workbook of
		// 100,000 sheets would otherwise take minutes to open.
		var worksheets = new Dictionary<string, ExcelSheet>(StringComparer.OrdinalIgnoreCase);
		foreach (var sheet in Sheets)
		{
			// The first of a name, as a search in tab order finds it.
			if (sheet.Kind == ExcelSheetKind.Worksheet && !worksheets.ContainsKey(sheet.Name))
			{
				worksheets[sheet.Name] = sheet;
			}
		}

		DefinedNames = new ReadOnlyCollection<ExcelDefinedName>([.. part.DefinedNames.Select(n => new ExcelDefinedName(Sheets, worksheets, n))]);
	}

	/// <summary>
	/// The sheets, in tab order: worksheets, chartsheets and the legacy kinds, hidden ones included. A sheet's
	/// <see cref="ExcelSheet.Index"/> is its place here.
	/// </summary>
	public IReadOnlyList<ExcelSheet> Sheets { get; }

	/// <summary>Every sheet's tables (Excel tables, or ListObjects): by sheet in tab order, then as each sheet lists them.</summary>
	public IReadOnlyList<ExcelTable> Tables { get; }

	/// <summary>
	/// The defined names, the workbook's and its sheets', as the workbook lists them, Excel's built-in and hidden ones
	/// included (<c>Print_Area</c>, <c>_FilterDatabase</c>).
	/// </summary>
	public IReadOnlyList<ExcelDefinedName> DefinedNames { get; }

	/// <summary>
	/// Whether the workbook uses the 1904 date system, whose serial date 0 is January 1, 1904 rather than the end of 1899:
	/// Excel for Mac used it by default before 2011.
	/// </summary>
	public bool Date1904 => part.Date1904;

	/// <summary>Whether the workbook is disposed.</summary>
	internal bool IsDisposed => archive.IsDisposed;

	/// <summary>Opens the workbook at a path.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The workbook's options, which its readers use unless they are given their own; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <returns>The workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid: both a sheet name and an index, or no <see cref="ExcelReaderOptions.Csv"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	public static ExcelWorkbook Open(string path, ExcelReaderOptions? options = null) =>
		SyncOrAsync.Result(OpenCoreAsync(path, options, false, CancellationToken.None));

	/// <summary>Opens the workbook in a stream.</summary>
	/// <param name="stream">The stream of the workbook: one that can seek is read from its start, and one that can't is copied to a temporary file.</param>
	/// <param name="options">The workbook's options, which its readers use unless they are given their own; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="leaveOpen">
	/// Whether disposing the workbook leaves <paramref name="stream"/> open. A stream that is copied is disposed once the
	/// copy is made, unless this is <see langword="true"/>. When the open fails, a stream that can seek is left open.
	/// </param>
	/// <returns>The workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	public static ExcelWorkbook Open(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false) =>
		SyncOrAsync.Result(OpenCoreAsync(stream, options, leaveOpen, false, CancellationToken.None));

	/// <summary>Asynchronously opens the workbook at a path. See <see cref="Open(string, ExcelReaderOptions?)"/>.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The workbook's options; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels the open.</param>
	/// <returns>A task whose result is the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	public static ValueTask<ExcelWorkbook> OpenAsync(string path, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default) =>
		OpenCoreAsync(path, options, true, cancellationToken);

	/// <summary>Asynchronously opens the workbook in a stream. See <see cref="Open(Stream, ExcelReaderOptions?, bool)"/>.</summary>
	/// <param name="stream">The stream of the workbook.</param>
	/// <param name="options">The workbook's options; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether disposing the workbook leaves <paramref name="stream"/> open.</param>
	/// <param name="cancellationToken">Cancels the open.</param>
	/// <returns>A task whose result is the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	public static ValueTask<ExcelWorkbook> OpenAsync(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default) =>
		OpenCoreAsync(stream, options, leaveOpen, true, cancellationToken);

	/// <summary>
	/// Opens a reader of the sheet the options choose (<see cref="ExcelReaderOptions.Sheet"/> or
	/// <see cref="ExcelReaderOptions.SheetIndex"/>), or of the first worksheet, hidden ones included.
	/// </summary>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <returns>The reader, which holds the sheet's stream until it is disposed.</returns>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The options name no sheet, table or defined name of the workbook, or a defined name that is no block of cells, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, a limit is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet, or the workbook has none.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public CsvReader OpenReader(ExcelReaderOptions? options = null) =>
		SyncOrAsync.Result(OpenReaderCoreAsync(null, null, null, options, false, false, CancellationToken.None));

	/// <summary>Opens a reader of the sheet with a name, matched ignoring case.</summary>
	/// <param name="sheetName">The sheet's name; it wins over the options' sheet.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <returns>The reader, which holds the sheet's stream until it is disposed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sheetName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The workbook has no sheet of that name (the message lists its sheets), the options choose a sheet of their own (a table, a defined name, or a range that names its sheet), or they are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public CsvReader OpenReader(string sheetName, ExcelReaderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(sheetName);

		return SyncOrAsync.Result(OpenReaderCoreAsync(sheetName, null, null, options, false, false, CancellationToken.None));
	}

	/// <summary>Opens a reader of the sheet at a place in tab order.</summary>
	/// <param name="sheetIndex">The sheet's 0-based index in <see cref="Sheets"/>; it wins over the options' sheet.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <returns>The reader, which holds the sheet's stream until it is disposed.</returns>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="sheetIndex"/> is out of range, a limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="ArgumentException">The options choose a sheet of their own (a table, a defined name, or a range that names its sheet), or are not valid.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public CsvReader OpenReader(int sheetIndex, ExcelReaderOptions? options = null) =>
		SyncOrAsync.Result(OpenReaderCoreAsync(null, sheetIndex, null, options, false, false, CancellationToken.None));

	/// <summary>Opens a reader of a sheet of this workbook.</summary>
	/// <param name="sheet">The sheet, from <see cref="Sheets"/>; it wins over the options' sheet.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <returns>The reader, which holds the sheet's stream until it is disposed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sheet"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The sheet is another workbook's, the options choose a sheet of their own (a table, a defined name, or a range that names its sheet), or they are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public CsvReader OpenReader(ExcelSheet sheet, ExcelReaderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(sheet);

		return SyncOrAsync.Result(OpenReaderCoreAsync(null, null, sheet, options, false, false, CancellationToken.None));
	}

	/// <summary>Asynchronously opens a reader of the sheet the options choose, or of the first worksheet. See <see cref="OpenReader(ExcelReaderOptions?)"/>.</summary>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <param name="cancellationToken">Cancels reading the styles and shared strings.</param>
	/// <returns>A task whose result is the reader.</returns>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The options name no sheet, table or defined name of the workbook, or a defined name that is no block of cells, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, a limit is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet, or the workbook has none.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public ValueTask<CsvReader> OpenReaderAsync(ExcelReaderOptions? options = null, CancellationToken cancellationToken = default) =>
		OpenReaderCoreAsync(null, null, null, options, false, true, cancellationToken);

	/// <summary>Asynchronously opens a reader of the sheet with a name. See <see cref="OpenReader(string, ExcelReaderOptions?)"/>.</summary>
	/// <param name="sheetName">The sheet's name, matched ignoring case.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <param name="cancellationToken">Cancels reading the styles and shared strings.</param>
	/// <returns>A task whose result is the reader.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sheetName"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The workbook has no sheet of that name, the options choose a sheet of their own, or they are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public ValueTask<CsvReader> OpenReaderAsync(string sheetName, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sheetName);

		return OpenReaderCoreAsync(sheetName, null, null, options, false, true, cancellationToken);
	}

	/// <summary>Asynchronously opens a reader of the sheet at a place in tab order. See <see cref="OpenReader(int, ExcelReaderOptions?)"/>.</summary>
	/// <param name="sheetIndex">The sheet's 0-based index in <see cref="Sheets"/>.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <param name="cancellationToken">Cancels reading the styles and shared strings.</param>
	/// <returns>A task whose result is the reader.</returns>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="sheetIndex"/> is out of range, a limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="ArgumentException">The options choose a sheet of their own (a table, a defined name, or a range that names its sheet), or are not valid.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public ValueTask<CsvReader> OpenReaderAsync(int sheetIndex, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default) =>
		OpenReaderCoreAsync(null, sheetIndex, null, options, false, true, cancellationToken);

	/// <summary>Asynchronously opens a reader of a sheet of this workbook. See <see cref="OpenReader(ExcelSheet, ExcelReaderOptions?)"/>.</summary>
	/// <param name="sheet">The sheet, from <see cref="Sheets"/>.</param>
	/// <param name="options">The reader's options, in place of the workbook's; <see langword="null"/> for the workbook's.</param>
	/// <param name="cancellationToken">Cancels reading the styles and shared strings.</param>
	/// <returns>A task whose result is the reader.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sheet"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	/// <exception cref="ArgumentException">The sheet is another workbook's, the options choose a sheet of their own (a table, a defined name, or a range that names its sheet), or they are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit of the options is 0 or less, or the header row they give (<see cref="ExcelHeaderRow.At"/>) is not in the range or defined name read.</exception>
	/// <exception cref="NotSupportedException">The sheet is not a worksheet.</exception>
	/// <exception cref="InvalidDataException">The sheet's part is missing, or the styles or shared strings can't be read or pass a limit.</exception>
	/// <exception cref="CsvFormatException">The sheet's merged cells can't be read (<see cref="ExcelReaderOptions.FillMergedCells"/>), or its rows up to the header row can't be read, or the header row given has no value (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="KeyNotFoundException">No row tried has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The header row's record type has no mapper (<see cref="ExcelHeaderRow.Find{T}(int)"/>).</exception>
	public ValueTask<CsvReader> OpenReaderAsync(ExcelSheet sheet, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sheet);

		return OpenReaderCoreAsync(null, null, sheet, options, false, true, cancellationToken);
	}

	/// <summary>Disposes the workbook: its file, or its stream unless it was opened to leave it open. Its readers' next fills throw.</summary>
	public void Dispose() => archive.Dispose();

	/// <summary>Asynchronously disposes the workbook. See <see cref="Dispose"/>.</summary>
	/// <returns>A task that completes when the workbook is disposed.</returns>
	public ValueTask DisposeAsync() => archive.DisposeAsync();

	/// <summary>Opens the workbook at a path, synchronously or not.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The workbook's options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the workbook.</returns>
	internal static async ValueTask<ExcelWorkbook> OpenCoreAsync(string path, ExcelReaderOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(path);
		var settings = Prepare(options);
		var archive = await PackageArchive.OpenAsync(path, settings.Limits, async, cancellationToken).ConfigureAwait(false);

		return await LoadAsync(archive, settings, false, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Opens the workbook in a stream, synchronously or not.</summary>
	/// <param name="stream">The stream of the workbook.</param>
	/// <param name="options">The workbook's options, or <see langword="null"/>.</param>
	/// <param name="leaveOpen">Whether disposing the workbook leaves <paramref name="stream"/> open.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the workbook.</returns>
	internal static async ValueTask<ExcelWorkbook> OpenCoreAsync(Stream stream, ExcelReaderOptions? options, bool leaveOpen, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var settings = Prepare(options);
		var archive = await PackageArchive.OpenAsync(stream, leaveOpen, settings.Limits, async, cancellationToken).ConfigureAwait(false);

		return await LoadAsync(archive, settings, true, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Opens a reader of the workbook's default sheet that owns the workbook, for <see cref="ExcelReader"/>: disposing the
	/// reader disposes the workbook. When the reader can't be opened, the workbook is disposed, leaving the caller's
	/// stream open.
	/// </summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the reader.</returns>
	internal async ValueTask<CsvReader> OpenOwningReaderAsync(bool async, CancellationToken cancellationToken)
	{
		try
		{
			return await OpenReaderCoreAsync(null, null, null, null, true, async, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			if (fromStream)
			{
				archive.LeaveInputOpen();
			}

			archive.Dispose();

			throw;
		}
	}

	/// <summary>
	/// Reads the used range a worksheet states, for <see cref="ExcelSheet.Dimension"/>: <see langword="null"/> for a sheet
	/// other than a worksheet, one without a part, or one whose part's start can't be read.
	/// </summary>
	/// <param name="sheet">The sheet.</param>
	/// <returns>The range, or <see langword="null"/>.</returns>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	internal ExcelRange? ReadDimension(WorkbookSheet sheet)
	{
		if (sheet.Kind != ExcelSheetKind.Worksheet || sheet.PartName is null)
		{
			return null;
		}

		if (IsDisposed)
		{
			throw new ObjectDisposedException(nameof(ExcelWorkbook), "The workbook is disposed.");
		}

		try
		{
			return SyncOrAsync.Result(SheetDimension.ReadAsync(archive, sheet.PartName, options.Limits, false, CancellationToken.None));
		}
		catch (Exception e) when (e is InvalidDataException or IOException)
		{
			// Reading the sheet says why.
			return null;
		}
	}

	/// <summary>Checks the version of CsvHelper and the options, before an open reads anything.</summary>
	/// <param name="options">The options, or <see langword="null"/> for the default.</param>
	/// <returns>The options to use.</returns>
	private static ExcelReaderOptions Prepare(ExcelReaderOptions? options)
	{
		VersionGuard.EnsureMatch();
		var settings = options ?? ExcelReaderOptions.Default;
		settings.Validate(nameof(options));

		return settings;
	}

	/// <summary>Loads the workbook part of an open package; when that fails, disposes the package, leaving the caller's stream open.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="fromStream">Whether the package reads the caller's stream.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the workbook.</returns>
	private static async ValueTask<ExcelWorkbook> LoadAsync(PackageArchive archive, ExcelReaderOptions options, bool fromStream, bool async, CancellationToken cancellationToken)
	{
		try
		{
			var part = await WorkbookPart.LoadAsync(archive, options.Limits, async, cancellationToken).ConfigureAwait(false);

			return new ExcelWorkbook(archive, part, options, fromStream);
		}
		catch
		{
			if (fromStream)
			{
				archive.LeaveInputOpen();
			}

			archive.Dispose();

			throw;
		}
	}

	/// <summary>The name of a kind of sheet, for messages.</summary>
	/// <param name="kind">The kind.</param>
	/// <returns>The name, such as "chartsheet".</returns>
	private static string Describe(ExcelSheetKind kind) => kind switch
	{
		ExcelSheetKind.Worksheet => "worksheet",
		ExcelSheetKind.Chartsheet => "chartsheet",
		ExcelSheetKind.DialogSheet => "dialog sheet",
		_ => "macro sheet",
	};

	/// <summary>Opens a reader, synchronously or not: chooses the sheet, loads the styles and shared strings the first time, and opens the sheet's part.</summary>
	/// <param name="sheetName">The sheet's name, or <see langword="null"/>.</param>
	/// <param name="sheetIndex">The sheet's index, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/>.</param>
	/// <param name="options">The reader's options, or <see langword="null"/> for the workbook's.</param>
	/// <param name="ownsWorkbook">Whether disposing the reader disposes the workbook.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the reader.</returns>
	private async ValueTask<CsvReader> OpenReaderCoreAsync(
		string? sheetName, int? sheetIndex, ExcelSheet? sheet, ExcelReaderOptions? options, bool ownsWorkbook, bool async, CancellationToken cancellationToken)
	{
		if (IsDisposed)
		{
			throw new ObjectDisposedException(nameof(ExcelWorkbook), "The workbook is disposed.");
		}

		var settings = options ?? this.options;
		settings.Validate(nameof(options));
		var (chosen, layout, header) = Target(sheetName, sheetIndex, sheet, settings);
		if (chosen.Kind != ExcelSheetKind.Worksheet)
		{
			throw new NotSupportedException($"The sheet \"{chosen.Name}\" is a {Describe(chosen.Kind)}, and only worksheets have cells to read.");
		}

		// The rows a header search reads, checked before anything is read: a row given must be in the area.
		var headerRow = settings.HeaderRow;
		var searchArea = headerRow is null ? null : HeaderSearch.SearchArea(headerRow, layout ?? SheetLayout.Sheet);
		var partName = chosen.Part.PartName ?? throw new InvalidDataException($"The sheet \"{chosen.Name}\" can't be read: {chosen.Part.Problem}.");
		var limits = settings.Limits;
		styles ??= await StyleTable.LoadAsync(archive, part.StylesPart, limits, async, cancellationToken).ConfigureAwait(false);
		strings ??= await SharedStrings.LoadAsync(archive, part.SharedStringsPart, limits, async, cancellationToken).ConfigureAwait(false);

		// Merged ranges come after the rows: a read of the part through finds them, before the reader reads it again.
		var mergedCells = settings.FillMergedCells
			? await MergedCells.LoadAsync(archive, partName, chosen.Name, limits, async, cancellationToken).ConfigureAwait(false)
			: null;
		if (headerRow is not null)
		{
			(layout, header) = await FindHeaderAsync(partName, chosen.Name, settings, headerRow, searchArea!, layout ?? SheetLayout.Sheet, mergedCells, async, cancellationToken)
				.ConfigureAwait(false);
		}

		var scanner = await OpenScannerAsync(partName, chosen.Name, settings, layout, settings.Csv.SkipEmptyLines, settings.SkipHiddenRows, mergedCells, async, cancellationToken)
			.ConfigureAwait(false);
		if (header is not null)
		{
			scanner.HoldFirstRecord(header);
		}

		try
		{
			return new CsvReader(new SheetRecordSource(scanner, this, ownsWorkbook, settings.CellOptions(Date1904)), settings.Csv);
		}
		catch
		{
			// The options of the reader can't be used: the reader never took the source.
			scanner.Dispose();

			throw;
		}
	}

	/// <summary>Opens a scanner of a worksheet's part, with a reader's options; when that fails, the part's stream is disposed.</summary>
	/// <param name="partName">The sheet's part.</param>
	/// <param name="sheetName">The sheet's name, for locations.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="layout">The area read, or <see langword="null"/> for the whole sheet.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="skipHiddenRows">Whether hidden rows are no records.</param>
	/// <param name="mergedCells">The sheet's merged ranges, to fill their cells with; <see langword="null"/> to fill none.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the scanner, which owns the part's stream.</returns>
	private async ValueTask<SheetScanner> OpenScannerAsync(
		string partName, string sheetName, ExcelReaderOptions options, SheetLayout? layout, bool skipEmptyRows, bool skipHiddenRows, MergedCells? mergedCells, bool async,
		CancellationToken cancellationToken)
	{
		var limits = options.Limits;
		var stream = await archive.OpenPartAsync(partName, false, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			var xml = new XmlScanner(new XmlWindow(stream, partName, limits.MaxPartSize));

			return new SheetScanner(
				xml, sheetName, strings!, styles!, skipEmptyRows, stream, Date1904, options.ErrorCells, options.Formulas, layout, skipHiddenRows, options.SkipHiddenColumns,
				mergedCells);
		}
		catch
		{
			stream.Dispose();

			throw;
		}
	}

	/// <summary>
	/// Finds the header row that the options ask for (<see cref="ExcelReaderOptions.HeaderRow"/>), with a scanner of its own
	/// over the rows it can be in, which reads the sheet's part from its start again (<see cref="HeaderSearch"/>).
	/// </summary>
	/// <param name="partName">The sheet's part.</param>
	/// <param name="sheetName">The sheet's name, for locations.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="headerRow">The header row asked for.</param>
	/// <param name="searchArea">The rows the search reads, in the area's columns (<see cref="HeaderSearch.SearchArea"/>).</param>
	/// <param name="area">The area read.</param>
	/// <param name="mergedCells">The sheet's merged ranges, to fill their cells with; <see langword="null"/> to fill none.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is what the reader reads (the rows after the header, in its columns) and the header to hold.</returns>
	/// <exception cref="KeyNotFoundException">No row searched has the columns asked for.</exception>
	/// <exception cref="CsvFormatException">The row given has no value, or the sheet's content up to the header row can't be read.</exception>
	/// <exception cref="InvalidOperationException">The search's record type has no mapper.</exception>
	private async ValueTask<(SheetLayout Layout, HeldRecord Header)> FindHeaderAsync(
		string partName, string sheetName, ExcelReaderOptions options, ExcelHeaderRow headerRow, SheetLayout searchArea, SheetLayout area, MergedCells? mergedCells,
		bool async, CancellationToken cancellationToken)
	{
		// Rows without a value are no candidates; a row given is the header even when hidden, as it is named.
		var search = await OpenScannerAsync(partName, sheetName, options, searchArea, true, options.SkipHiddenRows && headerRow.Row == 0, mergedCells, async, cancellationToken)
			.ConfigureAwait(false);
		try
		{
			return await HeaderSearch.RunAsync(search, headerRow, area, options, Date1904, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			if (async)
			{
				await search.DisposeAsync().ConfigureAwait(false);
			}
			else
			{
				search.Dispose();
			}
		}
	}

	/// <summary>
	/// Chooses what a reader reads: the sheet a table, a defined name or a range with a sheet's name is on, or else the sheet
	/// chosen as usual (<see cref="Choose"/>); the area of the table's data rows, of the name's block or of the range, or the
	/// whole sheet; and the record of a table's column names to read as its header.
	/// </summary>
	/// <param name="sheetName">The sheet's name, or <see langword="null"/>.</param>
	/// <param name="sheetIndex">The sheet's index, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/>.</param>
	/// <param name="options">The reader's options, which are valid.</param>
	/// <returns>The sheet, the area or <see langword="null"/> for the whole sheet, and the header to hold or <see langword="null"/>.</returns>
	/// <exception cref="ArgumentException">
	/// A sheet is given with an option that chooses its own, or the options name no sheet, table or defined name of the
	/// workbook, or a defined name that is no block of cells, or one of several sheets' names without its sheet.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">The sheet index is out of range.</exception>
	private (ExcelSheet Sheet, SheetLayout? Layout, HeldRecord? Header) Target(string? sheetName, int? sheetIndex, ExcelSheet? sheet, ExcelReaderOptions options)
	{
		if (options.ChoosesSheet() is { } chooser && (sheet is not null || sheetName is not null || sheetIndex is not null))
		{
			var paramName = sheet is not null ? nameof(sheet) : sheetName is not null ? nameof(sheetName) : nameof(sheetIndex);

			throw new ArgumentException($"{chooser}, so no sheet can be given with it.", paramName);
		}

		if (options.Table is not null)
		{
			var table = FindTable(options.Table);
			var range = table.Range;
			var layout = new SheetLayout(range.FirstRow + table.HeaderRowCount, range.LastRow - table.TotalsRowCount, range.FirstColumnIndex, range.LastColumnIndex);

			// The names head the table whether its header row shows or not; without that row, the record has none (line 0).
			var header = options.Csv.HasHeader ? HeldRecord.Names(table.Columns, range.FirstColumnIndex, range.ColumnCount, table.HeaderRowCount > 0 ? range.FirstRow : 0) : null;

			return (table.Sheet, layout, header);
		}

		if (options.DefinedName is not null)
		{
			var name = FindName(options.DefinedName);

			return (name.Sheet!, SheetLayout.Of(name.Range!.Value), null);
		}

		if (options.Range is not null)
		{
			AreaReference.TryParse(options.Range.AsSpan(), out var rangeSheet, out var range);
			var chosen = rangeSheet is null ? Choose(sheetName, sheetIndex, sheet, options) : Find(rangeSheet, nameof(options));

			return (chosen, SheetLayout.Of(range), null);
		}

		return (Choose(sheetName, sheetIndex, sheet, options), null, null);
	}

	/// <summary>Finds a table by name, ignoring case.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The table: the first of that name, in <see cref="Tables"/>' order.</returns>
	/// <exception cref="ArgumentException">The workbook has no table of that name.</exception>
	private ExcelTable FindTable(string name)
	{
		foreach (var table in Tables)
		{
			if (string.Equals(table.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return table;
			}
		}

		var tables = Tables.Count == 0
			? "It has no tables."
			: "Its tables are " + string.Join(", ", Tables.Select(t => $"\"{t.Name}\" ({SheetLocation.Quote(t.Sheet.Name)}!{t.Range})")) + ".";

		throw new ArgumentException($"The workbook has no table named \"{name}\". {tables}", "options");
	}

	/// <summary>
	/// Finds a defined name that is a block of cells: <c>Name</c>, the workbook's, or else the one sheet's of that name; or
	/// <c>Sheet!Name</c>, that sheet's own. Names and sheets are matched ignoring case, and a built-in name's <c>_xlnm.</c>
	/// prefix is optional.
	/// </summary>
	/// <param name="text">The name, with or without a sheet's.</param>
	/// <returns>The name, whose <see cref="ExcelDefinedName.Sheet"/> and <see cref="ExcelDefinedName.Range"/> are set.</returns>
	/// <exception cref="ArgumentException">
	/// The workbook has no such name, several sheets have one of that name and the workbook none, or the name is not a
	/// block of cells on a worksheet.
	/// </exception>
	private ExcelDefinedName FindName(string text)
	{
		const string BuiltInPrefix = "_xlnm.";
		ExcelDefinedName? found = null;
		if (AreaReference.TrySplitSheet(text.AsSpan(), out var sheetName, out var rest))
		{
			var name = rest.ToString();
			if (name.StartsWith(BuiltInPrefix, StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(BuiltInPrefix.Length);
			}

			var matches = DefinedNames.Where(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
			if (sheetName is not null)
			{
				found = matches.FirstOrDefault(n => n.Scope is not null && string.Equals(n.Scope.Name, sheetName, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				found = matches.FirstOrDefault(n => n.Scope is null);
				if (found is null)
				{
					var sheets = matches.Where(n => n.Scope is not null).ToList();
					if (sheets.Count > 1)
					{
						throw new ArgumentException(
							$"The defined name \"{name}\" is a name of several sheets: {string.Join(", ", sheets)}. Give one of them, such as {sheets[0]}.", "options");
					}

					found = sheets.FirstOrDefault();
				}
			}
		}

		if (found is null)
		{
			throw new ArgumentException($"The workbook has no defined name \"{text}\".", "options");
		}

		if (found.Range is null)
		{
			throw new ArgumentException($"The defined name \"{found}\" refers to {found.RefersTo}, which is not a block of cells on a worksheet.", "options");
		}

		return found;
	}

	/// <summary>Chooses the sheet to read: the one given, else the options', else the first worksheet.</summary>
	/// <param name="sheetName">The sheet's name, or <see langword="null"/>.</param>
	/// <param name="sheetIndex">The sheet's index, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/>.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The sheet.</returns>
	private ExcelSheet Choose(string? sheetName, int? sheetIndex, ExcelSheet? sheet, ExcelReaderOptions options)
	{
		if (sheet is not null)
		{
			if (!ReferenceEquals(sheet.Workbook, this))
			{
				throw new ArgumentException("The sheet is a sheet of another workbook.", nameof(sheet));
			}

			return sheet;
		}

		if (sheetName is not null)
		{
			return Find(sheetName, nameof(sheetName));
		}

		if (sheetIndex is not null)
		{
			return At(sheetIndex.Value, nameof(sheetIndex));
		}

		if (options.Sheet is not null)
		{
			return Find(options.Sheet, nameof(options));
		}

		if (options.SheetIndex is not null)
		{
			return At(options.SheetIndex.Value, nameof(options));
		}

		if (Sheets.Count == 0)
		{
			throw new InvalidDataException("The workbook has no sheets.");
		}

		return Sheets.FirstOrDefault(s => s.Kind == ExcelSheetKind.Worksheet) ??
			throw new NotSupportedException($"The workbook has no worksheet, and only worksheets have cells to read. Its sheets are {List()}.");
	}

	/// <summary>Finds a sheet by name, ignoring case.</summary>
	/// <param name="name">The name.</param>
	/// <param name="paramName">The parameter the name came from.</param>
	/// <returns>The sheet.</returns>
	/// <exception cref="ArgumentException">The workbook has no sheet of that name.</exception>
	private ExcelSheet Find(string name, string paramName)
	{
		foreach (var sheet in Sheets)
		{
			if (string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return sheet;
			}
		}

		var sheets = Sheets.Count == 0 ? "It has no sheets." : $"Its sheets are {List()}.";

		throw new ArgumentException($"The workbook has no sheet named \"{name}\". {sheets}", paramName);
	}

	/// <summary>Gets a sheet by its place in tab order.</summary>
	/// <param name="index">The 0-based index.</param>
	/// <param name="paramName">The parameter the index came from.</param>
	/// <returns>The sheet.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The index is out of range.</exception>
	private ExcelSheet At(int index, string paramName)
	{
		if ((uint)index >= (uint)Sheets.Count)
		{
			var range = Sheets.Count == 0 ? "The workbook has no sheets." : $"The workbook has {Sheets.Count} sheets, so an index is 0 to {Sheets.Count - 1}.";

			throw new ArgumentOutOfRangeException(paramName, index, range);
		}

		return Sheets[index];
	}

	/// <summary>The names of the sheets, quoted, for messages.</summary>
	/// <returns>The list, such as <c>"Data", "Chart1" (chartsheet)</c>.</returns>
	private string List() => string.Join(", ", Sheets.Select(s => s.Kind == ExcelSheetKind.Worksheet ? $"\"{s.Name}\"" : $"\"{s.Name}\" ({Describe(s.Kind)})"));
}
