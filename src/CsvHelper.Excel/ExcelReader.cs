using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Reads one sheet of a workbook in one call, for the common case: a <see cref="CsvReader"/> that owns its
/// <see cref="ExcelWorkbook"/>, so disposing the reader closes the file or stream. The names mirror
/// <see cref="CsvReader.FromFile"/> and <see cref="CsvReader.Create(Stream, CsvReaderOptions?, bool)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The sheet is the one the options choose (<see cref="ExcelReaderOptions.Sheet"/> or <see cref="ExcelReaderOptions.SheetIndex"/>),
/// else the first worksheet, hidden ones included. To list the sheets or read several, open an <see cref="ExcelWorkbook"/>.
/// </para>
/// <para>
/// <see cref="Import{T}(string, ExcelReaderOptions?, int)"/> and its overloads read a sheet's records and every cell that
/// doesn't read, by its address, such as an <see cref="ExcelTemplate"/> that users filled in.
/// </para>
/// </remarks>
public static class ExcelReader
{
	/// <summary>Opens a reader of a sheet of the workbook at a path.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <returns>The reader, which owns the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options name no sheet of the workbook, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, or a limit is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	public static CsvReader FromFile(string path, ExcelReaderOptions? options = null) =>
		SyncOrAsync.Result(FromFileCoreAsync(path, options, false, CancellationToken.None));

	/// <summary>Opens a reader of a sheet of the workbook in a stream.</summary>
	/// <param name="stream">The stream of the workbook: one that can seek is read from its start, and one that can't is copied to a temporary file.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether disposing the reader leaves <paramref name="stream"/> open. When the reader can't be opened, a stream that can seek is left open.</param>
	/// <returns>The reader, which owns the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options name no sheet of the workbook or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, or a limit is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	public static CsvReader Create(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false) =>
		SyncOrAsync.Result(CreateCoreAsync(stream, options, leaveOpen, false, CancellationToken.None));

	/// <summary>Asynchronously opens a reader of a sheet of the workbook at a path. See <see cref="FromFile"/>.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="cancellationToken">Cancels the open.</param>
	/// <returns>A task whose result is the reader, which owns the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options name no sheet of the workbook, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, or a limit is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	public static ValueTask<CsvReader> FromFileAsync(string path, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default) =>
		FromFileCoreAsync(path, options, true, cancellationToken);

	/// <summary>Asynchronously opens a reader of a sheet of the workbook in a stream. See <see cref="Create"/>.</summary>
	/// <param name="stream">The stream of the workbook.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether disposing the reader leaves <paramref name="stream"/> open.</param>
	/// <param name="cancellationToken">Cancels the open.</param>
	/// <returns>A task whose result is the reader, which owns the workbook.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options name no sheet of the workbook or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The options' sheet index is out of range, or a limit is 0 or less.</exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	public static ValueTask<CsvReader> CreateAsync(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default) =>
		CreateCoreAsync(stream, options, leaveOpen, true, cancellationToken);

