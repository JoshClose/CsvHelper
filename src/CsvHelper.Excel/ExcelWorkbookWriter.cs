using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Writes a workbook (<c>.xlsx</c>), a sheet at a time, each through a plain <see cref="CsvWriter"/>: records of a
/// <see cref="CsvRecordAttribute"/> type, dynamic records, or fields written by hand become the sheet's cells, streamed
/// into the package as they are written.
/// </summary>
/// <remarks>
/// <para>
/// Sheets are written in turn, in tab order: <see cref="WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/> writes one whole, and
/// <see cref="AddSheet(string, ExcelSheetOptions)"/> returns a writer for one, which disposing completes. A workbook is written one sheet at a
/// time, so adding a sheet while another's writer is open throws. Disposing the workbook writer completes an open sheet,
/// then writes the workbook's other parts and closes the package; a workbook with no sheet gets one empty sheet,
/// <c>Sheet1</c>, since Excel opens no workbook without one.
/// </para>
/// <para>
/// A record that fails (a converter's exception, a cell too long for Excel, a row past Excel's last) is discarded by the
/// record methods, as a CSV writer discards one: the sheet keeps the records before it, and the workbook can still be
/// completed into a file Excel opens. An exception from the stream leaves the file incomplete.
/// </para>
/// <para>
/// Not thread-safe, as <see cref="CsvWriter"/> isn't. Before .NET 10, closing a sheet's entry and the package writes to
/// the stream synchronously, even from <see cref="DisposeAsync"/>, so a stream that forbids synchronous writes throws;
/// write to a file or a buffer there.
/// </para>
/// </remarks>
public sealed class ExcelWorkbookWriter : IDisposable, IAsyncDisposable
{
	/// <summary>The buffer of a file the writer creates: the zip writes headers in small pieces.</summary>
	private const int FileBufferSize = 64 * 1024;

	/// <summary>The sink of each sheet's writer, for <see cref="ExcelWriterExtensions.MergeCells"/>: weak, so that it goes with the writer.</summary>
	private static readonly ConditionalWeakTable<CsvWriter, SheetSink> Sinks = new();

	private readonly PackageWriter package;
	private readonly List<string> sheetNames = [];
	// The name each key belongs to (SheetNames.Key), to refuse names that Excel takes to be the same.
	private readonly Dictionary<string, string> namesByKey = new(StringComparer.Ordinal);
	private readonly SharedStringTable? strings;
	private readonly StyleRegistry styles = new();
	// The workbook's tables' names, and the number of tables written, whose ids are 1 to it.
	private readonly TableNames tableNames = new();
	private int tables;
	// The sheets' filters, for the workbook's _xlnm._FilterDatabase names: each sheet's place in tab order, from 0, and range.
	private readonly List<(int Sheet, ExcelRange Range)> filters = [];
	// The sheet being written, and its writer.
	private SheetSink? openSheet;
	private CsvWriter? openWriter;
	private bool disposed;

	/// <summary>Creates a workbook writer over its package.</summary>
	/// <param name="package">The package.</param>
	/// <param name="options">The options, which are valid.</param>
	private ExcelWorkbookWriter(PackageWriter package, ExcelWriterOptions options)
	{
		this.package = package;
		Options = options;
		strings = options.Strings == ExcelStrings.Shared ? new SharedStringTable() : null;
	}

	/// <summary>The options this writer was created with.</summary>
	public ExcelWriterOptions Options { get; }

	/// <summary>
	/// The names of the sheets added so far, in tab order, the one being written included, with those that
	/// <see cref="ExcelRowLimit.NewSheet"/> added.
	/// </summary>
	public IReadOnlyList<string> SheetNames => sheetNames;

	/// <summary>
	/// The rows a sheet holds before <see cref="ExcelWriterOptions.RowLimit"/> applies: Excel's 1,048,576, and fewer in
	/// tests, which set it before adding a sheet. At least 2.
	/// </summary>
	internal int MaxRows { get; set; } = CellReference.MaxRow;

	/// <summary>The sink of the sheet being written, or <see langword="null"/>, for tests of what a sheet was added with.</summary>
	internal SheetSink? OpenSheet => openSheet;

