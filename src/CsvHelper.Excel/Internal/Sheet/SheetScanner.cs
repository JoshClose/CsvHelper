using System;
using System.Buffers.Text;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Reads the rows of a worksheet part into records: each row with a value becomes a record whose field <c>i</c> is column
/// <c>i</c> (<c>A</c> is 0), written into an <see cref="IRecordTarget"/> with the kind of each cell. It reads forward
/// only, over the part's <see cref="XmlScanner"/>, and stops at <c>&lt;/sheetData&gt;</c>, inflating nothing after it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cells.</b> In <c>sheetData</c> it reads the <c>row</c> elements, in a row the <c>c</c> elements, and in a cell its
/// <c>v</c>, or for an inline string its <c>is</c>; everything else is skipped. A cell's type (<c>t</c>) decides its
/// value and kind: a number (<c>n</c>, or no type) is its <c>v</c> without the white space around it, with the kind its
/// style gives (<see cref="StyleTable.NumberKind"/>); <c>s</c> is the shared string its <c>v</c> indexes; <c>b</c> is
/// <c>1</c> or <c>0</c> (<c>true</c> and <c>false</c> of any case become those; anything else stays text); <c>e</c> is
/// an error, or valueless when error cells read as null; <c>d</c> is an ISO 8601 date or duration converted to its serial
/// number in the workbook's date system (<see cref="IsoDate"/>), with the kind its style gives as a number has, or text
/// when it isn't one; <c>inlineStr</c> is the runs of its <c>is</c> joined, or its <c>v</c>; and <c>str</c> and any other
/// type are the text of <c>v</c>. Values are decoded as XML and then for their <c>_xHHHH_</c> escapes; between the two,
/// a text (a <c>v</c>, or a <c>t</c> of an <c>is</c>) that <c>xml:space="preserve"</c> doesn't apply to, on it or inherited,
/// loses the white space around it, as Excel trims it. A cell without a <c>v</c> or <c>is</c>, or whose value is empty, is
/// valueless and adds no field.
/// </para>
/// <para>
/// <b>Formulas.</b> When formulas read as their values, a cell's <c>f</c> is skipped. When they read as text, a cell whose
/// <c>f</c> has text is a text field of <c>=</c> and the text, decoded as a value is, whatever the cell's type; the rest
/// of the cell, its value included, is skipped. A shared formula (<c>t="shared"</c>) is defined by the cell whose <c>f</c>
/// has a <c>ref</c>, with the formula's text, and each other cell of the group (an <c>f</c> with the same <c>si</c> and no
/// <c>ref</c>, whose own text is ignored, as Excel ignores it) is the defining cell's formula with its relative references
/// moved by the cell's offset from it (<see cref="FormulaShifter"/>). A cell of a group that no cell before it defines
/// can't be read. A cell whose <c>f</c> has no text, the cells of a group whose defining cell has none, and the cells of a
/// data table (<c>t="dataTable"</c>) read their values. The groups are held while the sheet is read
/// (<see cref="SharedFormulas"/>), within the part's size limit.
/// </para>
/// <para>
/// <b>Placement.</b> A row's number is its <c>r</c>, else the row of its first cell's <c>r</c>, else the row before's plus
/// one; rows must ascend, within 1 to 1,048,576. A cell's column is the letters of its <c>r</c>, else the cell before's
/// plus one; columns must ascend within a row, within <c>A</c> to <c>XFD</c>. A valued cell in column <c>k</c> first adds
/// an empty field for each column skipped since the last field, so a row ends at its last valued cell; a row with a value
/// is then padded with empty fields to the field count of the first record with a value. A row without a value is
/// dropped, or, when empty rows are kept, is a record of one empty field, as is each row missing before a row: record
/// <c>n</c> is then row <c>n</c>. Each empty field that a gap or the padding adds, and each missing row, counts as one
/// byte toward the workbook's uncompressed size, and so do the bytes a shared string or a moved shared formula has beyond
/// its cell's XML, so that a few bytes of XML can't make millions of fields, or gigabytes of them, unchecked.
/// </para>
/// <para>
/// <b>Areas.</b> A scanner of an area (<see cref="SheetLayout"/>) reads only its rows and columns: field 0 is its first
/// column, a cell outside its columns is no field and its value is not decoded, a row above it is skipped whole, and the
/// first row below it ends the sheet, so nothing after it is inflated. When formulas read as text, rows above the area and
/// cells outside its columns are read for their shared formulas' definitions only, so that the cells of a group in the area
/// read their formula wherever the group's defining cell is. Missing rows are written from the area's first row,
/// and none after the last row the file has in it. A held first record (<see cref="HoldFirstRecord"/>), such as a table's
/// column names, comes before the rows, and its width is the width the rows are padded to.
/// </para>
/// <para>
/// <b>Hidden rows and columns.</b> When hidden rows are skipped, a row that Excel hides (<see cref="HiddenState"/>) is no
/// record: once the missing rows before it are written, it is skipped whole, or, when formulas read as text, read for its
/// shared formulas' definitions only, as a row above an area is. In a sheet that hides the rows it lacks, missing rows are
/// no records either. When hidden columns are skipped, <c>sheetFormatPr</c> and <c>cols</c>, which come before
/// <c>sheetData</c>, are read, and when the rows begin the layout (of the area, or of the whole sheet) leaves the hidden
/// columns out: their cells are no fields, as cells outside an area are, and the fields are the shown columns in order.
/// The held first record is written then, through that layout.
/// </para>
/// <para>
/// <b>Merged cells.</b> When merged cells are filled, each cell of a merged range (<see cref="MergedCells"/>) that is a
/// field reads as the range's top-left cell, whatever it stores, as Excel shows the range (<see cref="MergedFill"/>). The
/// top-left cell is read into the range's store wherever it is: in a row above the area or hidden (which is then read for
/// it, as for shared formulas' definitions), outside the area's columns, or in a hidden column. The range's other cells are
/// passed over as cells outside an area are, and its fields are written in column order among the row's cells. A range
/// whose top-left cell has no value fills with nothing. A row the file lacks is a record when a range with a value covers
/// it, unless the sheet hides the rows it lacks, and so are the rows after the file's last row in the area while such a
/// range goes on. Each fill but the top-left cell's own counts its bytes, or 1 for an empty one, toward the workbook's
/// uncompressed size, and each such missing row 1, as empty fields and missing rows do.
/// </para>
/// <para>
/// <b>Fills.</b> A fill writes whole records: it ends after the row that fills the target, or at the end of the rows,
/// and reads the window as often as it needs to in between. A cell is written only once its value is all in the window:
/// otherwise the scanner goes back to the cell's start, refills (which keeps the cell, and grows the window for one larger
/// than it), and reads the cell again. The rest of a cell after its value, elements it doesn't read, and text between the
/// elements it reads (white space between rows and cells) are skipped across refills, so they never grow the window. Only
/// the refills read the part's stream, synchronously (<see cref="Fill"/>) or not (<see cref="FillAsync"/>).
/// </para>
/// <para>
/// <b>Errors.</b> The sheet's content that can't be read (its XML, a cell reference, cells or rows out of order, a shared
/// string index) throws <see cref="CsvFormatException"/> with the cell, or the row, in its location and message, and the
/// part and byte offset in its message. A limit, or a part that can't be inflated, throws
/// <see cref="InvalidDataException"/>. After either, every fill throws the same exception again.
/// </para>
/// </remarks>
internal sealed class SheetScanner : IDisposable, IAsyncDisposable
{
	/// <summary>The depth of a row's element: worksheet, sheetData, row.</summary>
	private const int RowDepth = 3;

	/// <summary>The room on the stack for a short value that is decoded to be read, such as a shared string index.</summary>
	private const int ScratchSize = 64;

	/// <summary>The most characters of a value an error message quotes.</summary>
	private const int MaxQuoted = 32;

	/// <summary>The room for the text of a serial number: <see cref="FloatFormatter"/> writes at most 24 bytes.</summary>
	private const int MaxSerialLength = 32;