	/// <summary>
	/// Imports a sheet of the workbook at a path: its records of <typeparamref name="T"/> that read, and every cell that
	/// doesn't, by its address, for telling users which cells to fix.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The sheet is the one <see cref="FromFile"/> reads: <see cref="ExcelReaderOptions.Sheet"/>, else the first worksheet,
	/// so not the very hidden lists sheet of an <see cref="ExcelTemplate"/>, which is last. Its header must have each column
	/// that <typeparamref name="T"/> requires: each one missing is an error at the header's row, and then no record is read.
	/// A header row that isn't found is the same: one that <see cref="ExcelReaderOptions.HeaderRow"/> names and that has no
	/// value (<see cref="ExcelHeaderRow.At"/>), such as the second row of an upload of another file where a template under a
	/// title has its header, gives an error at that row for each column; and one that a search finds in no row
	/// (<see cref="ExcelHeaderRow.Find{T}(int)"/>), an error for each column the closest row it tried lacks, at that row, or
	/// without a place when no row it tried has a value.
	/// </para>
	/// <para>
	/// Each record is read with <typeparamref name="T"/>'s mapper, as <see cref="CsvReader.GetRecords{T}"/> reads it. A
	/// record that reads is added to <see cref="ExcelImportResult{T}.Records"/>. One that doesn't is left out, and gives an
	/// error for each member whose cell doesn't read on its own (<see cref="CsvRecordMapper{T}.FindFailingMembers"/>): a
	/// text in a number's column, a name that isn't the enum's, a value a <see cref="CsvValidateAttribute"/> refuses; and an
	/// error at the record's row when none does, such as when its constructor throws. The import checks what the mapping
	/// reads, not a column's <see cref="ExcelColumnOptions.List"/> or the bounds of its validation: a
	/// <see cref="CsvValidateAttribute"/> says what a member takes.
	/// </para>
	/// <para>
	/// Empty rows are skipped, as <see cref="CsvReaderOptions.SkipEmptyLines"/> skips them; error cells read as
	/// <see cref="ExcelReaderOptions.ErrorCells"/> says, so that under <see cref="ExcelErrorCells.Text"/> a <c>#N/A</c> in a
	/// number's column is an error of its own. <see cref="CsvReaderOptions.OnRecordError"/> isn't called, since every
	/// error is in the result; a workbook that can't be read throws, as <see cref="FromFile"/> throws.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="maxErrors">
	/// The errors after which reading stops, 1 or more, since an upload of the wrong file can have a million cells at fault;
	/// <see cref="ExcelImportResult{T}.Complete"/> is then <see langword="false"/>.
	/// </param>
	/// <returns>The records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options name no sheet of the workbook, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxErrors"/> is less than 1, the options' sheet index is out of range, or a limit is 0 or less.
	/// </exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with, or <typeparamref name="T"/> is not a
	/// <see cref="CsvRecordAttribute"/> type.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The options' <see cref="CsvReaderOptions.HasHeader"/> is off, and a required member of <typeparamref name="T"/> has no
	/// column index.
	/// </exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	public static ExcelImportResult<T> Import<T>(string path, ExcelReaderOptions? options = null, int maxErrors = 1000) =>
		SyncOrAsync.Result(ImportCoreAsync<T>(path, options, maxErrors, false, CancellationToken.None));

	/// <summary>
	/// Imports a sheet of the workbook in a stream, leaving it open: its records that read, and every cell that doesn't. See
	/// <see cref="Import{T}(string, ExcelReaderOptions?, int)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="stream">The stream of the workbook: one that can seek is read from its start, and one that can't is copied to a temporary file.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops, 1 or more.</param>
	/// <returns>The records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options name no sheet of the workbook or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxErrors"/> is less than 1, the options' sheet index is out of range, or a limit is 0 or less.
	/// </exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with, or <typeparamref name="T"/> is not a
	/// <see cref="CsvRecordAttribute"/> type.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The options' <see cref="CsvReaderOptions.HasHeader"/> is off, and a required member of <typeparamref name="T"/> has no
	/// column index.
	/// </exception>
	public static ExcelImportResult<T> Import<T>(Stream stream, ExcelReaderOptions? options = null, int maxErrors = 1000) =>
		SyncOrAsync.Result(ImportCoreAsync<T>(stream, options, maxErrors, false, CancellationToken.None));