	/// <summary>
	/// Creates (or overwrites) a workbook file, which other processes can read while it is written. Disposing the writer
	/// closes the file.
	/// </summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="ExcelWriterOptions.Default"/>.</param>
	/// <param name="fileOptions">How the file is opened.</param>
	/// <returns>The writer, with no sheet yet.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="path"/> is empty, or the options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	/// <exception cref="DirectoryNotFoundException">The directory of the file does not exist.</exception>
	/// <exception cref="UnauthorizedAccessException">The file cannot be written with the caller's permissions.</exception>
	/// <exception cref="IOException">The file cannot be created, for example because another process has it open.</exception>
	public static ExcelWorkbookWriter ToFile(string path, ExcelWriterOptions? options = null, FileOptions fileOptions = FileOptions.None)
	{
		ArgumentNullException.ThrowIfNull(path);
		var settings = Prepare(options);
		var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, FileBufferSize, fileOptions);
		try
		{
			return new ExcelWorkbookWriter(new PackageWriter(stream, leaveOpen: false, settings.CompressionLevel), settings);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	/// <summary>Creates a writer of a workbook into a stream, from its position. The stream need not seek.</summary>
	/// <param name="stream">The stream, which must be writable.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="ExcelWriterOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether disposing the writer leaves <paramref name="stream"/> open.</param>
	/// <returns>The writer, with no sheet yet.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written, or the options are not valid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
	/// <exception cref="InvalidOperationException">The CsvHelper loaded is not the version this CsvHelper.Excel was built with.</exception>
	public static ExcelWorkbookWriter Create(Stream stream, ExcelWriterOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var settings = Prepare(options);
		if (!stream.CanWrite)
		{
			throw new ArgumentException("The stream can't be written.", nameof(stream));
		}

		return new ExcelWorkbookWriter(new PackageWriter(stream, leaveOpen, settings.CompressionLevel), settings);
	}

	/// <summary>
	/// Writes a sheet whole, as <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> writes records: the header of
	/// <typeparamref name="T"/> when <see cref="CsvWriterOptions.HasHeader"/> is set, then each record; then the sheet is
	/// complete. <typeparamref name="T"/>'s formats and Excel attributes apply, as for
	/// <see cref="AddSheet{T}(string, ExcelSheetOptions)"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type, or a dynamic record's type.</typeparam>
	/// <param name="name">The sheet's name (see <see cref="AddSheet(string, ExcelSheetOptions)"/>).</param>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="options">
	/// How the sheet looks, or <see langword="null"/> for <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/>, else
	/// <see cref="ExcelWriterOptions.Sheets"/>.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The name is not one Excel opens, the workbook has a sheet Excel takes to be the same, or <paramref name="options"/>
	/// are not valid or name a table the workbook has.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Another sheet's writer is open, <typeparamref name="T"/>'s Excel attributes are not valid, a record can't be written
	/// (it is discarded, and the sheet completed with the records before it), <see cref="ExcelSheetOptions.Columns"/> names
	/// a column the header doesn't have (the header is discarded so), or <typeparamref name="T"/> has no record writer.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	public void WriteSheet<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string name, IEnumerable<T> records, ExcelSheetOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(records);
		using var writer = AddSheet<T>(name, options);
		writer.WriteRecords(records);
	}

	/// <summary>
	/// Writes a sheet whole, as <see cref="WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/> does, writing to the
	/// stream asynchronously.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type, or a dynamic record's type.</typeparam>
	/// <param name="name">The sheet's name (see <see cref="AddSheet(string, ExcelSheetOptions)"/>).</param>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="options">
	/// How the sheet looks, or <see langword="null"/> for <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/>, else
	/// <see cref="ExcelWriterOptions.Sheets"/>.
	/// </param>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>A task that completes when the sheet is complete.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The name is not one Excel opens, the workbook has a sheet Excel takes to be the same, or <paramref name="options"/>
	/// are not valid or name a table the workbook has.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Another sheet's writer is open, <typeparamref name="T"/>'s Excel attributes are not valid, or a record can't be
	/// written.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteSheetAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string name, IEnumerable<T> records, ExcelSheetOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		var writer = AddSheet<T>(name, options);

