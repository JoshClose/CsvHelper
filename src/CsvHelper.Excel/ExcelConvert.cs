using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Converts a sheet of a workbook to CSV: from files, from streams, or from a reader into a writer; and every worksheet of
/// a workbook to a CSV file each.
/// </summary>
/// <remarks>
/// <para>
/// <b>The records</b> are the sheet's as <see cref="ExcelWorkbook.OpenReader(ExcelReaderOptions?)"/> reads them with
/// <see cref="ExcelToCsvOptions.Excel"/>: its sheet or area, header row, hidden rows and columns, merged cells, error cells
/// and formulas. Field <c>i</c> is column <c>i</c> from the sheet's or area's first column, so data from <c>C3</c> starts
/// with two empty fields; rows are padded to the first record's width, and a longer row stays longer. With
/// <see cref="CsvReaderOptions.SkipEmptyLines"/> off, an empty row is a record of empty fields as wide as the first. The
/// header, with <see cref="CsvReaderOptions.HasHeader"/> (the default), is the reader's <see cref="CsvReader.Headers"/>.
/// </para>
/// <para>
/// <b>Each field</b> is the text <see cref="CsvReader.GetString(int)"/> gets, copied without a string: text as stored;
/// numbers to <see cref="ExcelReaderOptions.SignificantDigits"/>, 15 by default, as Excel shows them, so that
/// <c>0.30000000000000004</c> is <c>0.3</c>; dates and times in ISO 8601 (<c>2024-01-15</c>,
/// <c>2024-01-15T10:30:00</c>, <c>10:30:00</c>); durations as <c>[h]:mm:ss</c>; booleans as <c>TRUE</c> and
/// <c>FALSE</c>; errors as <see cref="ExcelReaderOptions.ErrorCells"/> says; and formulas as their saved values, or their
/// text with <see cref="ExcelFormulas.FormulaText"/>. A number, date, duration or boolean cell is written as a value of its
/// type, so <see cref="CsvWriterOptions.Injection"/> protects text cells and passes over a negative number.
/// </para>
/// <para>
/// <b>The CSV</b> is written as <see cref="ExcelToCsvOptions.Csv"/> says: by default with commas, quotes where a field
/// needs them, CR LF and no byte-order mark. Unlike Excel's "Save As" CSV, it holds the cells' values rather than the
/// text their formats show, starts from the sheet's first column rather than its used range, and has no byte-order mark
/// unless <see cref="CsvWriterOptions.WriteByteOrderMark"/> is set, without which Excel reads it in the system's code page.
/// Text that Excel computes when it opens a CSV (<c>=1+1</c>, <c>+1+1</c>, <c>@SUM(1)</c>) is written as it is, unless
/// <see cref="CsvWriterOptions.Injection"/> protects it.
/// </para>
/// <para>
/// <b>Files and streams.</b> A path overload creates or overwrites the CSV file, and when the conversion fails, deletes
/// it, so that none is left half written. A stream overload reads the workbook as
/// <see cref="ExcelWorkbook.Open(Stream, ExcelReaderOptions?, bool)"/> reads a stream, writes the CSV from the stream's
/// position, and leaves both streams open. Every read and write of an asynchronous overload is asynchronous.
/// </para>
/// </remarks>
public static class ExcelConvert
{
	/// <summary>Converts a sheet of the workbook at a path to a CSV file.</summary>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="csvPath">The path of the CSV file, which is created or overwritten.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="excelPath"/> or <paramref name="csvPath"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid or name no sheet of the workbook, or <paramref name="csvPath"/> is the workbook's.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range, or the options' sheet index is.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">No row has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">A file can't be opened or created.</exception>
	/// <exception cref="UnauthorizedAccessException">The CSV file can't be written with the caller's permissions.</exception>
	public static void ToCsv(string excelPath, string csvPath, ExcelToCsvOptions? options = null) =>
		SyncOrAsync.Wait(ToCsvCoreAsync(excelPath, csvPath, options, false, CancellationToken.None));

	/// <summary>Converts a sheet of the workbook in a stream to CSV in another stream, leaving both open.</summary>
	/// <param name="excel">The stream of the workbook: one that can seek is read from its start, and one that can't is copied to a temporary file.</param>
	/// <param name="csv">The stream the CSV is written to, from its position.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="excel"/> or <paramref name="csv"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid or name no sheet of the workbook, <paramref name="excel"/> can't be read, or <paramref name="csv"/> can't be written.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range, or the options' sheet index is.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">No row has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">A stream can't be read or written.</exception>
	public static void ToCsv(Stream excel, Stream csv, ExcelToCsvOptions? options = null) =>
		SyncOrAsync.Wait(ToCsvCoreAsync(excel, csv, options, false, CancellationToken.None));