	/// <summary>
	/// Asynchronously imports a sheet of the workbook at a path, reading with <see cref="CsvReader.ReadAsync"/>. See
	/// <see cref="Import{T}(string, ExcelReaderOptions?, int)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops, 1 or more.</param>
	/// <param name="cancellationToken">Cancels the import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The options name no sheet of the workbook, or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxErrors"/> is less than 1, the options' sheet index is out of range, or a limit is 0 or less.
	/// </exception>
	/// <exception cref="InvalidDataException">The file is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The file is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with, or <typeparamref name="T"/> is not a
	/// <see cref="CsvRecordAttribute"/> type.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The options' <see cref="CsvReaderOptions.HasHeader"/> is off, and a required member of <typeparamref name="T"/> has no
	/// column index.
	/// </exception>
	/// <exception cref="IOException">The file can't be opened.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask<ExcelImportResult<T>> ImportAsync<T>(string path, ExcelReaderOptions? options = null, int maxErrors = 1000, CancellationToken cancellationToken = default) =>
		ImportCoreAsync<T>(path, options, maxErrors, true, cancellationToken);

	/// <summary>
	/// Asynchronously imports a sheet of the workbook in a stream, leaving it open, reading with
	/// <see cref="CsvReader.ReadAsync"/>. See <see cref="Import{T}(string, ExcelReaderOptions?, int)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="stream">The stream of the workbook: one that can seek is read from its start, and one that can't is copied to a temporary file.</param>
	/// <param name="options">The options, with the sheet to read; <see langword="null"/> for <see cref="ExcelReaderOptions.Default"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops, 1 or more.</param>
	/// <param name="cancellationToken">Cancels the import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The stream can't be read, or the options name no sheet of the workbook or are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="maxErrors"/> is less than 1, the options' sheet index is out of range, or a limit is 0 or less.
	/// </exception>
	/// <exception cref="InvalidDataException">The stream is not a workbook, a part can't be read, or a part passes a limit.</exception>
	/// <exception cref="NotSupportedException">The stream is an <c>.xlsb</c>, an encrypted workbook or an <c>.xls</c>, or the sheet is not a worksheet.</exception>
	/// <exception cref="InvalidOperationException">
	/// The CsvHelper loaded is not the version this CsvHelper.Excel was built with, or <typeparamref name="T"/> is not a
	/// <see cref="CsvRecordAttribute"/> type.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The options' <see cref="CsvReaderOptions.HasHeader"/> is off, and a required member of <typeparamref name="T"/> has no
	/// column index.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask<ExcelImportResult<T>> ImportAsync<T>(Stream stream, ExcelReaderOptions? options = null, int maxErrors = 1000, CancellationToken cancellationToken = default) =>
		ImportCoreAsync<T>(stream, options, maxErrors, true, cancellationToken);

	/// <summary>
	/// Imports the records of a reader from where it stands, reading its header first when it hasn't: a sheet of a workbook
	/// already open (<see cref="ExcelWorkbook.OpenReader(string, ExcelReaderOptions?)"/>), or a CSV file. See
	/// <see cref="Import{T}(string, ExcelReaderOptions?, int)"/>.
	/// </summary>
	/// <remarks>
	/// The reader's own options apply, and it isn't disposed. For CSV, an error's <see cref="ExcelImportError.Location"/> is
	/// <see langword="null"/> and its <see cref="ExcelImportError.Row"/> the record's number. A column the header lacks is
	/// located where the reader stands, which is the header's row unless it has read a record.
	/// </remarks>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="maxErrors">The errors after which reading stops, 1 or more.</param>
	/// <returns>The records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxErrors"/> is less than 1.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type, or the reader is the one a parallel read's
	/// selector got.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The reader has no header (<see cref="CsvReaderOptions.HasHeader"/>), and a required member of
	/// <typeparamref name="T"/> has no column index.
	/// </exception>
	/// <exception cref="CsvFormatException">The reader throws it for malformed CSV, as its options say.</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	public static ExcelImportResult<T> Import<T>(CsvReader reader, int maxErrors = 1000) =>
		SyncOrAsync.Result(ImportCoreAsync<T>(reader, maxErrors, false, CancellationToken.None));

