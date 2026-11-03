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
/// The sheet is the one the options choose (<see cref="ExcelReaderOptions.Sheet"/> or <see cref="ExcelReaderOptions.SheetIndex"/>),
/// else the first worksheet, hidden ones included. To list the sheets or read several, open an <see cref="ExcelWorkbook"/>.
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