	/// <summary>Asynchronously converts a sheet of the workbook at a path to a CSV file. See <see cref="ToCsv(string, string, ExcelToCsvOptions?)"/>.</summary>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="csvPath">The path of the CSV file, which is created or overwritten.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels the conversion, which then deletes the CSV file.</param>
	/// <returns>A task that completes when the CSV file is written and closed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="excelPath"/> or <paramref name="csvPath"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid or name no sheet of the workbook, or <paramref name="csvPath"/> is the workbook's.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range, or the options' sheet index is.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">No row has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">A file can't be opened or created.</exception>
	/// <exception cref="UnauthorizedAccessException">The CSV file can't be written with the caller's permissions.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask ToCsvAsync(string excelPath, string csvPath, ExcelToCsvOptions? options = null, CancellationToken cancellationToken = default) =>
		ToCsvCoreAsync(excelPath, csvPath, options, true, cancellationToken);

	/// <summary>Asynchronously converts a sheet of the workbook in a stream to CSV in another stream, leaving both open. See <see cref="ToCsv(Stream, Stream, ExcelToCsvOptions?)"/>.</summary>
	/// <param name="excel">The stream of the workbook.</param>
	/// <param name="csv">The stream the CSV is written to, from its position.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels the conversion.</param>
	/// <returns>A task that completes when the CSV is written and flushed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="excel"/> or <paramref name="csv"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid or name no sheet of the workbook, <paramref name="excel"/> can't be read, or <paramref name="csv"/> can't be written.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range, or the options' sheet index is.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">No row has the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">A stream can't be read or written.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask ToCsvAsync(Stream excel, Stream csv, ExcelToCsvOptions? options = null, CancellationToken cancellationToken = default) =>
		ToCsvCoreAsync(excel, csv, options, true, cancellationToken);

	/// <summary>
	/// Writes a reader's records to a CSV writer as the other overloads write a sheet's: the reader's header, when its
	/// options have <see cref="CsvReaderOptions.HasHeader"/>, then the records it reads from where it stands, as
	/// <see cref="CsvReader.GetRecords{T}"/> reads them (a record it is on has been read). Disposes neither.
	/// </summary>
	/// <remarks>
	/// For readers and writers that the other overloads don't open: a sheet of a workbook already open, a writer of a
	/// <see cref="TextWriter"/>, or several sheets into one CSV. The reader can be any <see cref="CsvReader"/>; a reader of CSV
	/// gives each field as it reads it. A record longer than the first throws when the writer's
	/// <see cref="CsvWriterOptions.ValidateFieldCount"/> is on, as it is by default, which the other overloads turn off.
	/// </remarks>
	/// <param name="sheet">The reader, of a sheet.</param>
	/// <param name="csv">The writer.</param>
	/// <exception cref="ArgumentNullException"><paramref name="sheet"/> or <paramref name="csv"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The reader, its workbook or the writer is disposed.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it.</exception>
	/// <exception cref="FormatException">A cell is an error and the sheet's error cells fail every read; the message names the cell.</exception>
	/// <exception cref="InvalidOperationException">A record has a different number of fields than the first, and the writer checks.</exception>
	/// <exception cref="IOException">The writer's destination can't be written.</exception>
	public static void ToCsv(CsvReader sheet, CsvWriter csv)
	{
		ArgumentNullException.ThrowIfNull(sheet);
		ArgumentNullException.ThrowIfNull(csv);
		SyncOrAsync.Wait(SheetToCsv.CopyAsync(sheet, csv, false, CancellationToken.None));
	}

	/// <summary>Asynchronously writes a reader's records to a CSV writer. See <see cref="ToCsv(CsvReader, CsvWriter)"/>.</summary>
	/// <param name="sheet">The reader, of a sheet.</param>
	/// <param name="csv">The writer.</param>
	/// <param name="cancellationToken">Cancels reading and writing.</param>
	/// <returns>A task that completes when the last record is written to the writer, which may still hold it in its buffer.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="sheet"/> or <paramref name="csv"/> is <see langword="null"/>.</exception>
	/// <exception cref="ObjectDisposedException">The reader, its workbook or the writer is disposed.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it.</exception>
	/// <exception cref="FormatException">A cell is an error and the sheet's error cells fail every read; the message names the cell.</exception>
	/// <exception cref="InvalidOperationException">A record has a different number of fields than the first, and the writer checks.</exception>
	/// <exception cref="IOException">The writer's destination can't be written.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask ToCsvAsync(CsvReader sheet, CsvWriter csv, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(sheet);
		ArgumentNullException.ThrowIfNull(csv);