		return WriteAndCompleteAsync(writer, w => w.WriteRecordsAsync(records, cancellationToken));
	}

	/// <summary>
	/// Writes a sheet whole from an asynchronous sequence, as
	/// <see cref="WriteSheet{T}(string, IEnumerable{T}, ExcelSheetOptions)"/> does, writing to the stream asynchronously.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type, or a dynamic record's type.</typeparam>
	/// <param name="name">The sheet's name (see <see cref="AddSheet(string, ExcelSheetOptions)"/>).</param>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="options">
	/// How the sheet looks, or <see langword="null"/> for <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/>, else
	/// <see cref="ExcelWriterOptions.Sheets"/>.
	/// </param>
	/// <param name="cancellationToken">Cancels the enumeration and writing.</param>
	/// <returns>A task that completes when the sheet is complete.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The name is not one Excel opens, the workbook has a sheet Excel takes to be the same, or <paramref name="options"/>
	/// are not valid or name a table the workbook has.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Another sheet's writer is open, <typeparamref name="T"/>'s Excel attributes are not valid, or a record can't be
	/// written.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteSheetAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string name, IAsyncEnumerable<T> records, ExcelSheetOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		var writer = AddSheet<T>(name, options);

		return WriteAndCompleteAsync(writer, w => w.WriteRecordsAsync(records, cancellationToken));
	}

	/// <summary>
	/// Adds a sheet, after the sheets added before it, and returns the writer of its cells: records written with
	/// <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/>, <see cref="CsvWriter.WriteRecord{T}(T)"/> or dynamic
	/// records, and fields written by hand (<see cref="CsvWriter.WriteField(string?)"/> and the rest, then
	/// <see cref="CsvWriter.EndRecord"/>). Record <c>n</c> (from 0) is row <c>n + 1</c>, and field <c>i</c> is column
	/// <c>i</c> from <c>A</c>. Disposing the writer completes the sheet.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A name has 1 to 31 UTF-16 code units, none of <c>\ / ? * [ ] :</c>, no apostrophe first or last, and no control
	/// characters or lone surrogates; anything else is allowed, <c>History</c> included. Two names that Excel takes to be
	/// the same can't both be used: names that differ in case, ASCII or not (<c>Data</c> and <c>data</c>, <c>straße</c>
	/// and <c>STRASSE</c>), or by Unicode equivalence (<c>é</c> composed and decomposed, <c>ﬁ</c> and <c>fi</c>). The
	/// writer also takes Unicode's compatibility forms (<c>A</c> and the full-width <c>Ａ</c>) to be the same name, which
	/// Excel doesn't.
	/// </para>
	/// <para>
	/// A field's cell is typed by the field's type, when its text is .NET's default: integers, <see cref="decimal"/>,
	/// <see cref="double"/>, <see cref="float"/> and <c>Half</c> are number cells; <see cref="bool"/> a boolean cell;
	/// <see cref="DateTime"/>, <c>DateOnly</c>, <c>TimeOnly</c> and <see cref="TimeSpan"/> date, time and duration cells,
	/// rounded to the millisecond, in a format by what each value holds (<c>yyyy-mm-dd</c>, <c>yyyy-mm-dd hh:mm:ss</c>,
	/// <c>hh:mm:ss</c>, <c>[h]:mm:ss</c>, with <c>.000</c> for milliseconds). A number in the writer's culture is read
	/// with it; other text, such as a format's (<c>1,234.50</c>, <c>0042</c>), is a text cell, as are <c>NaN</c> and the
	/// infinities, and every other type. (<see cref="AddSheet{T}(string, ExcelSheetOptions)"/> reads the dates and numbers of its members'
	/// formats and cultures back.) Integers and decimals of more than 15 significant digits,
	/// <see cref="DateTimeOffset"/>, and dates before 1900 and negative durations follow <see cref="Options"/>' choices.
	/// The first data record's dates and times widen their columns, so that Excel shows them.
	/// </para>
	/// <para>
	/// A text has at most 32,767 UTF-16 code units, the most Excel opens: a longer field throws. CR is written as Excel
	/// writes it (<c>_x000D_</c>), as are the other control characters but tab and LF, and invalid UTF-8 (which only
	/// <see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/> can pass) becomes U+FFFD. Text that starts like a number
	/// (<c>00123</c>) gets no warning triangle in Excel. An empty field, or a null one, is no cell.
	/// </para>
	/// <para>
	/// An <see cref="ExcelFormula"/> field, and one written with <see cref="ExcelWriterExtensions.WriteFormula"/>, is a
	/// formula cell without a cached value, which Excel computes when it opens the workbook. A record past row 1,048,576,
	/// Excel's last, follows <see cref="ExcelWriterOptions.RowLimit"/>: it throws, or the sheet goes on in a new one after
	/// it, which <see cref="SheetNames"/> then has. A field past column <c>XFD</c>, Excel's last, throws.
	/// </para>
	/// <para>
	/// How the sheet looks is <paramref name="options"/>' (<see cref="ExcelSheetOptions"/>), or
	/// <see cref="ExcelWriterOptions.Sheets"/>' without them: a title, a bold and frozen header, a filter or a table,
	/// columns as wide as their text, and each column's width, format, wrap and visibility. They are checked when the sheet
	/// is added.
	/// </para>
	/// </remarks>
	/// <param name="name">The sheet's name.</param>
	/// <param name="options">How the sheet looks, or <see langword="null"/> for <see cref="ExcelWriterOptions.Sheets"/>.</param>
	/// <returns>The sheet's writer, with <see cref="Options"/>' <see cref="ExcelWriterOptions.Csv"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The name is not one Excel opens, the workbook has a sheet Excel takes to be the same, or <paramref name="options"/>
	/// are not valid or name a table the workbook has.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Another sheet's writer is open (a workbook is written one sheet at a time), or the sheet has no header
	/// (<see cref="CsvWriterOptions.HasHeader"/> is off) and <see cref="ExcelSheetOptions.Columns"/> names columns, which
	/// only a header's names find.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	public CsvWriter AddSheet(string name, ExcelSheetOptions? options = null)
	{
		var key = CheckNewSheet(name);
		var settings = SheetSettings.Resolve(options, null, null, Options);
		var columns = Options.Csv.HasHeader ? null : settings.ColumnsFor(null);
		var table = TableName(name, settings);

		return Open(NewSheet(name, key, settings, null, null, columns, table));
	}

	/// <summary>
	/// Adds a sheet of <typeparamref name="T"/>'s records, as <see cref="AddSheet(string, ExcelSheetOptions)"/> does, with the
	/// formats and cultures of <typeparamref name="T"/>'s members, so that a value written with them is still a typed cell,
	/// and with <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/> and its members' <see cref="ExcelColumnAttribute"/>s.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A member's <see cref="CsvFormatAttribute"/> (the format the record writer writes) and <see cref="CsvCultureAttribute"/>
	/// are read by reflection, once per type, for the column of the member's header name: the header record's, or, without
	/// <see cref="CsvWriterOptions.HasHeader"/>, the names <typeparamref name="T"/>'s record writer writes in its header. So
	/// they apply to records written with <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> and the other record
	/// methods, and to fields written by hand under those names.
	/// </para>
	/// <para>
	/// A date or time in a format Excel can show (<c>dd.MM.yyyy</c>, <c>MMMM yyyy</c>, <c>HH:mm</c>, the culture's
	/// <c>d</c> and <c>g</c>) is read back with the format and the member's culture, and is a date cell in that format when
	/// it formats to the same text again: the cell holds what the text shows, so <c>yyyy-MM</c> holds the month's first day.
	/// Names of days and months show in the culture's language. Other formats (offsets, eras, a day and month without a
	/// year) make text cells, as do custom <see cref="TimeSpan"/> formats, and a <see cref="DateTimeOffset"/>'s format but
	/// with <see cref="ExcelDateTimeOffsets.ClockTime"/>. A number with a culture of its own is read with that culture.
	/// </para>
	/// <para>
	/// How the sheet looks is <paramref name="options"/>' (<see cref="ExcelSheetOptions"/>); without them,
	/// <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/>'s, from it or the nearest base type with one; else
	/// <see cref="ExcelWriterOptions.Sheets"/>'. Each is taken whole. A member's <see cref="ExcelColumnAttribute"/> applies to
	/// its column, as its format does, unless the sheet's <see cref="ExcelSheetOptions.Columns"/> has the column's name.
	/// </para>
	/// </remarks>
	/// <typeparam name="T">
	/// A <see cref="CsvRecordAttribute"/> type; for any other type, the sheet is <see cref="AddSheet(string, ExcelSheetOptions)"/>'s,
	/// with the type's <see cref="ExcelSheetAttribute"/>.
	/// </typeparam>
	/// <param name="name">The sheet's name (see <see cref="AddSheet(string, ExcelSheetOptions)"/>).</param>
	/// <param name="options">
	/// How the sheet looks, or <see langword="null"/> for <typeparamref name="T"/>'s <see cref="ExcelSheetAttribute"/>, else
	/// <see cref="ExcelWriterOptions.Sheets"/>.
	/// </param>
	/// <returns>The sheet's writer, with <see cref="Options"/>' <see cref="ExcelWriterOptions.Csv"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The name is not one Excel opens, the workbook has a sheet Excel takes to be the same, or <paramref name="options"/>
	/// are not valid or name a table the workbook has.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Another sheet's writer is open (a workbook is written one sheet at a time); <typeparamref name="T"/>'s
	/// <see cref="ExcelSheetAttribute"/> or <see cref="ExcelColumnAttribute"/>s are not valid; or the sheet has no header
	/// (<see cref="CsvWriterOptions.HasHeader"/> is off) and <see cref="ExcelSheetOptions.Columns"/> names a column that
	/// <typeparamref name="T"/>'s record writer doesn't write in its header.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	public CsvWriter AddSheet<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string name, ExcelSheetOptions? options = null)
	{
		var key = CheckNewSheet(name);
		var members = MemberFormats.For<T>();
		var settings = SheetSettings.Resolve(options, typeof(T), members, Options);
		// The sink reads cells with the members' formats and cultures; their Excel options come through the settings.
		var formats = members is { HasFormats: true } ? members : null;
		// Without a header, the names T's record writer writes say which column each member's and each option's is.
		var headerNames = !Options.Csv.HasHeader && (formats is not null || settings.HasColumns) ? HeaderCapture.Of<T>() : null;
		var columns = Options.Csv.HasHeader ? null : settings.ColumnsFor(headerNames);
		var table = TableName(name, settings);

		return Open(NewSheet(name, key, settings, formats, headerNames, columns, table));
	}

	/// <summary>
	/// Adds a sheet as <see cref="AddSheet{T}(string, ExcelSheetOptions)"/> does, with members' formats given rather than read
	/// from a type, for tests of formats no type has: random ones. The header record's names say which column each is for.
	/// </summary>
	/// <param name="name">The sheet's name.</param>
	/// <param name="members">The members' formats.</param>
	/// <returns>The sheet's writer.</returns>
	internal CsvWriter AddSheet(string name, MemberFormats members)
	{
		var key = CheckNewSheet(name);
		var settings = SheetSettings.Resolve(null, null, members, Options);
		var columns = Options.Csv.HasHeader ? null : settings.ColumnsFor(null);
		var table = TableName(name, settings);

		return Open(NewSheet(name, key, settings, members.HasFormats ? members : null, null, columns, table));
	}

	/// <summary>
	/// Completes an open sheet, writes the workbook's other parts and closes the package, and the file or stream unless it
	/// was left open. Calling it again does nothing.
	/// </summary>
	public void Dispose() => SyncOrAsync.Wait(DisposeCoreAsync(false));

	/// <summary>
	/// Asynchronously completes an open sheet, writes the workbook's other parts and closes the package, and the file or
	/// stream unless it was left open. Calling it again does nothing.
	/// </summary>
	/// <returns>A task that completes when the package is closed.</returns>
	public ValueTask DisposeAsync() => DisposeCoreAsync(true);

	/// <summary>
	/// Adds the sheet that a full one goes on in (<see cref="ExcelRowLimit.NewSheet"/>), after it, named as Excel names a
	/// copy of it: <c>Orders (2)</c> for <c>Orders</c>, <c>Orders (3)</c> for <c>Orders (2)</c>, with the next number while
	/// the workbook has a sheet Excel takes that name to be.
	/// </summary>
	/// <param name="name">The full sheet's name.</param>
	/// <returns>The new sheet's name and part.</returns>
	internal (string Name, string PartName) ContinueSheet(string name)
	{
		var stem = Excel.SheetNames.CopyStem(name, out var number);
		while (true)
		{
			var copy = Excel.SheetNames.CopyName(stem, number++);
			var key = Excel.SheetNames.Key(copy);
			if (namesByKey.ContainsKey(key))
			{
				continue;
			}

			namesByKey.Add(key, copy);
			sheetNames.Add(copy);

			return (copy, PackageParts.SheetPart(sheetNames.Count));
		}
	}

	/// <summary>
	/// The sink of a sheet's writer, which <see cref="AddSheet(string, ExcelSheetOptions)"/> and the other methods returned, for
	/// <see cref="ExcelWriterExtensions.MergeCells"/>.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <returns>The sink, or <see langword="null"/> for a writer of CSV.</returns>
	internal static SheetSink? SinkOf(CsvWriter writer) => Sinks.TryGetValue(writer, out var sink) ? sink : null;

	/// <summary>
	/// Called by the sheet being written when it ends with a filter: the workbook gets Excel's hidden
	/// <c>_xlnm._FilterDatabase</c> name for it, as Excel writes one.
	/// </summary>
	/// <param name="range">The filter's range: the header and the rows below it.</param>
	internal void AddFilter(ExcelRange range) => filters.Add((sheetNames.Count - 1, range));

	/// <summary>Called by the sheet being written when it ends with a table, for the table's id and part.</summary>
	/// <returns>The table's id, from 1, unique in the workbook.</returns>
	internal int AddTable() => ++tables;

	/// <summary>
	/// The name of the table of a new sheet of <see cref="ExcelRowLimit.NewSheet"/>: the first sheet's table's, with the
	/// first number from 2 that the workbook doesn't have (<c>Orders2</c>), which the workbook then has.
	/// </summary>
	/// <param name="first">The first sheet's table's name.</param>
	/// <returns>The name.</returns>
	internal string NextTableName(string first)
	{
		var name = tableNames.Next(first);
		tableNames.Add(name);

		return name;
	}

	/// <summary>Called by a sheet's sink when it completes: another sheet can then be added.</summary>
	/// <param name="sink">The sink.</param>
	internal void SheetCompleted(SheetSink sink)
	{
		if (ReferenceEquals(openSheet, sink))
		{
			openSheet = null;
			openWriter = null;
		}
	}

	/// <summary>Checks the version of CsvHelper and the options, before a writer is created.</summary>
	/// <param name="options">The options, or <see langword="null"/> for the default.</param>
	/// <returns>The options to use.</returns>
	private static ExcelWriterOptions Prepare(ExcelWriterOptions? options)
	{
		VersionGuard.EnsureMatch();
		var settings = options ?? ExcelWriterOptions.Default;
		settings.Validate(nameof(options));

		return settings;
	}

	/// <summary>
	/// Writes a sheet's records, then completes the sheet, whether or not they were written: also when the write throws
	/// before it returns a task (a type without a record writer).
	/// </summary>
	/// <param name="writer">The sheet's writer.</param>
	/// <param name="write">Writes the records.</param>
	/// <returns>A task that completes when the sheet is complete.</returns>
	private static async ValueTask WriteAndCompleteAsync(CsvWriter writer, Func<CsvWriter, ValueTask> write)
	{
		try
		{
			await write(writer).ConfigureAwait(false);
		}
		finally
		{
			await writer.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Checks that a sheet can be added: the writer is open, no other sheet is, and the name is one Excel opens and the workbook doesn't have.</summary>
	/// <param name="name">The sheet's name.</param>
	/// <returns>The name's key (<see cref="Excel.SheetNames.Key"/>).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The name is not one Excel opens, or the workbook has a sheet Excel takes to be the same.</exception>
	/// <exception cref="InvalidOperationException">Another sheet's writer is open.</exception>
	/// <exception cref="ObjectDisposedException">The workbook writer has been disposed.</exception>
	private string CheckNewSheet(string name)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		ArgumentNullException.ThrowIfNull(name);
		if (openSheet is not null)
		{
			throw new InvalidOperationException(
				$"The sheet \"{openSheet.Location.Name}\" is still being written: dispose its CsvWriter before adding a sheet. A workbook is written one sheet at a time.");
		}

		return CheckName(name, nameof(name));
	}

	/// <summary>Checks a sheet's name: one Excel opens, and not one Excel takes a sheet of the workbook's to be.</summary>
	/// <param name="name">The name.</param>
	/// <param name="paramName">The name of the parameter it came from.</param>
	/// <returns>The name's key.</returns>
	/// <exception cref="ArgumentException">The name is not one Excel opens, or the workbook has a sheet Excel takes to be the same.</exception>
	private string CheckName(string name, string paramName)
	{
		Excel.SheetNames.Check(name, paramName);
		var key = Excel.SheetNames.Key(name);
		if (namesByKey.TryGetValue(key, out var existing))
		{
			throw new ArgumentException($"The workbook already has the sheet \"{existing}\", which Excel takes \"{name}\" to be.", paramName);
		}

		return key;
	}

	/// <summary>
	/// The name of a new sheet's table: the one its options give, which the workbook mustn't have; else the sheet's name
	/// made into one (<see cref="TableNames.FromSheetName"/>), with a number after it while the workbook has it (decision 8).
	/// </summary>
	/// <param name="sheetName">The sheet's name.</param>
	/// <param name="settings">How the sheet looks.</param>
	/// <returns>The name, or <see langword="null"/> when the sheet has no table.</returns>
	/// <exception cref="ArgumentException">The options given name a table the workbook has, ignoring case.</exception>
	/// <exception cref="InvalidOperationException">The record type's <see cref="ExcelSheetAttribute"/>, or <see cref="ExcelWriterOptions.Sheets"/>, names a table the workbook has.</exception>
	private string? TableName(string sheetName, SheetSettings settings)
	{
		if (settings.Table is not { } table || !settings.HasHeader)
		{
			return null;
		}

		if (table.Name is not { } name)
		{
			return tableNames.Unique(TableNames.FromSheetName(sheetName));
		}

		if (tableNames.Existing(name) is { } existing)
		{
			var message = $"{settings.Origin}: the workbook already has the table \"{existing}\", which Excel takes \"{name}\" to be: a workbook's tables have names unique ignoring case. Leave the table's name out for one made from the sheet's.";

			throw settings.Given ? new ArgumentException(message, "options") : new InvalidOperationException(message);
		}

		return name;
	}

	/// <summary>Adds a sheet whose name is checked, with its sink.</summary>
	/// <param name="name">The name.</param>
	/// <param name="key">The name's key.</param>
	/// <param name="settings">How the sheet looks.</param>
	/// <param name="members">The formats of the record type's members, or <see langword="null"/> when none has one.</param>
	/// <param name="headerNames">Without a header, the names the record writer writes in its header, or <see langword="null"/>.</param>
	/// <param name="columns">Without a header, each column's options, or <see langword="null"/> for none.</param>
	/// <param name="table">The name of the sheet's table (<see cref="TableName"/>), which the workbook then has; or <see langword="null"/>.</param>
	/// <returns>The sheet's sink.</returns>
	private SheetSink NewSheet(string name, string key, SheetSettings settings, MemberFormats? members, IReadOnlyList<string>? headerNames, ExcelColumnOptions?[]? columns, string? table)
	{
		var sink = new SheetSink(this, package, name, PackageParts.SheetPart(sheetNames.Count + 1), strings, styles, Options, MaxRows, settings, members, headerNames, columns, table);
		namesByKey.Add(key, name);
		sheetNames.Add(name);
		if (table is not null)
		{
			tableNames.Add(table);
		}

		return sink;
	}

	/// <summary>Makes a sheet the open one, with its writer.</summary>
	/// <param name="sink">The sheet's sink.</param>
	/// <returns>The sheet's writer.</returns>
	private CsvWriter Open(SheetSink sink)
	{
		var writer = new CsvWriter(sink, Options.Csv);
		Sinks.Add(writer, sink);
		openSheet = sink;
		openWriter = writer;

		return writer;
	}

	/// <summary>Disposes the writer, synchronously or not.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the package is closed.</returns>
	private async ValueTask DisposeCoreAsync(bool async)
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		try
		{
			if (openWriter is { } writer)
			{
				if (async)
				{
					await writer.DisposeAsync().ConfigureAwait(false);
				}
				else
				{
					writer.Dispose();
				}
			}

			if (sheetNames.Count == 0)
			{
				await NewSheet("Sheet1", CheckName("Sheet1", "name"), SheetSettings.Empty, null, null, null, null).CompleteAsync(async).ConfigureAwait(false);
			}

			await PackageParts.WriteAsync(package, sheetNames, strings, styles, filters, tables, async, CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			// The package is closed, and the stream unless left open, but the first failure is the one that says why: closing
			// a package that failed to write fails too.
			try
			{
				await package.DisposeAsync(async).ConfigureAwait(false);
			}
			catch
			{
			}

			throw;
		}

		await package.DisposeAsync(async).ConfigureAwait(false);
	}
}
