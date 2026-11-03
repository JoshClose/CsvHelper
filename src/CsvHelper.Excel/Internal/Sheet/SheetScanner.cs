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
/// an error; <c>inlineStr</c> is the runs of its <c>is</c> joined, or its <c>v</c>; and <c>str</c>, <c>d</c> and any other
/// type are the text of <c>v</c>. Values are decoded as XML and then for their <c>_xHHHH_</c> escapes; between the two,
/// a text (a <c>v</c>, or a <c>t</c> of an <c>is</c>) that <c>xml:space="preserve"</c> doesn't apply to, on it or inherited,
/// loses the white space around it, as Excel trims it. A cell without a <c>v</c> or <c>is</c>, or whose value is empty, is
/// valueless and adds no field.
/// </para>
/// <para>
/// <b>Placement.</b> A row's number is its <c>r</c>, else the row of its first cell's <c>r</c>, else the row before's plus
/// one; rows must ascend, within 1 to 1,048,576. A cell's column is the letters of its <c>r</c>, else the cell before's
/// plus one; columns must ascend within a row, within <c>A</c> to <c>XFD</c>. A valued cell in column <c>k</c> first adds
/// an empty field for each column skipped since the last field, so a row ends at its last valued cell; a row with a value
/// is then padded with empty fields to the field count of the first record with a value. A row without a value is
/// dropped, or, when empty rows are kept, is a record of one empty field, as is each row missing before a row: record
/// <c>n</c> is then row <c>n</c>. Each empty field that a gap or the padding adds, and each missing row, counts as one
/// byte toward the workbook's uncompressed size, so that a few bytes of XML can't make millions of fields unchecked.
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

	private readonly XmlScanner scanner;
	private readonly SheetLocation location;
	private readonly SharedStrings strings;
	private readonly StyleTable styles;
	private readonly bool skipEmptyRows;
	private readonly LimitedReadStream? budget;
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

	/// <summary>Creates the scanner of a worksheet part.</summary>
	/// <param name="scanner">The part's XML, at its start; the sheet scanner owns it, and disposes it.</param>
	/// <param name="sheetName">The sheet's name, for the locations of errors.</param>
	/// <param name="strings">The workbook's shared strings.</param>
	/// <param name="styles">The workbook's styles.</param>
	/// <param name="skipEmptyRows">Whether a row without a value is dropped, rather than read as a record of one empty field.</param>
	/// <param name="budget">The part's stream, to count gap fields toward the workbook's uncompressed size; <see langword="null"/> to count nothing.</param>
	/// <exception cref="ArgumentNullException">An argument other than <paramref name="budget"/> is <see langword="null"/>.</exception>
	public SheetScanner(XmlScanner scanner, string sheetName, SharedStrings strings, StyleTable styles, bool skipEmptyRows, LimitedReadStream? budget)
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
	}

	/// <summary>Where things are in the sheet, for the record source's <c>Locate</c>.</summary>
	public SheetLocation Location => location;

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
	/// <returns>The kind; <see cref="CellType.Text"/> for <c>str</c>, <c>d</c> and types Excel doesn't have.</returns>
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
				XmlTokenKind.End => End(),
				_ => Step.Read,
			};
			if (step != Step.Read)
			{
				return step;
			}
		}
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

					return mode == Mode.Done ? Step.Done : Step.Read;
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

		// sheetPr, cols and the rest before sheetData; extLst in a row; anything else.
		if (!scanner.IsEmptyElement)
		{
			resume = mode;
			skipDepth = scanner.Depth - 1;
			mode = Mode.Skip;
		}

		return Step.Read;
	}

	/// <summary>Handles an end tag: of a row, of <c>sheetData</c>, or of a root without one.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>What to do next.</returns>
	private Step EndTag<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		if (mode == Mode.Row)
		{
			return EndRow(target) ? Step.Full : Step.Read;
		}

		// </sheetData>, after which nothing is read, or the root's end tag when it has no sheetData.
		return End();
	}

	/// <summary>Ends the rows.</summary>
	/// <returns><see cref="Step.Done"/>.</returns>
	private Step End()
	{
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
		}

		rowPreserves = space ?? dataPreserves;

		mode = Mode.Row;
		rowKnown = false;
		lastColumn = -1;
		fields = 0;
		if (number is not null)
		{
			SetRow(number.Value);
			if (!FlushGaps(target))
			{
				// The row waits for the next fill, after the empty records before it.
				scanner.Restore(before);
				mode = Mode.SheetData;

				return Step.Full;
			}
		}

		if (scanner.IsEmptyElement)
		{
			return EndRow(target) ? Step.Full : Step.Read;
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
		if (!skipEmptyRows)
		{
			gapNext = Math.Max(gapNext, lastRow + 1);
			gapEnd = number;
		}
	}

	/// <summary>
	/// Writes the empty records of the rows missing before the current one, while the target has room. They come before
	/// the row's first field, so a fill can end after them.
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

			// Counted before it is written, so that the limit stops a million missing rows at the first past it.
			Charge(1);
			target.EndRecord(gapNext++);
			wrote = true;
		}

		// When they fill the target, the row waits for the next fill. A row's fields never wait: nothing is missing then.
		return !wrote || !target.IsFull;
	}

	/// <summary>Ends a row: pads it and writes its record, or drops it when it has no value and empty rows are skipped.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <returns>Whether the target is full, so the fill ends.</returns>
	private bool EndRow<TTarget>(TTarget target)
		where TTarget : struct, IRecordTarget
	{
		if (!rowKnown)
		{
			SetRow(lastRow + 1);
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

		return target.IsFull;
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
		}

		// Also when the cell is read again after the missing rows before it filled a fill: the rest of them come first.
		if (!FlushGaps(target))
		{
			scanner.Restore(before);

			return Step.Full;
		}

		if (scanner.IsEmptyElement)
		{
			lastColumn = column;
			cellColumn = -1;

			return Step.Read;
		}

		segmentCount = 0;
		rawLength = 0;
		var found = false;
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

			// f (read from Phase 2 on), extLst, and anything else.
			if (!scanner.IsEmptyElement && !scanner.TrySkipTo(scanner.Depth - 1))
			{
				scanner.Restore(before);

				return Step.More;
			}
		}

		lastColumn = column;
		if (!found)
		{
			cellColumn = -1;

			return Step.Read;
		}

		AddValue(target, type, style, column, cellStart);

		// The rest of the cell, normally just </c>, is skipped across refills: the value is written.
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

				AddGap(target, column);
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

				AddGap(target, column);
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
			default:
			{
				if (!HasText())
				{
					return;
				}

				AddGap(target, column);
				var span = target.GetFieldSpan(rawLength);
				var length = Decode(span);

				// Only references can decode to blank text: an empty field, then, as for numbers.
				target.CommitField(length, length == 0 ? SheetCellKind.Empty : type == CellType.Error ? SheetCellKind.Error : SheetCellKind.Text);

				break;
			}
		}

		fields = column + 1;
	}

	/// <summary>Adds an empty field for each column skipped since the row's last field.</summary>
	/// <typeparam name="TTarget">The target's type.</typeparam>
	/// <param name="target">Takes the records.</param>
	/// <param name="column">The column of the field that follows them.</param>
	private void AddGap<TTarget>(TTarget target, int column)
		where TTarget : struct, IRecordTarget
	{
		if (column > fields)
		{
			AddEmpty(target, column - fields);
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

		/// <summary>In <c>sheetData</c>, between rows.</summary>
		SheetData,

		/// <summary>In a row, between cells.</summary>
		Row,

		/// <summary>In a cell whose value is written: skipping the rest of it.</summary>
		CellRest,

		/// <summary>Skipping an element, to go back to the mode it was skipped in.</summary>
		Skip,

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

		/// <summary><c>str</c>, <c>d</c>, and any type Excel doesn't have: the text of <c>v</c>.</summary>
		Text,
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