	private readonly XmlScanner scanner;
	private readonly SheetLocation location;
	private readonly SharedStrings strings;
	private readonly StyleTable styles;
	private readonly bool skipEmptyRows;
	private readonly LimitedReadStream? budget;
	private readonly bool date1904;
	private readonly bool nullErrors;
	// The sheet's shared formulas when formulas read as text; null when they read as their values.
	private readonly SharedFormulas? sharedFormulas;
	// The text and CDATA tokens of the value of the cell being read, in the window, and their length.
	private Segment[] segments = new Segment[4];
	private int segmentCount;
	private int rawLength;
	private Mode mode;
	private Mode resume;
	private int skipDepth;
	// The row being read (when known), and the last row read: 0 before the first.
	private long row;
	private bool rowKnown;
	private long lastRow;
	// Whether xml:space="preserve" applies in the root, in sheetData and in the row being read.
	private bool rootPreserves;
	private bool dataPreserves;
	private bool rowPreserves;
	// The column of the row's last cell (-1 before its first), and the number of fields the row has.
	private int lastColumn;
	private int fields;
	// The column of the cell being read, for errors; -1 between cells.
	private int cellColumn = -1;
	// The field count of the first record with a value, which shorter ones are padded to; -1 until then.
	private int width = -1;
	// The rows still to write as empty records before the current one, with empty rows kept: gapNext up to gapEnd.
	private long gapNext = 1;
	private long gapEnd = 1;
	private Exception? fault;
	// The area read, or null for the whole sheet, where field i is column i; final once the rows begin.
	private SheetLayout? layout;
	// The record to write before the rows, when they begin; null once it is written.
	private HeldRecord? held;
	// The field of the cell being read.
	private int cellField;
	// Whether the row being read is above the area or hidden, and read only for shared formulas' definitions: nothing of it is written.
	private bool unwritten;
	// Room to decode a shared formula's definition in a cell that is no field.
	private byte[]? unwrittenRoom;
	// Whether hidden rows are skipped: a row's hidden state is then read, and a hidden row is no record.
	private readonly bool skipHiddenRows;
	// The sheet's hidden columns, read before the rows, when hidden columns are skipped; null otherwise.
	private readonly HiddenColumns? hiddenColumns;
	// Whether the row being read is hidden, when hidden rows are skipped.
	private bool rowHidden;
	// Whether the rows the file lacks are written as empty records: with empty rows kept, unless the sheet hides them.
	private bool keepsMissingRows;
	// Whether the rows have begun: the layout is final, and the held record written.
	private bool rowsBegun;
	// The sheet's merged ranges when merged cells are filled, until the rows begin and the fill state takes them.
	private MergedCells? mergedCells;
	// The fill state of merged cells, from when the rows begin; null when merged cells aren't filled, or none fills a field.
	private MergedFill? merged;
	// Whether the rows the file lacks are hidden, when hidden rows are skipped: then no fill makes one a record.
	private bool missingRowsHidden;
	// The field from which the record being written has fills still to write.
	private int fillFrom;

	/// <summary>Creates the scanner of a worksheet part.</summary>
	/// <param name="scanner">The part's XML, at its start; the sheet scanner owns it, and disposes it.</param>
	/// <param name="sheetName">The sheet's name, for the locations of errors.</param>
	/// <param name="strings">The workbook's shared strings.</param>
	/// <param name="styles">The workbook's styles.</param>
	/// <param name="skipEmptyRows">Whether a row without a value is dropped, rather than read as a record of one empty field.</param>
	/// <param name="budget">The part's stream, to count gap fields toward the workbook's uncompressed size; <see langword="null"/> to count nothing.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system, for the serial numbers of <c>t="d"</c> cells.</param>
	/// <param name="errorCells">How error cells read: as <see cref="ExcelErrorCells.Null"/>, they are valueless.</param>
	/// <param name="formulas">What cells with a formula read as: as <see cref="ExcelFormulas.FormulaText"/>, their formulas.</param>
	/// <param name="layout">The area to read, or <see langword="null"/> for the whole sheet.</param>
	/// <param name="skipHiddenRows">Whether the rows Excel hides are no records.</param>
	/// <param name="skipHiddenColumns">Whether the columns Excel hides are no fields.</param>
	/// <param name="mergedCells">The sheet's merged ranges, to fill their cells with; <see langword="null"/> to fill none.</param>
	/// <exception cref="ArgumentNullException">
	/// An argument other than <paramref name="budget"/>, <paramref name="layout"/> and <paramref name="mergedCells"/> is <see langword="null"/>.
	/// </exception>
	public SheetScanner(
		XmlScanner scanner, string sheetName, SharedStrings strings, StyleTable styles, bool skipEmptyRows, LimitedReadStream? budget,
		bool date1904 = false, ExcelErrorCells errorCells = ExcelErrorCells.Text, ExcelFormulas formulas = ExcelFormulas.CachedValue, SheetLayout? layout = null,
		bool skipHiddenRows = false, bool skipHiddenColumns = false, MergedCells? mergedCells = null)
	{
		ArgumentNullException.ThrowIfNull(scanner);
		ArgumentNullException.ThrowIfNull(strings);
		ArgumentNullException.ThrowIfNull(styles);
		this.scanner = scanner;
		location = new SheetLocation(sheetName);
		this.strings = strings;
		this.styles = styles;
		this.skipEmptyRows = skipEmptyRows;
		this.budget = budget;
		this.date1904 = date1904;
		nullErrors = errorCells == ExcelErrorCells.Null;
		if (formulas == ExcelFormulas.FormulaText)
		{
			sharedFormulas = new SharedFormulas(scanner.Window.PartName, scanner.Window.MaxSize);
		}

		this.skipHiddenRows = skipHiddenRows;
		keepsMissingRows = !skipEmptyRows;
		if (skipHiddenColumns)
		{
			hiddenColumns = new HiddenColumns();
		}

		// Hidden cells are skipped, and merged cells filled, through a layout: the whole sheet's, when no area is given.
		this.mergedCells = mergedCells;
		this.layout = layout ?? (skipHiddenRows || skipHiddenColumns || mergedCells is not null ? SheetLayout.Sheet : null);
		if (this.layout is not null)
		{
			gapNext = Math.Max(this.layout.FirstRow, 1);
		}
	}

	/// <summary>Where things are in the sheet, for the record source's <c>Locate</c>.</summary>
	public SheetLocation Location => location;

	/// <summary>The area read, or <see langword="null"/> for the whole sheet, where field <c>i</c> is column <c>i</c>.</summary>
	public SheetLayout? Layout => layout;

	/// <summary>Holds a record to write before the rows, when they begin, such as a table's column names.</summary>
	/// <param name="record">The record.</param>
	public void HoldFirstRecord(HeldRecord record) => held = record;

	/// <summary>The part's name.</summary>
	public string PartName => scanner.Window.PartName;