	/// <summary>
	/// Asynchronously imports the records of a reader from where it stands, reading with <see cref="CsvReader.ReadAsync"/>.
	/// See <see cref="Import{T}(CsvReader, int)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="reader">The reader, which isn't disposed.</param>
	/// <param name="maxErrors">The errors after which reading stops, 1 or more.</param>
	/// <param name="cancellationToken">Cancels the import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxErrors"/> is less than 1.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type, or the reader is the one a parallel read's
	/// selector got.
	/// </exception>
	/// <exception cref="System.Collections.Generic.KeyNotFoundException">
	/// The reader has no header (<see cref="CsvReaderOptions.HasHeader"/>), and a required member of
	/// <typeparamref name="T"/> has no column index.
	/// </exception>
	/// <exception cref="CsvFormatException">The reader throws it for malformed CSV, as its options say.</exception>
	/// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static ValueTask<ExcelImportResult<T>> ImportAsync<T>(CsvReader reader, int maxErrors = 1000, CancellationToken cancellationToken = default) =>
		ImportCoreAsync<T>(reader, maxErrors, true, cancellationToken);

	/// <summary>Imports a sheet of the workbook at a path, synchronously or not, and closes the workbook.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	private static async ValueTask<ExcelImportResult<T>> ImportCoreAsync<T>(string path, ExcelReaderOptions? options, int maxErrors, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(path);
		Importer.CheckMaxErrors(maxErrors);
		CsvReader reader;
		try
		{
			reader = await FromFileCoreAsync(path, options, async, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (HeaderSearch.NotFoundOf(e) is { } notFound)
		{
			return Importer.HeaderNotFound<T>(notFound, e, maxErrors);
		}

		try
		{
			return await Importer.ImportAsync<T>(reader, maxErrors, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await ExcelConvert.DisposeAsync(reader, async).ConfigureAwait(false);
		}
	}

	/// <summary>Imports a sheet of the workbook in a stream, synchronously or not, leaving the stream open.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="stream">The stream of the workbook.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	private static async ValueTask<ExcelImportResult<T>> ImportCoreAsync<T>(Stream stream, ExcelReaderOptions? options, int maxErrors, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(stream);
		Importer.CheckMaxErrors(maxErrors);
		CsvReader reader;
		try
		{
			reader = await CreateCoreAsync(stream, options, true, async, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (HeaderSearch.NotFoundOf(e) is { } notFound)
		{
			return Importer.HeaderNotFound<T>(notFound, e, maxErrors);
		}

		try
		{
			return await Importer.ImportAsync<T>(reader, maxErrors, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await ExcelConvert.DisposeAsync(reader, async).ConfigureAwait(false);
		}
	}

	/// <summary>Imports the records of a reader, synchronously or not.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="maxErrors">The errors after which reading stops.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous import.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	private static async ValueTask<ExcelImportResult<T>> ImportCoreAsync<T>(CsvReader reader, int maxErrors, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		Importer.CheckMaxErrors(maxErrors);

		return await Importer.ImportAsync<T>(reader, maxErrors, async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Opens the workbook at a path and a reader that owns it, synchronously or not.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the reader.</returns>
	private static async ValueTask<CsvReader> FromFileCoreAsync(string path, ExcelReaderOptions? options, bool async, CancellationToken cancellationToken)
	{
		var workbook = await ExcelWorkbook.OpenCoreAsync(path, options, async, cancellationToken).ConfigureAwait(false);

		return await workbook.OpenOwningReaderAsync(async, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Opens the workbook in a stream and a reader that owns it, synchronously or not.</summary>
	/// <param name="stream">The stream of the workbook.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="leaveOpen">Whether disposing the reader leaves <paramref name="stream"/> open.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the reader.</returns>
	private static async ValueTask<CsvReader> CreateCoreAsync(Stream stream, ExcelReaderOptions? options, bool leaveOpen, bool async, CancellationToken cancellationToken)
	{
		var workbook = await ExcelWorkbook.OpenCoreAsync(stream, options, leaveOpen, async, cancellationToken).ConfigureAwait(false);

		return await workbook.OpenOwningReaderAsync(async, cancellationToken).ConfigureAwait(false);
	}
}