		return SheetToCsv.CopyAsync(sheet, csv, true, cancellationToken);
	}

	/// <summary>
	/// Converts every worksheet of the workbook at a path to a CSV file in a folder: <c>&lt;folder&gt;/&lt;sheet&gt;.csv</c>,
	/// in tab order, hidden sheets included. Chartsheets and the legacy kinds have no cells, and are skipped.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The folder is created if it is missing, and a file of the same name is overwritten. A file's name is its sheet's,
	/// made a file name on every system: <c>&lt; &gt; : " / \ | ? *</c> and control characters are <c>_</c>, dots and spaces
	/// at the end are dropped, Windows' device names (<c>CON</c>, <c>NUL</c>, <c>COM1</c>) get <c>_</c> before them, a name
	/// is cut to 64 characters, and an empty one is <c>Sheet</c> and the sheet's number in tab order. Names then the same
	/// ignoring case are numbered as Excel numbers copies, <c>Data (2)</c>. So a crafted sheet named <c>../x</c> is written to
	/// <c>.._x.csv</c>, and no file is written outside the folder.
	/// </para>
	/// <para>
	/// <see cref="ExcelToCsvOptions.Excel"/> applies to every sheet, so options that choose a sheet or an area of one can't be
	/// set. When a sheet fails, its file is deleted, and the files of the sheets before it are left.
	/// </para>
	/// </remarks>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="folder">The folder the files are written to.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <returns>The files' paths, each the folder combined with the file's name, in tab order.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="excelPath"/> or <paramref name="folder"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The options are not valid, or choose a sheet or an area (<see cref="ExcelReaderOptions.Sheet"/>,
	/// <see cref="ExcelReaderOptions.SheetIndex"/>, <see cref="ExcelReaderOptions.Range"/>, <see cref="ExcelReaderOptions.Table"/>
	/// or <see cref="ExcelReaderOptions.DefinedName"/>), or a sheet's file would be the workbook.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">A sheet has no row with the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The workbook can't be opened, or the folder or a file can't be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The folder or a file can't be written with the caller's permissions.</exception>
	public static IReadOnlyList<string> ToCsvFiles(string excelPath, string folder, ExcelToCsvOptions? options = null) =>
		SyncOrAsync.Result(ToCsvFilesCoreAsync(excelPath, folder, options, false, CancellationToken.None));

	/// <summary>Asynchronously converts every worksheet of the workbook at a path to a CSV file in a folder. See <see cref="ToCsvFiles"/>.</summary>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="folder">The folder the files are written to.</param>
	/// <param name="options">The options; <see langword="null"/> for <see cref="ExcelToCsvOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels the conversion, which then deletes the file being written.</param>
	/// <returns>A task whose result is the files' paths, in tab order.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="excelPath"/> or <paramref name="folder"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options are not valid, or choose a sheet or an area, or a sheet's file would be the workbook.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>.</exception>
	/// <exception cref="CsvFormatException">A cell can't be read, naming it, or the header row the options ask for can't be.</exception>
	/// <exception cref="FormatException">A cell is an error and <see cref="ExcelReaderOptions.ErrorCells"/> is <see cref="ExcelErrorCells.Error"/>; the message names the cell.</exception>
	/// <exception cref="KeyNotFoundException">A sheet has no row with the header row's columns (<see cref="ExcelReaderOptions.HeaderRow"/>).</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The workbook can't be opened, or the folder or a file can't be created.</exception>
	/// <exception cref="UnauthorizedAccessException">The folder or a file can't be written with the caller's permissions.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask<IReadOnlyList<string>> ToCsvFilesAsync(string excelPath, string folder, ExcelToCsvOptions? options = null, CancellationToken cancellationToken = default) =>
		ToCsvFilesCoreAsync(excelPath, folder, options, true, cancellationToken);

	/// <summary>Converts a sheet of the workbook at a path to a CSV file, synchronously or not.</summary>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="csvPath">The path of the CSV file.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous conversion.</param>
	/// <returns>A task that completes when the file is written and closed.</returns>
	private static async ValueTask ToCsvCoreAsync(string excelPath, string csvPath, ExcelToCsvOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(excelPath);
		ArgumentNullException.ThrowIfNull(csvPath);
		var settings = Prepare(options);
		if (SamePath(excelPath, csvPath))
		{
			throw new ArgumentException($"The CSV file, \"{csvPath}\", is the workbook, which converting would overwrite.", nameof(csvPath));
		}

		// The workbook is opened first, so that one that can't be read leaves a CSV file of that name as it was.
		var sheet = async
			? await ExcelReader.FromFileAsync(excelPath, settings.Excel, cancellationToken).ConfigureAwait(false)
			: ExcelReader.FromFile(excelPath, settings.Excel);
		try
		{
			await WriteFileAsync(sheet, csvPath, settings.WriterOptions(), async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await DisposeAsync(sheet, async).ConfigureAwait(false);
		}
	}

	/// <summary>Converts a sheet of the workbook in a stream to CSV in another stream, synchronously or not.</summary>
	/// <param name="excel">The stream of the workbook.</param>
	/// <param name="csv">The stream of the CSV.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous conversion.</param>
	/// <returns>A task that completes when the CSV is written and flushed.</returns>
	private static async ValueTask ToCsvCoreAsync(Stream excel, Stream csv, ExcelToCsvOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(excel);
		ArgumentNullException.ThrowIfNull(csv);
		var settings = Prepare(options);
		if (!excel.CanRead)
		{
			throw new ArgumentException("The workbook's stream can't be read.", nameof(excel));
		}

		if (!csv.CanWrite)
		{
			throw new ArgumentException("The CSV's stream can't be written.", nameof(csv));
		}

		var sheet = async
			? await ExcelReader.CreateAsync(excel, settings.Excel, leaveOpen: true, cancellationToken).ConfigureAwait(false)
			: ExcelReader.Create(excel, settings.Excel, leaveOpen: true);
		try
		{
			await CopyAsync(sheet, CsvWriter.Create(csv, settings.WriterOptions(), leaveOpen: true), null, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await DisposeAsync(sheet, async).ConfigureAwait(false);
		}
	}

	/// <summary>Converts every worksheet of the workbook at a path to a CSV file in a folder, synchronously or not.</summary>
	/// <param name="excelPath">The path of the workbook.</param>
	/// <param name="folder">The folder.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous conversion.</param>
	/// <returns>A task whose result is the files' paths.</returns>
	private static async ValueTask<IReadOnlyList<string>> ToCsvFilesCoreAsync(string excelPath, string folder, ExcelToCsvOptions? options, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(excelPath);
		ArgumentNullException.ThrowIfNull(folder);
		var settings = Prepare(options);
		if (SheetChoice(settings.Excel) is { } choice)
		{
			throw new ArgumentException($"ToCsvFiles writes every worksheet, so ExcelReaderOptions.{choice} can't be set. Use ToCsv for one sheet.", nameof(options));
		}

		var workbook = async
			? await ExcelWorkbook.OpenAsync(excelPath, settings.Excel, cancellationToken).ConfigureAwait(false)
			: ExcelWorkbook.Open(excelPath, settings.Excel);
		try
		{
			var sheets = workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet).ToList();
			var names = ConversionNames.FileNames([.. sheets.Select(s => (s.Name, s.Index + 1))]);
			var paths = new string[names.Length];
			for (var i = 0; i < paths.Length; i++)
			{
				paths[i] = Path.Combine(folder, names[i]);
				CheckFile(paths[i], folder, sheets[i].Name);
				if (SamePath(excelPath, paths[i]))
				{
					throw new ArgumentException($"The CSV file of the sheet \"{sheets[i].Name}\", \"{paths[i]}\", is the workbook, which converting would overwrite.", nameof(folder));
				}
			}

			Directory.CreateDirectory(folder);
			var writerOptions = settings.WriterOptions();
			for (var i = 0; i < paths.Length; i++)
			{
				var sheet = async
					? await workbook.OpenReaderAsync(sheets[i], null, cancellationToken).ConfigureAwait(false)
					: workbook.OpenReader(sheets[i]);
				try
				{
					await WriteFileAsync(sheet, paths[i], writerOptions, async, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					await DisposeAsync(sheet, async).ConfigureAwait(false);
				}
			}

			return paths;
		}
		finally
		{
			await DisposeAsync(workbook, async).ConfigureAwait(false);
		}
	}

	/// <summary>Checks the version of CsvHelper and the options, before a conversion reads anything.</summary>
	/// <param name="options">The options, or <see langword="null"/> for the default.</param>
	/// <returns>The options to use.</returns>
	private static ExcelToCsvOptions Prepare(ExcelToCsvOptions? options)
	{
		VersionGuard.EnsureMatch();
		var settings = options ?? ExcelToCsvOptions.Default;
		settings.Validate(nameof(options));
		// The writer's options are checked before the workbook is opened, and before a file is created for a writer that can't be.
		settings.Csv.Validate();

		return settings;
	}

	/// <summary>The option that chooses a sheet or an area, which <see cref="ToCsvFiles"/> can't take.</summary>
	/// <param name="options">The reader's options.</param>
	/// <returns>The option's name, or <see langword="null"/> for none.</returns>
	private static string? SheetChoice(ExcelReaderOptions options) =>
		options.Sheet is not null ? nameof(ExcelReaderOptions.Sheet)
		: options.SheetIndex is not null ? nameof(ExcelReaderOptions.SheetIndex)
		: options.Range is not null ? nameof(ExcelReaderOptions.Range)
		: options.Table is not null ? nameof(ExcelReaderOptions.Table)
		: options.DefinedName is not null ? nameof(ExcelReaderOptions.DefinedName)
		: null;

	/// <summary>
	/// Whether two paths are the same file's, ignoring case, as Windows' and macOS' file systems do; a path that can't be
	/// made full is no other's, and its open says why.
	/// </summary>
	/// <param name="first">A path.</param>
	/// <param name="second">Another path.</param>
	/// <returns>Whether they are the same.</returns>
	private static bool SamePath(string first, string second)
	{
		try
		{
			return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
		}
		catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
		{
			return false;
		}
	}

	/// <summary>
	/// Checks that a sheet's file is in the folder, and not below it: <see cref="ConversionNames"/> makes names without
	/// separators, so this holds unless that has a bug.
	/// </summary>
	/// <param name="path">The file's path.</param>
	/// <param name="folder">The folder.</param>
	/// <param name="sheetName">The sheet's name, for the message.</param>
	/// <exception cref="InvalidOperationException">The file is not in the folder.</exception>
	private static void CheckFile(string path, string folder, string sheetName)
	{
		var root = Path.GetFullPath(folder);
		if (root[root.Length - 1] != Path.DirectorySeparatorChar && root[root.Length - 1] != Path.AltDirectorySeparatorChar)
		{
			root += Path.DirectorySeparatorChar;
		}

		var full = Path.GetFullPath(path);
		if (!full.StartsWith(root, StringComparison.Ordinal) || full.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], root.Length) >= 0)
		{
			throw new InvalidOperationException($"The CSV file of the sheet \"{sheetName}\", \"{full}\", is not in the folder \"{root}\".");
		}
	}

	/// <summary>Writes a reader's records to a new CSV file, which is deleted when that fails.</summary>
	/// <param name="sheet">The reader.</param>
	/// <param name="path">The file's path.</param>
	/// <param name="options">The writer's options, which are valid.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous conversion.</param>
	/// <returns>A task that completes when the file is written and closed.</returns>
	private static ValueTask WriteFileAsync(CsvReader sheet, string path, CsvWriterOptions options, bool async, CancellationToken cancellationToken) =>
		CopyAsync(sheet, CsvWriter.ToFile(path, options, async ? FileOptions.Asynchronous : FileOptions.None), path, async, cancellationToken);

	/// <summary>
	/// Writes a reader's records to a writer, and disposes the writer, which flushes it. When either fails, the writer is
	/// disposed without its failure hiding the first, and its file, if it has one, is deleted.
	/// </summary>
	/// <param name="sheet">The reader.</param>
	/// <param name="csv">The writer, which this owns.</param>
	/// <param name="path">The path of the writer's file, or <see langword="null"/> for a stream.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous conversion.</param>
	/// <returns>A task that completes when the writer is disposed.</returns>
	private static async ValueTask CopyAsync(CsvReader sheet, CsvWriter csv, string? path, bool async, CancellationToken cancellationToken)
	{
		try
		{
			await SheetToCsv.CopyAsync(sheet, csv, async, cancellationToken).ConfigureAwait(false);
			await DisposeAsync(csv, async).ConfigureAwait(false);
		}
		catch
		{
			try
			{
				await DisposeAsync(csv, async).ConfigureAwait(false);
			}
			catch
			{
				// The first failure says why.
			}

			if (path is not null)
			{
				Delete(path);
			}

			throw;
		}
	}

	/// <summary>Deletes a file that a failed conversion wrote, if it can: a failure to delete it doesn't hide the conversion's.</summary>
	/// <param name="path">The file's path.</param>
	private static void Delete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// The conversion's failure says why.
		}
	}

	/// <summary>Disposes a reader, writer or workbook, synchronously or not.</summary>
	/// <typeparam name="T">Its type.</typeparam>
	/// <param name="value">It.</param>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <returns>A task that completes when it is disposed.</returns>
	private static ValueTask DisposeAsync<T>(T value, bool async)
		where T : IDisposable, IAsyncDisposable
	{
		if (async)
		{
			return value.DisposeAsync();
		}

		value.Dispose();

		return default;
	}
}