	/// <summary>Writes the next records into <paramref name="target"/>, reading the part synchronously.</summary>
	/// <typeparam name="TTarget">The target's type: a struct, so that the scanner is specialized for it.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns><see langword="false"/> when the sheet has no more records (the last ones can be in this fill); otherwise <see langword="true"/>.</returns>
	/// <exception cref="CsvFormatException">The sheet's content can't be read.</exception>
	/// <exception cref="InvalidDataException">The part passes a limit, or can't be inflated.</exception>
	public bool Fill<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		ThrowIfFaulted();
		try
		{
			while (true)
			{
				switch (Scan(target))
				{
					case Step.More:
						scanner.Refill();

						break;
					case Step.Full:
						return true;
					default:
						return false;
				}
			}
		}
		catch (Exception e) when (e is CsvFormatException or InvalidDataException)
		{
			fault = e;

			throw;
		}
	}

	/// <summary>Writes the next records into <paramref name="target"/>, reading the part asynchronously. See <see cref="Fill"/>.</summary>
	/// <typeparam name="TTarget">The target's type: a struct, so that the scanner is specialized for it.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="cancellationToken">Cancels reading the part; the scanner can be filled again after.</param>
	/// <returns>A task whose result is <see langword="false"/> when the sheet has no more records; otherwise <see langword="true"/>.</returns>
	/// <exception cref="CsvFormatException">The sheet's content can't be read.</exception>
	/// <exception cref="InvalidDataException">The part passes a limit, or can't be inflated.</exception>
	public async ValueTask<bool> FillAsync<TTarget>(TTarget target, CancellationToken cancellationToken)
		where TTarget : struct, IRecordTarget
	{
		ThrowIfFaulted();
		try
		{
			while (true)
			{
				switch (Scan(target))
				{
					case Step.More:
						await scanner.RefillAsync(cancellationToken).ConfigureAwait(false);

						break;
					case Step.Full:
						return true;
					default:
						return false;
				}
			}
		}
		catch (Exception e) when (e is CsvFormatException or InvalidDataException)
		{
			fault = e;

			throw;
		}
	}

	/// <summary>Disposes the part's XML, and so its stream.</summary>
	public void Dispose() => scanner.Dispose();

	/// <summary>Asynchronously disposes the part's XML, and so its stream.</summary>
	/// <returns>A task that completes when the stream is disposed.</returns>
	public ValueTask DisposeAsync() => scanner.DisposeAsync();

	/// <summary>The kind of a cell's type (<c>t</c>), for how its value is read.</summary>
	/// <param name="type">The type, without white space around it.</param>
	/// <returns>The kind; <see cref="CellType.Text"/> for <c>str</c> and types Excel doesn't have.</returns>
	private static CellType TypeOf(ReadOnlySpan<byte> type)
	{
		if (type.Length == 1)
		{
			switch (type[0])
			{
				case (byte)'n':
					return CellType.Number;
				case (byte)'s':
					return CellType.SharedString;
				case (byte)'b':
					return CellType.Boolean;
				case (byte)'e':
					return CellType.Error;
				case (byte)'d':
					return CellType.IsoDate;
			}
		}

		return type.SequenceEqual("inlineStr"u8) ? CellType.InlineString : CellType.Text;
	}

	/// <summary>An attribute's value without the white space around it, decoded when it has a reference.</summary>
	/// <param name="attributes">The attributes, at the attribute.</param>
	/// <returns>The value.</returns>
	/// <exception cref="InvalidDataException">The value has a reference that can't be decoded.</exception>
	private static ReadOnlySpan<byte> ValueOf(XmlAttributes attributes)
	{
		var value = attributes.Value;

		// A reference in a reference or a type is odd enough to allocate for.
		return Trim(value.IndexOf((byte)'&') < 0 ? value : Encoding.UTF8.GetBytes(attributes.DecodeValue()));
	}

	/// <summary>A value without the XML white space around it.</summary>
	/// <param name="value">The value.</param>
	/// <returns>The trimmed value.</returns>
	private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
	{
		var start = 0;
		var end = value.Length;
		while (start < end && XmlScanner.IsWhitespace(value[start]))
		{
			start++;
		}

		while (end > start && XmlScanner.IsWhitespace(value[end - 1]))
		{
			end--;
		}

		return value.Slice(start, end - start);
	}

	/// <summary>A value for an error message, quoted, and cut short when it is long.</summary>
	/// <param name="value">The value, as UTF-8.</param>
	/// <returns>The quoted text.</returns>
	private static string Quoted(ReadOnlySpan<byte> value)
	{
		var text = Encoding.UTF8.GetString(value);

		return "\"" + (text.Length > MaxQuoted ? text.Substring(0, MaxQuoted) + "..." : text) + "\"";
	}

	/// <summary>Throws the exception that a fill threw before, if one did: the scanner's state is then not to be trusted.</summary>
	private void ThrowIfFaulted()
	{
		if (fault is not null)
		{
			ExceptionDispatchInfo.Capture(fault).Throw();
		}
	}

	/// <summary>Scans what the window holds, turning the XML's errors into the sheet's.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>Why the scan stopped.</returns>
	/// <exception cref="CsvFormatException">The sheet's content can't be read.</exception>
	private Step Scan<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		try
		{
			return TryScan(target);
		}
		catch (InvalidDataException e) when (XmlError.TryGet(e, out var offset, out var detail))
		{
			// The XML's errors; a limit's (from counting gap fields) is not one, and the window doesn't refill in a scan.
			var inRow = (mode == Mode.Skip ? resume : mode) is Mode.Row or Mode.CellRest;

			throw Error(offset, detail, inRow ? cellColumn : -1, inRow ? CurrentRow : 0, e);
		}
	}

	/// <summary>Reads tokens from the window until it needs a refill, the target is full, or the rows end.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>Why the scan stopped.</returns>
	private Step TryScan<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		while (true)
		{
			switch (mode)
			{
				case Mode.Skip:
					if (!scanner.TrySkipTo(skipDepth))
					{
						return Step.More;
					}

					mode = resume;

					continue;
				case Mode.CellRest:
					if (!scanner.TrySkipTo(RowDepth))
					{
						return Step.More;
					}

					cellColumn = -1;
					mode = Mode.Row;

					continue;
				case Mode.Tail:
					return Tail(target);
				case Mode.Done:
					return Step.Done;
			}

			// Text between the elements read, mostly white space between rows and cells, is ignored, so it is skipped as it
			// comes rather than held in the window: a long run of it doesn't grow the window. A cell's value is read whole,
			// in ReadCell.
			if (!scanner.TrySkipText())
			{
				return Step.More;
			}

			var before = scanner.Save();
			if (!scanner.TryRead())
			{
				return Step.More;
			}

			var step = scanner.Kind switch
			{
				XmlTokenKind.StartTag => StartTag(target, before),
				XmlTokenKind.EndTag => EndTag(target),
				XmlTokenKind.End => End(target),
				_ => Step.Read,
			};
			if (step != Step.Read)
			{
				return step;
			}
		}
	}

	/// <summary>
	/// Begins the rows, at <c>sheetData</c>, or at the end of a sheet without rows: the layout leaves out the hidden columns
	/// read before them, and the held first record is written through it.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>
	/// <see cref="Step.Full"/> when the held record fills the target; <see cref="Step.Done"/> when the sheet has no rows;
	/// otherwise <see cref="Step.Read"/>.
	/// </returns>
	private Step BeginRows<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		rowsBegun = true;
		if (hiddenColumns is not null)
		{
			layout = layout!.Without(hiddenColumns);
		}

		if (mergedCells is not null)
		{
			merged = MergedFill.Create(mergedCells, layout!);
			mergedCells = null;
		}

		if (held is not null)
		{
			WriteHeld(target);
			if (target.IsFull)
			{
				return Step.Full;
			}
		}

		return mode == Mode.Done ? Step.Done : Step.Read;
	}

	/// <summary>Writes the held first record, whose fields set the width the rows after it are padded to.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the record.</param>
	private void WriteHeld<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		var record = held!;
		held = null;
		var written = record.WriteTo(target, layout);
		if (width < 0 && written > 0)
		{
			width = written;
		}
	}

	/// <summary>
	/// Reads <c>sheetFormatPr</c>, when hidden rows or columns are skipped: whether the rows the file lacks are hidden
	/// (<c>zeroHeight</c>, or a custom default height of 0), and whether the columns that no <c>col</c> covers are (a
	/// default width of 0), as Excel reads them (<see cref="HiddenState"/>).
	/// </summary>
	private void ReadFormat()
	{
		var zeroHeight = false;
		var customHeight = false;
		var zeroDefaultHeight = false;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("zeroHeight"u8))
			{
				zeroHeight = HiddenState.IsTrue(attributes);
			}
			else if (attributes.IsName("customHeight"u8))
			{
				customHeight = HiddenState.IsTrue(attributes);
			}
			else if (attributes.IsName("defaultRowHeight"u8))
			{
				zeroDefaultHeight = HiddenState.ZeroOf(attributes) == true;
			}
			else if (hiddenColumns is not null && attributes.IsName("defaultColWidth"u8))
			{
				hiddenColumns.HidesUncovered = HiddenState.ZeroOf(attributes) == true;
			}
		}

		if (skipHiddenRows && (zeroHeight || (customHeight && zeroDefaultHeight)))
		{
			// The rows the file lacks are hidden, so none of them is a record, filled or not.
			keepsMissingRows = false;
			missingRowsHidden = true;
		}
	}

	/// <summary>
	/// Reads a <c>col</c> of <c>cols</c>, when hidden columns are skipped: the columns from its <c>min</c> to its <c>max</c>,
	/// which it hides when its <c>hidden</c> is true, or its <c>width</c> is 0, missing or no number, as Excel reads them
	/// (<see cref="HiddenState"/>). A <c>max</c> past the last column is the last, as Excel takes it.
	/// </summary>
	/// <exception cref="CsvFormatException">The <c>col</c> has no <c>min</c> or <c>max</c>, or its <c>max</c> is before its <c>min</c>: Excel opens no such file.</exception>
	/// <exception cref="InvalidDataException">Its <c>min</c> or <c>max</c> is no column's number; <see cref="Scan"/> makes it the sheet's error.</exception>
	private void ReadColumn()
	{
		int? min = null;
		int? max = null;
		var hides = false;
		var shown = false;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("min"u8))
			{
				min = attributes.GetInt32();
				if (min < 1 || min > CellReference.MaxColumns)
				{
					throw attributes.InvalidValue($"a column's number (1 to {CellReference.MaxColumns})");
				}
			}
			else if (attributes.IsName("max"u8))
			{
				max = attributes.GetInt32();
			}
			else if (attributes.IsName("hidden"u8))
			{
				hides = HiddenState.IsTrue(attributes);
			}
			else if (attributes.IsName("width"u8))
			{
				shown = HiddenState.ZeroOf(attributes) == false;
			}
		}

		if (min is null || max is null)
		{
			throw Error(TokenOffset, $"a col has no {(min is null ? "min" : "max")}", -1, 0);
		}

		if (max < min)
		{
			throw Error(TokenOffset, $"a col's max, {max}, is before its min, {min}", -1, 0);
		}

		hiddenColumns!.Set(min.Value - 1, Math.Min(max.Value, CellReference.MaxColumns) - 1, hides || !shown);
	}

	/// <summary>
	/// Whether a row that is no record (above the area, or hidden) is read on: when formulas read as text, for the shared
	/// formulas its cells may define, which cells written later use; and when merged cells are filled and a range that
	/// fills fields starts in it, for the range's top-left cell. Nothing of it is written.
	/// </summary>
	/// <returns>Whether the row is read for its shared formulas' definitions or its merged ranges' top-left cells.</returns>
	private bool ReadsUnwritten()
	{
		unwritten = sharedFormulas is not null || (merged is not null && merged.StartsAt(row));

		return unwritten;
	}

	/// <summary>
	/// Leaves a row that isn't in the area: one above it is skipped, its cells unread; one below it ends the sheet, so that
	/// nothing after it is inflated, once the rows that merged ranges fill before it are written. Nothing of the row has
	/// been written.
	/// </summary>
	/// <param name="place">Where the row is.</param>
	/// <returns><see cref="Step.Done"/> below the area, or <see cref="Step.Read"/> to write filled rows first; otherwise <see cref="Step.Read"/>.</returns>
	private Step LeaveRow(RowPlace place)
	{
		if (place == RowPlace.Below)
		{
			cellColumn = -1;
			if (merged is not null)
			{
				mode = Mode.Tail;

				return Step.Read;
			}

			mode = Mode.Done;

			return Step.Done;
		}

		return SkipRow();
	}

	/// <summary>Skips the rest of a row that is no record (above the area, or hidden), its cells unread. Nothing of it has been written.</summary>
	/// <returns><see cref="Step.Read"/>.</returns>
	private Step SkipRow()
	{
		cellColumn = -1;
		lastRow = row;

		// Past the row's end tag, from its start tag, a cell in it, or after an empty row (where nothing is left to skip).
		resume = Mode.SheetData;
		skipDepth = RowDepth - 1;
		mode = Mode.Skip;

		return Step.Read;
	}

	/// <summary>Handles a start tag: the root, <c>sheetData</c>, a row or a cell, or an element to skip.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="before">The scanner's state before the tag, to read it again.</param>
	/// <returns>What to do next.</returns>
	private Step StartTag<TTarget>(TTarget target, XmlScannerState before)
		where TTarget : struct, IRecordTarget
	{
		switch (mode)
		{
			case Mode.Root:
				if (!scanner.IsStartTag("worksheet"u8))
				{
					throw Error(TokenOffset, "its root element is not worksheet", -1, 0);
				}

				rootPreserves = scanner.SpaceOf() ?? false;
				mode = Mode.Worksheet;

				return Step.Read;
			case Mode.Worksheet:
				if (scanner.IsStartTag("sheetData"u8))
				{
					// An empty sheetData has no rows, and nothing after it is read.
					dataPreserves = scanner.SpaceOf() ?? rootPreserves;
					mode = scanner.IsEmptyElement ? Mode.Done : Mode.SheetData;

					return BeginRows(target);
				}

				if (hiddenColumns is not null && scanner.IsStartTag("cols"u8))
				{
					// Its col elements are read one tag at a time, across refills.
					if (!scanner.IsEmptyElement)
					{
						mode = Mode.Cols;
					}

					return Step.Read;
				}

				if ((skipHiddenRows || hiddenColumns is not null) && scanner.IsStartTag("sheetFormatPr"u8))
				{
					ReadFormat();
				}

				break;
			case Mode.Cols:
				if (scanner.IsStartTag("col"u8))
				{
					ReadColumn();
				}

				break;
			case Mode.SheetData:
				if (scanner.IsStartTag("row"u8))
				{
					return StartRow(target, before);
				}

				break;
			case Mode.Row:
				if (scanner.IsStartTag("c"u8))
				{
					return ReadCell(target, before);
				}

				break;
		}

		// sheetPr, cols and the rest before sheetData; extLst in a row; the content of a col; anything else.
		if (!scanner.IsEmptyElement)
		{
			resume = mode;
			skipDepth = scanner.Depth - 1;
			mode = Mode.Skip;
		}

		return Step.Read;
	}

	/// <summary>Handles an end tag: of a row, of <c>cols</c>, of <c>sheetData</c>, or of a root without one.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>What to do next.</returns>
	private Step EndTag<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		if (mode == Mode.Row)
		{
			return EndRow(target);
		}

		if (mode == Mode.Cols)
		{
			mode = Mode.Worksheet;

			return Step.Read;
		}

		// </sheetData>, after which nothing is read, or the root's end tag when it has no sheetData.
		return End(target);
	}

	/// <summary>
	/// Ends the rows; a sheet without any begins them first, so that the held first record is written. When merged cells
	/// are filled, the rows that ranges fill after the file's last row are written first (<see cref="Tail"/>).
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns><see cref="Step.Done"/>, or <see cref="Step.Read"/> to write filled rows first.</returns>
	private Step End<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		mode = Mode.Done;
		if (!rowsBegun)
		{
			BeginRows(target);
		}

		if (merged is not null)
		{
			mode = Mode.Tail;

			return Step.Read;
		}

		return Step.Done;
	}

	/// <summary>
	/// Writes the rows after the last one read, up to the area's last, that merged ranges with a value cover, once the rows
	/// have ended or reached the area's end: as missing rows, filled. The rows between them are written as missing rows are.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns><see cref="Step.Full"/> when the target fills first; otherwise <see cref="Step.Done"/>.</returns>
	private Step Tail<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		// The fill state is at the last row read, and the ranges closed during the rows written stay closed, so the last
		// filled row is the same when a fill goes on with them.
		var last = Math.Min(layout!.LastRow, merged!.LastFilledRow());
		if (last > lastRow)
		{
			// Not up to a row below the area that ended it, whose number set the gap's end.
			gapNext = Math.Max(gapNext, lastRow + 1);
			gapEnd = last + 1;
			if (!FlushGaps(target))
			{
				return Step.Full;
			}
		}

		mode = Mode.Done;

		return Step.Done;
	}

	/// <summary>Starts a row: its number, when it has an <c>r</c>, and the empty records of the rows missing before it.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="before">The scanner's state before the row's tag, to read it again when the target fills first.</param>
	/// <returns>What to do next.</returns>
	private Step StartRow<TTarget>(TTarget target, XmlScannerState before)
		where TTarget : struct, IRecordTarget
	{
		int? number = null;
		bool? space = null;
		var hiding = RowHiding.None;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("r"u8))
			{
				number = attributes.GetInt32();
			}
			else if (attributes.IsQualifiedName("xml:space"u8))
			{
				space = XmlScanner.SpaceValue(attributes.Value);
			}
			else if (skipHiddenRows)
			{
				hiding |= HiddenState.RowFlagOf(attributes);
			}
		}

		rowPreserves = space ?? dataPreserves;

		mode = Mode.Row;
		rowKnown = false;
		unwritten = false;
		rowHidden = HiddenState.IsHiddenRow(hiding);
		lastColumn = -1;
		fields = 0;
		fillFrom = 0;
		if (number is not null)
		{
			SetRow(number.Value);
			if (layout is not null && layout.Place(row) is var place && place != RowPlace.In && (place == RowPlace.Below || !ReadsUnwritten()))
			{
				return LeaveRow(place);
			}

			if (!FlushGaps(target))
			{
				// The row waits for the next fill, after the empty records before it.
				scanner.Restore(before);
				mode = Mode.SheetData;

				return Step.Full;
			}

			// Once the missing rows before it are written: a hidden row is no record.
			if (rowHidden && !ReadsUnwritten())
			{
				return SkipRow();
			}
		}

		if (scanner.IsEmptyElement)
		{
			return EndRow(target);
		}

		return Step.Read;
	}

	/// <summary>Sets the row's number, and the rows missing before it.</summary>
	/// <param name="number">The row's number.</param>
	/// <exception cref="CsvFormatException">The number is not a sheet's, or not after the row before.</exception>
	private void SetRow(long number)
	{
		if (number < 1 || number > CellReference.MaxRow)
		{
			throw Error(TokenOffset, $"its row number, {number}, is not one of a sheet's (1 to {CellReference.MaxRow})", -1, 0);
		}

		if (number <= lastRow)
		{
			throw Error(TokenOffset, $"it is not after the row before it, {lastRow}: rows must be in order", -1, number);
		}

		row = number;
		rowKnown = true;

		// With merged cells filled, a missing row can be a record of fills even when empty rows are skipped.
		if (keepsMissingRows || merged is not null)
		{
			gapNext = Math.Max(gapNext, lastRow + 1);
			gapEnd = number;
		}
	}

	/// <summary>
	/// Writes the empty records of the rows missing before the current one, while the target has room, or, when merged
	/// cells are filled, the records of their fills. They come before the row's first field, so a fill can end after them.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>
	/// Whether the row can go on in this fill: <see langword="false"/> when the target is full before they are all written,
	/// or after them.
	/// </returns>
	private bool FlushGaps<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		var wrote = false;
		while (gapNext < gapEnd)
		{
			// Never at the start of a fill, whose target is empty: each fill writes at least one record.
			if (target.IsFull)
			{
				return false;
			}

			if (merged is not null)
			{
				merged.Advance(gapNext);
				if (!missingRowsHidden && merged.HasFills)
				{
					WriteFilledRow(target);
					wrote = true;

					continue;
				}

				if (!keepsMissingRows)
				{
					// No range opened in a missing row has a value, so none of the rest is filled.
					gapNext = gapEnd;

					break;
				}
			}

			// Counted before it is written, so that the limit stops a million missing rows at the first past it.
			Charge(1);
			target.EndRecord(gapNext++);
			wrote = true;
		}

		// When they fill the target, the row waits for the next fill. A row's fields never wait: nothing is missing then.
		return !wrote || !target.IsFull;
	}

	/// <summary>
	/// Writes a missing row that merged ranges with a value cover, at <see cref="gapNext"/>: a record of their fills, padded
	/// as a row is, counted as a missing row is. The current row has no fields yet: the missing rows come before them.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the record.</param>
	private void WriteFilledRow<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		Charge(1);
		fields = 0;
		fillFrom = 0;
		AddFills(target, int.MaxValue, gapNext);
		if (width < 0)
		{
			width = fields;
		}
		else if (fields < width)
		{
			AddEmpty(target, width - fields);
		}

		target.EndRecord(gapNext++);
		fields = 0;
		fillFrom = 0;
	}

	/// <summary>
	/// Writes the fills of the merged ranges with a value whose runs start from <see cref="fillFrom"/> up to a field, each
	/// after an empty field for each field skipped since the row's last one: every field of a run is the range's top-left
	/// cell's field. Each fill but the top-left cell's own counts its bytes, at least 1, toward the uncompressed size first.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the fields.</param>
	/// <param name="before">The field the runs start before: a field the row writes next, which no run covers, or <see cref="int.MaxValue"/> for the rest.</param>
	/// <param name="atRow">The row written.</param>
	/// <exception cref="InvalidDataException">The fills take the workbook past its uncompressed size.</exception>
	private void AddFills<TTarget>(TTarget target, int before, long atRow)
		where TTarget : struct, IRecordTarget
	{
		var fill = merged!;
		for (var first = fill.NextFill(fillFrom); first >= 0 && first < before; first = fill.NextFill(fillFrom))
		{
			var last = fill.LastField(first);
			var value = fill.Value(first);
			var kind = fill.Kind(first);
			var counted = last - first + 1 - (fill.IsOwnCell(first, atRow) ? 1 : 0);
			Charge((long)Math.Max(value.Length, 1) * counted);
			AddGap(target, first);
			for (var field = first; field <= last; field++)
			{
				target.AddField(value, kind);
			}

			fields = last + 1;
			fillFrom = last + 1;
		}
	}

	/// <summary>Ends a row: pads it and writes its record, or drops it when it has no value and empty rows are skipped.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>
	/// <see cref="Step.Full"/> when the target is full, so the fill ends; <see cref="Step.Done"/> for a row below the area;
	/// otherwise <see cref="Step.Read"/>.
	/// </returns>
	private Step EndRow<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		if (!rowKnown)
		{
			SetRow(lastRow + 1);
			if (layout is not null && layout.Place(row) is var place && place != RowPlace.In)
			{
				// A row without cells, whose number came from the row before: nothing of it is left to skip.
				return LeaveRow(place);
			}
		}

		if (unwritten || rowHidden)
		{
			// A row that is no record: above the area or hidden, read for its shared formulas' definitions, or hidden
			// without cells (with them, it was skipped at its start or its first cell).
			unwritten = false;
			lastRow = row;
			cellColumn = -1;
			mode = Mode.SheetData;

			return Step.Read;
		}

		if (merged is not null)
		{
			// The fills after the row's last cell, or of a row without cells.
			merged.Advance(row);
			AddFills(target, int.MaxValue, row);
		}

		if (fields > 0)
		{
			if (width < 0)
			{
				width = fields;
			}
			else if (fields < width)
			{
				AddEmpty(target, width - fields);
			}

			target.EndRecord(row);
		}
		else if (!skipEmptyRows)
		{
			target.EndRecord(row);
		}

		lastRow = row;
		cellColumn = -1;
		mode = Mode.SheetData;

		return target.IsFull ? Step.Full : Step.Read;
	}

	/// <summary>Reads a cell: its reference, type and style, and its value once the window holds all of it.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="before">The scanner's state before the cell's tag, to read the cell again after a refill.</param>
	/// <returns>What to do next.</returns>
	private Step ReadCell<TTarget>(TTarget target, XmlScannerState before)
		where TTarget : struct, IRecordTarget
	{
		var cellStart = scanner.TokenStart;
		var hasReference = false;
		ReadOnlySpan<byte> reference = default;
		var type = CellType.Number;
		var style = 0;
		bool? space = null;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("r"u8))
			{
				hasReference = true;
				reference = ValueOf(attributes);
			}
			else if (attributes.IsName("t"u8))
			{
				type = TypeOf(ValueOf(attributes));
			}
			else if (attributes.IsName("s"u8))
			{
				style = attributes.GetInt32();
			}
			else if (attributes.IsQualifiedName("xml:space"u8))
			{
				space = XmlScanner.SpaceValue(attributes.Value);
			}
		}

		var preserves = space ?? rowPreserves;

		int column;
		var referenceRow = 0;
		if (hasReference)
		{
			if (!CellReference.TryParse(reference, out column, out referenceRow))
			{
				throw Error(Offset(cellStart), $"the attribute r={Quoted(reference)} is not a cell reference (A1 to XFD1048576)", -1, CurrentRow);
			}

			if (column <= lastColumn)
			{
				throw Error(Offset(cellStart), $"it is not after the cell before it, {CellReference.ColumnName(lastColumn)}{CurrentRow}: a row's cells must be in column order", column, CurrentRow);
			}
		}
		else
		{
			column = lastColumn + 1;
			if (column >= CellReference.MaxColumns)
			{
				throw Error(Offset(cellStart), "it is past the last column, XFD", -1, CurrentRow);
			}
		}

		cellColumn = column;
		if (!rowKnown)
		{
			SetRow(hasReference ? referenceRow : lastRow + 1);
			if (layout is not null && layout.Place(row) is var place && place != RowPlace.In && (place == RowPlace.Below || !ReadsUnwritten()))
			{
				// The row's first cell gave its number: nothing of the row has been written.
				return LeaveRow(place);
			}
		}

		// Also when the cell is read again after the missing rows before it filled a fill: the rest of them come first.
		if (!FlushGaps(target))
		{
			scanner.Restore(before);

			return Step.Full;
		}

		var field = column;
		if (layout is not null)
		{
			// A hidden row without r, once its first cell gave its number and the missing rows before it are written.
			if (rowHidden && !unwritten && !ReadsUnwritten())
			{
				return SkipRow();
			}

			field = unwritten ? -1 : layout.FieldOf(column);
			if (merged is not null)
			{
				merged.Advance(row);
				if (merged.IsTopLeft(column))
				{
					// A merged range's top-left cell, wherever it is: read into the range's store, and written as its fills.
					return CaptureCell(before, type, style, column, cellStart, preserves);
				}

				if (field >= 0)
				{
					if (merged.Covers(field))
					{
						// Another cell of an open range: it reads as the range's top-left cell, so its own value is not decoded.
						field = -1;
					}
					else
					{
						// The fills of the fields before it come first.
						AddFills(target, field, row);
					}
				}
			}

			if (field < 0)
			{
				// Outside the area, in a hidden column, or in a row that is no record: the cell is no field, and its value is not decoded.
				return SkipCell(before, column, cellStart, preserves);
			}
		}

		cellField = field;
		if (scanner.IsEmptyElement)
		{
			lastColumn = column;
			cellColumn = -1;

			return Step.Read;
		}

		segmentCount = 0;
		rawLength = 0;
		var found = false;
		var formula = CellFormula.None;
		var group = -1;
		while (true)
		{
			if (!scanner.TryRead())
			{
				// Nothing of the cell is written: read it all again once the window holds more.
				scanner.Restore(before);

				return Step.More;
			}

			var kind = scanner.Kind;
			if (kind == XmlTokenKind.EndTag)
			{
				// </c>, without a value.
				break;
			}

			if (kind != XmlTokenKind.StartTag)
			{
				continue;
			}

			if (scanner.IsStartTag("v"u8) || (type == CellType.InlineString && scanner.IsStartTag("is"u8)))
			{
				var whole = scanner.IsStartTag("v"u8) ? TryReadText(preserves) : TryReadInline(preserves);
				if (!whole)
				{
					scanner.Restore(before);

					return Step.More;
				}

				found = true;

				break;
			}

			if (sharedFormulas is not null && scanner.IsStartTag("f"u8))
			{
				if (!TryReadFormula(preserves, column, cellStart, out formula, out group))
				{
					scanner.Restore(before);

					return Step.More;
				}

				if (formula != CellFormula.None)
				{
					break;
				}

				continue;
			}

			// f when formulas read as their values, extLst, and anything else.
			if (!scanner.IsEmptyElement && !scanner.TrySkipTo(scanner.Depth - 1))
			{
				scanner.Restore(before);

				return Step.More;
			}
		}

		lastColumn = column;
		if (formula != CellFormula.None)
		{
			AddFormula(target, formula, group, column, cellStart);
		}
		else if (found)
		{
			AddValue(target, type, style, column, cellStart);
		}
		else
		{
			cellColumn = -1;

			return Step.Read;
		}

		// The rest of the cell, normally just </c>, is skipped across refills: the value is written.
		mode = Mode.CellRest;

		return Step.Read;
	}

	/// <summary>
	/// Passes over a cell that is no field: outside the area's columns, or in a row above it. Its value is not decoded.
	/// When formulas read as text, a shared formula's definition in it is read and kept, so that the group's cells in the
	/// area read their formula; otherwise the rest of the cell is skipped unread.
	/// </summary>
	/// <param name="before">The scanner's state before the cell's tag, to read the cell again after a refill.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="cellStart">The position of the cell's tag, for errors.</param>
	/// <param name="preserves">Whether <c>xml:space="preserve"</c> applies in the cell.</param>
	/// <returns>What to do next.</returns>
	/// <exception cref="CsvFormatException">The cell is of a shared formula's group that no cell before it defines.</exception>
	private Step SkipCell(XmlScannerState before, int column, int cellStart, bool preserves)
	{
		if (scanner.IsEmptyElement)
		{
			lastColumn = column;
			cellColumn = -1;

			return Step.Read;
		}

		if (sharedFormulas is null)
		{
			lastColumn = column;
			mode = Mode.CellRest;

			return Step.Read;
		}

		segmentCount = 0;
		rawLength = 0;
		while (true)
		{
			if (!scanner.TryRead())
			{
				// Nothing of the cell is kept: read it all again once the window holds more.
				scanner.Restore(before);

				return Step.More;
			}

			var kind = scanner.Kind;
			if (kind == XmlTokenKind.EndTag)
			{
				// </c>, without a formula.
				lastColumn = column;
				cellColumn = -1;

				return Step.Read;
			}

			if (kind != XmlTokenKind.StartTag)
			{
				continue;
			}

			if (scanner.IsStartTag("f"u8))
			{
				if (!TryReadFormula(preserves, column, cellStart, out var formula, out var group))
				{
					scanner.Restore(before);

					return Step.More;
				}

				if (formula == CellFormula.Defining)
				{
					if (unwrittenRoom is null || unwrittenRoom.Length < rawLength)
					{
						unwrittenRoom = new byte[Math.Max(rawLength, 256)];
					}

					sharedFormulas.Define(group, unwrittenRoom.AsSpan(0, Decode(unwrittenRoom)), row, column);
				}

				lastColumn = column;
				mode = Mode.CellRest;

				return Step.Read;
			}

			if (scanner.IsStartTag("v"u8) || scanner.IsStartTag("is"u8))
			{
				// A value: a cell's formula comes before it, as ReadCell reads them.
				lastColumn = column;
				mode = Mode.CellRest;

				return Step.Read;
			}

			// extLst, and anything else.
			if (!scanner.IsEmptyElement && !scanner.TrySkipTo(scanner.Depth - 1))
			{
				scanner.Restore(before);

				return Step.More;
			}
		}
	}

	/// <summary>
	/// Reads a merged range's top-left cell into the range's store (<see cref="MergedCapture"/>), as <see cref="ReadCell"/>
	/// would write it into a record, once the window holds all of its value; nothing of it is written into the target. Its
	/// value is the range's fills, which the row writes in column order; a cell without one leaves the range without fills.
	/// </summary>
	/// <param name="before">The scanner's state before the cell's tag, to read the cell again after a refill.</param>
	/// <param name="type">The cell's type.</param>
	/// <param name="style">The cell's style.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="cellStart">The position of the cell's tag, for errors.</param>
	/// <param name="preserves">Whether <c>xml:space="preserve"</c> applies in the cell.</param>
	/// <returns>What to do next.</returns>
	/// <exception cref="CsvFormatException">A shared string index can't be read, or a shared formula's group has no definition.</exception>
	private Step CaptureCell(XmlScannerState before, CellType type, int style, int column, int cellStart, bool preserves)
	{
		if (scanner.IsEmptyElement)
		{
			lastColumn = column;
			cellColumn = -1;

			return Step.Read;
		}

		segmentCount = 0;
		rawLength = 0;
		var found = false;
		var formula = CellFormula.None;
		var group = -1;
		while (true)
		{
			if (!scanner.TryRead())
			{
				// Nothing of the cell is kept: read it all again once the window holds more.
				scanner.Restore(before);

				return Step.More;
			}

			var kind = scanner.Kind;
			if (kind == XmlTokenKind.EndTag)
			{
				// </c>, without a value.
				break;
			}

			if (kind != XmlTokenKind.StartTag)
			{
				continue;
			}

			if (scanner.IsStartTag("v"u8) || (type == CellType.InlineString && scanner.IsStartTag("is"u8)))
			{
				var whole = scanner.IsStartTag("v"u8) ? TryReadText(preserves) : TryReadInline(preserves);
				if (!whole)
				{
					scanner.Restore(before);

					return Step.More;
				}

				found = true;

				break;
			}

			if (sharedFormulas is not null && scanner.IsStartTag("f"u8))
			{
				if (!TryReadFormula(preserves, column, cellStart, out formula, out group))
				{
					scanner.Restore(before);

					return Step.More;
				}

				if (formula != CellFormula.None)
				{
					break;
				}

				continue;
			}

			// f when formulas read as their values, extLst, and anything else.
			if (!scanner.IsEmptyElement && !scanner.TrySkipTo(scanner.Depth - 1))
			{
				scanner.Restore(before);

				return Step.More;
			}
		}

		lastColumn = column;
		if (formula == CellFormula.None && !found)
		{
			cellColumn = -1;

			return Step.Read;
		}

		// Written as a record's field would be, but with no gap before it, and the row's fields as they were.
		var capture = new MergedCapture(merged!);
		var rowFields = fields;
		cellField = -1;
		if (formula != CellFormula.None)
		{
			AddFormula(capture, formula, group, column, cellStart);
		}
		else
		{
			AddValue(capture, type, style, column, cellStart);
		}

		fields = rowFields;

		// The rest of the cell, normally just </c>, is skipped across refills: the value is kept.
		mode = Mode.CellRest;

		return Step.Read;
	}

	/// <summary>
	/// Reads the text and CDATA of a <c>v</c> or <c>t</c> element as one text, if the window holds all of it, marked to be
	/// trimmed unless <c>xml:space="preserve"</c> applies to it.
	/// </summary>
	/// <param name="inherited">Whether <c>xml:space="preserve"</c> applies in the element's parent.</param>
	/// <returns>Whether the element's end tag was read.</returns>
	private bool TryReadText(bool inherited)
	{
		if (scanner.IsEmptyElement)
		{
			return true;
		}

		var trim = !(scanner.SpaceOf() ?? inherited);
		var first = segmentCount;
		while (true)
		{
			if (!scanner.TryRead())
			{
				return false;
			}

			switch (scanner.Kind)
			{
				case XmlTokenKind.Text:
				case XmlTokenKind.CData:
					AddSegment(scanner.ContentStart, scanner.Content.Length, scanner.Kind == XmlTokenKind.CData);

					break;
				case XmlTokenKind.StartTag:
					if (!scanner.IsEmptyElement && !scanner.TrySkipTo(scanner.Depth - 1))
					{
						return false;
					}

					break;
				case XmlTokenKind.EndTag:
					// The element's own: one inside it was skipped whole.
					for (var i = first; i < segmentCount; i++)
					{
						segments[i].Trim = trim;
					}

					if (segmentCount > first)
					{
						segments[segmentCount - 1].EndsText = true;
					}

					return true;
			}
		}
	}

	/// <summary>
	/// Reads the text of an <c>is</c> element, if the window holds all of it: its <c>t</c> elements, directly in it or in
	/// its runs (<c>r</c>), each a text of its own; phonetic runs, properties and other elements are skipped.
	/// </summary>
	/// <param name="inherited">Whether <c>xml:space="preserve"</c> applies in the cell.</param>
	/// <returns>Whether the element's end tag was read.</returns>
	private bool TryReadInline(bool inherited)
	{
		if (scanner.IsEmptyElement)
		{
			return true;
		}

		var depth = scanner.Depth;
		var inlinePreserves = scanner.SpaceOf() ?? inherited;
		var runPreserves = inlinePreserves;
		var inRun = false;
		while (true)
		{
			if (!scanner.TryRead())
			{
				return false;
			}

			var kind = scanner.Kind;
			if (kind == XmlTokenKind.EndTag)
			{
				// </is>, or the end of a run in it.
				if (scanner.Depth < depth)
				{
					return true;
				}

				inRun = false;

				continue;
			}

			if (kind != XmlTokenKind.StartTag)
			{
				continue;
			}

			if (scanner.IsStartTag("r"u8))
			{
				if (!scanner.IsEmptyElement)
				{
					inRun = true;
					runPreserves = scanner.SpaceOf() ?? inlinePreserves;
				}

				continue;
			}

			var whole = scanner.IsStartTag("t"u8)
				? TryReadText(inRun ? runPreserves : inlinePreserves)
				: scanner.IsEmptyElement || scanner.TrySkipTo(scanner.Depth - 1);
			if (!whole)
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Reads a cell's <c>f</c>, when formulas read as text, if the window holds all of it: its type, its shared formula's
	/// group, and its text, as the value being read when the cell reads it.
	/// </summary>
	/// <param name="inherited">Whether <c>xml:space="preserve"</c> applies in the cell.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="cellStart">The position of the cell's tag, for errors.</param>
	/// <param name="formula">What the cell reads: its formula, a group's, or its value (<see cref="CellFormula.None"/>).</param>
	/// <param name="group">The shared formula's group, for <see cref="CellFormula.Defining"/> and <see cref="CellFormula.Shared"/>.</param>
	/// <returns>Whether the element's end was read.</returns>
	/// <exception cref="CsvFormatException">The cell is of a group that no cell before it defines.</exception>
	/// <exception cref="InvalidDataException">The <c>si</c> is not an index; <see cref="Scan"/> makes it the cell's error.</exception>
	private bool TryReadFormula(bool inherited, int column, int cellStart, out CellFormula formula, out int group)
	{
		formula = CellFormula.None;
		group = -1;
		var shared = false;
		var dataTable = false;
		var defines = false;
		var attributes = scanner.Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsName("t"u8))
			{
				var type = ValueOf(attributes);
				shared = type.SequenceEqual("shared"u8);
				dataTable = type.SequenceEqual("dataTable"u8);
			}
			else if (attributes.IsName("si"u8))
			{
				group = attributes.GetInt32();
				if (group < 0)
				{
					throw attributes.InvalidValue("a shared formula index");
				}
			}
			else if (attributes.IsName("ref"u8))
			{
				defines = true;
			}
		}

		// A data table's cells hold its results, which they read.
		if (dataTable)
		{
			group = -1;

			return scanner.IsEmptyElement || scanner.TrySkipTo(scanner.Depth - 1);
		}

		if (!TryReadText(inherited))
		{
			return false;
		}

		if (shared && group >= 0 && !defines)
		{
			// Another cell of a group: the defining cell's formula moved here, whatever text the cell's own f has.
			segmentCount = 0;
			rawLength = 0;
			if (!sharedFormulas!.TryFind(group, out var hasFormula))
			{
				throw Error(Offset(cellStart), $"it uses shared formula {group}, which no cell before it defines", column, row);
			}

			formula = hasFormula ? CellFormula.Shared : CellFormula.None;

			return true;
		}

		if (!shared || group < 0)
		{
			group = -1;
		}

		if (HasText())
		{
			formula = group < 0 ? CellFormula.Own : CellFormula.Defining;

			return true;
		}

		// No formula: the cell reads its value, and so do the other cells of a group it defines.
		segmentCount = 0;
		rawLength = 0;
		if (group >= 0)
		{
			sharedFormulas!.Define(group, default, row, column);
		}

		return true;
	}

	/// <summary>Adds a text or CDATA token to the value being read.</summary>
	/// <param name="start">Its content's position in the window.</param>
	/// <param name="length">Its content's length.</param>
	/// <param name="cdata">Whether it is a CDATA section.</param>
	private void AddSegment(int start, int length, bool cdata)
	{
		if (segmentCount == segments.Length)
		{
			Array.Resize(ref segments, segments.Length * 2);
		}

		segments[segmentCount++] = new Segment(start, length, cdata);
		rawLength += length;
	}

	/// <summary>Writes a cell's value, after an empty field for each column skipped since the row's last field.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="type">The cell's type.</param>
	/// <param name="style">The cell's style.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="cellStart">The position of the cell's tag, for errors.</param>
	/// <exception cref="CsvFormatException">A shared string index is not a number, or out of range.</exception>
	private void AddValue<TTarget>(TTarget target, CellType type, int style, int column, int cellStart)
		where TTarget : struct, IRecordTarget
	{
		switch (type)
		{
			case CellType.SharedString:
			{
				if (IsBlank())
				{
					return;
				}

				var index = ReadIndex(cellStart, column);
				if (!strings.TryGet(index, out var text))
				{
					var detail = strings.PartName is null ? $"it is shared string {index}, and the workbook has no shared strings"
						: strings.Count == 0 ? $"it is shared string {index}, and {strings.PartName} has no strings"
						: $"it is shared string {index}, past the last of {strings.PartName}, {strings.Count - 1}";

					throw Error(Offset(cellStart), detail, column, row);
				}

				if (text.IsEmpty)
				{
					return;
				}

				AddGap(target, cellField);
				ChargeBeyondCell(text.Length, cellStart);
				target.AddField(text, SheetCellKind.Text);

				break;
			}
			case CellType.Number:
			case CellType.Boolean:
			{
				if (IsBlank())
				{
					return;
				}

				AddGap(target, cellField);
				var span = target.GetFieldSpan(rawLength);
				var length = XmlText.TrimWhitespace(span.Slice(0, Decode(span)));
				var kind = type == CellType.Number ? styles.NumberKind(style) : SheetCellKind.Bool;
				if (type == CellType.Boolean)
				{
					length = ToBoolean(span, length, out kind);
				}

				// Only references can decode to blank text: an empty field, then.
				target.CommitField(length, length == 0 ? SheetCellKind.Empty : kind);

				break;
			}
			case CellType.IsoDate:
			{
				if (!HasText())
				{
					return;
				}

				AddGap(target, cellField);
				AddIsoDate(target, style);

				break;
			}
			default:
			{
				// An error that reads as null is valueless, as a cell without a value is.
				if (!HasText() || (type == CellType.Error && nullErrors))
				{
					return;
				}

				AddGap(target, cellField);
				var span = target.GetFieldSpan(rawLength);
				var length = Decode(span);

				// Only references can decode to blank text: an empty field, then, as for numbers.
				target.CommitField(length, length == 0 ? SheetCellKind.Empty : type == CellType.Error ? SheetCellKind.Error : SheetCellKind.Text);

				break;
			}
		}

		fields = cellField + 1;
	}

	/// <summary>
	/// Writes a <c>t="d"</c> cell's value: its serial number when its text is an ISO 8601 date or duration
	/// (<see cref="IsoDate"/>), with the kind its style gives, as Excel reads it (a date in a date style, a number in
	/// General); else its text, as Phase 1 reads it.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="style">The cell's style.</param>
	private void AddIsoDate<TTarget>(TTarget target, int style)
		where TTarget : struct, IRecordTarget
	{
		// The text is decoded and read, then the serial written over it.
		var span = target.GetFieldSpan(Math.Max(rawLength, MaxSerialLength));
		var length = Decode(span);
		if (IsoDate.TryParse(span.Slice(0, length), date1904, out var serial) && FloatFormatter.TryFormat(serial, span, out var written))
		{
			target.CommitField(written, styles.NumberKind(style));

			return;
		}

		// Only references can decode to blank text: an empty field, then, as for text.
		target.CommitField(length, length == 0 ? SheetCellKind.Empty : SheetCellKind.Text);
	}

	/// <summary>
	/// Writes a cell's formula as a text field, after an empty field for each column skipped since the row's last field: its
	/// own <c>f</c>'s text, which a defining cell also stores for its group, or its group's formula moved to it.
	/// </summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="formula">Where the formula comes from.</param>
	/// <param name="group">The shared formula's group, for <see cref="CellFormula.Defining"/> and <see cref="CellFormula.Shared"/>.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="cellStart">The position of the cell's tag, for the bytes a moved formula has beyond the cell's XML.</param>
	/// <exception cref="InvalidDataException">
	/// The shared formulas pass the part's size limit, or a moved formula takes the workbook past its uncompressed size.
	/// </exception>
	private void AddFormula<TTarget>(TTarget target, CellFormula formula, int group, int column, int cellStart)
		where TTarget : struct, IRecordTarget
	{
		AddGap(target, cellField);
		if (formula == CellFormula.Shared)
		{
			sharedFormulas!.TryGet(group, row, column, out var moved);
			ChargeBeyondCell(moved.Length, cellStart);
			target.AddField(moved, SheetCellKind.Text);
		}
		else
		{
			var span = target.GetFieldSpan(rawLength + 1);
			span[0] = (byte)'=';
			var length = Decode(span.Slice(1));

			// Stored as the cell's field is written, so once, even when a refill had the cell read again.
			if (formula == CellFormula.Defining)
			{
				sharedFormulas!.Define(group, span.Slice(1, length), row, column);
			}

			// Only references can decode to blank text: an empty field, then, as for values.
			target.CommitField(length == 0 ? 0 : length + 1, length == 0 ? SheetCellKind.Empty : SheetCellKind.Text);
		}

		fields = cellField + 1;
	}

	/// <summary>Adds an empty field for each field skipped since the row's last one: the columns between them.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="field">The field that follows them.</param>
	private void AddGap<TTarget>(TTarget target, int field)
		where TTarget : struct, IRecordTarget
	{
		if (field > fields)
		{
			AddEmpty(target, field - fields);
		}
	}

	/// <summary>Adds empty fields, counting each as one byte toward the workbook's uncompressed size before adding them.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="count">The number of fields.</param>
	private void AddEmpty<TTarget>(TTarget target, int count)
		where TTarget : struct, IRecordTarget
	{
		Charge(count);
		for (var i = 0; i < count; i++)
		{
			target.AddField(default, SheetCellKind.Empty);
		}
	}

	/// <summary>Counts fields or records that the XML doesn't hold toward the workbook's uncompressed size.</summary>
	/// <param name="count">The number of them.</param>
	/// <exception cref="InvalidDataException">The workbook passes its uncompressed size.</exception>
	private void Charge(long count)
	{
		if (count > 0)
		{
			budget?.Charge(count);
		}
	}

	/// <summary>
	/// Counts the bytes a field has beyond its cell's XML (up to where the scanner is, the end of the cell's value) toward
	/// the workbook's uncompressed size: a shared string or a moved shared formula, which a short cell can repeat. Other
	/// fields are no longer than their XML, which counted as it was inflated.
	/// </summary>
	/// <param name="length">The field's bytes.</param>
	/// <param name="cellStart">The position of the cell's tag.</param>
	/// <exception cref="InvalidDataException">The workbook passes its uncompressed size.</exception>
	private void ChargeBeyondCell(int length, int cellStart) => Charge(length - (scanner.Position - cellStart));

	/// <summary>
	/// Whether the value being read has text once each of its texts is trimmed as <c>xml:space</c> says: a text kept whole
	/// has it when it isn't empty, a trimmed one when it has more than XML white space.
	/// </summary>
	/// <returns>Whether it has text.</returns>
	private bool HasText()
	{
		var buffer = scanner.Window.Buffer;
		for (var i = 0; i < segmentCount; i++)
		{
			var segment = segments[i];
			if (segment.Length == 0)
			{
				continue;
			}

			if (!segment.Trim)
			{
				return true;
			}

			foreach (var b in buffer.AsSpan(segment.Start, segment.Length))
			{
				if (!XmlScanner.IsWhitespace(b))
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>Whether the value being read is only XML white space, or nothing.</summary>
	/// <returns>Whether it is blank.</returns>
	private bool IsBlank()
	{
		var buffer = scanner.Window.Buffer;
		for (var i = 0; i < segmentCount; i++)
		{
			var segment = segments[i];
			foreach (var b in buffer.AsSpan(segment.Start, segment.Length))
			{
				if (!XmlScanner.IsWhitespace(b))
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Decodes the value being read: each token as XML, and each text's <c>_xHHHH_</c> escapes once its tokens are joined.
	/// </summary>
	/// <param name="destination">Where the value goes: at least <see cref="rawLength"/> bytes.</param>
	/// <returns>The value's length.</returns>
	/// <exception cref="InvalidDataException">The value has a reference that can't be decoded.</exception>
	private int Decode(Span<byte> destination)
	{
		var buffer = scanner.Window.Buffer;
		var written = 0;
		var textStart = 0;
		for (var i = 0; i < segmentCount; i++)
		{
			var segment = segments[i];
			var source = buffer.AsSpan(segment.Start, segment.Length);
			if (segment.CData)
			{
				written += XmlText.DecodeCData(source, destination.Slice(written));
			}
			else
			{
				var decoded = XmlText.DecodeText(source, destination.Slice(written));
				if (decoded < 0)
				{
					throw scanner.ReferenceError(segment.Start + ~decoded, segment.Start + segment.Length);
				}

				written += decoded;
			}

			if (segment.EndsText)
			{
				if (segment.Trim)
				{
					written = textStart + XmlText.TrimWhitespace(destination.Slice(textStart, written - textStart));
				}

				written = textStart + XmlText.DecodeEscapes(destination.Slice(textStart, written - textStart));
				textStart = written;
			}
		}

		return written;
	}

	/// <summary>Reads the value being read as a shared string index.</summary>
	/// <param name="cellStart">The position of the cell's tag, for errors.</param>
	/// <param name="column">The cell's column, for errors.</param>
	/// <returns>The index: 0 or more.</returns>
	/// <exception cref="CsvFormatException">The value is not an index.</exception>
	private int ReadIndex(int cellStart, int column)
	{
		Span<byte> scratch = rawLength <= ScratchSize ? stackalloc byte[ScratchSize] : new byte[rawLength];
		var text = Trim(scratch.Slice(0, Decode(scratch)));
		if (!Utf8Parser.TryParse(text, out int index, out var consumed) || consumed != text.Length || index < 0)
		{
			throw Error(Offset(cellStart), $"its value, {Quoted(text)}, is not a shared string index", column, row);
		}

		return index;
	}

	/// <summary>Reads a boolean cell's value: <c>1</c> or <c>0</c>, or <c>true</c> or <c>false</c> of any case as those.</summary>
	/// <param name="span">The value, trimmed, from its start; a <c>true</c> or <c>false</c> is rewritten.</param>
	/// <param name="length">The value's length.</param>
	/// <param name="kind">Bool, or Text for any other value.</param>
	/// <returns>The length of the value written.</returns>
	private static int ToBoolean(Span<byte> span, int length, out SheetCellKind kind)
	{
		kind = SheetCellKind.Bool;
		var value = span.Slice(0, length);
		if (length == 1 && value[0] is (byte)'1' or (byte)'0')
		{
			return 1;
		}

		if (IsWord(value, "true"u8))
		{
			span[0] = (byte)'1';

			return 1;
		}

		if (IsWord(value, "false"u8))
		{
			span[0] = (byte)'0';

			return 1;
		}

		kind = SheetCellKind.Text;

		return length;
	}

	/// <summary>Whether a value is a lowercase ASCII word, ignoring case.</summary>
	/// <param name="value">The value.</param>
	/// <param name="word">The word, in lowercase.</param>
	/// <returns>Whether they match.</returns>
	private static bool IsWord(ReadOnlySpan<byte> value, ReadOnlySpan<byte> word)
	{
		if (value.Length != word.Length)
		{
			return false;
		}

		for (var i = 0; i < word.Length; i++)
		{
			if ((value[i] | 0x20) != word[i])
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>The row being read: its number when known, else the one after the last.</summary>
	private long CurrentRow => rowKnown ? row : lastRow + 1;

	/// <summary>The offset in the part of the token read.</summary>
	private long TokenOffset => scanner.Window.Offset(scanner.TokenStart);

	/// <summary>The offset in the part of a position in the window.</summary>
	/// <param name="position">The position.</param>
	/// <returns>The offset.</returns>
	private long Offset(int position) => scanner.Window.Offset(position);

	/// <summary>
	/// Creates the exception for sheet content that can't be read, at a cell (<c>Cell Orders!C14</c>), a row
	/// (<c>Row Orders!14:14</c>), or the sheet itself.
	/// </summary>
	/// <param name="offset">The offset in the part of what can't be read.</param>
	/// <param name="detail">What is wrong, as a clause.</param>
	/// <param name="column">The cell's column, or -1 for the row.</param>
	/// <param name="atRow">The row, or 0 for the sheet.</param>
	/// <param name="inner">The XML's error, if it is one.</param>
	/// <returns>The exception, to throw.</returns>
	private CsvFormatException Error(long offset, string detail, int column, long atRow, Exception? inner = null)
	{
		var where = atRow <= 0 ? null : column < 0 ? location.Row(atRow) : location.Cell(column, atRow);
		var subject = where is null ? "Sheet " + location.QuotedName : (column < 0 ? "Row " : "Cell ") + where;
		var message = $"{subject} can't be read at byte {offset} of {scanner.Window.PartName}: {detail}.";

		return new CsvFormatException(message, 0, 0, inner)
		{
			LineNumber = Math.Max(atRow, 0),
			Location = where,
		};
	}

	/// <summary>What a fill's scan stopped for.</summary>
	private enum Step : byte
	{
		/// <summary>Go on reading tokens.</summary>
		Read,

		/// <summary>The window needs a refill.</summary>
		More,

		/// <summary>The target is full: the fill ends, and the next one goes on.</summary>
		Full,

		/// <summary>The rows have ended.</summary>
		Done,
	}

	/// <summary>Where the scanner is in the part.</summary>
	private enum Mode : byte
	{
		/// <summary>Before the root element.</summary>
		Root,

		/// <summary>In the root, before <c>sheetData</c>.</summary>
		Worksheet,

		/// <summary>In <c>cols</c>, reading its <c>col</c> elements.</summary>
		Cols,

		/// <summary>In <c>sheetData</c>, between rows.</summary>
		SheetData,

		/// <summary>In a row, between cells.</summary>
		Row,

		/// <summary>In a cell whose value is written: skipping the rest of it.</summary>
		CellRest,

		/// <summary>Skipping an element, to go back to the mode it was skipped in.</summary>
		Skip,

		/// <summary>After the rows read: writing the rows that merged ranges fill after them, then done.</summary>
		Tail,

		/// <summary>After the rows: nothing more is read.</summary>
		Done,
	}

	/// <summary>How a cell's value is read, from its type (<c>t</c>).</summary>
	private enum CellType : byte
	{
		/// <summary><c>n</c>, or no type: a number.</summary>
		Number,

		/// <summary><c>s</c>: a shared string.</summary>
		SharedString,

		/// <summary><c>inlineStr</c>: the runs of its <c>is</c>.</summary>
		InlineString,

		/// <summary><c>b</c>: a boolean.</summary>
		Boolean,

		/// <summary><c>e</c>: an error value.</summary>
		Error,

		/// <summary><c>d</c>: an ISO 8601 date, read as its serial number.</summary>
		IsoDate,

		/// <summary><c>str</c>, and any type Excel doesn't have: the text of <c>v</c>.</summary>
		Text,
	}

	/// <summary>What a cell reads when formulas read as text, from its <c>f</c>.</summary>
	private enum CellFormula : byte
	{
		/// <summary>No formula: its value.</summary>
		None,

		/// <summary>Its <c>f</c>'s text.</summary>
		Own,

		/// <summary>Its <c>f</c>'s text, which defines a shared formula's group.</summary>
		Defining,

		/// <summary>The formula of the group it is in, moved to it.</summary>
		Shared,
	}

	/// <summary>A text or CDATA token of a value, in the window.</summary>
	private struct Segment
	{
		/// <summary>Creates the segment.</summary>
		/// <param name="start">Its content's position in the window.</param>
		/// <param name="length">Its content's length.</param>
		/// <param name="cdata">Whether it is a CDATA section.</param>
		public Segment(int start, int length, bool cdata)
		{
			Start = start;
			Length = length;
			CData = cdata;
			EndsText = false;
			Trim = false;
		}

		/// <summary>Its content's position in the window.</summary>
		public int Start { get; }

		/// <summary>Its content's length.</summary>
		public int Length { get; }

		/// <summary>Whether it is a CDATA section, whose content is decoded only for line ends.</summary>
		public bool CData { get; }

		/// <summary>Whether it is the last token of its text (a <c>v</c> or <c>t</c>), whose escapes are decoded after it.</summary>
		public bool EndsText { get; set; }

		/// <summary>Whether its text is trimmed of the XML white space around it: <c>xml:space="preserve"</c> doesn't apply to it.</summary>
		public bool Trim { get; set; }
	}
}
