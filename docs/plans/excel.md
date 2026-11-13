# CsvHelper.Excel plan

Status: approved 2026-11-01. Phase 0 (core hooks) was implemented 2026-11-01, and its writer sink was reworked 2026-11-02 to meet the benchmark gate; see "As built" under Phase 0. Phase 1's detailed design was approved 2026-11-02, with every recommendation accepted. Its steps 1 (skeleton and packaging), 2 (XML layer), 3 (package), 4 (shared strings), 5 (sheet scanner), 6 (record source and public API), 7 (corpus and oracles) and 8 (security and fuzzing) were built 2026-11-02, and 9 (benchmarks) and 10 ("As built") 2026-11-03. Phase 1 is complete, its gate met; see "As built" under Phase 1, which opens with a summary. Phase 2's detailed design was approved 2026-11-03, with every recommendation accepted. Its steps 1 to 8 were built 2026-11-03, and 9 (benchmarks, with the deferred core gate) and 10 ("As built") 2026-11-04. Phase 2 is complete, its gate met; see "As built" under Phase 2, which opens with a summary. Phase 3's detailed design was approved 2026-11-04, with every recommendation accepted. Its steps 1 (ranges and metadata), 2 (areas) and 3 (hidden rows and columns) were built and committed 2026-11-04, step 4 (merged cells) built that day and committed 2026-11-05, steps 5 (header row), 6 (corpus and oracles) and 7 (security and fuzzing) built and committed 2026-11-05, and step 8 (benchmarks) built 2026-11-05 and committed 2026-11-06. Step 9 ("As built") was written and committed 2026-11-06, with the gate re-checked. Phase 3 is complete. Every gate item is met but the default path's "every A/B method within 1%", which isn't plainly met: shared Rows has one clean pair, at +1.2%, which the review accepted as within the noise. The user called the phase complete with that item as it stands, rather than after measuring it again, and the measurement moved to Phase 9's list. See "As built" under Phase 3, which opens with a summary. Phase 4's detailed design was written 2026-11-06, after checks of Excel and LibreOffice, and approved that day, with every recommendation accepted. Its steps 1 (package and text) and 2 (typed cells) were built and approved that day, and committed together; step 3 (formulas and limits) was built that day, and approved and committed 2026-11-07; step 4 (formats) was built, approved and committed 2026-11-07, with dates' and times' formats only: `[CsvFormat]` doesn't apply to numbers, and the user left numbers' formats out of Phase 4, as recommended. Step 5 (corpus and oracles) was built, approved, with every recommendation, and committed 2026-11-07, step 6 (security and fuzzing) too, and step 7 (benchmarks) was built, run, approved and committed that day. Step 8 ("As built") was written, approved and committed 2026-11-07. Phase 4 is complete, its gate met; see "As built" under Phase 4, which opens with a summary. Core's UTF-8 date-literal workaround, Phase 5's first item, decided 2026-11-07, was built that day, and gated, approved and committed 2026-11-08. Phase 5's detailed design was written 2026-11-08, after checks of Excel and LibreOffice, and approved that day, with every recommendation accepted. Its step 1 (settings) was built that day, and approved and committed 2026-11-09, and steps 2 (styles and the header) and 3 (auto-fit) were built, approved, with every recommendation, and committed 2026-11-09. Step 4 (filters, merges and tables) was built, approved, with every recommendation, and committed 2026-11-10. Step 5 (links) was built, approved, with every recommendation, and committed that day. Step 6 (corpus and oracles) was built, approved, with every recommendation, and committed 2026-11-11. Step 7 (security, fuzzing and AOT) was built, approved, with every recommendation, and committed 2026-11-11.

CsvHelper.Excel is a first-party package for reading and writing `.xlsx` with CsvHelper's mapping: fast, typed, streaming tabular I/O. It is not a workbook object model. Charts, pivot tables and editing existing workbooks stay ClosedXML/EPPlus territory.

## Why

- **Every existing adapter is broken against the new CsvHelper.** That covers the ClosedXML family (CsvHelper.Excel, CsvHelper.Excel.Core and its many republished forks), the EPPlus pair, and CsvHelper.OpenXml.Excel. They all implement the old `IParser` or subclass `CsvWriter`. The new `CsvReader` and `CsvWriter` are sealed and there is no `IParser`. Their dependency ranges are open-ended (e.g. `>= 27.2.1`), so NuGet won't stop anyone pairing them with the new CsvHelper.
- **They are string-in/string-out.** Reads call `cell.Value.ToString()`, which ignores the configured culture. Writes produce text cells, so Excel shows "number stored as text". Only CsvHelper.OpenXml.Excel handles typed cells.
- **They hold the whole workbook in memory.**
- **They have a column-shift bug.** The ClosedXML parser reads columns `1..Count`, where Count is computed as max minus min used column. That's wrong whenever the data doesn't start in column A.
- **Licensing.** EPPlus 5+ is not free for commercial use, which is why one author ships two packages.

What sets this package apart is real Excel cell types instead of strings, constant memory, and reuse of CsvHelper's mapping. No existing adapter does all three.

## Architecture decisions

1. **Reading returns a plain `CsvReader` fed by an internal record source.**
   - `CsvReader` already runs on a buffer and index it didn't scan itself: parallel workers get batches through `ProcessBatch`, which sets `buffer`, `ends`, `fieldBase` and `fieldCount`.
   - The Excel source fills the same structure: decoded UTF-8 cell values and the same `ends` index. It adds one byte per field for the **cell kind**: Text, Number, DateNumber, Bool, Error, Empty.
   - The kind is needed because number cells are stored invariant (`<v>1.5</v>`). A de-DE reader must parse those with the invariant culture, but text cells a German user typed as `1,5` with its own culture.
   - The "needs decoding" flag of `ends` is set on DateNumber, Bool and Error cells (and on every cell when the reader trims). That sends `GetString` off its fast path to `MakeString`, whose typed-cell path will give those kinds their own text, at no cost to CSV. Decoding a flagged cell only trims it.
   - Result: generated `CsvRecordMapper<T>`s, `ICsvConverter<T>`, dynamic records, `CsvDataReader` (and so `SqlBulkCopy`), CsvHelper.Linq's `reader.Query<T>()` and `ParallelEnumerate` all work unchanged.
2. **Writing is a `CsvWriter` in an internal cell-sink mode.** Every `WriteField` overload (numbers, `DateTime`, `DateOnly`, `bool`, `string`, null) hands the sink its field as CSV formats it (UTF-8, before quoting) together with the field's CLR type, and the sink emits cell XML into the zip entry instead of CSV text. Generated `CsvRecordWriter<T>`s and converters work unchanged. (Phase 0 changed this from typed values: see its "As built".)
3. **The core hooks stay internal.** Core gets `InternalsVisibleTo` CsvHelper.Excel (with the public key), and CsvHelper.Excel depends on an *exact* CsvHelper version. A public record-source API would freeze the `ends` encoding, which changed as recently as the compress flattener.
4. **No Excel-library dependency.** Use `System.IO.Compression` plus our own XML scanner. Ship the same target frameworks as core (net10.0, net8.0, netstandard2.1, netstandard2.0, net47), strong-named, AOT and trim compatible on net8+.
5. **Excel attributes are read by reflection**, with `[DynamicallyAccessedMembers]` on `T`. The same settings are available as options keyed by header name, for dynamic records and types you don't own. The generator stays untouched in v1. Emitting Excel metadata from it is a later option if reflection shows up in profiles.

## API sketch

Names are placeholders.

```csharp
using var workbook = ExcelWorkbook.Open("orders.xlsx");

foreach (var sheet in workbook.Sheets)
{
	Console.WriteLine($"{sheet.Name} {sheet.Visibility}");
}

using var reader = workbook.OpenReader("Orders");	// a CsvReader

foreach (var order in reader.GetRecords<Order>())
{
	// ...
}

using var output = ExcelWorkbookWriter.ToFile("report.xlsx");
output.WriteSheet("Orders", orders);

using (var sheet = output.AddSheet("Raw", new ExcelSheetOptions { AutoFilter = true }))	// a CsvWriter
{
	sheet.WriteRecords(rows);
}
```

- **`ExcelWorkbook`**: `Open(path)` and `Open(stream)`. Exposes `Sheets` (name, index, visibility, kind, and from Phase 3 `Dimension` and `Tables`), `Tables`, `DefinedNames` and `Date1904`. `OpenReader(...)` takes a sheet name, index or `ExcelSheet`, and returns a `CsvReader`; a table, a defined name or a range comes through the options. Phase 1's "Public API" has the exact signatures and what each phase adds.
- **`ExcelReader`**: `FromFile(path)` and `Create(stream)`, and their async forms. One call returns a `CsvReader` that owns its workbook, for the common case of reading one sheet.
- **`ExcelReaderOptions`**:
  - `Sheet` (a name) or `SheetIndex`, then `Range`, `Table` or `DefinedName`, each a string: what to read. A `Range` may name its sheet (`'Q1 Orders'!B5:F200`).
  - `HeaderRow`: an `ExcelHeaderRow`: a fixed row (`At`), or a search of the first rows for `T`'s columns (`Find<T>`) or for names (`Find(names)`), which also clips the columns to the header's.
  - `FillMergedCells`, `SkipHiddenRows`, `SkipHiddenColumns`, all off by default.
  - `ErrorCells`: Text (default; the error's text, and typed targets fail), Null (an empty cell) or Error (every read of the cell fails, and `GetRecords` routes it to `OnRecordError`; a header cell still names its column, since step 8 of Phase 2).
  - `SignificantDigits`: significant digits for `decimal` and integer targets (default 15; null reads the stored text exactly).
  - `Formulas`: CachedValue (default) or FormulaText (`=` and the formula, with shared formulas moved to each cell).
  - `Text`: Raw (default) or Formatted (Phase 8).
  - `MaxUncompressedSize`, `MaxPartSize`: zip-bomb limits. Phase 1's "Public API" says what each bounds.
  - `Csv`: a composed `CsvReaderOptions` for `FormatProvider`, header matching, `OnRecordError`, null values, trimming and the rest. CSV-only settings (separator, quoting, comments) are ignored.
- **`ExcelWorkbookWriter`**: `ToFile(path)` and `Create(stream)` (Phase 4's name for the sketch's `ToStream`). `WriteSheet(name, records)` writes a whole sheet. `AddSheet(name)` and `AddSheet<T>(name)` return a `CsvWriter` for manual or mixed writing, the second with `T`'s formats. From Phase 5 each takes an optional `ExcelSheetOptions`, and a sheet's `CsvWriter` takes `MergeCells("A1:C3")`. Phase 4's and Phase 5's "Public API" have the exact signatures.
- **`ExcelWriterOptions`**:
  - `Strings`: Inline (default) or Shared.
  - `CompressionLevel`: `Fastest` by default, as Excel's own files are deflated.
  - `RowLimit`: Throw (default) or NewSheet (continues on a new sheet with the header repeated).
  - `LongNumbers`: Text (default), Number (lossy) or Throw.
  - `DateTimeOffsets`: Text (ISO 8601, default), Utc or ClockTime (the time at its own offset).
  - `OutOfRangeDates`: Text (default) or Throw, for dates before 1900 and negative durations.
  - `Sheets` (Phase 5): the `ExcelSheetOptions` of a sheet given none, and whose type has no `[ExcelSheet]`.
  - `Links` (Phase 5): Web (default; `Uri` fields with http, https or mailto are links, others text), All or None.
  - `Csv`: a composed `CsvWriterOptions`. CSV-only settings (separator, quoting, fixed width, comments) are ignored. Injection protection is unnecessary, because text cells are never evaluated.
- **`ExcelFormula`**: a formula, written as a formula cell without a cached value (Excel and LibreOffice compute it on open), through a member of that type or `WriteFormula`.
- **`ExcelSheetOptions`** (Phase 5): `Title` (a row above the header, merged across it), `BoldHeader` and `FreezeHeader` (both on), `FreezeColumns`, `AutoFilter`, `Table` (an `ExcelTableOptions`: name, style, banded rows, filter buttons), `AutoFit` (rows to measure, 100; 0 for none) and `MaxAutoFitWidth` (60 characters), and `Columns` (per header name, an `ExcelColumnOptions`: `Width`, `Format`, `Wrap`, `Hidden`). A sheet's are the argument, else `T`'s `[ExcelSheet]`, else `ExcelWriterOptions.Sheets`, each taken whole.
- **Attributes** (Phase 5): `[ExcelColumn(Width, Format, Wrap, Hidden)]` on members, and `[ExcelSheet]` on the class, with `ExcelSheetOptions`' settings but `Columns` (the table's as `Table`, `TableName` and `TableStyle`) and no `Name`, since a sheet's name is always given.

## Phases

Sizes are S, M, L and XL. Phases 0–6 make v1.0.

### Phase 0: core hooks (M)

- **Reader:**
  - An internal `CsvRecordSource` with `Fill`/`FillAsync`, which writes batches into the existing buffer, `ends` and `rowEnds`.
  - An internal constructor that takes a source.
  - A per-field cell-kind array.
  - Typed-cell paths in `ParseSpan` and `MakeString`, behind one readonly flag that is false for CSV.
- **Errors:** add a public `Location` (e.g. `Orders!C14`) to `CsvFormatException` and `CsvRecordError`. `LineNumber` carries the Excel row number. `ByteOffset` is not meaningful for sheets.
- **Writer:** an internal cell-sink reference, null for CSV. Every typed overload checks it first. Header and record boundaries notify the sink. (Changed while building: the hot overloads don't test the sink, fields arrive as text plus type, and headers aren't notified. See "As built".)
- **Uri:** add `WriteField(Uri)` and have the generator call it. Today it writes `OriginalString` as a plain string, so the sink can't tell a link from text.
- **`InternalsVisibleTo`** CsvHelper.Excel.
- **Gate:**
  - All existing tests pass.
  - ReadBenchmarks, WriteBenchmarks and NCsvPerf show no change beyond noise. Measure with the usual benchmark-machine procedure: affinity pinning, and a memory check first.
  - If a branch costs measurable time, use a separate internal code path instead.

**As built** (decisions made while implementing; the code is the reference):

- **Record source** (`src/CsvHelper/Internal/CsvRecordSource.cs`, `CsvReader.Cells.cs`):
  - The source writes through `CsvReader.RecordBatch`: `AddField`, or `GetFieldSpan` plus `CommitField` to decode straight into the buffer, then `EndRecord(line)`. It stops when `IsFull` (the reader's `BufferSize`, or the index capacity).
  - A `Fill` must write whole records; the reader starts each batch over at the start of its buffer. `Fill` returns false once the source is exhausted.
  - Kinds live in a `CsvCellKind[]` parallel to `ends`, at each field's first entry. Line numbers live in a `long[]` parallel to `rowEnds`.
  - A source reader has no CSV dialect: no quotes, escapes, comments, fixed widths or separator detection. Trimming, null values, empty-line skipping and field-count checks apply as for CSV.
  - Line tracking is always on, with the source's line (the sheet row) as `LineNumber`.
  - The reader owns the source and disposes it.
- **Typed cells:** Number, DateNumber and Bool parse with the invariant culture and the built-in true/false values. This holds even when the caller passes a provider. Text, Error and Empty use the reader's provider. Date conversion and the text of date and boolean cells are still Phase 2: date cells parse as their serial number, and every kind's `GetString` is its bytes.
- **Dynamic records:** they keep each field's kind after their offsets. The dynamic schema is keyed on "has cell kinds", so CSV and sheet readers with the same options don't share one. Phase 2's "GetString on typed cells" must cover `CsvDynamicRecord` values too.
- **`ParallelEnumerate`** already works over a record source: one `Fill` is one batch for the workers, kinds travel with each batch's index, and workers locate errors through the source. Phase 9 still adds the inflate/scan pipeline.
- **Errors:** `CsvRecordSource.Locate(line, field)` formats the location. It gives `Orders!C14` for a field, and the source decides the form for a whole row (field -1). The reader puts it in `Location` and in messages, in place of `(line n)`. `CsvRecordError.Location` is an init property, so the positional record keeps its shape. CSV leaves `Location` null.
- **Cell sink** (`src/CsvHelper/Internal/CsvCellSink.cs`):
  - **Fields arrive as text plus type** (a change from typed values): `WriteField(ReadOnlySpan<byte> utf8, Type type)`. The bytes are the field as CSV formats it, before quoting. The type is `string` for text, the value's type for formattable values (including a null nullable), `bool`, `Uri`, and the runtime type for `WriteField(object)`.
  - What the text carries:
    - The default formats round-trip. Invariant numbers are the shortest text that reads back as the same value, which is exactly what an XLSX `<v>` stores. Dates and times are ISO 8601 (`"O"`), and booleans are `True`/`False`.
    - A field written with a format string or a culture arrives as that text. The format string itself does not reach the sink.
    - Null is an empty field: the writer ignores `NullValue`, `TrueValue` and `FalseValue` with a sink, and quoting, escapes, padding and injection protection don't apply either.
  - **No header notification** (a change from "header boundaries notify the sink"). A sink takes its first record as the header when `CsvWriterOptions.HasHeader` is set. That's where `WriteRecords` writes one and where a caller of `WriteHeader` puts one.
  - The writer still counts fields and checks field counts against the first record. It calls `DiscardRecord` on a failed record.
  - `EndRecord` returns whether the sink wants a flush. The record loops then call `Flush` or `FlushAsync`, matching the caller's sync or async API.
  - Dispose ends an open record, flushes, and calls `Complete`/`CompleteAsync`.
  - Comments and parallel writes throw `NotSupportedException` with a sink, until Phase 9's parallel cell XML.
  - **Why text, and why the hot overloads don't test the sink.** The code that record loops inline (`WriteField(string)`, `WriteField(ReadOnlySpan<char>)`, `WriteField<T>`, `WriteHeader`, `FinishRecord`) sits at the JIT's inlining budget. Any IL added there moved CSV writes by 2–13%, even a test that is never taken or a single store. The cost came from inlining and register allocation in the caller's loop, not from executing the branch:
    - A `sink` test in each overload: FloatsWrite +13%, ByHand +6.5%. A smaller `ValidateRecord` got inlined and used up the budget the float formatter needed, and 4 more IL bytes in `WriteField(ReadOnlySpan<char>)` stopped `ContainsAny` from inlining.
    - A typed hook in the cold capacity-check branch of `WriteField<T>`: FloatsWrite +2% to +8% depending on the argument shape. `WriteField<float>` is inlined into the loop, and the extra IL changed which helpers the JIT inlined and how it allocated registers for the whole loop (`xmm6`/`xmm7` stopped being used).
    - A one-store header flag in `WriteHeader`: FloatsWrite +7%, for the same reason, though it runs once.
  - So a sink's writer takes the branches CSV takes only for custom handling (injection, ShouldQuote), which already exist:
    - It sets `custom` and `encodeAll` and clears `encodeQuote`/`narrowFast`. Every field is then formatted into the buffer as for CSV and reaches `EncodeCustom` with its type: values and UTF-8 through `Encode`, text through `WriteFieldOther`'s custom branch, booleans and nulls through `WriteUtf8`. `EncodeCustom` hands the bytes and type to the sink and moves `pos` back to the record start, so the buffer holds no fields.
    - It sets `checkRecord` and `protectComments`, so `ValidateRecord` checks each record as for CSV and then calls `ProtectFromComment`, which ends the record in the sink. CSV calls `ProtectFromComment` only with a comment prefix.
    - It sets `flushThreshold` to 0, so the record loops call `FlushBuffer` after every record. That drops the line ending `FinishRecord` wrote and flushes the sink if it asked.
    - Every method that CSV write loops compile has base's exact IL, except `FlushBuffer` and `Dispose`. Verified with the JIT's method summaries and disassembly for six write benchmarks: identical IL sizes, identical inlinee counts, and root code differing less than two runs of the same base build do.
  - **Field layout.** The new fields are declared last in `CsvWriter.cs`, and the reader's in `CsvReader.cs`. The CLR lays out reference fields first, in declaration order (partial files in compile order, where `CsvReader.Cells.cs` sorts before `CsvReader.cs`). A reference declared ahead of `buffer` moved it from offset 0x78 to 0x80, which made each access 3 bytes longer (a 4-byte displacement instead of 1). Value fields still shift by the size of the new references, but they were all past 0x80 already.
  - Keep all this in mind when touching these methods: diff the JIT disassembly of the benchmark's root method, base vs new, before trusting a change.
  - **Gate results (2026-11-02).** These were measured with BenchmarkDotNet, pinned and interleaved: 2 full pairs, 2 write-only pairs and 2 NCsvPerf pairs.
    - Reads, NCsvPerf and every write benchmark except FloatsWrite are within noise, about ±3%, the same spread as the Sep and Sylvan controls.
    - FloatsWrite came out about 1% slower over 6 targeted pairs. Two controls showed the same: a hybrid with the base writer and the new reader, and a variant that kept the sink off the writer so its layout matched base.
    - FloatsWrite's timed loop uses only the writer, and the reader runs only in its setup. So the shift comes from where the reader's changes place data or code, not from a cost in a CSV path.
    - The off-writer variant put the sink on the writer's copy of the options. It was rejected: `CsvWriter.Options` would hand the sink to any writer created from those options.
- **`WriteField(Uri?)`** writes `OriginalString` exactly as `WriteField(string)` does, so CSV output is unchanged; a sink gets it with the type `Uri`, to make a link. The generator calls it when the referenced `CsvWriter` has it. One compatibility cost: a literal `WriteField(null)` no longer compiles, because it is ambiguous between `string?` and `Uri?`.

### Phase 1: reader foundation (L)

Status: the detailed design below was approved on 2026-11-02, with every recommendation under "Decisions" accepted (question 12 updated: Python and LibreOffice are both installed). Steps 1 to 8 of "Order of work" were built on 2026-11-02; "As built", at the end of this phase, records them. Step 7 changed one rule of the design: texts are trimmed of XML white space unless `xml:space="preserve"` applies, as Excel reads them. The changes the design made to other sections are listed under "Changes elsewhere".

Scope, as approved:

- **Project:** `src/CsvHelper.Excel`, `tests/CsvHelper.Excel.Tests`, and Excel read benchmarks in `bench/CsvHelper.Benchmarks`.
- **Zip container:** `ZipArchive`. Non-seekable input is copied to a delete-on-close temp file.
- **Package parts:** `[Content_Types].xml`, the relationships, `workbook.xml` (sheet list, hidden state, `date1904`) and `styles.xml` (`cellXfs` mapped to number format ids, plus custom `numFmts`).
- **Shared strings:** one UTF-8 byte arena plus offsets. Rich-text runs are joined, `<rPh>` phonetic runs are skipped, and XML entities and `_xHHHH_` escapes are decoded (`_x005F_` is a literal underscore).
- **Sheet XML scanner:** forward-only over the inflated bytes.
  - It matches local names (`x:c` is `c`) and accepts the transitional and strict namespaces.
  - It handles cell types `n`, `s`, `str`, `inlineStr`, `b`, `e` and `d`.
  - It places sparse cells by their `r` reference and handles rows and cells without one.
  - It rejects a DOCTYPE.
- **Sheet choice:** the first sheet, or one by name or index.
- **Zip-bomb guard:** limits on total and per-part uncompressed size.

#### Public API

What Phase 1 ships, in `namespace CsvHelper.Excel`. These are signatures only; the code carries the full XML docs.

```csharp
public sealed class ExcelWorkbook : IDisposable, IAsyncDisposable
{
	public static ExcelWorkbook Open(string path, ExcelReaderOptions? options = null);
	public static ExcelWorkbook Open(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false);
	public static ValueTask<ExcelWorkbook> OpenAsync(string path, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);
	public static ValueTask<ExcelWorkbook> OpenAsync(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default);

	public IReadOnlyList<ExcelSheet> Sheets { get; }
	public bool Date1904 { get; }

	public CsvReader OpenReader(ExcelReaderOptions? options = null);
	public CsvReader OpenReader(string sheetName, ExcelReaderOptions? options = null);
	public CsvReader OpenReader(int sheetIndex, ExcelReaderOptions? options = null);
	public CsvReader OpenReader(ExcelSheet sheet, ExcelReaderOptions? options = null);
	public ValueTask<CsvReader> OpenReaderAsync(ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);
	public ValueTask<CsvReader> OpenReaderAsync(string sheetName, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);
	public ValueTask<CsvReader> OpenReaderAsync(int sheetIndex, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);
	public ValueTask<CsvReader> OpenReaderAsync(ExcelSheet sheet, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);

	public void Dispose();
	public ValueTask DisposeAsync();
}

// One call for the common case: the reader owns its workbook. The names mirror CsvReader.FromFile and CsvReader.Create.
public static class ExcelReader
{
	public static CsvReader FromFile(string path, ExcelReaderOptions? options = null);
	public static CsvReader Create(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false);
	public static ValueTask<CsvReader> FromFileAsync(string path, ExcelReaderOptions? options = null, CancellationToken cancellationToken = default);
	public static ValueTask<CsvReader> CreateAsync(Stream stream, ExcelReaderOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default);
}

public sealed record ExcelReaderOptions
{
	public static ExcelReaderOptions Default { get; } = new();

	public string? Sheet { get; init; }
	public int? SheetIndex { get; init; }
	public CsvReaderOptions Csv { get; init; } = CsvReaderOptions.Default;
	public long MaxPartSize { get; init; } = 256L * 1024 * 1024;
	public long MaxUncompressedSize { get; init; } = 4L * 1024 * 1024 * 1024;
}

public sealed class ExcelSheet
{
	public string Name { get; }
	public int Index { get; }
	public ExcelSheetVisibility Visibility { get; }
	public ExcelSheetKind Kind { get; }

	public override string ToString();
}

public enum ExcelSheetVisibility { Visible, Hidden, VeryHidden }

public enum ExcelSheetKind { Worksheet, Chartsheet, DialogSheet, MacroSheet }
```

- **Choosing the sheet:**
  - An explicit argument (`sheetName`, `sheetIndex`, `sheet`) wins over `Sheet`/`SheetIndex` in the options.
  - With neither, the reader takes the first worksheet in workbook order, hidden sheets included.
  - Setting both `Sheet` and `SheetIndex` throws `ArgumentException`.
  - Names match ignoring case (ordinal), as Excel keeps them unique ignoring case. An unknown name throws `ArgumentException` that lists the sheet names.
  - An index is 0-based into `Sheets`, which lists every sheet in tab order, chartsheets included. Out of range throws `ArgumentOutOfRangeException`.
  - A sheet that is not a worksheet throws `NotSupportedException`.
  - An `ExcelSheet` from another workbook throws `ArgumentException`.
  - With no sheet chosen, a workbook without a worksheet throws `NotSupportedException`, and one without sheets `InvalidDataException`.
- **Options and limits:**
  - The options passed to `Open` belong to the workbook and are the default for `OpenReader`. Options passed to `OpenReader` replace them for that reader; they are not merged.
  - Each call checks the parts it reads against the limits of its own options.
    - `Open` reads the content types, the relationships and `workbook.xml`.
    - `OpenReader` reads the styles and shared strings (on first use, kept by the workbook) and streams its sheet.
  - `MaxPartSize` bounds each part held in memory (content types, relationships, workbook, styles, shared strings) and any single token or cell of a streamed sheet.
  - `MaxUncompressedSize` bounds everything inflated through the workbook, plus the temp-file copy of a non-seekable input, plus what a sheet's cells give beyond their XML (decision 5, and since Phase 2's step 8 the bytes a shared string or a moved shared formula has beyond its cell).
  - A limit of 0 or less throws `ArgumentOutOfRangeException`.
- **`Csv`** applies as for any record source (Phase 0 "As built"):
  - These apply: `HasHeader`, `SkipEmptyLines`, trimming, `NullValues`, `TrueValues`/`FalseValues` (text cells only), `FieldCountMismatch`, `MissingField`, header matching, `OnRecordError`, `FormatProvider` (text cells only), `PoolStrings` and `BufferSize`.
  - The dialect settings are ignored.
- **Ownership:**
  - A reader from `OpenReader` holds its sheet's entry stream, not the workbook. Disposing the workbook first makes the reader's next fill throw `ObjectDisposedException`; the records the reader holds already can still be read.
  - A failed `Open`, or `ExcelReader` call, leaves the caller's seekable stream open, whatever `leaveOpen` says.
  - A reader from `ExcelReader` owns its workbook and disposes it.
- **Threading:**
  - A workbook is not thread-safe. Several readers of one workbook can be open and used in turn on one thread, because `ZipArchive` repositions its stream before each entry read. They must not be used from several threads at once.
  - `ParallelEnumerate` works, because its fills run one at a time.
- **Files:** `Open(path)` uses `FileShare.ReadWrite`, so a workbook that is open in Excel can still be read. Excel saves to a new file and swaps it in, so the reader sees the last saved copy.
- **Exceptions:**
  - `InvalidDataException`: the package or a part is unreadable or missing, or a part passes a limit. The message names the part.
  - `CsvFormatException`: a sheet's content can't be read. It carries `Location` (`Orders!C14`) and `LineNumber` (the row).
  - `NotSupportedException`: `.xlsb`, an encrypted `.xlsx` or an `.xls` (both are compound files), or a sheet that is not a worksheet.
  - `ObjectDisposedException`, and `FileStream`'s own exceptions.
- **Version guard:**
  - The first `Open` checks that the loaded CsvHelper's `AssemblyInformationalVersion` (the part before `+`) equals CsvHelper.Excel's own. If not, it throws `InvalidOperationException` naming both versions.
  - NuGet only warns (NU1608) when an application's direct CsvHelper reference breaks the exact pin. Without the check, a mismatched pair would fail later with `MissingMethodException` from deep inside a read.
  - If the attribute is missing (trimmed away), the check is skipped.

Later phases add:

- **Phase 2:** `ErrorCells`, `SignificantDigits`, `Formulas`, and the enums `ExcelErrorCells` and `ExcelFormulas`. Date conversion uses `Date1904`.
- **Phase 3:**
  - Options: `Range`, `Table` and `DefinedName` (strings), `HeaderRow` (an `ExcelHeaderRow`: `At`, `Find<T>`, `Find(names)`), `FillMergedCells`, `SkipHiddenRows` and `SkipHiddenColumns`.
  - `ExcelRange`, `ExcelTable` and `ExcelDefinedName`; `ExcelSheet.Dimension` and `ExcelSheet.Tables`; `ExcelWorkbook.Tables` and `ExcelWorkbook.DefinedNames`.
  - Reading all sheets is a loop over `Sheets`, one reader per sheet, which this phase's API already allows.
- **Phase 8:** `Text`.
- **Later, if `[ExcelSheet]` settings call for it:** an `ExcelReaderOptions.For<T>()` like `CsvReaderOptions.For<T>()`.

How a sheet becomes a `CsvReader`:

1. `OpenReader` validates the options and resolves the sheet to its part name.
2. On first use it loads the style table and the shared strings, and keeps them on the workbook. They are immutable after loading.
3. It opens the sheet's entry through the inflate guard and creates a `SheetRecordSource` with the sheet name, the stream, the shared strings, the style table and the options.
4. It returns `new CsvReader(source, options.Csv)`, Phase 0's internal constructor. Nothing of the sheet is read yet; the first `Read` fills.
5. Sync reads go `Read` → `Fill` → `SheetRecordSource.Fill`, which scans rows from the XML window and refills it with `Stream.Read`. Async reads go `ReadAsync` → `FillAsync` → `SheetRecordSource.FillAsync`, which runs the same scanner and refills with `ReadAsync(cancellationToken)`. The scanner is synchronous code over the window; only the refills await.
6. `OpenReaderAsync` loads styles and shared strings with async reads, and on net10.0 opens the entry with `ZipArchiveEntry.OpenAsync`.
7. Disposing the reader disposes the source, which disposes the entry stream (and, for `ExcelReader`, the workbook).

#### Internal design

Type layout (`src/CsvHelper.Excel`):

```
ExcelWorkbook.cs, ExcelReader.cs, ExcelReaderOptions.cs, ExcelSheet.cs (with the two enums), AssemblyInfo.cs
Internal/
  VersionGuard.cs       the check that the loaded CsvHelper is the version built with (see "Version guard")
  SyncOrAsync.cs        the results of code written once for both modes, with an async flag
Internal/Package/
  PackageArchive.cs     the ZipArchive: input handling, signature sniffing, temp-file spill, entries by part name
  InflateGuard.cs       the workbook's byte budget, and LimitedReadStream around every part
  PartName.cs           OPC part names: normalizing, resolving relationship targets
  ContentTypes.cs       [Content_Types].xml
  Relationships.cs      *.rels
  WorkbookPart.cs       workbook.xml and its relationships: sheets, date1904, the styles and shared strings parts
  StyleTable.cs         styles.xml: cellXfs, numFmts, the number kind of each xf
  SharedStrings.cs      the UTF-8 arena
Internal/Xml/
  XmlWindow.cs          the refillable byte window over a part's stream
  Utf16Stream.cs        UTF-16 parts read as UTF-8
  XmlScanner.cs         forward-only tokens (with XmlTokenKind and XmlScannerState)
  XmlAttributes.cs      the attributes of a start tag, read one at a time
  XmlElements.cs        walking a part's elements, sync or async, for the package parts' parsers
  XmlText.cs            decoding text into UTF-8
  XmlError.cs           the XML's exceptions, with the offset and the detail kept apart for a sheet's errors
Internal/Sheet/
  SheetRecordSource.cs  the CsvRecordSource: Fill, FillAsync, Locate, Dispose
  SheetScanner.cs       the row and cell state machine
  IRecordTarget.cs      where the scanner writes, and RecordBatchTarget, a struct over CsvReader.RecordBatch
  SheetCellKind.cs      a mirror of CsvCellKind (see "Writing into the reader")
  CellReference.cs      A1 references: parsing, column letters
  SheetLocation.cs      Location text: Orders!C14 and Orders!14:14, with the sheet's name quoted as Excel quotes it
```

**XML layer:**

- **`XmlWindow`:**
  - It is a rented 64 KB `byte[]` (or `MaxPartSize`, if that is smaller) with an end.
  - A refill is two calls. `Compact` moves the unconsumed bytes, from the start of the token in progress, to the front; it is synchronous. `Read`/`ReadAsync` then reads once from the stream. A cancelled async read leaves the window as `Compact` left it.
  - When a token fills the window, the window doubles, up to `MaxPartSize`.
  - A UTF-8 byte order mark is skipped. UTF-16 (LE or BE, with its byte order mark) is transcoded to UTF-8 by a wrapping stream. That is rare, but OPC allows it.
  - The declaration's `encoding` is otherwise ignored.
- **`XmlScanner`:**
  - It yields start tags (local name, attribute span, self-closing flag), end tags, text and CDATA.
  - It skips comments, processing instructions and the XML declaration, and rejects a DOCTYPE before reading past it.
  - Names match by local name; prefixes and namespaces are not checked. The one namespaced attribute we need, `r:id` on `<sheet>`, is matched as local name `id` with any prefix.
  - Unknown elements are skipped with a depth counter, never recursion.
  - Every step consumes input or asks for more, so it cannot loop forever.
  - It is not a validating parser. It throws when something stops it from reading, such as a truncated tag, an undefined entity or input that ends early, but it does not catch every well-formedness error (a mismatched end-tag name, for example).
- **`XmlText`** decodes into a destination span:
  - The five predefined entities, and decimal and hex character references, which must be valid XML characters.
  - CR LF and lone CR become LF, as XML's end-of-line handling (and `XmlReader`) does.
  - CDATA is copied verbatim, except that its line ends become LF too (as XML's end-of-line handling, which comes first, does).
  - Attribute values also get XML's attribute-value normalization: a literal tab, LF or CR becomes a space, and CR LF one space.
  - Text values then get `_xHHHH_` decoding (ST_Xstring), in place, over the joined text of a value:
    - Hex digits in either case; `_x005F_` is `_`.
    - Two escapes forming a surrogate pair become one character; a lone surrogate becomes U+FFFD.
  - Decoded text is never longer than its XML, so `GetFieldSpan(raw length)` always has room.
  - The fast path is one `IndexOfAny` for `&` and CR, then a copy; escapes are a second pass, one `IndexOf` for `_`. (The design had one pass for all three; see "As built".)

**Package:**

- **Input:**
  - A path opens a `FileStream` (`FileShare.ReadWrite | FileShare.Delete`, so that Excel can still save the file; `FileOptions.Asynchronous` for `OpenAsync`).
  - A seekable stream is used as is, from its start whatever its position, as `ZipArchive` reads it.
  - A non-seekable stream is copied to a temp file, because `ZipArchive` in Read mode would copy it into a `MemoryStream`.
    - The file gets a random name in `Path.GetTempPath()` and opens with `CreateNew`, `FileShare.None` and `DeleteOnClose`. On Windows the OS deletes it even if the process dies; elsewhere it is also deleted right after creation, since the open handle keeps it readable.
    - `OpenAsync` copies asynchronously, in counted 80 KB chunks.
    - The copied bytes count toward `MaxUncompressedSize`.
- **Signature:** the first 8 bytes decide.
  - `PK\x03\x04` is a zip. `PK\x05\x06` is an empty zip, which has no workbook.
  - `D0 CF 11 E0 A1 B1 1A E1` is a compound file, so an encrypted `.xlsx` or an `.xls`: `NotSupportedException`.
  - Anything else: `InvalidDataException`.
- **Archive:**
  - `new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen, Encoding.UTF8)`, or `ZipArchive.CreateAsync` on net10.0. UTF-8 is passed so every target decodes entry names the same way.
  - Entries are indexed by normalized part name: backslashes become `/`, the leading `/` is dropped, names are percent-decoded and compared ignoring case (OPC names are case-insensitive). On a duplicate, the first entry wins.
- **Guard:**
  - Before a part that is held in memory is opened, its declared size must be within `MaxPartSize`.
  - Every part stream is wrapped in a counting stream. It throws `InvalidDataException` when a held part passes `MaxPartSize` or the workbook's total passes `MaxUncompressedSize`.
  - It counts real bytes because declared sizes can lie. .NET caps Deflate reads at the declared size, which truncates a lying entry; .NET Framework doesn't. Counting ourselves makes every target the same.
- **Parts:**
  - `_rels/.rels`: the officeDocument relationship (transitional or strict type URI, matched by its last segment) gives the workbook part. With no relationships part, it falls back to `xl/workbook.xml`.
  - `[Content_Types].xml`:
    - It must give a known spreadsheet main type: `.xlsx`, `.xlsm`, `.xltx`, `.xltm` or `.xlam`, ignoring case and parameters. A content types part that gives the workbook part no type is accepted.
    - The `.xlsb` type throws `NotSupportedException`, and any other type `InvalidDataException` ("not a spreadsheet", for a `.docx` say).
    - A missing content types part is accepted.
  - The workbook's relationships map each `rId` to a type and a target.
    - The type is matched by its last segment: `worksheet`, `chartsheet`, `dialogsheet`, `xlMacrosheet` (or `xlIntlMacrosheet`), `sharedStrings` or `styles`. Both the transitional and strict URIs match.
    - Targets are resolved against the source part's folder. External targets are ignored.
  - `workbook.xml`:
    - It gives `workbookPr/@date1904`, and for each `sheets/sheet` its name (entity-decoded), its `state` (`visible`, `hidden` or `veryHidden`) and its `r:id`.
    - A sheet whose part is missing is still listed; opening it throws `InvalidDataException`.
  - Boolean attributes accept `1`, `0`, `true` and `false`, because LibreOffice writes `true`/`false`.
- **Styles:**
  - `cellXfs/xf/@numFmtId` in order (missing means 0), and `numFmts` from id to decoded format code.
  - From these, `StyleTable` builds a `SheetCellKind[]` per xf: the kind of a number cell with that style. Phase 1 makes every entry Number, and Phase 2 marks the date formats DateNumber. So the scanner's style lookup is in place and tested before date detection exists.
  - With no styles part (or one the workbook names but the package lacks), every xf is General. An `s` out of range means xf 0.

**Shared strings:**

- The part is streamed through an `XmlWindow`, not buffered whole. Its parser works token by token over what the window holds, and only the refills wait (synchronously or not), as in the sheet scanner. A large table has many tokens, and the package parts' walk (`XmlElements`) awaits each element.
- Strings go into a `byte[]` arena plus an `int[]` of starts (count + 1).
  - The initial capacities come from the entry's declared size and `uniqueCount`, capped at 16 MiB and a million strings, since either can lie.
  - `uniqueCount` is only a hint, and a value that isn't a count is ignored.
  - The arrays double as needed. At the end, they are trimmed when more than an eighth is spare.
- In each `<si>`, the text is the `<t>` elements directly under `<si>` or under `<r>`, joined in order.
  - `<rPh>`, `<phoneticPr>`, `<rPr>` and unknown elements are skipped with their text, as are elements inside a `<t>`. Text outside a `<t>` is ignored.
  - Each `<t>` loses the XML white space (space, tab, CR, LF) at its ends unless `xml:space="preserve"` applies to it: on the `<t>`, or inherited from its `<r>`, its `<si>` or the root, where `xml:space="default"` ends it. Each run is trimmed on its own. (Changed at step 7, which checked Excel: the design kept white space with or without `xml:space`.)
- Each `<t>` is decoded with `XmlText`: its XML first (entities, character references, CDATA, line ends), then trimmed, then its `_xHHHH_` escapes. So spaces from character references or CDATA are trimmed, and `_x0020_` is kept.
  - The escapes are decoded over the `<t>`'s whole text, but not across runs, since each `<t>` is a string value of its own.
  - So a surrogate pair whose halves are escapes in two runs reads as two U+FFFD.
- A cell gets its string with `TryGet(index, out span)`, and copies it with one copy and no .NET string: `AddField(span, Text)`. An empty string makes the cell valueless (see "Placement").
- The table loads once per workbook, sync or async, by the first `OpenReader` that needs it. It is immutable afterwards, so readers and workers share it.
- No shared strings part, or one the workbook names but the package lacks, gives the empty table, as for styles. A sheet without `t="s"` cells still reads.
- An arena past `Array.MaxLength` bytes, or more strings than that, throws `InvalidDataException`. An index out of range in a cell throws `CsvFormatException` at that cell.
- Memory is the UTF-8 size of the strings plus 4 bytes each, about half of what a `string[]` costs for ASCII text.

**Sheet scanner:**

- Its root must be `worksheet`. It skips to `<sheetData>` (by local name). It stops at `</sheetData>` and inflates nothing after it; Phase 3's merged-cell fill pays for its own second pass. No `sheetData`, or `<sheetData/>`, gives no records.
- Elements it reads:
  - In `sheetData`, it reads `row` and skips everything else.
  - In `row`, it reads `c` and skips everything else (`extLst`).
  - In `c`, it reads `v` and `is`. `f` is skipped in Phase 1 (Phase 2 reads it). Everything else is skipped.
  - Text outside a cell (mostly white space between rows and cells) is skipped as it comes, so that it never has to fit in the window. A cell, its own white space included, is read whole. (Added after step 8's commit; see its "As built".)
- How each cell type is read:

| `t` | Value | Kind |
| --- | --- | --- |
| `n` or none | `<v>`, whitespace trimmed | Number (DateNumber from the style table, from Phase 2) |
| `s` | the shared string whose index `<v>` holds | Text |
| `str` | `<v>`, decoded | Text |
| `inlineStr` | the runs of `<is>` joined (or `<v>`, if a producer writes one) | Text |
| `b` | `<v>`: `1` or `0`; `true` and `false` (any case) become `1`/`0`; anything else stays text | Bool, or Text |
| `e` | `<v>` | Error |
| `d` | `<v>`: ISO 8601 text | Text (Phase 2 converts it) |
| anything else | `<v>`, decoded | Text |

- A cell with no `<v>` or `<is>`, or with empty text, is **valueless**. That covers styled empty cells, formulas without a cached value (openpyxl writes these) and empty strings.
  - A value whose `<v>` (or every `<t>`) is only XML white space, and is trimmed, is valueless too.
  - A value that only character references make blank (`&#32;`) is an Empty field.
- `<is>` is read only for `inlineStr`, and `<v>` for every type. A cell's first `<v>` (or `<is>`) is its value, and the rest of the cell is skipped.
- **White space** (changed at step 7, which checked Excel; the design trimmed only numbers, booleans and indexes):
  - A `<v>`, and each `<t>` of an `<is>`, loses the XML white space at its ends unless `xml:space="preserve"` applies: on it, or inherited from its `<r>`, `<is>`, `<c>`, `<row>`, `<sheetData>` or the root. `xml:space="default"` ends an inherited one. That covers text, errors and `t="d"` dates.
  - Trimming comes after the XML is decoded and before the escapes, as for shared strings.
  - Numbers, booleans and shared string indexes are always trimmed, since they must parse. Excel won't open a workbook where `preserve` keeps spaces around one, or around an error; the reader reads such a cell anyway, and an error keeps its spaces, as text does.

**Placement:**

- **Row number:**
  - It is the row's `r`; else the row part of its first cell's `r`; else the previous row + 1 (the first row is 1).
  - Rows must ascend, within 1–1,048,576. Anything else throws `CsvFormatException`.
- **Column:**
  - It is the letters of the cell's `r` (A–XFD, either case); else the previous column + 1 (the first is A). The digits are parsed but not checked against the row.
  - Columns must ascend within a row; otherwise `CsvFormatException` names the cell.
  - The limits (1,048,576 rows, 16,384 columns) are Excel's. They also bound the gap fill below.
- **Field i is column i (A = 0):**
  - Phase 1 doesn't clip to the used range. Data that starts in column C reads with two empty fields first, and an empty header name for each.
  - Duplicate header names already resolve to the first, so mapping by name works.
  - Ranges, Tables and header detection (Phase 3) do the clipping.
- **Emission:** a valued cell in column k first adds an Empty field for each column skipped since the last valued cell, then its value. Valueless cells add nothing, so a row ends at its last valued cell.
- **Width:**
  - A row with a value is padded with Empty fields to the field count of the first record with a value (the header, when `HasHeader` and the first row has a value).
  - Ragged sheets then read the way Excel's own CSV export pads them, and `FieldCountMismatch = Throw` flags only rows with cells beyond the header. Longer rows stay longer.
  - Without padding, every short row would differ in `FieldCount`.
- **Empty rows:**
  - With `SkipEmptyLines` (the default), the source drops rows without a value itself, which is cheaper than handing them to the reader to skip.
  - With `SkipEmptyLines = false`, every row from 1 to the last `<row>` is a record: a missing row and a valueless row are each one empty field. Record n is then row n.

**Writing into the reader:**

- The scanner writes through `IRecordTarget`, as a generic struct parameter, so the JIT specializes it and nothing is dispatched through an interface.
  - Shared strings, gap fields and padding go through `AddField(span, kind)`.
  - Decoded values go through `GetFieldSpan(raw length)`, are decoded straight into that span, and are committed with `CommitField(decoded length, kind)`.
  - Each row ends with `EndRecord(row number)`.
- Core does the rest, as Phase 0 built it: the separator byte, the `ends` entry and its needs-decoding flag (Bool, Error, DateNumber, or trimming), the kind at the field's first entry, the LF and the record's line. The scanner never writes `ends` or kinds itself.
- **`SheetCellKind`:**
  - It mirrors `CsvCellKind`'s names and values, and `RecordBatchTarget` casts one to the other. A constant expression in CsvHelper.Excel breaks the build if they ever differ.
  - The mirror lets the Excel tests check kinds with their own capturing target, without seeing core's internals (see "Core changes").

**Location:**

- **Rows:** the source passes each record's sheet row to `EndRecord`. The reader keeps it per record (Phase 0's line array) and hands it back to `Locate(line, field)`, on `ParallelEnumerate` workers too.
- **Columns:** a field's column is its index plus the sheet's first column. That is 0 in Phase 1; Phase 3's ranges and hidden-column skipping replace it with a field-to-column map (`SheetLayout`).
- **Format:**
  - `Locate` gives `Orders!C14` for a field and `Orders!14:14` for a whole row (field -1).
  - Sheet names are quoted as Excel quotes them in formulas (`'Q1 Orders'!C14`, `'Bob''s'!A1`). That applies when a name has characters other than letters, digits, `_` and `.`, starts with a digit or a `.`, is `TRUE` or `FALSE`, or reads as a cell reference. That includes R1C1 references and names that start like one (`R2D2`, `C3PO`). Excel 365 was checked through COM; see step 5's "As built".
  - `Locate` uses only immutable data, so it is thread-safe.
- **Scanner errors** (malformed XML, a bad shared string index, cells out of order) name the cell being scanned in `Location`, set `LineNumber`, and put the part name in the message: `Cell Orders!B3 can't be read at byte 1234 of xl/worksheets/sheet1.xml: ...`. Without a cell they name the row (`Row Orders!3:3`), or the sheet (`Sheet Orders`, no `Location`). Limits, and a part that can't be inflated, stay `InvalidDataException`.

**A value split across batches or reads:**

- **Across `Fill` calls:** never. A fill writes whole records. The scanner pauses only after `</row>` once `IsFull`, or at the end of `sheetData`, and resumes there. Gap rows (with `SkipEmptyLines = false`) resume from a counter.
- **Across inflate reads (window refills):**
  - The window holds raw XML and the reader's buffer holds decoded fields. They are separate buffers.
  - A cell is committed only once its value is whole in the window.
    - If `</v>` or `</is>` isn't there yet, the scanner rewinds to the cell's `<c`, having committed nothing for it.
    - The window then refills, compacting from that `<c`, and grows for a value larger than itself.
    - After a value is committed, the rest of the cell (normally just `</c>`) is skipped in a resumable state.
  - Between cells the scanner keeps only the row number, the next column, the width and any pending gap rows.
  - Fields of the current row that are already committed stay in the reader's buffer across refills.
- **Tests** cover every split position: a stream that returns one byte per read, a 16-byte initial window (an internal hook), and both again async.

Phase 1 performance: the BCL's vectorized `IndexOf` finds `<`, attributes are plain loops, and there is no SIMD of our own until Phase 9.

#### Project setup

`src/CsvHelper.Excel/CsvHelper.Excel.csproj`:

- **Targets:** `net10.0;net8.0;netstandard2.1;netstandard2.0;net47`, the same as core.
- **Basics:** `ImplicitUsings` off, `Nullable` on, `GenerateDocumentationFile`, and package metadata like CsvHelper.Linq's: description, tags `excel;xlsx;spreadsheet;csvhelper`, icon, MS-PL OR Apache-2.0.
- **Strong name:** `SignAssembly` with `..\CsvHelper\CsvHelper.snk`, and `PublicSign` off Windows. That is the key CsvHelper's `InternalsVisibleTo CsvHelper.Excel` already names.
- **AOT and trimming:**
  - `IsAotCompatible` and `IsTrimmable` on net8+, with the same condition as core.
  - Phase 1 uses no reflection, so it needs no `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]`, and the analyzers keep it that way.
  - Phase 5's attribute reading adds `[DynamicallyAccessedMembers]` on `T`, and a NativeAOT publish check comes with it.
- **net47:** add `<Reference Include="System.IO.Compression" />`.
  - SDK-style .NET Framework projects reference only `System.IO.Compression.FileSystem` implicitly (checked in SDK 10.0.402's `Microsoft.NET.Sdk.BeforeCommon.targets`).
  - Pack turns the reference into a `frameworkAssembly` entry in the nuspec.
- **Dependencies:**
  - CsvHelper only. `System.Memory` and `Microsoft.Bcl.AsyncInterfaces` come through CsvHelper's netstandard2.0/net47 dependencies.
  - Polyfills come from core's internal ones, visible through `InternalsVisibleTo`. Copies would collide (CS0436).
- **Reference:** a project reference to CsvHelper with `PrivateAssets="none"`, as CsvHelper.Linq has, so the generator reaches applications.
- **Exact pin:**
  - NuGet writes a project reference as `>= version`. A target after NuGet's `_GetProjectReferenceVersions` rewrites CsvHelper's version to `[version]`:

```xml
<!-- CsvHelper.Excel uses CsvHelper's internals, so its package needs that exact CsvHelper version, not a minimum. -->
<Target Name="PinCsvHelperVersion" AfterTargets="_GetProjectReferenceVersions">
	<ItemGroup>
		<_ProjectReferencesWithVersions Update="@(_ProjectReferencesWithVersions)" Condition="'%(Filename)' == 'CsvHelper'" ProjectVersion="[%(ProjectVersion)]" />
	</ItemGroup>
</Target>
```

  - The target relies on NuGet's internal target and item names (present in SDK 10.0.402's `NuGet.Build.Tasks.Pack.targets`). So a CI step packs with `-p:PackageVersion=0.0.0-ci` and fails unless the nuspec says `[0.0.0-ci]`.
  - The version guard (see "Public API") covers applications that override the pin anyway.
- **`InternalsVisibleTo`:** CsvHelper.Excel.Tests, with the key.
- **Solution:** add `src/CsvHelper.Excel`, `tests/CsvHelper.Excel.Tests` and `tools/CsvHelper.Excel.Corpus` to `CsvHelper.sln`.
- **Publishing:** `publish.yaml` stays as is until v1.0 (Phase 6). It then gets a pack and push step after CsvHelper's, like CsvHelper.Linq's.

`ZipArchive` by target, checked against the .NET 10 source (`ZipArchive.cs`, `ZipArchiveEntry.cs`) and the reference packs installed here:

| | netstandard2.0, netstandard2.1 | net47 | net8.0 (runs on .NET 8 and 9) | net10.0 |
| --- | --- | --- | --- | --- |
| Implementation | the platform's: .NET Framework 4.6.1–4.6.2, Mono, Unity, .NET Core 3–7 | .NET Framework's System.IO.Compression | .NET's | .NET's |
| Async open, entry and dispose | no | no | no | `CreateAsync`, `OpenAsync`, `DisposeAsync` |
| Deflate reads stop at the declared size | depends on the platform | no | yes | yes |
| CRC-32 checked on read | no | no | no | no |
| Non-seekable input in Read mode | copied into a `MemoryStream` | same | same | same |
| Inflate | the platform's | .NET Framework's zlib | .NET 8: zlib; .NET 9: zlib-ng | zlib-ng |

What follows from it:

- Before net10.0, `OpenAsync` and `OpenReaderAsync` still read the central directory and local headers synchronously, from a local file or the caller's seekable stream. A non-seekable input (a request body) is copied asynchronously first, and that is where async matters in practice.
- No target checks CRCs. Corrupt Deflate data surfaces as an `InvalidDataException` from `DeflateStream` or as malformed XML; corruption that still inflates can read as wrong values. Phase 9's own zip reader could check CRCs.
- Read speed depends on the runtime's inflate, so benchmarks run on net10.0 only.
- The tests run net10.0 (async zip), net8.0 (the sync fallback) and net48 (the net47 build on .NET Framework's `ZipArchive`). The netstandard builds are compiled but not run, as for core. Their zip code is the same as net8.0's.

#### Testing

`tests/CsvHelper.Excel.Tests`:

- It targets `net10.0;net8.0;net48`, is signed with the key, and references CsvHelper.Excel, CsvHelper.Linq (for the `Query<T>` gate) and the generator as an analyzer.
- Every reading test runs sync and async.
- Test-only dependencies are the oracles: DocumentFormat.OpenXml 3.5.1, ClosedXML 0.105.1 and ExcelDataReader 3.9.0.

**Synthetic packages** carry most of Phase 1's tests. A `TestPackage` builder in the test project writes XML strings into a zip, with control over compression, entry names, byte order marks and missing parts. Cases:

- **Namespaces and prefixes:** `x:`-prefixed elements, strict namespaces, and both relationship type URIs.
- **References:** rows and cells without `r`, rows without `r` whose cells have one, gaps in rows and columns, data starting at C3, `XFD`, lowercase references.
- **Out of order:** rows and cells out of order or repeated (errors with a Location).
- **Cell types:** every `t`, booleans written `true`/`false`, whitespace around numbers, `t="str"` without a formula, `inlineStr` with `<v>`.
- **Text:** entities, character references, CDATA, CR LF in text, every `_xHHHH_` case (`_x005F_`, surrogate pairs, lone surrogates), rich text and `rPh`.
- **Empty and valueless:** valueless and empty-text cells at the start, middle and end of rows; `<row/>`; `<sheetData/>`; no `sheetData`.
- **Shared strings:** empty, missing (then a `t="s"` cell is an error), index out of range.
- **Encodings:** UTF-8 with and without a byte order mark, UTF-16 LE and BE.
- **Part names:** backslashes and different case in entry names, absolute and `..` relationship targets, percent-encoded names, a missing `_rels/.rels`, missing content types.
- **Workbook:** sheet states, chartsheets first and between worksheets, sheet names that need quoting in a Location.
- **Options:** `SkipEmptyLines` on and off, padding, `HasHeader` off, trimming.
- **Splits:** each case also runs through the one-byte stream and the 16-byte window.

Gate tests, over synthetic sheets and corpus files:

- `GetRecords<T>` with a generated mapper, `CsvDynamicRecord`, `CsvDataReader` (with `DataTable.Load`), `reader.Query<T>()`, `ParallelEnumerate`, and header validation (a missing column throws `KeyNotFoundException`).
- Every error carries its cell: `CsvFormatException.Location`, `CsvRecordError.Location` and the message.
- Errors from the scanner come out of `Read`, not through `OnRecordError`, which is for records.
- Two readers of one workbook used in turn on one thread read correctly.

**What this machine has** (checked 2026-11-02):

- Excel: Microsoft 365, Excel 16.0 build 20527. COM automation works: a test created a workbook and quit cleanly.
- LibreOffice: 26.8.2.2, installed by the user on 2026-11-02 at `C:\Program Files\LibreOffice\program\soffice.exe`.
  - It is not on PATH. The `App Paths\soffice.exe` registry entry points at it, so the corpus tool takes it from there.
  - Headless conversion was checked both ways (CSV to `.xlsx` with "Calc MS Excel 2007 XML", and back to CSV).
  - Each run passes `-env:UserInstallation=file:///<temp>/profile`, so a throwaway profile keeps it clear of the user's settings and of a LibreOffice window that is already open.
  - What its `.xlsx` showed: boolean attributes as `true`/`false` (`date1904="false"`, `hidden="false"`), the sheet's relationship as `rId3` rather than `rId1`, and `[Content_Types].xml` as the last zip entry.
- Python: 3.14.10, installed by the user on 2026-11-02 at `%LOCALAPPDATA%\Programs\Python\Python314`, with pip 26.2.2 and venv. Shells started before the install still resolve `python` to the Microsoft Store alias, so the corpus tool takes the interpreter from the PEP 514 registry entry (`HKCU\Software\Python\PythonCore`), not from PATH. All the producers support 3.14: openpyxl 3.1.5 and XlsxWriter 3.3.1 are pure Python, and pandas 3.1.0 (with numpy 2.5.4) has cp314 wheels.
- Node: 24.13.1, with npm 11.8.0.
- Java: none, so Apache POI itself can't run here. (Changed at step 7: the user approved a portable JDK, Eclipse Temurin 25.0.5+8, unpacked from its zip into `%LOCALAPPDATA%\CsvHelper.Excel.Corpus` beside the venv. Nothing is installed, and nothing on PATH or in the registry changes.)
- .NET: SDKs 8.0.427, 10.0.401 and 10.0.402; runtimes 8.0.33, 9.0.20 and 10.0.13.
- No Mac and no Google account are reachable from here.

**Corpus** (`tests/CsvHelper.Excel.Tests/Files`, committed; a `README.md` there records each file's producer, version, how it was made, and for downloads the source URL, commit and license):

| Files | Producer | How it is made here |
| --- | --- | --- |
| `excel/basic.xlsx`; variants `date1904.xlsx`, `strict.xlsx` (Strict Open XML, FileFormat 61), `macro.xlsm` (52), `template.xltx` (54) | Excel 16.0 build 20527 | the corpus tool drives Excel through COM (`dynamic` over `Excel.Application`) |
| `excel/binary.xlsb` (50), `excel/encrypted.xlsx` (password to open), `excel/legacy.xls` (56) | Excel | the same, for the `NotSupportedException` cases |
| `excel/packageassets-1695.xlsx` | Excel | NCsvPerf's CSV opened with every column as text (`Workbooks.OpenText`, `xlTextFormat`) and saved as `.xlsx`, to cross-check the benchmark generator |
| `libreoffice/basic.xlsx`, `libreoffice/basic-ooxml.xlsx` | LibreOffice 26.8.2.2 | `soffice --headless --convert-to` from `excel/basic.xlsx`, with the "Calc MS Excel 2007 XML" and "Calc Office Open XML" filters and a throwaway `-env:UserInstallation` profile |
| `openpyxl/`, `xlsxwriter/` (default, and `constant_memory`, which writes inline strings), `pandas/` (each engine named explicitly, since pandas picks XlsxWriter when it is installed) | Python 3.14.10 with openpyxl 3.1.5, XlsxWriter 3.3.1 and pandas 3.1.0 | a venv outside the repo (`python -m venv`, then `pip install` of the exact versions from a committed `requirements.txt`); a script in `tools/CsvHelper.Excel.Corpus/python`, run with `-I` |
| `poi/xssf.xlsx`, `poi/sxssf.xlsx` | Apache POI 6.0.0 on Java 25 | added at step 7, in place of NPOI: `java/Corpus.java` in the corpus tool, run by the portable JDK with the jars `java/jars.txt` pins |
| `closedxml/`, `openxmlsdk/` (`x:`-prefixed), `epplus/`, `miniexcel/`, `spreadcheetah/`, `largexlsx/` | ClosedXML 0.105.1, Open XML SDK 3.5.1, EPPlus 4.5.3.3 (the last LGPL version), MiniExcel 1.46.0, SpreadCheetah 1.28.0, LargeXlsx 2.0.2 | the corpus tool. Which of these omit `r` or write inline strings is recorded in the corpus's `README.md`. (Changed at step 7: the SDK can't write the default-namespace variant, and NPOI is out; see step 7's "As built".) |
| `google/`, `numbers/`, `excel-mac/` | Google Sheets, Apple Numbers, Excel for Mac | not possible here: the user exports `excel/basic.xlsx` through them, or a few files come from Apache POI's test-data and SheetJS's test_files (both Apache-2.0). The user can't make the exports. Added after step 7's commit: two Google Sheets and two Excel for Mac files from POI's test data, which the tool's `downloads` command fetches. Neither source has a Numbers file: POI's test data has none, and SheetJS's test files are blocked on GitHub |

- **`basic` content:** the corpus tool (`tools/CsvHelper.Excel.Corpus`, a net10.0 console that is not packed) defines it once:
  - Sheets: "Data", "Hidden" (hidden), "VeryHidden" (very hidden), a chartsheet in second place, "Q1 Orders", "Bob's" and a Japanese name.
  - Values: text, integers, decimals, negatives, `1E-05`, a 17-digit double, booleans, `#N/A` and `#DIV/0!`, empty cells mid-row and at row ends, an empty row, a block that starts in column C, emoji, leading and trailing spaces, a line break, U+0001, a literal `_x0041_`, `&<>"'`, a 32,767-character cell, rich text, a phonetic run (the Open XML SDK and ClosedXML write it; Excel can't through COM here, see step 7), and formulas with number, text and error results plus a shared formula down a column.
  - Dates: dates and times in built-in and custom formats, and a time alone, which every producer writes as a number in a time format.
- **Regenerating:** the tool runs on demand, never in CI. Tests only read the committed files.

**Oracles:**

- **Open XML SDK** (DOM): the raw structure of each cell: reference, `t`, `s`, `<v>`, the runs of `<is>`, and shared strings by index.
  - The test side decodes `_xHHHH_` and skips phonetic runs with its own small decoder, written independently of `XmlText`. As far as I know the SDK leaves escapes undecoded, and `InnerText` includes `rPh`; the first oracle run will confirm.
  - Raw text and kind are compared exactly.
- **ClosedXML:** typed values (blank, boolean, number, text, error, date). Kind and value are compared; Phase 1 compares dates as serial numbers.
- **ExcelDataReader:** an independent streaming reader. Values and the placement of sparse cells are compared.
- **Disagreements:** where oracles disagree on a file, the test records the difference with its reason. Our reader must match the documented rule.
- **LibreOffice `--convert-to csv`:** a local-only test, skipped when `soffice` is missing, and limited to text cells since it exports displayed text. Installing it in CI waits for Phase 4, whose gate needs LibreOffice anyway.

**Security** (the bombs are generated at test run time; no large files are committed):

- **Inflated size:** a sheet that inflates past `MaxUncompressedSize` (64 MB of whitespace under a 16 MB limit) throws `InvalidDataException`, naming the part, after at most the limit plus one read.
- **Part size:**
  - Shared strings or styles past `MaxPartSize` throw.
  - A part whose declared size is already over the limit fails before inflating.
- **Lying headers:** a central directory that understates a sheet's size (patched bytes) still ends in an exception on every target: a guard error, or the malformed XML of a truncated part.
- **Overlapping entries:** several entries sharing one compressed stream are caught by the total.
- **Non-seekable input:** input past `MaxUncompressedSize` stops the temp copy at the limit, and the temp file is gone after the exception.
- **Gap amplification:** a million rows, each with one cell in column XFD, counts each gap field as one byte toward `MaxUncompressedSize` (decision 5), so it hits the limit.
- **Single tokens:** a 300 MB `<v>`, or a tag with a million attributes, stops the window at `MaxPartSize`.
- **Nesting:** a million nested unknown elements inside a cell don't overflow the stack.
- **DOCTYPE:** a DOCTYPE in each part (content types, relationships, workbook, styles, shared strings, sheet) is rejected. Billion laughs and an external entity (`SYSTEM "file:///..."`) both throw before any expansion, and a canary file shows nothing was read.
- **Bad references:** undefined entities and invalid character references (`&#0;`, `&#xD800;`, `&#x110000;`) throw.
- **Not a workbook:** a compound file throws `NotSupportedException`. A `.docx` and random bytes throw `InvalidDataException`.

**Fuzzing:**

- Mutation fuzzing runs in the test suite, seeded and deterministic.
  - It starts from the synthetic and corpus files.
  - It mutates the inflated XML (byte flips, deletions, inserted `<`, `>`, `&`, `"`, `/`, random tag names, truncation) and the zip bytes (local headers, central directory).
  - It reads every sheet to the end, sync and async.
- **Outcome:**
  - Each input must finish within a time limit (5 s), and either succeed or throw `InvalidDataException`, `CsvFormatException` or `NotSupportedException`.
  - Anything else fails the test with its seed: `IndexOutOfRangeException`, `NullReferenceException`, `ArgumentOutOfRangeException`, `InvalidOperationException` from the batch, `OutOfMemoryException`.
- Fuzzing uses small limits (`MaxPartSize` 1 MB, total 16 MB), so runaway growth shows up as a limit error rather than as memory use.
- CI runs a few thousand iterations. An environment variable raises the count for long local runs.
- Coverage-guided fuzzing (SharpFuzz 2.3.0 with libFuzzer) is optional and waits for Phase 9's scanner work.

#### Baseline benchmark

- **Data:**
  - Copy NCsvPerf's `TestData/PackageAssets.csv` (1,695 lines, 25 columns, no header, no quoted fields; MIT, © Joel Verhagen) into `bench/CsvHelper.Benchmarks/Data/`, with its license.
  - Repeat it to 1,000,000 rows the way NCsvPerf's `LineCache` does: 589 full copies, then the first 1,645 lines.
  - Put `DataGenerator.PackageAssetHeader` in row 1, for 1,000,001 rows, which is under Excel's 1,048,576.
- **Generator:** `ExcelDataGenerator.PackageAssets(rows, strings)`.
  - It writes the package with `ZipArchive` (`CompressionLevel.Optimal`) in the shape Excel writes: content types, relationships, workbook, minimal styles, `<dimension ref="A1:Y1000001"/>`, rows with `r` and `spans="1:25"`, cells with `r`, and no cell for an empty field.
  - It has two variants. Shared strings keeps the unique values in first-seen order, with `count` and `uniqueCount`. Inline strings writes `t="inlineStr"`.
  - The result is cached under `%TEMP%\csvhelper-bench`, keyed by row count, variant, and a hash of the CSV and the generator, because building it takes seconds.
  - (Changed at step 9: the writing is `ExcelTextWorkbook.Write(stream, sheetName, rows, strings, level)`, in a file of its own that the test project links, so the cross-check tests the code the benchmark runs. `ExcelDataGenerator` keeps the data and the cache. The generator's hash is of its two source files, embedded in the bench assembly as resources. Excel's file showed `s="1"` on every cell, the text format, so the generator writes it; it also writes each row's `x14ac:dyDescent` and the sheet's `sheetViews`, `sheetFormatPr` and `pageMargins` as Excel does, and `spans` per block of 16 rows, the rule Excel follows, checked through COM.)
- **Cross-check:**
  - A test compares the generator's 1,695-row output with Excel's `excel/packageassets-1695.xlsx`: the same values through our reader and through ExcelDataReader.
  - The XML shapes get compared once by hand. If Excel adds `s` attributes for the text format, the generator writes them too, so the benchmark runs the style lookup.
  - (Changed at step 9: the shapes are compared by the test, not by hand. The sheet from its `dimension` on, the shared strings and the cell formats are identical to Excel's, byte for byte, and the zip entries come in Excel's order.)
- **Class:** `ExcelPackageAssetsRead` in `ExcelReadBenchmarks.cs`.
  - It is a `ReadBenchmarkBase` whose `Generate` returns the CSV text, so its MB/s column is CSV-equivalent throughput, directly comparable with `PackageAssetsRead`.
  - `[Params]` `Strings` is Shared or Inline.
  - Every library reads the same `byte[]` through a `MemoryStream`.
  - Categories: Rows (count them), Strings (every field as a string; the method returns their total length) and Records (a `PackageAsset` per row). Sylvan is each category's baseline, as Sep is in the CSV classes: the reader that claims to be fastest.
  - (Changed at step 9: the CSV reference is three `Csv_` methods in this class, reading the same data as CSV, rather than `PackageAssetsRead`, whose synthetic data differs. The CSV text has LF line ends; NCsvPerf's has `Environment.NewLine`.)

| Library | Version | Rows | Strings | Records |
| --- | --- | --- | --- | --- |
| CsvHelper.Excel | this repo | `Read()` loop | `GetString` per field | `GetRecords<PackageAsset>()` (generated mapper) |
| ExcelDataReader | 3.9.0 | `Read()` loop (skips the header row) | `GetString(i)` | `PackageAsset.Create` through an `IColumnSource` |
| Sylvan.Data.Excel | 0.5.8 | `Read()` loop | `GetString(i)` | `IColumnSource` |
| MiniExcel | 1.46.0 | counts `Query(useHeaderRow: true)` | the row dictionary's values | `IColumnSource` over the row dictionary |
| Open XML SDK SAX | DocumentFormat.OpenXml 3.5.1 | `OpenXmlReader` over the worksheet part, counting `Row` elements | each `Cell`'s attributes and `CellValue` text, with shared strings preloaded into a `string[]` | `IColumnSource` |
| CsvHelper, CSV | this repo | reference only, the same data as CSV: a `Read()` loop (changed at step 9 from citing `PackageAssetsRead`) | `GetString` per field | `GetRecords<PackageAsset>()` (generated mapper) |

- **Setup details:**
  - ExcelDataReader on .NET needs `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`, done in the setup.
  - The bench project already references Sylvan.Data.Csv 1.4.4. Check that Sylvan.Data.Excel's Sylvan.Common dependency resolves next to it.
  - Sylvan's and ExcelDataReader's `ExcelDataReader` type names clash, so the file uses aliases.
- **Equal work:** QuickRunner's `verify` mode checks that every library returns the same row count and a checksum of the strings, so no library skips work.
  - (Changed at step 9: the checksum is the strings' total length, which costs little inside the timing; the Records category's records are hashed in `verify` mode, outside it. QuickRunner ran no `[Params]` before, so it now runs each combination, as BenchmarkDotNet does, and `verify` flags a category whose results differ.)
- **Procedure:**
  - Check the benchmark machine first: paged pool and free RAM.
  - Use `BenchConfig`'s core-4 pin, net10.0, and one BenchmarkDotNet run with every library, so libraries are only compared within one run.
  - The slower readers take seconds per operation, so the whole class runs on the order of an hour; QuickRunner serves for iterating.
  - Record the numbers in "As built". Phase 1 sets no speed target; tuning is Phase 9's.

#### Core changes

None. Phase 0's hooks cover Phase 1:

- `new CsvReader(CsvRecordSource, CsvReaderOptions?)`, visible through `InternalsVisibleTo CsvHelper.Excel`.
- `RecordBatch.AddField`, `GetFieldSpan`, `CommitField`, `EndRecord`, `IsFull` and `FieldCount`. Padding uses `FieldCount`.
- `CsvRecordSource.Fill`, `FillAsync`, `Locate`, `Dispose` and `DisposeAsync`.
- Ragged rows need nothing new: `FieldCountMismatch` defaults to `Allow` and `MissingField` to `ReturnEmpty`. Duplicate header names already resolve to the first, which covers the empty names of leading empty columns.

Considered and avoided:

- **`InternalsVisibleTo CsvHelper.Excel.Tests` in core**, so tests could name `CsvCellKind`. `SheetCellKind` avoids it. If the mirror ever has to go, this becomes a csproj-only change: it adds one assembly attribute and no IL to any method. It would then need the user's OK to verify it with an IL diff of CsvHelper.dll instead of the full benchmark gate.
- **A cell kind for `t="d"` ISO dates.** Phase 1 reads them as Text. Phase 2 settled it without a new `CsvCellKind`: its scanner converts them to serials.
- **A public record-source API.** Still not wanted (decision 3).

Because nothing in core changes, Phase 1 does not run the CSV benchmark gate. The bench project gains a class, a data file and package references; its CSV benchmark classes are not edited.

#### Decisions

These were the open questions. Every recommendation was accepted on 2026-11-02, and question 12 was updated.

1. **One-call API.** Add `ExcelReader.FromFile`/`Create` (and the async forms), returning a `CsvReader` that owns its workbook. It's the common case and mirrors `CsvReader`'s factories; it is now in the API sketch too. Rejected: `ExcelWorkbook` only, or another name.
2. **Default sheet.** The first worksheet in workbook order, hidden sheets included. That is what ExcelDataReader, Sylvan and pandas do. The alternatives are the first visible worksheet, or the active tab (`bookViews/workbookView/@activeTab`).
3. **Sheet index.** 0-based over `Sheets`, which includes chartsheets so that it matches the tab order; choosing a chartsheet throws.
4. **Limits.**
   - Defaults of `MaxPartSize` 256 MiB and `MaxUncompressedSize` 4 GiB, with the semantics under "Public API". 4 GiB covers about four million PackageAssets rows of sheet XML.
   - No compression-ratio check for now. POI's 100:1 default is a known source of false "zip bomb" errors, because rows of styled empty cells compress far better than that. Measure the ratios across the corpus first.
5. **Gap amplification.** One `<c r="XFD1">` makes 16,384 fields from about 30 bytes of XML. A sheet of such rows could keep a reader busy for minutes while inflating only a few hundred MB. Count each gap and padding field as one byte toward `MaxUncompressedSize`. (Phase 2's step 8 found that a shared string repeated does the same with bytes, and counts what each has beyond its cell.)
6. **Empty rows.** The source applies `SkipEmptyLines` itself: it drops valueless rows, or with `false` emits every row so that record n is row n.
7. **Padding.** Pad rows that have a value to the first record's width.
8. **Columns.** Field i is column i, with no clipping before Phase 3.
9. **Exceptions.** The `InvalidDataException`, `CsvFormatException` and `NotSupportedException` split under "Public API". The alternative is a new public `ExcelFormatException`, which adds a type users must learn for little gain.
10. **Thread safety.** A workbook is single-threaded, and its readers can be used in turn on one thread. Later, if wanted, a workbook opened from a path could give each reader its own `FileStream` and `ZipArchive`, so sheets could be read in parallel.
11. **Metadata now.** Expose `Sheets` (name, index, visibility, kind) and `Date1904` in Phase 1. That pulls part of Phase 3's metadata forward; `Dimension`, Tables and defined names stay in Phase 3.
12. **Installs for the corpus.** Done: the user installed Python 3.14.10 and LibreOffice 26.8.2.2 on 2026-11-02 (see "What this machine has"). Node producers (ExcelJS, SheetJS) come in only if a gap remains. (At step 7 the user also approved a portable JDK for Apache POI, unpacked rather than installed.)
13. **Google Sheets, Numbers and Excel for Mac.** The user exports `excel/basic.xlsx` through Google Sheets, and through Numbers or Excel for Mac if they have a Mac. That happens at step 7, once the file exists. Otherwise a few files from POI's and SheetJS's corpora, with licenses recorded. (At step 7's review the user said the exports can't be made. Google Sheets and Excel for Mac files came from POI's test data; SheetJS's test files are blocked on GitHub, and neither has a Numbers file.)
14. **NCsvPerf data in the repo.** Copy it: 519 KB, MIT. The alternative is the bench's synthetic `DataGenerator.PackageAssets`, which has the same columns but isn't NCsvPerf's data.
15. **CI.** Add the pack check for the pin now; add CsvHelper.Excel to `publish.yaml` at v1.0; keep LibreOffice out of CI before Phase 4.
16. **UTF-16 parts.** Support them by transcoding. They're rare and the cost is small.
17. **Beyond Excel's grid.** Reject rows past 1,048,576 and columns past XFD. Excel can't open such files, and the limit bounds gap filling.
18. **Docs.** XML docs are complete from the start. `docs/pages/excel.md` waits for Phase 6, when the API is settled.

#### Risks

- **Async before net10.0** is not fully async: the central directory and local headers are read synchronously, from a local file or a seekable stream.
- **No target checks CRCs.** Corrupt data that still inflates can read as wrong values.
- **The pin target relies on NuGet internals.** The CI pack check catches a change in them.
- **NU1608 lets an application override the pin.** The version guard turns that into a clear error.
- **Inflate bounds read speed,** and .NET 8 and .NET Framework inflate more slowly than .NET 9+. (Measured at step 9 on .NET 10: inflating the benchmark's sheet runs at 5.3–5.6 GB/s of XML, about 5% of CsvHelper.Excel's read of it. The scanning bounds read speed, not inflate.)
- **Shared-strings memory** on huge workbooks. The arena halves it, but it stays proportional to the table.
- **Producer tolerance.** Rejecting out-of-order rows and cells follows the spec and the producers I know; if the corpus shows a producer writing them, revisit.
- **Window growth:** a single huge cell can grow the window up to `MaxPartSize`. (White space between rows can't, since step 8.)

#### Order of work

Each step is reviewable on its own and gets committed when you ask.

1. **Skeleton and packaging (S):**
   - The projects, solution entries and csproj settings.
   - The pin target, with the nuspec checked by hand and the CI pack check.
   - The version guard.
2. **XML layer (M):**
   - `XmlWindow`, `XmlScanner` and `XmlText`.
   - Their tests: one-byte and small-window splits, DOCTYPE, malformed input.
3. **Package (M):**
   - `PackageArchive` (sniffing, temp-file spill), the inflate guard, part names, content types, relationships, the workbook part and styles.
   - The `TestPackage` builder, and part-level limit tests.
4. **Shared strings (S):** the arena, rich text, `rPh` and escapes.
5. **Sheet scanner (M):**
   - `SheetScanner` with `IRecordTarget`.
   - Placement and kind tests through a capturing target.
6. **Record source and public API (M):**
   - `SheetRecordSource`, `ExcelWorkbook`, `ExcelReader`, `ExcelReaderOptions` and `ExcelSheet`, sync and async.
   - The gate tests.
7. **Corpus and oracles (M):**
   - The corpus tool and its files, plus the Python and LibreOffice files if approved.
   - The oracle tests.
8. **Security and fuzzing (S).**
9. **Benchmarks (M):**
   - The data copy, the generator and its cross-check, and the benchmark class.
   - A run on the benchmark machine.
10. **"As built" (S):** the decisions made while building, and the baseline numbers.

#### Gate

As approved:

- `GetRecords<T>`, dynamic records, `CsvDataReader`, Linq and header validation work over a sheet.
- Errors name the cell.
- A baseline benchmark against ExcelDataReader, Sylvan.Data.Excel, MiniExcel and the Open XML SDK, using NCsvPerf's PackageAssets data converted to `.xlsx`.

Added at approval (2026-11-02):

- Every test passes on net10.0, net8.0 and net48, sync and async.
- The oracle tests agree on every corpus file, with each difference documented.
- The security and fuzz tests pass.
- `dotnet pack` shows the exact pin, and a mismatched CsvHelper fails with the guard's message.
- Nothing in core changes.

Met on 2026-11-03, every item; "As built" opens with the evidence for each.

#### Changes elsewhere

Applied at approval (2026-11-02):

- **API sketch:** added `ExcelReader`; gave `ExcelReaderOptions` `SheetIndex` next to `Sheet`; pointed the limits at this phase's "Public API".
- **Phase 3:** "Workbook metadata": the sheets' name, index, visibility and kind moved to Phase 1; dimension stays in Phase 3.
- **Testing:**
  - The LibreOffice oracle runs locally until Phase 4.
  - The oracles have versions.
  - Added the corpus tool and the mutation fuzzing.
- **Layout:** added `tools/CsvHelper.Excel.Corpus` and `bench/CsvHelper.Benchmarks/Data`.
- **Defaults:** added the limits, the default sheet, empty-row handling and padding.
- **Feature coverage:** split "All sheets, and the sheet list with metadata" into the list (Phase 1), and all sheets plus dimensions (Phase 3).

#### As built

Filled in step by step; the code is the reference.

**Summary (step 10, 2026-11-03).** Phase 1 is complete. This summary gathers what the steps below record one by one.

- **What it delivers:**
  - **Public API**, as "Public API" designs it:
    - `ExcelWorkbook`: `Open` and `OpenAsync` from a path or a stream, `Sheets`, `Date1904`, and `OpenReader`/`OpenReaderAsync` for the default sheet, a name, an index or an `ExcelSheet`.
    - `ExcelReader`: `FromFile` and `Create`, sync and async, returning a reader that owns its workbook.
    - `ExcelReaderOptions`: `Sheet`, `SheetIndex`, `Csv`, `MaxPartSize`, `MaxUncompressedSize`.
    - `ExcelSheet` (`Name`, `Index`, `Visibility`, `Kind`), and the two enums.
    - Every read comes back as a plain `CsvReader`.
  - **Internals:**
    - the XML layer (window, scanner, attributes, text, UTF-16);
    - the package layer (archive, inflate guard, part names, content types, relationships, workbook, styles);
    - shared strings;
    - the sheet scanner;
    - the record source over core's batch;
    - the version guard.
  - Size: 6,799 lines of library code, and 8,162 of tests.
  - **Tests:** 689 per target, run on net10.0, net8.0 and net48, every reading test sync and async. They cover:
    - unit tests of each layer, each in every scan mode;
    - comparisons with `XmlReader`- and `XDocument`-based references over generated documents;
    - the corpus against four oracles;
    - the security cases;
    - a seeded mutation fuzzer;
    - the benchmark generator's cross-check.
  - **Corpus:** 25 workbooks CsvHelper.Excel reads, plus three it rejects (`.xlsb`, encrypted, `.xls`). They come from 14 producers: Excel, LibreOffice, openpyxl, XlsxWriter, pandas, Apache POI (XSSF and SXSSF), ClosedXML, the Open XML SDK, EPPlus, MiniExcel, SpreadCheetah, LargeXlsx, Google Sheets and Excel for Mac. The corpus tool regenerates them.
  - **Benchmarks:** `ExcelPackageAssetsRead`, with NCsvPerf's data in the repository.
- **The gate**, item by item:
  - `GetRecords<T>`, dynamic records, `CsvDataReader`, Linq and header validation over a sheet: met by step 6's gate tests.
  - Errors name the cell: met (steps 5 and 6). Sheet errors carry the cell or row in `Location` and in the message, and a field that doesn't parse gives its cell.
  - The baseline benchmark against ExcelDataReader, Sylvan.Data.Excel, MiniExcel and the Open XML SDK: met (step 9, below).
  - Every test passes on net10.0, net8.0 and net48, sync and async: met. On 2026-11-03 the whole solution's tests passed:
    - CsvHelper.Excel.Tests: 689 on each target;
    - CsvHelper.Tests: 1,287, 1,282 and 823;
    - CsvHelper.Linq.Tests: 112, 110 and 101.
  - The oracles agree on every corpus file, with each difference documented: met (step 7). There are 34 listed cells, each with its reason, and Excel was asked about a cell of each reason.
  - The security and fuzz tests pass: met (step 8). That includes a 200,000-input run on each target.
  - `dotnet pack` shows the exact pin, and a mismatched CsvHelper fails with the guard's message:
    - CI's pack check was run again on 2026-11-03: CsvHelper is `[0.0.0-ci]` in all 5 targets, and net47 has System.IO.Compression.
    - The guard was checked end to end at step 6, and its unit tests run in the suite.
  - Nothing in core changes: met. `git diff e5207c4c` (Phase 0's commit) shows no change to `src/CsvHelper`, `src/CsvHelper.Linq`, `src/CsvHelper.Generators` or their tests, in the commits or the working tree. The CSV benchmark gate wasn't needed.
- **Decisions made while building** (each step's "As built" has the details):
  - **XML layer** (step 2):
    - The scanner tracks depth and the root, so that every truncated part throws.
    - A refill is a sync compaction, then one stream read.
    - Escapes are decoded in a second pass over a value's joined text.
  - **One parser for both modes** (step 3): each part's parser is one method with an `async` flag, checked synchronous by `SyncOrAsync`. The large parts, shared strings and sheets, are token state machines that wait only on refills (steps 4 and 5).
  - **Input** (step 3):
    - Paths open with `FileShare.Delete` too, so Excel can still save a file being read.
    - A seekable stream is read from its start.
    - Sheets without a readable part stay in the list, with the reason they can't be read.
  - **Placement** (step 5): the width is the first record with a value. Missing rows and gap fields are counted toward `MaxUncompressedSize` before they are written. Excel's rules for quoting sheet names in a location were found through COM.
  - **Ownership** (step 6): a failed open leaves the caller's stream open, and a disposed workbook stops its readers at their next fill.
  - **White space** (step 7): texts, errors and values are trimmed unless `xml:space="preserve"` applies, as Excel reads them. Checked through COM, this replaced the design's rule of keeping white space.
  - **The corpus** (step 7):
    - Apache POI on a portable JDK in place of NPOI, whose newer releases carry an EULA.
    - Google Sheets and Excel for Mac files from POI's test data.
    - No Numbers file exists in either source.
  - **Corrupt zips** (step 8): `ZipArchive`'s `IOException` and `ArgumentException` on .NET 8 and .NET Framework are reported as `InvalidDataException`.
  - **White space between rows** (after step 8): it is skipped as it comes, so it never grows the window.
  - **The benchmark** (step 9):
    - The generator writes Excel's shape exactly, and a test, not a look by hand, checks it against Excel's file.
    - The CSV reference is read in the same class.
    - QuickRunner runs `[Params]`.
- **The baseline** (step 9's tables):
  - With shared strings, CsvHelper.Excel reads the million-row PackageAssets workbook in 2.7–3.5 s. That is 1.36–1.53x Sylvan.Data.Excel's time, and second of the five readers.
  - With inline strings it is level with Sylvan, within 2%.
  - ExcelDataReader, MiniExcel and the Open XML SDK's SAX reader take 2.4–4.4x Sylvan's time.
  - Inflating is about 5% of the read on .NET 10; scanning is the rest.
- **Left for later phases:**
  - **Phase 2:** typed reading, and whether `t="d"` dates, read as text in Phase 1, need a cell kind of their own (a core change then). Phase 2's design settled it: no new kind.
  - **Phase 3:** dimensions, Tables and defined names.
  - **Phase 4:** LibreOffice in CI.
  - **Phase 6:** `publish.yaml` and the docs page.
  - **Phase 9:**
    - the scanner's per-cell cost;
    - shared strings returned as one string each;
    - coverage-guided fuzzing (SharpFuzz);
    - a zip reader that checks CRCs.

**Step 1: skeleton and packaging (2026-11-02).**

- **Projects:**
  - `src/CsvHelper.Excel` and `tests/CsvHelper.Excel.Tests` are in `CsvHelper.sln`, under its `src` and `tests` folders. `tools/CsvHelper.Excel.Corpus` joined at step 7, under `tools`.
  - The library's csproj follows "Project setup" item by item. It also has `AssemblyInfo.cs` with `[assembly: CLSCompliant(true)]`, as core and CsvHelper.Linq do.
  - Its description covers v1 (reads and writes). Nothing publishes it before Phase 6.
  - The test project targets `net10.0;net8.0;net48`, is signed, and references CsvHelper.Excel, CsvHelper.Linq and the generator (as an analyzer only). It has the net48 binding redirects and `App.config`, as CsvHelper.Linq.Tests does.
  - The oracle packages (DocumentFormat.OpenXml, ClosedXML, ExcelDataReader) aren't referenced yet. They come with step 7, whose tests are the first to use them.
- **Checked by hand:**
  - All the assemblies have CsvHelper's public key token (`8c4959082be5c823`).
  - Core's `InternalsVisibleTo CsvHelper.Excel` lets the assembly in. A throwaway file that used `CsvCellKind`, `CsvRecordSource` and the `ArgumentNullException.ThrowIfNull` polyfill compiled on all five targets, and was then deleted.
- **Pack:**
  - `dotnet pack src/CsvHelper.Excel -p:PackageVersion=0.0.0-ci` gives `<dependency id="CsvHelper" version="[0.0.0-ci]" include="All" />` in each of the five groups. `include="All"` comes from `PrivateAssets="none"`, so the generator reaches applications.
  - net47 gets `<frameworkAssembly assemblyName="System.IO.Compression" targetFramework=".NETFramework4.7" />`. That holds even though no code uses `ZipArchive` yet, because pack reads the `Reference` item, not the compiled references.
  - NuGet notes that the package has no readme, as it does for CsvHelper.Linq.
- **CI pack check:** the "Check CsvHelper.Excel Package" step in `ci.yaml`, after the tests.
  - It packs with `--no-build` and `-p:PackageVersion=0.0.0-ci`, then reads the nuspec from the `.nupkg`.
  - It fails unless every dependency group pins CsvHelper `[0.0.0-ci]` and net47 has the `System.IO.Compression` framework assembly.
  - It counts the groups rather than expecting five, so a change of targets doesn't break it.
  - It was run locally from the YAML text. It passes as is, and it fails, naming what's wrong, with the pin target disabled or with the net47 reference removed.
- **Version guard** (`Internal/VersionGuard.cs`):
  - `EnsureMatch()` is for every way of opening a workbook to call first. Step 6 wires it into `ExcelWorkbook` and `ExcelReader`.
  - Only a passing check is remembered. A failed one runs again, and throws again, on the next call.
  - `Check(csvHelper, excel)` compares the two assemblies' `AssemblyInformationalVersion` without the build metadata after `+`. Prerelease labels count: `34.0.0-beta.1` and `34.0.0` differ.
  - A missing or empty attribute skips the check.
  - The message: "CsvHelper.Excel 34.0.0 needs CsvHelper 34.0.0, but CsvHelper 34.1.0 is loaded. CsvHelper.Excel uses CsvHelper's internals, so it works only with the CsvHelper version of the same release: reference CsvHelper 34.0.0, or CsvHelper.Excel 34.1.0." The second suggestion relies on lockstep releases.
  - The guard touches only the public `CsvReader` type, so it still runs against a CsvHelper whose internals differ.
  - `VersionGuardTests`:
    - The built pair matches.
    - The mismatch tests use dynamic assemblies (`AssemblyBuilder`) with their own informational version as stand-ins for other builds, because one test run loads only one CsvHelper. They cover a mismatch with the exact message, prerelease labels, build metadata ignored, a missing or empty attribute, and `VersionOf`.
  - The gate's "a mismatched CsvHelper fails with the guard's message" is covered at unit level. Step 6 adds a test that each `Open` runs the guard before anything else.
- **Result:** the solution builds in Release with no warnings. Every test passes: the existing counts are unchanged, plus the 10 new tests on each of net10.0, net8.0 and net48.

**Step 2: XML layer (2026-11-02).**

- **Files** in `Internal/Xml`: `XmlWindow`, `XmlScanner` (with `XmlTokenKind` and `XmlScannerState`), `XmlAttributes`, `XmlText`, and `Utf16Stream`, which the layout above now lists.
- **Window:**
  - **The refill is split in two.** `Compact(keepFrom)` is sync: it drops the bytes before the token in progress and doubles the window when that token fills it. `Read`/`ReadAsync` then reads the stream once. So a cancelled async read leaves the window and the scanner consistent, and the next refill just reads again.
  - **One stream read per refill**, never a loop until the window is full. A one-byte stream then really splits tokens at every position. A real inflate stream returns as much as it has, so this costs nothing.
  - **Byte order mark:** the first reads ask for 2 bytes, then a third only after `EF BB`.
    - So nothing past a UTF-16 byte order mark is read before `Utf16Stream` wraps the stream.
    - A UTF-8 byte order mark is dropped, but byte offsets still count it, so they are file offsets.
    - In a UTF-16 part, offsets count bytes of the UTF-8 text.
  - **Size:** the window starts at 64 KB, or at `MaxPartSize` if that is smaller (at least 4 bytes). It grows to at most `MaxPartSize` (capped at `Array.MaxLength`).
    - The bound is exact: a 64-byte tag reads with a 64-byte limit, and a 65-byte one throws.
    - The error names the token's offset: "The XML of test.xml can't be read at byte 0: the token or cell there is larger than ExcelReaderOptions.MaxPartSize (64 bytes)."
- **Scanner:**
  - `TryRead` returns false at the start of a token the window doesn't hold yet, and `Refill` keeps that token. `Read`/`ReadAsync` loop over the two.
  - **Depth and root tracking (an addition).** The scanner counts open elements and knows whether the root has started and ended. It throws for:
    - a part that ends inside an element;
    - a part with no root element;
    - an end tag with no start tag;
    - a second root element.
  - With these, every truncated part throws, which the lying-header tests (step 8) rely on. A truncation test checks it at every length of seeded documents.
  - The depth also drives skipping: `TrySkipTo(depth)` / `SkipTo` / `SkipToAsync`.
    - **Skipped text is consumed as it arrives**, so it needn't fit the window. A 10,000-byte text inside a skipped element reads under a 64-byte limit.
    - Every other token, comments included, must fit the window.
  - **`Save`/`Restore`** (`XmlScannerState`) is for the sheet scanner's rewind to a cell's `<c` (step 5).
    - A test runs that pattern in every mode: save before `<c`, read the cell if the window holds it, otherwise restore and refill.
    - A state is no good after a refill. A refill counter catches that and `Restore` throws.
  - **Errors** are `InvalidDataException`: "The XML of {part} can't be read at byte {offset}: {detail}."
    - Step 6's sheet source will turn errors from scanning into `CsvFormatException` with a `Location`.
    - It leaves errors from refills alone, because those carry the limit and inflate errors, which stay `InvalidDataException`.
  - **Not validating**, as designed. It doesn't check end-tag names, name characters or duplicate attributes, and text or attributes that are never decoded go unchecked. A lowercase `<!doctype` is rejected as unknown `<!` markup rather than as a DOCTYPE.
- **Attributes:**
  - `XmlAttributes` is a plain struct of offsets, so the async read paths can hold one. It reads the attributes one at a time and skips namespace declarations (`xmlns`, `xmlns:*`).
  - Malformed attributes throw only when enumerated: no name, no `=`, or no quotes.
  - The scanner finds a tag's end past quoted values, so a `>` inside a value is fine.
- **Text:**
  - The decoders throw nothing. They return the bytes written, or `~index` of a reference they can't decode, and the scanner adds the part and byte offset.
  - Numeric references must use a lowercase `x` (`&#X41;` is rejected), as XML requires. They may have any number of leading zeros, and don't overflow.
  - **Escapes are a second pass** (a change from the single `IndexOfAny` for `&`, `_` and CR). `DecodeEscapes` runs in place over a value's joined text, after the XML decoding of its pieces. So an escape split across text and CDATA pieces still decodes, and CDATA content gets escapes too. The cost is one more vectorized `IndexOf` per value.
  - The escape needs a lowercase `x`: `_X0041_` is literal.
  - CDATA line ends become LF, as XML does before parsing (and `XmlReader` does); its references stay literal.
- **Tests** (191 per target, all passing on net10.0, net8.0 and net48):
  - **Modes:** most tests run in all 8 `ScanMode`s: whole or one-byte reads, a 64 KB or 16-byte starting window, sync or async.
  - **Oracle:** 200 seeded random documents are compared with `System.Xml.XmlReader` (DTDs prohibited) in every mode. They mix prefixes, namespaces, entities, character references, every kind of line end, CDATA, comments, processing instructions and non-ASCII text, in UTF-8 with and without a byte order mark and in UTF-16 LE and BE. They average about 640 bytes and 16 events, and run up to about 2.9 KB and 71 events.
  - **Truncation:** for 12 documents, every prefix either throws `InvalidDataException` or reads the same as the whole document.
  - **DOCTYPE:** five cases (including billion laughs and an external entity) throw with the DOCTYPE's offset. Through a one-byte stream, the scanner reads exactly the 9 bytes of `<!DOCTYPE` and no more.
  - **Exact error messages** for 30 malformed inputs, for attributes, and for references.
  - Plus: byte order marks, UTF-16 transcoding with code units and surrogate pairs split, window growth and the exact limit, parts much larger than the window, deep nesting (100,000 levels), skipping, save and restore, a cancelled refill retried, and disposal.
  - **Mutation check:** six bugs were planted one at a time and run against the net10.0 tests:
    - "a split text token taken as whole": 14 failures;
    - "an unclosed quote ends the tag": 15;
    - "the window never grows": 20;
    - "CR LF becomes two LFs": 7;
    - "the first read not limited to a byte order mark": 3;
    - "skipped text must fit the window": the tests hang. Without its `position = end`, the skip loop spins at the end of the input; that step is what keeps the "never loops forever" guarantee.

**Step 3: package (2026-11-02).**

- **Files:**
  - `Internal/Package`: `PackageArchive`, `InflateGuard` (with `PartLimits` and `LimitedReadStream`), `PartName`, `ContentTypes`, `Relationships` (with `Relationship`), `WorkbookPart` (with `WorkbookSheet`) and `StyleTable`.
  - New beyond the layout: `Internal/SyncOrAsync.cs` and `Internal/Xml/XmlElements.cs` (below).
  - Pulled forward from later steps: `Internal/Sheet/SheetCellKind.cs` and the two public enums in `ExcelSheet.cs`, because the styles and the workbook part produce them. The `ExcelSheet` class itself comes with step 6.
- **One parser for both modes (a design addition):**
  - Each part's parser is a single `async ValueTask` method that takes a `bool async`. `XmlElements` gives the scanner `ReadAsync(async)`, `ReadRootAsync`, `ReadChildAsync(parentDepth)`, `SkipElementAsync` and `DisposeAsync(async)`.
  - With `async: false`, every read is synchronous, so the task has completed when it returns. `SyncOrAsync.Result` checks that and throws if code that should be synchronous went async. The tests' sync paths all go through it.
  - That saves keeping a sync and an async copy of each parser in step. The cost is one state machine per part, which is nothing next to the part's inflate.
  - The scanner gained `ElementDepth`, the depth of a tag's element whether or not it is empty, which `ReadChildAsync` needs.
- **Input:**
  - **A path opens with `FileShare.ReadWrite | FileShare.Delete`** (the design had `ReadWrite` only).
    - Without `Delete`, Excel couldn't save a workbook while a reader holds it, because its save renames the old file away. That would turn "read a file that is open in Excel" into "block Excel's next save".
    - A test does what Excel's save does: renames the open file away, moves a new one in, deletes the old one. The archive goes on reading the copy it opened. A planted bug that dropped `Delete` failed that test.
  - **A seekable stream is read from its start, whatever its position** (a fix found by a test). `ZipArchive` reads a seekable stream as a whole, from its end and at absolute offsets. Sniffing at the current position read the end-of-central-directory signature when the same stream was opened twice.
  - **Non-seekable input** is copied to `CsvHelper.Excel.<random>` in `Path.GetTempPath()`, with `CreateNew` and `FileShare.None`.
    - On Windows the file has `DeleteOnClose`; elsewhere it is deleted at once, and only the open handle keeps it.
    - The copy uses 80 KB reads and writes, each counted, rather than `CopyToAsync`, so it stops at the read that passes `MaxUncompressedSize`. The partial copy is deleted.
    - An internal `tempDirectory` parameter lets tests check that nothing is left behind.
  - **A failed open disposes only what it created** (a path's `FileStream`, the temporary copy), never the caller's stream, even with `leaveOpen: false`. A successful open with `leaveOpen: false` that needed a copy disposes the caller's stream once the copy is done.
- **Signature:** a seekable or a copied stream gets the same check. Inputs shorter than 4 bytes, and `PK\x07\x08` (a spanned-archive marker), are "not a workbook".
- **Parts:**
  - **Reading order:** content types, the package's relationships, the workbook's relationships, then `workbook.xml`.
  - **Content types:**
    - Matched ignoring case, with parameters (`; charset=...`) dropped.
    - `.xlam` (add-in) is accepted too, as the macro-enabled workbook it is.
    - A content types part that gives the workbook part no type at all is accepted, like a missing content types part.
    - A type that isn't a workbook's (including `application/xml` from a `Default`, when the workbook part has no `Override`) is rejected.
  - **Relationships:**
    - The package's officeDocument relationship is matched by its last segment too.
    - `xlIntlMacrosheet` counts as a macro sheet.
    - A relationship without an `Id`, `Type` or `Target` is ignored, and of two with one id, the first counts.
    - Ids compare exactly (they are XML ids); part names ignoring case.
  - **Sheets with no readable part** stay in the list, in tab order, with the reason in `WorkbookSheet.Problem`, so that opening them (step 6) throws `InvalidDataException` with it. The cases:
    - no `r:id`;
    - an `r:id` that isn't in the relationships part (or no relationships part);
    - a relationship type that isn't a sheet's (the sheet is listed as a Worksheet);
    - an external target;
    - a missing part.
  - **Strict values:**
    - `state` must be `visible`, `hidden` or `veryHidden`.
    - Booleans must be `1`, `0`, `true` or `false` (white space around them allowed).
    - Number format ids must be integers 0 or more.
    - A sheet with no name throws.
    - Each throws `InvalidDataException` naming the attribute and its value, with the part and byte offset.
  - **Sheet names** are XML-decoded (entities, character references, attribute-value normalization). `_xHHHH_` escapes are not decoded in names or format codes: Excel allows no control characters in either, and the Open XML SDK leaves them alone too.
  - **A styles part the workbook names but the package lacks** gives the empty table rather than an error. Styles only tell dates from numbers.
- **Guard:**
  - `PartLimits` carries a call's two limits. `InflateGuard` holds the workbook's running total, which every call's reads add to and check against their own limit.
  - `LimitedReadStream.Charge(bytes)` is there for step 5's gap fields (decision 5).
  - `LimitedReadStream` also checks the archive's disposed flag on every read. So "dispose the workbook, then read" throws `ObjectDisposedException` on every target, whatever `ZipArchive` does with its open entry streams.
- **Messages:**
  - "The workbook has no part {part}."
  - "{part} is larger than ExcelReaderOptions.MaxPartSize ({n} bytes uncompressed): its size in the zip is {m} bytes." This one comes before inflating; without the clause after the colon, it comes from the count.
  - "Reading {part} takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({n} bytes uncompressed)."
  - "The input stream is longer than ExcelReaderOptions.MaxUncompressedSize ({n} bytes), which bounds its copy to a temporary file too."
  - "The package has no workbook: ...".
  - "The package is not a spreadsheet: its main part, {part}, has the content type {type}."
  - "The workbook is an Excel binary workbook (.xlsb), which CsvHelper.Excel doesn't read."
  - The empty-zip, compound-file and not-a-zip messages.
- **Test project:** the net48 build references `System.IO.Compression` for `TestPackage`, as the library does.
- **Tests** (139 new, 330 per target, all passing on net10.0, net8.0 and net48; each loading test runs sync and async):
  - `TestPackage` writes exact entries (names, compression, byte order marks, duplicates, order). Its `Workbook(...)` starts from Excel's parts.
  - `PartName`: normalizing, resolving (relative, absolute, `.` and `..` past the root, percent-encoding, backslashes, fragments), relationships part names, extensions.
  - `PackageArchive`:
    - every signature, for seekable and non-seekable input;
    - the temporary copy (counted, deleted on dispose and on failure, input disposed only as asked);
    - the copy limit (it stops at the read that passes it);
    - a path that others replace while it is open;
    - normalized entry lookup (a check confirms the zip itself keeps the raw names);
    - the declared-size check before inflating (the guard's total is still 0);
    - counting across parts against each call's own limit;
    - counting what a part really inflates to, and `Charge`;
    - disposal.
  - `WorkbookPart`:
    - kinds and states in tab order (chartsheets first and in between), decoded names and every `date1904` form;
    - strict namespaces, `x:` prefixes, LibreOffice's shape (rId3, `true`/`false`, content types last);
    - the fallback without `_rels/.rels`, a workbook part elsewhere with `..` and absolute targets, OPC name matching;
    - every unreadable-sheet case, missing workbook relationships;
    - all five workbook content types, missing or partial content types, `.xlsb` and `.docx`;
    - wrong roots and a DOCTYPE in each of the four parts, UTF-16 BE and a UTF-8 byte order mark;
    - both limits, exactly at and one past.
  - `StyleTable`: cell formats in order without the `cellStyleXfs`, an index out of range, the Number kind, no styles part or a missing one, format codes (first of duplicates, missing ids, white space), invalid ids, a wrong root and a DOCTYPE, the part limit.
  - **Mutation check:** six bugs planted one at a time, all caught on net10.0:
    - "no `FileShare.Delete`": 2 failures;
    - "no declared-size check": 4;
    - "every content type is a workbook": 4;
    - "`cellStyleXfs` read as cell formats": 4;
    - "the last duplicate entry wins": 2;
    - "relative targets resolved from the root": 39.

**Step 4: shared strings (2026-11-02).**

- **Files:** `Internal/Package/SharedStrings.cs`, with its parser nested in it. `PackageArchive` gained `DeclaredSize(part)`, the uncompressed size the zip declares for an entry, which sets the first capacity.
- **A parser that waits only on refills (a design change):**
  - `SharedStrings.ReadAsync` loops `TryParse` over what the window holds, and then refills, synchronously or not. The parsing itself is synchronous. Its state lasts across refills: where it is, what it is skipping, and where the current `<t>` starts. Decoded text is already in the arena, so a refill can come between any two tokens.
  - Step 3's parts walk their elements with `XmlElements`, which awaits a `ValueTask` for each element. That suits small parts, but a shared strings part has several tokens per string, and can have millions of strings. This is the shape the sheet scanner will have.
  - Skipped elements use `TrySkipTo`, which keeps what it skipped across a refill.
- **Decoding:**
  - Escapes are decoded for each `<t>` on its own, over its whole text. XML text, CDATA, and text split by a comment or processing instruction are joined first. An escape doesn't continue into the next run.
  - So `_x00` and `41_` in two runs stay literal. A surrogate pair whose halves are escapes in two runs reads as two U+FFFD. I know of no producer that splits a pair that way.
  - Elements inside a `<t>` are skipped, with their text. Text directly in `<si>` or `<r>` is ignored without being decoded, so a bad reference there is not an error.
- **Capacities:**
  - The first capacities come from `uniqueCount` and from the declared size less 16 bytes of markup per declared string. `uniqueCount` counts for 16 to 1,048,576 strings, and the arena gets 256 bytes to 16 MiB.
  - A `uniqueCount` that isn't a positive 32-bit integer is ignored: it is only a hint, and a wrong hint shouldn't fail a read. `count` (the number of cells that use shared strings) is ignored too.
  - The arrays double as needed. At the end, either is trimmed when more than an eighth of it is spare. `MemorySize` reports what the table holds on to.
  - At the limit, a text whose XML is longer than the room left is decoded into a temporary array first, since its decoded text may still fit.
  - Past the limit, it throws "its strings come to more than {n} bytes, the most the table can hold" or "it has more than {n} strings, the most the table can hold", with the part and the byte offset. `ReadAsync` has an internal `maxLength`, so that tests reach both without 2 GB of XML.
- **Missing part:** no shared strings part, or one the workbook names but the package lacks, gives `SharedStrings.Empty` (its `PartName` is null), as styles do. `PartName` lets step 5's error for an index out of range say which case it is.
- **Test helpers:**
  - `TestPackage.SharedStrings(items)` writes Excel's shape, with `count` and `uniqueCount`. `SharedStringsPart(xml)` writes any XML. Both add the workbook's relationship and the content type.
  - `Packages.LoadSharedStringsAsync` loads the workbook with the default limits and the strings with the test's, so that a small part limit doesn't stop the workbook part.
- **Tests** (24 new, 354 per target, all passing on net10.0, net8.0 and net48):
  - In every scan mode (whole and one-byte streams, 64 KB and 16-byte windows, sync and async):
    - order, and indexes out of range;
    - empty strings keeping their place (`<si/>`, `<t/>`, `<r/>`);
    - Excel's rich text, and a `<t>` before the runs;
    - phonetic runs;
    - white space with and without `xml:space`, and indented parts (kept then; step 7 trims it unless `xml:space="preserve"` applies);
    - XML decoding;
    - every escape case: after `&#95;`, inside CDATA, split by a comment, and across runs;
    - `x:` prefixes and the strict namespace;
    - skipped elements and text;
    - wrong `uniqueCount` values (allocations stay under 8 MB on .NET);
    - growth and trimming under true, zero and huge declared sizes;
    - both table limits, exactly at and one past;
    - a wrong root, a DOCTYPE, an undefined entity, a truncated part, and cancellation.
  - Through a package, sync and async:
    - the part the workbook names;
    - no part, a missing part, and an empty one;
    - UTF-16 LE and BE;
    - `MaxPartSize` (declared, exactly at and one past) and the workbook's total.
  - **Comparison:** 200 generated parts, read in every mode, against a reference built on `XmlReader` that decodes escapes with its own regular expression. The parts have Excel's shape, with rich text, phonetic runs, prefixes, indenting, noise, CDATA, comments and every escape case. Every truncation of 10 more parts throws `InvalidDataException`, in two modes.
  - **Mutation check:** nine bugs planted one at a time, all caught on net10.0:
    - "no escape pass": 3 failures;
    - "escapes decoded over the whole string, not each `<t>`": 2;
    - "phonetic runs read as runs": 4;
    - "text outside a `<t>` kept": 4;
    - "an empty `<si/>` not counted": 4;
    - "a skip inside a `<t>` ends the `<t>`": 3;
    - "arrays not trimmed": 3;
    - "no room check at the limit": 1;
    - "`uniqueCount` trusted past the cap": 1 (the allocation check).

**Step 5: sheet scanner (2026-11-02).**

- **Files:**
  - `Internal/Sheet`: `SheetScanner`, `IRecordTarget`, `CellReference` and `SheetLocation` (split from the layout's `CellReference`, since `Locate` needs the quoting too).
  - `Internal/Xml/XmlError.cs` is new. `XmlScanner` gained `ContentStart` and `TokenStart`.
  - `RecordBatchTarget`, the target over core's batch, comes with step 6, which first puts a reader over the scanner.
  - The scanner owns its `XmlScanner`, and disposes it.
- **Shape:**
  - The scanner is a token-by-token state machine over the window, like the shared strings parser. `Fill` and `FillAsync` loop over a synchronous scan, and only the refills differ.
  - Each mode skips what it doesn't read with `TrySkipTo`, which resumes across refills.
  - A cell is read from its `<c` to the end of its value in one go. When the window runs out first, the scanner restores the state it saved before the `<c`, and the refill keeps the cell. The rest of the cell after its value is skipped across refills.
- **Placement, as designed, with these decisions:**
  - **Width** is the field count of the first record with a value, not of the first record: with `SkipEmptyLines = false`, an empty first row would otherwise make the width 1.
  - **Missing rows** (`SkipEmptyLines = false`) are written before the row's first field.
    - When they fill a fill, the row waits for the next one. The scanner restores to the row's `<row` (or its first `<c`, when the cell's reference gave the number) and reads it again.
    - A row's fields never wait: rows are missing only before a row's first field.
  - **References:**
    - A reference is letters of either case, then digits, within `A1` to `XFD1048576`. A leading zero in the row is accepted (`A01`), as Excel's formulas accept it. `$`, white space and ranges are not references.
    - Past the last row or column without a reference is an error, as is a reference outside the grid.
  - **Counting:** gap fields and padding are counted before they are added, and each missing row before it is written. So a million missing rows stop at the first row past `MaxUncompressedSize`, rather than after all of them are written.
- **Values, with these decisions:**
  - `<is>` is read only for `inlineStr`, and `<v>` for every type; the first of them in the cell is the value. A `t="s"` or `t="str"` cell with an `<is>` is valueless.
  - Numbers, booleans and shared string indexes are trimmed of XML white space; text, errors and dates are not. (Step 7 changed this: they are trimmed too, unless `xml:space="preserve"` applies.)
  - A number, boolean or index whose `<v>` is only XML white space is valueless. A value that only character references make blank (`&#32;`) is an Empty field, since telling it apart would mean decoding before placing the gap fields.
  - The text of `<v>` is decoded as one text: its XML tokens, then its escapes. An `<is>` is decoded `<t>` by `<t>`, as shared strings are.
  - A cell's `s` must be an integer (strict, like step 3's attributes). One past the cell formats means the first (the style table's rule).
- **Errors:**
  - The format is `{Subject} can't be read at byte {offset} of {part}: {detail}.`, where the subject is `Cell Data!B3`, `Row Data!3:3` or `Sheet Data`, with the sheet's name quoted as in a location.
  - `CsvFormatException` carries `Location` (the cell or row, null for the sheet) and `LineNumber` (the row, 0 for the sheet). `RecordNumber` and `ByteOffset` are 0, since the reader doesn't know a sheet's records or bytes there; the message has the part's offset.
  - The XML's own errors keep their detail and get the cell or row the scanner is in, with the XML's `InvalidDataException` as the inner exception. `InvalidDataException` is sealed, so `XmlError` puts the offset and the detail in `Exception.Data`, and the scanner's filter tells them from a limit's.
  - Limits stay `InvalidDataException`: the window's `MaxPartSize` comes from a refill, outside the scan, and the guard's from counting gap fields, which has no XML detail.
  - After either kind, every fill throws the same exception object again: the scanner's state is not to be trusted after one.
  - The messages:
    - "its root element is not worksheet";
    - "its row number, {n}, is not one of a sheet's (1 to 1048576)";
    - "it is not after the row before it, {n}: rows must be in order";
    - "the attribute r=\"{r}\" is not a cell reference (A1 to XFD1048576)";
    - "it is not after the cell before it, {ref}: a row's cells must be in column order";
    - "it is past the last column, XFD";
    - "its value, \"{v}\", is not a shared string index" (quoting up to 32 characters);
    - "it is shared string {i}, past the last of {part}, {n}", "it is shared string {i}, and {part} has no strings", and "it is shared string {i}, and the workbook has no shared strings".
- **Sheet-name quoting, checked against Excel 365 (build 20527) through COM:**
  - A script named a sheet each of about 120 names, set `='name'!A1` on another sheet, and read the formula back. Excel re-quotes it, so the read-back shows Excel's own rule.
  - The plan's rule was right for spaces, `'`, a leading digit and A1 references (within the grid only: `XFE1`, `A0` and `A1048577` are not quoted, while `A01` is).
  - Excel also quotes:
    - names starting with `.`;
    - `TRUE` and `FALSE` in any case;
    - R1C1 references (`R`, `C`, `RC`, `R5`, `C5`, `R1C`, `RC1`, `R1C1`, with numbers up to 1,048,576);
    - names that start like one and go on with a letter or `_`: `R2D2`, `C3PO`, `R1_`, `RC1x`. A column number there must be at most 16,384: `C16385x` is not quoted, though `C16385` is.
  - A `.` after such a start doesn't count (`R1.` is not quoted).
  - Every probed name is in the tests. Lowercase forms (`a1`, `r`) can't sit beside the upper case ones in one workbook, so Excel couldn't confirm them; the tests check them against the rule only.
- **Test helpers:**
  - `CapturingTarget` is a struct over `Captured`, as the reader's will be over its batch. It checks the target's rules on every call:
    - whole records in each fill;
    - at least one record when a fill says more are to come;
    - no more records than a fill takes;
    - no call between `GetFieldSpan` and `CommitField`;
    - no value longer than the room asked for (it gives exactly that much).
  - `SheetMode` is each of the eight scan modes, with fills that take every record and fills that take one: sixteen modes.
- **Tests** (60 new, 414 per target, all passing on net10.0, net8.0 and net48):
  - In every sheet mode:
    - Excel's shape;
    - every cell type, with `true`/`false` booleans and other text;
    - trimming;
    - decoding (entities, CDATA, line ends, escapes per `<v>` and per `<t>`);
    - valueless cells at the start, middle and end;
    - placement by reference, lowercase and with row digits that differ from the row;
    - rows and cells without references;
    - the last column (16,384 fields);
    - padding;
    - empty rows skipped and kept;
    - no rows (no `sheetData`, `<sheetData/>`, an empty root);
    - nothing read after `</sheetData>` (broken XML there reads fine);
    - skipped elements;
    - prefixes and the strict namespace.
  - Errors, in every mode:
    - rows and cells out of order or repeated;
    - references outside the grid;
    - rows past the last;
    - shared string indexes;
    - XML errors at a cell, a row and the sheet;
    - a DOCTYPE;
    - quoted sheet names in locations;
    - the same exception again;
    - `MaxPartSize` staying `InvalidDataException`.
  - Through a package, sync and async: gap fields, padding and missing rows counted to the byte against `MaxUncompressedSize`, exactly at and one past, and a million missing rows stopped at the limit.
  - `CellReference` and `SheetLocation`: column names, parsing (every column round-trips), rejections, and the Excel quoting table.
  - **Comparison:** 100 generated sheets, read in all 16 modes with empty rows skipped and kept, against a reference that applies the rules to an `XDocument` and decodes escapes with its own regular expression. The sheets have rows and cells with and without references, gaps, every type, valueless cells, rich inline text, noise elements, prefixes, the strict namespace and indenting.
  - **Mutation check:** 17 bugs planted one at a time, all caught on net10.0:
    - "no gap fields": 12 failures;
    - "no padding": 9;
    - "an empty shared string is a value": 2;
    - "rows not checked for order": 3;
    - "cells not checked for order": 2;
    - "a row's number not taken from its first cell": 4;
    - "the row goes on after missing rows fill a fill": 2;
    - "missing rows never wait for a fill": 2;
    - "no rewind when a value is not whole": 20;
    - "no escapes in values": 2;
    - "`true` and `false` not booleans": 2;
    - "empty rows always kept": 5;
    - "XML errors not turned into the sheet's": 2;
    - "gap fields not counted": 4;
    - "reading on after `</sheetData>`": 1;
    - "numbers not trimmed": 3;
    - "R1C1 prefixes not quoted": 1.
  - **What the check fixed:**
    - "Reading on after `</sheetData>`" passed at first. The broken XML the test put after `</sheetData>` was inside a tag's attributes, which the scanner never parses for an empty element it skips.
    - The test now puts markup there that the tokenizer rejects at once. It also reads a long tail through a one-byte stream and checks that the stream delivered nothing past the first refill after `</sheetData>`.
  - **Bugs the tests found while building:** both were in the missing rows' pause.
    - After the missing rows filled a fill, the row's own record was written into it too, one past full.
    - Then, when a row whose number came from its first cell was read again, the rest of its missing rows were skipped. `FlushGaps` now runs before every cell, and pauses only after writing missing rows itself.

**Step 6: record source and public API (2026-11-02).**

- **Files:** `ExcelWorkbook.cs`, `ExcelReader.cs`, `ExcelReaderOptions.cs` and the `ExcelSheet` class (beside its two enums), with the signatures under "Public API". `Internal/Sheet/SheetRecordSource.cs`, and `RecordBatchTarget` in `IRecordTarget.cs`.
- **Shape:**
  - Every public method, sync or async, runs one internal method with an `async` flag, as the package layer does. The sync forms go through `SyncOrAsync.Result`.
  - `OpenReader` validates its options, chooses the sheet, and loads the styles and the shared strings the first time, under that call's limits. Then it opens the sheet's part (streamed, so only `MaxPartSize` bounds its window) and returns `new CsvReader(source, options.Csv)`.
  - When the reader's options can't be used (core's constructor throws), the sheet's stream is disposed before the exception goes on.
  - `ExcelReader` opens the workbook, then a reader that owns it. When that reader can't be opened, it disposes the workbook.
- **Options:**
  - `ExcelReaderOptions.Validate` runs on every `Open` and `OpenReader`, with the parameter name `options`. Its messages:
    - "ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't both be set.";
    - "ExcelReaderOptions.Csv can't be null.";
    - "ExcelReaderOptions.MaxPartSize must be more than 0.", and the same for `MaxUncompressedSize` (`ArgumentOutOfRangeException`).
  - The CSV options pass straight to core. `SkipEmptyLines` also goes to the scanner, which applies it itself.
- **Choosing the sheet**, as designed. The messages:
  - "The workbook has no sheet named \"{name}\". Its sheets are \"Chart\" (chartsheet), \"Hidden\", \"Orders\"." Sheets other than worksheets are marked with their kind.
  - "The workbook has {n} sheets, so an index is 0 to {n - 1}." (`ArgumentOutOfRangeException`).
  - "The sheet \"Chart\" is a chartsheet, and only worksheets have cells to read." (`NotSupportedException`).
  - "The sheet \"Orders\" can't be read: {problem}." (`InvalidDataException`, from step 3's unreadable-sheet reasons).
  - "The workbook has no worksheet, and only worksheets have cells to read. Its sheets are ..." (`NotSupportedException`), and "The workbook has no sheets." (`InvalidDataException`).
  - The parameter name is the argument's (`sheetName`, `sheetIndex`, `sheet`), or `options` when the choice came from the options.
- **Ownership:**
  - **A failed open leaves the caller's stream open:** a failed `Open(stream)`, or an `ExcelReader.Create` whose reader can't be opened, leaves a seekable stream open, even with `leaveOpen: false`.
    - `PackageArchive.LeaveInputOpen()` is new for this. A failed archive open already worked that way (step 3); now a failure after it does too.
    - A non-seekable stream was disposed once its copy was made, as `leaveOpen: false` says; the copy is deleted.
  - **A disposed workbook stops its readers:** the source checks the workbook before each fill, and throws `ObjectDisposedException` ("The workbook is disposed."). So the design's "next read" is the next fill: the records a reader holds already can still be read.
  - **Disposing a reader** disposes its sheet's stream. With `ExcelReader`, it also disposes the workbook, and so its file or stream.
- **`Locate`** returns `null` for a line outside 1 to 1,048,576, and a row's location for a field past `XFD`, so that it can't throw; neither happens with the scanner's records.
- **`Sheets`** is a `ReadOnlyCollection<ExcelSheet>`, so it can't be cast back to an array and changed. `ExcelSheet.ToString()` is its name.
- **Version guard:**
  - `VersionGuard.EnsureMatch()` runs first in every `Open`, before the options are checked. Only a pass is remembered.
  - **Checked end to end:**
    - A console app referenced CsvHelper.Excel, and opened a workbook with the matching CsvHelper.
    - Then CsvHelper was rebuilt with `-p:InformationalVersion=99.0.0-mismatch` (the same assembly version, so it binds), and its DLL was swapped into the app's output.
    - The app's `Open` then threw `InvalidOperationException`: "CsvHelper.Excel 1.0.0 needs CsvHelper 1.0.0, but CsvHelper 99.0.0-mismatch is loaded. CsvHelper.Excel uses CsvHelper's internals, so it works only with the CsvHelper version of the same release: reference CsvHelper 1.0.0, or CsvHelper.Excel 99.0.0-mismatch."
    - With the matching DLL back, it opened again.
  - That needs two builds of CsvHelper, so it isn't in the test suite; step 1's tests cover the check itself.
- **Tests** (48 new, 462 per target, all passing on net10.0, net8.0 and net48, each sync and async):
  - **The gate:**
    - `GetRecords<T>` and `GetRecordsAsync<T>` with generated mappers (text, numbers, decimals, booleans and empty cells);
    - `CsvDynamicRecord`;
    - `CsvDataReader` with `DataTable.Load`;
    - `reader.Query<T>()`;
    - `ParallelEnumerate` over 5,000 rows in 256-byte batches;
    - header validation (`KeyNotFoundException`).
  - **Errors name their cell:**
    - a field that doesn't parse gives `CsvFormatException` with `Location` (`Data!B3`, and `'Q1 Orders'!B3`), `LineNumber` and the location in the message;
    - `OnRecordError` gets the same `Location` and can skip the record;
    - a sheet's own error (rows out of order) comes out of the read, and no handler sees it.
  - **The workbook:**
    - the sheets in tab order with visibility and kind, and `Date1904`;
    - a path (closed after dispose), and a stream that can't seek;
    - `leaveOpen`, and a failed open leaving the stream open;
    - options checked on `Open` and `OpenReader`, and a reader's options replacing the workbook's;
    - each call's limits (a reader's `MaxPartSize` stops the shared strings, which, once loaded, are kept, and stops a large cell);
    - readers disposed and opened again;
    - a disposed workbook stopping its readers, and three readers of two sheets read in turn on one thread, in small batches.
  - **Choosing the sheet:**
    - the default past a chartsheet to a hidden worksheet;
    - by name ignoring case, by index, by `ExcelSheet`, and by the options;
    - an argument winning over the options;
    - every error, with its parameter name.
  - **`ExcelReader`:** a file closed with the reader, and not held when the reader can't be opened; a stream disposed unless left open, and left open on failure.
  - **CSV options:**
    - `SkipEmptyLines = false` (a missing row is an empty record), `HasHeader = false` and `TrimWhitespace`.
    - Numbers parse invariantly under a `de-DE` reader while its text cells use `de-DE`.
    - With empty rows skipped, a row a million down reads under a 100 KB limit, which the missing rows pass when they are kept.
  - **Mutation check:** 14 bugs planted one at a time, all caught on net10.0:
    - "the options' sheet wins over the one given": 2 failures;
    - "hidden sheets skipped by default": 4;
    - "names matched with case": 2;
    - "another workbook's sheet accepted": 2;
    - "a reader's options ignored": 16;
    - "a reader's options not checked": 2;
    - "styles and strings read again for each reader": 2;
    - "a failed open disposes the caller's stream": 2;
    - "a reader of OpenReader owns the workbook": 12;
    - "a reader of ExcelReader doesn't own the workbook": 4;
    - "empty rows kept whatever the options": 2;
    - "no disposed check before a fill": 2;
    - "fields located as rows": 2;
    - "kinds lost on the way to the batch": 2.
  - **What the check fixed:** three of the bugs passed at first, each through a test that couldn't tell.
    - "Empty rows kept whatever the options": core's reader drops the empty records anyway. The new test shows that the missing rows are then counted against the limit.
    - "No disposed check before a fill": the part's stream also throws once the workbook is disposed, so a large sheet hid the missing check. The test now also reads a sheet the window holds whole, which no stream read would stop.
    - "Kinds lost on the way to the batch": numbers parse the same as text under the invariant culture. The new test reads them under `de-DE`.
  - The version guard's call in `Open` can't be planted out in-process (both versions match there); the end-to-end check above covers it.

**Step 7: corpus and oracles (2026-11-02).**

- **Files:**
  - `tools/CsvHelper.Excel.Corpus`, a net10.0 console in `CsvHelper.sln` under `tools`, not packed: `Program.cs` (the commands), `Basic.cs` (the content), `ExcelProducer.cs`, `Privacy.cs`, a file per .NET producer with `DotNetProducers.cs`, `PoiProducer.cs`, `python/corpus.py` with its `requirements.txt`, and `java/Corpus.java` with its `jars.txt`.
  - `tests/CsvHelper.Excel.Tests/Files`: 24 workbooks, about 550 KB, with a `README.md`. It records each file's producer, version and how it was made, what each producer writes, and the download's source and license.
  - `tests/CsvHelper.Excel.Tests/Corpus`: `CorpusFiles`, `Oracles`, `LibreOffice`, `OracleTests` and `CorpusTests`.
  - The test project gained the three oracles and copies `Files/` to its output.
- **The tool:**
  - Its commands are `excel`, `packageassets`, `libreoffice`, `python`, `poi` and `dotnet`, each writing one producer's files; `spec`, which prints the content as JSON for the Python and Java producers; `check`; and `all`.
  - Excel runs through late-bound COM (`dynamic`). After `SaveAs`, Excel stays running for seconds to minutes and then exits by itself.
  - LibreOffice runs headless in a throwaway profile.
  - Python comes from the PEP 514 registry entry. It runs in a venv outside the repository, with `-I`.
  - Java comes from `--java`, or the newest `jdk-*` folder in `%LOCALAPPDATA%\CsvHelper.Excel.Corpus`, where the venv is. The JDK's source launcher runs `Corpus.java` without a build step, which is why it's a JDK and not the smaller JRE.
  - The tool pins System.Drawing.Common, System.Security.Cryptography.Pkcs and System.Security.Cryptography.Xml to 10.0.14. The producers' own versions pull in vulnerable ones (NU1902 to NU1904).
- **Privacy (found while building):**
  - Excel wrote the Office account's email address into `docProps/core.xml` (`dc:creator`, `cp:lastModifiedBy`), and the folder it saved to into `workbook.xml` (`x15ac:absPath`). That was caught before anything was committed.
  - Now Excel saves with `RemovePersonalInformation`, into `%PUBLIC%\CsvHelper.Excel.Corpus`, and the tool copies Excel's bytes. The other producers get the author `CsvHelper.Excel.Corpus`.
  - `check` runs at the end of every command. It reads every part of every file as UTF-8, Latin-1 and UTF-16, and fails on the user's or machine's name, the profile folder, or any email address.
- **Corpus decisions** (changes from the design):
  - **Apache POI itself, and not NPOI** (the user's decision at this step's review):
    - NPOI's releases from 2.8.0 (April 2026) carry an "Open Source Maintenance Fee" EULA. The package's build warns until `AcceptNPOIOSMFLicense` is set, but the EULA says that using the binary accepts it.
    - In the tool, NPOI would have reached every CI run and every contributor's build of the solution. So would a committed acceptance, made on their behalf.
    - NPOI 2.7.6 (February 2026) was the alternative: the last release under plain Apache-2.0, checked to have no EULA. But NPOI is a port of POI, and the corpus would have been stuck at that version.
    - POI is the producer people meet: Java systems' exports. So the user approved a portable JDK (Eclipse Temurin 25.0.5+8, a 141 MB zip unpacked to 291 MB) in the tool's folder outside the repository, checked against Adoptium's SHA-256.
    - POI 6.0.0 needs Java 17, and no longer ships a binary distribution: its jars are only on Maven Central. Without Maven, `jars.txt` pins the 13 jars of its runtime dependencies, resolved from POI's POMs as Maven would (log4j-api's version comes from the log4j-bom that POI's POM imports). The tool downloads each missing jar and checks its SHA-256, which was pinned after each jar matched Central's own `.sha512` or `.sha1`.
    - `Corpus.java` reads the spec's JSON with a small reader of its own, since Java has none. It writes `poi/xssf.xlsx` and `poi/sxssf.xlsx`, with POI's evaluator for the formulas' results.
    - POI can't create a chartsheet or a phonetic run. SXSSF writes rich text as plain text.
  - **The Open XML SDK writes only the `x:`-prefixed variant.** `AddNamespaceDeclaration("")` throws, and a root parsed with a default namespace is still written prefixed.
  - **LargeXlsx has one file.** With `requireCellReferences: false` it wrote the same bytes.
  - **Excel's workbook has no phonetic run.** Without the Japanese editing language, Excel ignores `Phonetics`, `SetPhonetic` and `PhoneticCharacters` through COM. The Open XML SDK and ClosedXML write `<rPh>`.
  - **The boolean rows' labels are "boolean true" and "boolean false".** Excel turns the text `true` into a boolean when a cell is set to it.
  - **A time alone is a number in a time format.** Producers that count from Excel's epoch wrote `DateTime(1899, 12, 30, ...)` as a negative serial.
  - **Not made yet:** Google Sheets, Numbers and Excel for Mac (decision 13). The user can't export through them, so Google Sheets and Excel for Mac came from POI's test data after this step's commit (see below), and Numbers not at all. The copy of NCsvPerf's CSV is step 9's.
- **The white space rule (a design change):**
  - The design kept the white space of texts. Excel 365 (build 20527) reads them otherwise. Workbooks of cases were opened through COM, and each cell's `Value2` read back.
  - What Excel does:
    - It trims space, tab, CR and LF from the ends of a `<t>` or `<v>` unless `xml:space="preserve"` applies: on it, or inherited from the `si`, `r`, `is`, `c`, `row`, `sheetData` or root. Each of those was checked, and `xml:space="default"` ends an inherited one.
    - It trims each run on its own: `bold ` and ` plain` read `boldplain`.
    - It trims spaces that come from character references or CDATA, but not `_x0020_`.
    - It reads a text of only white space as an empty string.
    - It trims errors as it trims text. It won't open a workbook where `preserve` keeps spaces around a number, boolean, shared string index or error, though the attribute alone is fine.
  - The library now follows it:
    - `XmlScanner.SpaceOf()` reads a start tag's `xml:space`, through the new `XmlAttributes.IsQualifiedName`. `XmlText.TrimWhitespace` trims in place.
    - The shared strings parser tracks whether `preserve` applies at the root, the `si` and the `r`. It trims each `<t>` after its XML and before its escapes.
    - The scanner tracks it for the root, `sheetData` and the row, and reads it on `c`, `v`, `is`, `r` and `t`. Each group of text segments is marked to trim.
    - `HasText()` decides whether a cell is valueless before decoding: a trimmed text of only raw XML white space is valueless. One that references make blank is still an Empty field, for the reason step 5 gives.
  - The test references follow it too. The Open XML SDK oracle uses the nearest `xml:space`, and both comparison tests' generators put random `xml:space` on `si`, `r`, `t`, rows and `v`.
- **Oracles:**
  - CsvHelper.Excel's side reads each worksheet through its own layers (`CorpusFiles.Read`), so that kinds are compared, not only text.
  - **The Open XML SDK** gives the DOM's raw cells, read by the documented rules (placement, valueless cells, trimming, booleans). Escapes are decoded by a regular expression, and phonetic runs are left out.
    - It must match exactly, and it does on all 21 workbooks.
    - That also confirms that the SDK leaves `_xHHHH_` undecoded, as the design expected: decoding twice would read `_x005F_x0041_` as `A`.
  - **ClosedXML** gives typed values, and dates compare as serials. On `excel/strict.xlsx` it throws `FormatException` at Excel's `t="d"` dates (17 digits of fraction), and the test expects that.
  - **ExcelDataReader** gives each row's values. On .NET it needs `CodePagesEncodingProvider`.
  - **Dates from both:**
    - Like Excel, they count 1900 as a leap year, so a serial below 61 comes back a day later than its OLE Automation date. `Oracles.Serial` takes that day back, and subtracts 1,462 days in a 1904 workbook.
    - A .NET date from a serial is rounded to the millisecond, so dates compare within a few milliseconds.
  - **LibreOffice** converts every workbook in one headless run, to a CSV per sheet. Only text cells are compared, since it exports what cells show.
  - **On .NET Framework**, the Open XML SDK and ClosedXML can't open `poi/sxssf.xlsx`. They use WindowsBase's `System.IO.Packaging` there, and it rejects SXSSF's zip as corrupt: a Zip64 data descriptor after each entry, behind local headers that say version 45 without a Zip64 field. The net48 tests check that they throw `FileFormatException`. Excel opens the file, and CsvHelper.Excel (`ZipArchive`) reads it on every target.
  - **Differences:** each one is listed by cell, with its reason, and each test checks that the differences are exactly those. Excel was asked through COM about a cell of each reason, and it reads each as CsvHelper.Excel does.
    - ClosedXML, 17 cells in 7 files:
      - it returns the line break of an inline or `str` text as CR LF, where Excel keeps LF;
      - it doesn't decode the escapes of an inline or `str` text, where Excel gives `A`, U+0001 and the emoji;
      - it keeps the spaces of a text without `preserve`, where Excel trims them.
    - ExcelDataReader, 10 cells in 2 files:
      - it converts `t="d"` cells to dates, which Phase 1 leaves as text (Phase 2 converts them);
      - it reads only `1` as true, while the Open XML SDK writes `true`, and Excel reads that as TRUE.
    - LibreOffice, 7 cells in 3 files: it shows `t="d"` cells in their date formats, doesn't decode the escapes of a `str` value, and keeps the spaces of a text without `preserve`.
- **What the producers write:** the corpus's `README.md` has the list. These are the shapes that matter most:
  - `x:` prefixes (the Open XML SDK, ClosedXML, MiniExcel), and EPPlus's runs with a `d:` prefix declared on each;
  - SpreadCheetah's cells without `r`;
  - inline strings (openpyxl, SpreadCheetah, LargeXlsx, XlsxWriter's `constant_memory`), and MiniExcel's text as `t="str"`;
  - openpyxl's formulas without cached values;
  - a literal `_x0041_` written unescaped by the Open XML SDK, openpyxl, POI and SpreadCheetah, so that every reader, Excel included, reads `A`;
  - Excel's Strict dates as `t="d"`;
  - SpreadCheetah's spaces without `preserve`, which Excel trims;
  - POI's numbers in Java's form (`42.0`, `1.0E-5`), and its `?` for U+0001;
  - SXSSF's stray `>` between an error formula's `</f>` and its `<v>`, which Excel ignores, as every reader here does, and its Zip64 data descriptors.
  - Excel was asked through COM about the unusual cells of both POI files, and read each as CsvHelper.Excel does.
- **Tests** (131 new, 593 per target, all passing on net10.0, net8.0 and net48; the LibreOffice tests run here and skip where `soffice` is missing):
  - `CorpusTests`:
    - the list of workbooks matches the table of each producer's sheets;
    - every worksheet of every workbook is read through the public API, sync and async, with empty rows kept, so that record n is row n and each field is checked against its cell;
    - each producer's sheets (name, visibility, kind) and `Date1904`;
    - `.xlsb`, encrypted and `.xls` files rejected with their messages;
    - Excel's basic workbook read as records, with its header, against golden records.
  - `OracleTests`: the four oracles on each of the 21 workbooks.
  - White space: `SharedStringsTests.TrimsATextUnlessItsSpaceIsPreserved` and `SheetScannerTests.TrimsValuesUnlessTheirSpaceIsPreserved`, with Excel's cases, replace step 4's and step 5's white space tests.
  - Every other test in the solution passes too (CsvHelper.Tests and CsvHelper.Linq.Tests unchanged), and the solution, the tool included, builds with no warnings.
- **Mutation check** (before the POI files were added): seven bugs in the white space rule, planted one at a time, all caught on net10.0:
  - "shared strings not trimmed": 3 failures;
  - "a shared string's `preserve` not inherited": 3;
  - "shared strings trimmed after their escapes": 4;
  - "cell values not trimmed": 6, the four oracle tests among them;
  - "a row's `xml:space` ignored": 2;
  - "blank text counted as a value": 2;
  - "`default` read as `preserve`": 5.
- **What the check fixed:**
  - At first, only the generated comparison caught "blank text counted as a value". In the direct test, an Empty field and a valueless cell look the same once the row is padded.
  - The test now has a row whose texts are all raw white space, which is dropped, and a row that a reference makes blank, which is kept.
- **Left for step 9:** the cross-check of `excel/packageassets-1695.xlsx` against the generator.
- **Core:** no change.

**Step 7, after its commit: Google Sheets and Excel for Mac files (2026-11-02).**

- **Sources:**
  - The user can't export `excel/basic.xlsx` through Google Sheets, Numbers or Excel for Mac, so the files come from test data, as decision 13 allowed.
  - SheetJS's test files are blocked on GitHub ("Repository access blocked", reason "private_information", since 2024-07-19). So nothing is taken from them, or from a copy of them.
  - Apache POI's `test-data/spreadsheet`, at commit `81ab52621cb3ecaf9cb96a5a92edcb2e5fd60408`, is under POI's Apache License 2.0; its LICENSE excludes nothing there. The corpus README records the license and POI's NOTICE.
- **Choosing:**
  - All 353 of POI's `.xlsx` files were surveyed for their producer: `docProps/app.xml`'s application, `fileVersion`, and the sheets' default sizes.
  - Google Sheets' files have no `docProps`, a theme named "Sheets", Arial, and its default column widths (14.43, or 12.63 in newer exports). POI has three; the two without a person's name are in.
  - Excel for Mac ("Microsoft Macintosh Excel") wrote 30, and all but three name a person. Of those three, two record only "Microsoft Office User" or "Apache POI", and one `IEUser`, the stock name of Microsoft's test VMs. One of the first two records a person's work email in its threaded comment's `persons` part, so the other two are in: a 16.0300 file and a 15.0300 file.
  - No file in POI's test data comes from Numbers.
  - POI also has files from Excel Online, ONLYOFFICE, Hancom Hancell, the ODF Converter and Access. They're not in the corpus.
- **The tool:** `downloads` fetches each file that `downloads.txt` lists (path, URL at the pinned commit, SHA-256) when it is missing or doesn't match. It shares its checked download with POI's jars. `all` runs it last.
- **What they turned up:**
  - `google/64450.xlsx` has a relationship with no `Id` or `Target` in its pivot cache's relationships (POI's bug 64450). `System.IO.Packaging` rejects the package on every target, so the Open XML SDK and ClosedXML can't read it; the tests check their `XmlException`. Excel opens it. CsvHelper.Excel ignores such a relationship (step 3), and never reads pivot caches.
  - Its pivot table's sheet is written as seven empty rows, without the table's cells. Excel builds the cells from the pivot table when it opens the file, but every reader of the sheet's XML, CsvHelper.Excel included, reads the sheet as empty. Pivot tables are out of scope.
  - Apart from that, the oracles agree with CsvHelper.Excel on every cell. Excel, asked through COM, reads every other cell of the four files as their XML holds it.
- **Tests:** 24 new, 617 per target, all passing on net10.0, net8.0 and net48.

**Step 8: security and fuzzing (2026-11-02).**

- **Files:**
  - `tests/CsvHelper.Excel.Tests/Security`:
    - `SecurityTests`, the plan's security cases end to end through the public API;
    - `FuzzTests`, the mutation fuzzer;
    - `ZipBytes`, which patches a zip's headers;
    - `ReadTrackingStream`, which records the byte ranges a reader reads.
  - `TestPackage.SetStreamed` writes an entry from a callback straight into the zip's deflate stream, so that a bomb's inflated bytes are never held. The bombs are made at test time; nothing large is committed.
- **What the earlier steps already covered** (not repeated here):
  - a DOCTYPE in each of the six parts, and billion laughs and external entities stopping the scanner right after `<!DOCTYPE`, with no byte read past it (step 2);
  - the declared-size check, counting what a part really inflates to, and the non-seekable copy's limit and deletion (step 3);
  - gap fields, padding and missing rows counted to the byte (step 5);
  - compound files, `.xlsb` and other packages (steps 3 and 7).
- **Security tests**, each sync and async:
  - **Inflate bomb:** 64 MB of white space between two rows, about 64 KB deflated, under a 16 MB limit, throws the limit's `InvalidDataException`, naming the part.
    - "After at most the limit plus one read" is measured on the compressed side: the reader got no further into the sheet's compressed data than twice the limit's share of it, plus 16 KB for the inflater's input buffer.
    - The record of what was read starts after the workbook opens. `ZipArchive` reads 4 KB blocks backwards from the end of the file, looking for the central directory, and in a small zip those reach into the last entries' data.
  - **A held part too large:** 64 MB of shared strings under a 1 MB part limit fail on the declared size, before any of the part's compressed data is read.
  - **Sizes the zip understates:** the sheet (20 MB under a 16 MB total) or the shared strings (2 MB under 1 MB), with the uncompressed size patched to 100 in both headers, or the compressed size halved. Every case ends in an error on every target:
    - .NET 8 and 10 stop inflating at the declared uncompressed size, so those cases end as truncated XML ("it ends inside a tag", "it ends inside an element").
    - .NET Framework ignores the declared size and inflates everything, so the count stops it: the window's `MaxPartSize`, or the shared strings' part limit.
    - A halved compressed size ends at the part limit on every target.
  - **Entries that share one entry's data:** twelve sheets whose central records point at the first sheet's data. Each sheet reads as the first, each read counts toward the workbook's total, and the seventh sheet stops at the limit.
  - **Gap amplification:** a million rows of one cell in XFD under a 16 MB limit stop within the first 1,024 rows, in about 0.4 s.
  - **Single tokens:** a 300 MB `<v>`, and a cell with a million attributes (about 11 MB), stop at a 4 MB `MaxPartSize` with the window's error. The reader got no further into the compressed data than twice the limit's share.
  - **Nesting:** a million unknown elements nested in a cell, in `sheetData`, or in a shared string are skipped, and the value after them is read.
  - **External entities and billion laughs** in the workbook, styles, shared strings and sheet parts are rejected at the DOCTYPE, with the part's name in the message.
    - The external entity points at a file held open with `FileShare.None`, so any attempt to open it would throw a sharing violation instead. That is the plan's canary.
  - **References that aren't characters** (`&#0;`, `&#xD800;`, `&#x110000;`, an undefined entity) in a cell throw `CsvFormatException` at `Data!A1`.
- **Fuzzer:**
  - **Sources:** three synthetic packages (one with every kind of cell, the orders workbook, and numbers), and the 25 corpus workbooks CsvHelper.Excel reads.
  - **Mutations:** each seed picks a source, then applies one to four mutations.
    - Two times in three, it mutates an XML part (sheets and shared strings three times in four). The mutations are bit flips, byte changes, deletions, inserted markup from a list of the scanner's delimiters, elements and edge cases, copies of a range elsewhere, extreme numbers in place of a run of digits, and truncation. The part is then zipped again, stored or deflated.
    - Otherwise it mutates the zip's bytes: a byte of a local header, a central record or the end record; a byte anywhere; or truncation.
  - **Reading:** every worksheet is read to its end, every field through `GetString`, under 1 MB and 16 MB limits. Seeds alternate sync and async, and vary `HasHeader`, `SkipEmptyLines` and trimming.
  - **Outcome:** each input must finish within 5 s, and either read or throw `InvalidDataException`, `CsvFormatException` or `NotSupportedException`.
  - **Runs:** 3,000 inputs by default, in about 2 s on net10.0, 2 s on net8.0 and 3 s on net48. `CSVHELPER_EXCEL_FUZZ_INPUTS` sets the count, and `CSVHELPER_EXCEL_FUZZ_SEED` the first seed, so that a failure, reported with its seed and mutations, replays alone.
  - **A long run:** 200,000 inputs on each target passed in 1 min 16 s (net10.0), 1 min 59 s (net8.0) and 4 min 7 s (net48). About 40% read to the end, 29% failed at a sheet with `CsvFormatException`, 9% at a sheet with `InvalidDataException`, and 22% as a workbook with `InvalidDataException`.
- **What fuzzing found** (a library fix in `PackageArchive`, not core):
  - On net8.0 and net48, `ZipArchive` throws `IOException` ("Zip file corrupt: unexpected end of stream reached") when a central record runs a little past the end of the archive. On net48 it also throws `ArgumentException` ("Illegal characters in path") for an entry name with a character Windows paths forbid. .NET 10 throws `InvalidDataException` for both, or accepts the name.
  - `PackageArchive` now reports both as `InvalidDataException`: "The workbook's zip archive is corrupt: {message}", with the original inside. The check is narrow:
    - only an `IOException` of exactly that type, thrown by `System.IO.Compression` itself, so that the stream's own I/O errors still pass through, as "Public API" says;
    - only an `ArgumentException` raised inside `System.IO.Compression`.
  - It reads the stack trace's text rather than `TargetSite` or `StackFrame.GetMethod`, which would bring trimming warnings.
  - A failed open now disposes the `ZipArchive` it made, too.
  - Two tests pin both cases down without the fuzzer: a central record whose extra field runs 8 bytes past the end, and a `<` in an entry's name (on .NET, that name just reads as a missing styles part).
- **A finding, changed after the commit (see below):** white space between rows was a text token like any other, so it had to fit in the window. A long run of it grew the window up to `MaxPartSize`, and past that failed with the window's error, though the scanner could skip it without holding it, as the XML layer's own skip already did. Memory stayed within the limit, which is what "Public API" and "Risks" promise.
- **Tests:** 47 new, 664 per target, all passing on net10.0, net8.0 and net48. The whole solution builds without warnings, and the other suites pass.
- **Mutation check** on net8.0, five bugs planted one at a time, all caught:
  - "corrupt zips not reported as InvalidDataException": 3 failures;
  - "the workbook's total not checked": 6;
  - "gap fields not counted": 2;
  - "no declared-size check": 2;
  - "the window grows past MaxPartSize": 6.
- **What the check fixed:** at first, only the fuzzer caught "corrupt zips not reported". The deterministic test declared 65,535 bytes of extra field, which hits net8.0's own "Central Directory is invalid" check, an `InvalidDataException`. A scratch probe showed that only a field running a little past the end gives the `IOException`, so the test now declares 30 bytes.
- **Left for Phase 9:** coverage-guided fuzzing (SharpFuzz).
- **Core:** no change.

**Step 8, after its commit: skipping the text between elements (2026-11-03).**

- **The change** (the user's choice, over leaving it for Phase 9):
  - `XmlScanner.TrySkipText()` skips the text at the scanner's position up to the next `<`, as it comes, like `TrySkipTo`'s skipping of text: what is skipped is dropped at the next refill.
  - At the end of what the window holds, it asks for a refill unless the part has ended, since more text may follow.
  - The sheet scanner calls it before each token it reads outside a cell: before the root, in the worksheet, in `sheetData` and in rows. All text there was ignored already; now it is never held.
  - A cell is read whole, as before, its white space included.
  - CDATA, comments and processing instructions between rows are still tokens, which must fit in the window.
- **What it changes:** a sheet's memory no longer grows with the white space between its rows. A long run of it now fails only at `MaxUncompressedSize`, as other inflated bytes do, and never at `MaxPartSize`.
- **A bug the tests found while building:** at first, at the end of the window `TrySkipText` reported that it was past the text, though a refill could bring more. The sheet scanner recovered, since it calls it again after each refill, but the XML layer's own tests failed.
- **Tests** (3 new, 667 per target, all passing on net10.0, net8.0 and net48; 30,000 fuzz inputs passed on each):
  - `XmlScannerTests`:
    - text skipped up to markup across refills, without growing a 64-byte window, with CDATA left as a token;
    - text that ends inside an element, whose error comes from the next read.
  - `SheetScannerTests`: 100 KB of white space before `sheetData`, between rows and between cells reads with a window that can't grow past 1 KB, in every mode; the same white space inside a cell still needs the window.
  - `SecurityTests`: the inflate bomb now also has a 64 KB part limit, which the white space would pass first if it were held.
- **Mutation check** on net8.0, three bugs planted one at a time, all caught:
  - "white space not skipped in the sheet": 3 failures;
  - "`TrySkipText` says it's past the text at the window's end": 2;
  - "skipped text kept in the window": 5.
- **What the check fixed:** a fourth bug, "text skipped before the root too", passed every test. The scanner doesn't check text outside the root, though my first comments said it did, so skipping it there changes nothing. The sheet scanner now skips text in every mode outside a cell, and the comments say what it does.
- **Core:** no change.

**Step 9: benchmarks (2026-11-03).**

- **Data:**
  - `bench/CsvHelper.Benchmarks/Data/PackageAssets.csv` is NCsvPerf's file at commit `c7bff62`, byte for byte as committed there: 517,049 bytes with LF line ends. Decision 14's 519 KB was its size with CRLF.
  - Beside it are `LICENSE-NCsvPerf.txt` (the MIT text) and a `README.md` with the source and the SHA-256.
  - The corpus tool's `--csv` now defaults to this copy, not to an NCsvPerf checkout beside the repository.
- **Generator:**
  - `ExcelTextWorkbook.Write` writes the package. It compiles on net48 too, since the test project links it.
  - `ExcelDataGenerator` makes the data (NCsvPerf's lines repeated as `LineCache` does, under `DataGenerator.PackageAssetHeader`), the CSV text and the cache.
  - The shape is Excel's: `s="1"` (the text format) on every cell, `spans` per block of 16 rows, `x14ac:dyDescent` on each row, and Excel's `sheetViews`, `sheetFormatPr` and `pageMargins`.
  - The parts are in Excel's order, so the shared strings come after the sheet. Excel's theme and document properties are left out.
  - It writes the text this data has: `&`, `<` and `>` are escaped, and leading or trailing spaces get `xml:space="preserve"`. Control characters, and text of the form `_xHHHH_`, throw `NotSupportedException`, since Excel would escape them.
  - The million-row workbooks:
    - Shared strings: 53 MB, with 714 MB of sheet XML, 16,123,924 cells and 3,085 distinct strings.
    - Inline strings: 80 MB, with 1,223 MB of sheet XML.
    - Writing both takes about 16 s.
  - They are cached in `%TEMP%\csvhelper-bench`. The key is the row count, the variant, and the first 8 bytes of a SHA-256 of the CSV and of the generator's two source files, which the bench assembly embeds as resources. Writing a new one deletes the variant's stale ones.
- **Cross-check** (`Bench/ExcelTextWorkbookTests`, 22 tests):
  - Against Excel's `excel/packageassets-1695.xlsx`, these are identical: the sheet from its `dimension` on, `sharedStrings.xml`, the `cellXfs`, and the order of the entries.
  - Both variants read as NCsvPerf's lines through CsvHelper.Excel (sync and async) and through ExcelDataReader, as Excel's file does.
  - The Open XML SDK's validator finds no error in either variant.
  - `spans`: Excel 16.0 build 20527, through COM, wrote `1:2` on rows 1–16, `1:3` on 17 and 18, and `5:5` on 40 for cells in A1:B16, C17, A18 and E40: the columns of each block of 16 rows. That case is now a test.
  - The rest: escapes, `xml:space`, the rejections, and column names up to XFD.
  - Excel opened both generated 1,695-row variants read-only through COM without repairing them (no repair log). It found 27,330 values in A1:Y1695, in the text format.
  - **Mutation check** on net10.0, eight bugs planted in the writer one at a time, all caught:
    - no `x14ac:dyDescent` on rows: 1 failure;
    - no `s="1"` on shared cells: 1;
    - an unknown element in `sheetData`: 3, the validator's among them;
    - no `xml:space="preserve"`: 4;
    - `&` not escaped: 4;
    - no rejections: 4;
    - per-row spans: 2;
    - the `_xHHHH_` check reading past the text's end: 4. It survived at first. A test now has a text that ends in `_x0041`.
- **Class:** `ExcelPackageAssetsRead`, as designed, with the changes noted under "Baseline benchmark":
  - Sylvan is each category's baseline.
  - The CSV reference is the class's own `Csv_` methods.
  - Strings returns the total length.
- **QuickRunner:**
  - It now runs each combination of a class's `[Params]` values; before, it ran only the defaults.
  - Warmup stops after three calls when they average a second or more. That leaves the warmup of sub-second benchmarks, such as all the CSV ones, as it was.
  - `verify` prints "the results differ" under a category whose results disagree.
- **Equal work:** in `verify`, all six readers agree on both variants:
  - 1,000,000 rows;
  - 280,044,328 characters;
  - records hash `A46BD7E4C52574F1`.
- **The run** (2026-11-03, 06:50–07:55):
  - Tools: BenchmarkDotNet 0.15.8, .NET 10.0.13 (x64, x86-64-v4).
  - Job: `BenchConfig`'s. Core 4 is pinned, with 4 warmups, 12 iterations and `DOTNET_TC_CallCountingDelayMs=0`.
  - One run held all 36 benchmarks and took 64 minutes.
  - The machine before: paged pool 3.6 GB, 32.5 GB free, CPU idle. After: 3.7 GB and 31.1 GB.
  - The Excel readers' standard deviation was at most 3.3% of the mean (ExcelDataReader's inline Rows). The CSV reference's Records reached 5.3%.
  - MB/s is of the same data as CSV, 305 MB.

Mean times, with shared strings (as Excel writes them):

| Library | Rows | Strings | Records |
| --- | ---: | ---: | ---: |
| Sylvan.Data.Excel 0.5.8 (baseline) | 2,019 ms (151 MB/s) | 2,052 ms (149 MB/s) | 2,301 ms (133 MB/s) |
| CsvHelper.Excel | 2,742 ms, 1.36x slower | 2,804 ms, 1.37x slower | 3,511 ms, 1.53x slower |
| ExcelDataReader 3.9.0 | 5,677 ms, 2.81x | 5,590 ms, 2.72x | 6,451 ms, 2.81x |
| MiniExcel 1.46.0 | 7,205 ms, 3.57x | 7,499 ms, 3.65x | 7,894 ms, 3.43x |
| Open XML SDK 3.5.1, SAX | 8,198 ms, 4.06x | 9,020 ms, 4.40x | 9,900 ms, 4.31x |
| CsvHelper, the same data as CSV | 22.5 ms | 104 ms | 712 ms |

With inline strings:

| Library | Rows | Strings | Records |
| --- | ---: | ---: | ---: |
| Sylvan.Data.Excel 0.5.8 (baseline) | 3,151 ms (97 MB/s) | 3,171 ms (96 MB/s) | 3,864 ms (79 MB/s) |
| CsvHelper.Excel | 3,117 ms, 1.01x faster | 3,229 ms, 1.02x slower | 3,874 ms, 1.00x |
| ExcelDataReader 3.9.0 | 7,833 ms, 2.49x slower | 7,528 ms, 2.37x | 10,871 ms, 2.81x |
| MiniExcel 1.46.0 | 8,935 ms, 2.84x | 8,725 ms, 2.75x | 12,127 ms, 3.14x |
| Open XML SDK 3.5.1, SAX | 11,643 ms, 3.70x | 12,588 ms, 3.97x | 15,520 ms, 4.02x |
| CsvHelper, the same data as CSV | 23.0 ms | 104 ms | 716 ms |

Allocated per operation, in BenchmarkDotNet's MB (1,024 KB), shared / inline:

| Library | Rows | Strings | Records |
| --- | ---: | ---: | ---: |
| Sylvan.Data.Excel | 5 / 922 | 6 / 922 | 228 / 1,144 |
| CsvHelper.Excel | 0.13 / 0.02 | 913 / 913 | 1,135 / 1,135 |
| ExcelDataReader | 3,558 / 7,037 | 3,558 / 7,037 | 3,780 / 7,259 |
| MiniExcel | 17,052 / 26,803 | 17,136 / 26,887 | 17,275 / 27,024 |
| Open XML SDK, SAX | 7,285 / 10,781 | 8,854 / 12,348 | 9,076 / 12,570 |
| CsvHelper, CSV | 0.003 | 913 | 1,135 |

- **What the numbers say:**
  - **Ranking:** CsvHelper.Excel comes second of the five with shared strings, at 1.36–1.53x Sylvan's time. With inline strings it is level with Sylvan, within 2%. The others take 2.4–4.4x Sylvan's time.
  - **Inflate is not the limit.** A scratch program pinned to core 4 inflated the cached sheets through `ZipArchive` alone, median of 8:
    - shared: 135 ms for 714 MB;
    - inline: 218 ms for 1,223 MB;
    - that is 5.3–5.6 GB/s. So about 95% of CsvHelper.Excel's 2.7 s goes to scanning the XML and filling records, about 160 ns per cell.
  - **Shared strings:**
    - Sylvan returns its table's own string objects: 6 MB allocated for Strings, against CsvHelper.Excel's 913 MB, a new string per field, as for CSV.
    - Sylvan's lead also shows in Rows, where no strings are made: 720 ms.
    - My guess is that the cost is in handling a shared cell, copying its UTF-8 into the batch, as well as in the strings. It is a hypothesis for Phase 9 to profile; nothing here measured it.
  - **Excel against CSV:** with shared strings, reading the data from a workbook takes CsvHelper 122x the CSV time for Rows, 27x for Strings and 4.9x for Records. Inline: 136x, 31x and 5.4x.
- **Phase 9 candidates** (not decisions):
  - one string per shared-string index;
  - not copying shared strings into the batch;
  - the scanner's per-cell cost.
  - Phase 1 sets no speed target.
- **Tests:** 689 per target, all passing on net10.0, net8.0 and net48. The solution builds with 0 warnings.
- **Core:** no change. The bench's CSV benchmark classes aren't edited; its project gained CsvHelper.Excel, four package references, the data and the embedded generator source.

### Phase 2: typed reading (L)

Status: the detailed design below was written and approved on 2026-11-03, with every recommendation accepted. The user also settled that core, CsvHelper.Linq and CsvHelper.Excel are always released together. The design makes the phase L rather than M, because it adds formula text with shared formulas, Excel's own answers as an oracle, and a typed benchmark. "Changes elsewhere" was applied to the rest of the plan at approval. All ten steps were built on 2026-11-03 and 2026-11-04, and the phase is complete. "As built" at the end opens with a summary (step 10), then records each step. Where building changed the design, the design says so.

Scope, as planned:

- **Numbers:** number cells are doubles, parsed invariant. Integer targets accept integral doubles only.
  - Excel sometimes writes 17 significant digits (`0.30000000000000004`), so `decimal` targets round to `DecimalDigits` (15), matching what Excel displays.
- **Date detection:**
  - Built-in number format ids 14–22 and 45–47.
  - Custom formats scanned for date and time tokens. The scan skips quoted text, `\` escapes and `[Red]`-style colors, and recognizes `[h]`, `[m]`, `[s]` elapsed-time tokens.
- **Date conversion:**
  - Serial numbers become `DateTime`, `DateOnly` and `TimeOnly`; durations become `TimeSpan`.
  - Handles the 1900 leap-year bug (serial 60 is a February 29 that never existed) and the 1904 date system.
  - `DateTimeOffset` targets get offset zero from serial numbers, or are parsed from ISO text.
  - ISO 8601 `t="d"` cells are supported.
- **Booleans:** boolean cells map to `bool`.
- **Error cells** (`#N/A`, `#DIV/0!` and the rest) follow the `ErrorCells` policy.
- **`GetString` on typed cells:** raw invariant text for numbers, ISO 8601 for date cells, `TRUE`/`FALSE` for booleans.
- **Formulas:** the cached value by default, or the formula text.
  - Shared formulas store their text only in the cell that defines them. Every other cell in the range needs the relative references shifted from that cell, and that's the fiddly part.

#### Public API

Typed reading adds three options and two enums. Everything else goes through `CsvReader`'s existing methods, which now read each cell by its kind.

```csharp
public sealed record ExcelReaderOptions
{
	// Phase 1: Default, Sheet, SheetIndex, Csv, MaxPartSize, MaxUncompressedSize.

	public ExcelErrorCells ErrorCells { get; init; } = ExcelErrorCells.Text;
	public int? SignificantDigits { get; init; } = 15;
	public ExcelFormulas Formulas { get; init; } = ExcelFormulas.CachedValue;
}

public enum ExcelErrorCells { Text, Null, Error }

public enum ExcelFormulas { CachedValue, FormulaText }
```

- **`ErrorCells`** decides how a cell holding an error (`#N/A`, `#DIV/0!` and the rest) reads:
  - `Text` (default): as its text. String targets, `GetString`, dynamic records and `CsvDataReader` values get `#N/A`. Any other target fails with a `FormatException` that names the error. Inside `GetRecords`, that becomes a `CsvFormatException` with the cell's `Location`, and goes to `OnRecordError`. `CsvReaderOptions.NullValues` can name error texts to read them as null.
  - `Null`: as an empty cell would, so nullable targets get null. A row whose only values are errors is an empty row.
  - `Error`: reading the cell fails for every target, strings included: `GetString`, `Parse`, dynamic record values and `CsvDataReader` values throw `FormatException`. Inside `GetRecords`, it goes to `OnRecordError` with the cell. Cells that aren't read raise nothing, and `GetSpan` still gives the stored text. A header cell is a name, not a value: it names its column with the error's text, as under `Text` (changed at step 8, when the fuzzer found `Read` throwing the policy's `FormatException` for one).
- **`SignificantDigits`:** `decimal` and integer targets get a number cell's value rounded to this many significant digits, half away from zero, which is what Excel displays. `null` reads the stored text exactly.
  - It must be from 1 to 28, or null; anything else throws `ArgumentOutOfRangeException`.
  - `double` and `float` targets are never rounded, and neither are text cells.
- **`Formulas`:**
  - `CachedValue` (default) reads the value Excel saved with a formula, as Phase 1 does. A formula saved without one (openpyxl writes these) is an empty cell.
  - `FormulaText` reads a cell that has a formula as a text cell: `=` and the formula as the file stores it. Cells without a formula read their values.
- **Validation:** `Validate` adds "ExcelReaderOptions.ErrorCells is not an ExcelErrorCells value.", the same for `Formulas`, and "ExcelReaderOptions.SignificantDigits must be from 1 to 28, or null." (each `ArgumentOutOfRangeException`).
- `ExcelWorkbook.Date1904` (Phase 1) now decides how serial dates convert.

**What a cell reads as.** A number cell is a date cell when its number format shows a date or a time, and a duration cell when it shows elapsed time (`[h]:mm`); see "Date detection" below.

| Target | Number cell | Date cell | Duration cell | Boolean cell | Error cell |
| --- | --- | --- | --- | --- | --- |
| `string`, and every text accessor (below) | its stored text: `1.5`, `0.30000000000000004`, POI's `42.0` | ISO 8601: `2024-01-15`, `13:45:00`, `2024-01-15T13:45:00.25` | `36:00:00` | `TRUE` or `FALSE` | `#N/A`, as `ErrorCells` says |
| `double`, `float` | the number | its serial | its serial (days) | 1 or 0 | fails |
| `decimal` | the number, rounded to `SignificantDigits` | the same | the same | 1 or 0 | fails |
| `int`, `long` and the other BCL integers | the number, rounded to `SignificantDigits`, if whole and in range | the same | the same | 1 or 0 | fails |
| `bool` | 1 is true, 0 false; any other number fails | fails | fails | its value | fails |
| `DateTime` | the serial's date and time | the same | the same | fails | fails |
| `DateOnly` (.NET 8+) | the serial's date | the same | the same | fails | fails |
| `TimeOnly` (.NET 8+) | the serial's time of day | the same | the same | fails | fails |
| `TimeSpan` | the serial as days | the same | the same | fails | fails |
| `DateTimeOffset` | the serial's date and time, offset zero | the same | the same | fails | fails |
| enums, `Guid`, anything else | its stored text, with the invariant culture (Phase 1) | the same | the same | the same | fails |

- Text cells read as CSV text does: the reader's format provider, true and false values (Phase 1). Empty cells read as empty CSV fields.
- "Fails" is a `FormatException`, or an `OverflowException` for a number out of the type's range, as for CSV. `GetRecords` reports either with the cell's `Location`.
- Integer targets read `42`, `42.0` and `1E3`; `3.0000000000000004` reads as 3 after rounding to 15 digits; `1.5` fails.
- Date and time targets read plain number cells as serials too, not only date cells: the target says the column holds dates, and a number in Excel can only be a serial then.

**Serial dates:**

- Times are rounded to the millisecond, as Excel shows them and `DateTime.FromOADate` rounds.
- **1900 date system** (the default):
  - Serial 1 is 1900-01-01. Excel counts a 1900-02-29 that never existed (serial 60), so serials 1 to 59 are 1900-01-01 to 1900-02-28, and 61 on are 1900-03-01 on.
  - Serial 60 fails for date targets, and its text is `1900-02-29`.
  - Serials below 1 are times alone. Excel shows them on day 0 (`1/0/1900`). Date targets get 1899-12-31, the day before serial 1, as ClosedXML and ExcelDataReader do.
- **1904 date system:** serial 0 is 1904-01-01.
- Dates run to 9999-12-31 23:59:59.999. A serial past that, a negative one, or one that isn't a number fails for date targets (`DateTime`, `DateOnly`, `TimeOnly`, `DateTimeOffset`), and the cell's text is its stored text. Excel shows such cells as `####`, or, in the 1904 system, negatives as negative dates.
- `TimeSpan` takes any finite serial, negatives included, within `TimeSpan`'s range.
- `DateTime` values have `DateTimeKind.Unspecified`.

**Text of date and duration cells:**

- A date cell's text comes from its value, not its format:
  - a whole day: `yyyy-MM-dd`;
  - a serial below 1 (a time alone): `HH:mm:ss`;
  - otherwise: `yyyy-MM-ddTHH:mm:ss`;
  - with `.fff` when the milliseconds aren't 0, and trailing zeros dropped (`.25`).
- A duration cell's text is Excel's `[h]:mm:ss`: hours of at least two digits and no day part (`36:00:00`, `-01:30:00`), with `.fff` as above.
- A boolean cell's text is `TRUE` or `FALSE`.

**Text accessors and byte accessors:**

- These give a typed cell's text: `GetString`, `GetStringOrNull`, `GetChars`, `Parse<string>`, `CsvField.ToString()`, `CsvRow.ToArray()` and `ToDictionary()`, dynamic record values, `CsvDataReader` values, and header names. So a date in the header row names its column `2024-01-15`.
- These give the stored form (`45306.5`, `1`): `GetSpan`, `GetRawSpan`, `RawRecord`, `CsvField.Span` and `CsvRow.GetSpan`.
- A new `CsvReader.FieldEquals(int index, ReadOnlySpan<byte> utf8)` compares a field's text without making a string, for CsvHelper.Linq (decision 3).

**Formats on members:**

- `ParseExact<DateTime>` and `ParseExact<DateTimeOffset>`, which the generator emits for `[CsvFormat]`, read number, date and duration cells as `Parse` does, without the format. The format applies to text cells.
- `DateOnly`, `TimeOnly` and `TimeSpan` members with `[CsvFormat]` do the same once `ParseExact` covers them (decision 4).
- A provider (`Parse<T>(index, provider)`, `[CsvCulture]`) applies to text cells only, as in Phase 1.

**Date detection** (which number cells are date or duration cells):

- **Built-in format ids,** as Excel (en-US) shows them (checked at step 4):
  - Dates and times: 14–22, 27–36, 45, 47, 50–58, 71–79 and 81.
  - Durations: 46 and 80 (`[h]:mm:ss`).
  - Every other id is a number.
  - (The design said durations were 46 and 79; Excel shows 79 as `mm:ss` and 80 as `[h]:mm:ss`.)
- **Format codes:** an id that `styles.xml` defines in `numFmts` is classified by its code, even when the id is a built-in one, as Excel reads it. A cell format's `applyNumberFormat="0"` is ignored, as Excel ignores it. Only the code's first section counts, as in Excel (`0;yyyy` is a number). It is read token by token:
  - Skipped: quoted text (`"days"`), escaped characters (`\d`), the character after `_` and `*`, and brackets such as `[Red]`, `[Color10]`, `[>=100]` and `[$-409]`.
  - `General` (in any case) is a number. `E+` and `E-` after a digit placeholder are exponents.
  - `y`, `d`, `e`, `g`, `b` (in any case) and a weekday (`aaa`) make a date. A date part wins over elapsed time: `yyyy-mm-dd [h]:mm` shows the hour of the day.
  - Otherwise `[h]`, `[hh]`, `[hhh]`, `[m]`, `[mm]`, `[s]` and `[ss]` make a duration.
  - Otherwise `h`, `m`, `s`, `AM/PM` and `A/P` make a date (a time).
- **ISO dates (`t="d"`):** the text is converted to a serial in the workbook's date system, and the cell then reads as a number cell of that serial does: its style decides between a date, a duration and a number, as Excel reads it (a `t="d"` cell in General is a number to Excel). Excel's Strict files write these, as dates (`2024-01-02`), date-times with 17 digits of fraction (`2024-01-02T03:04:05.00000006519257800`), times alone (`03:04:05.000...`), and in elapsed-time formats ISO 8601 durations (`PT36H0M0.000S`). A text that isn't one of those forms, with an optional `Z` on a date or time, or a date outside the date system's range, stays a text cell, as in Phase 1. (The design had every `t="d"` date read as a date cell, and no durations; Excel's answers at step 7 changed both.)
  - Excel's Strict workbooks have `dateCompatibility="0"`, a 1900 date system without 1900-02-29 that counts days as OLE Automation dates do. Their `t="d"` dates read as the same dates in the 1900 system, so their serials before March 1900 are a day less than Excel's, and 1899-12-30, which the 1900 system lacks, stays text. A plain serial in such a workbook reads in the 1900 system; Excel writes Strict dates as `t="d"`, so only serials outside the date range are plain there. See step 7's "As built".

**Formula text** (`Formulas = FormulaText`):

- A cell's formula is its `<f>` text, decoded like a value (entities, `_xHHHH_` escapes, CR LF as LF, and the white space around it trimmed unless `xml:space="preserve"` applies), after `=`.
  - A cell with a formula reads it whatever its type and value, so `ErrorCells` doesn't apply to it.
  - An `f` without text (`<f/>`, `<f></f>`, white space only) is no formula: the cell reads its value.
- **Shared formulas** (`t="shared"`): the cell whose `f` has a `ref` defines the group (`si`) with its text. Every other cell of the group has an `f` with the `si` and no `ref`, and reads the defining cell's formula with the relative references moved by its offset from that cell, as Excel reads them. Such a cell's own text, if it has any, is ignored, as Excel ignores it.
  - What moves: `A1` one row down becomes `A2`. `$A$1` stays, `A$1` keeps its row and `$A1` its column. Whole columns and rows (`A:A`, `$1:1`) move too, and so do references after a sheet name (`'Q1 Orders'!A1`, `Sheet1:Sheet3!A1`) or a workbook index (`[1]Sheet1!A1`).
  - What doesn't: function names (`LOG10(` looks like a reference), defined names, `TRUE` and `FALSE`, string literals (`"A1"`), structured references (`Table1[Col]`), array constants (`{1,2}`), numbers and error literals.
  - A reference moved past an edge of the grid wraps around it, as Excel reads it: `XFD1` moved a column right is `A1`, and `SUM(1:1048576)` moved a row down is `SUM(2:1)`. (The design said `#REF!`, which is what Excel's own fills store, in each cell's full formula; see step 6's "As built".)
  - A moved formula's references are written in capitals (`a1+$b$1` moved down is `A2+$B$1`), as Excel and ClosedXML write them. The rest of it stays as stored.
  - A defining cell without text reads its value, and so do the other cells of its group.
  - A cell whose group no cell before it defines throws `CsvFormatException` (decision 17).
  - Excel repairs files with a cell outside its group's `ref`, or an `si` defined twice; CsvHelper.Excel moves the formula to such a cell anyway, and a later definition replaces the earlier one.
- **Array formulas** (`t="array"`): the first cell of the range has the formula. The other cells have no `<f>` of their own, so they read their values. Excel's `Formula` property repeats the formula there for a legacy array (Ctrl+Shift+Enter) and gives nothing for a spilled one.
- **Data tables** (`t="dataTable"`) read their values.
- Newer functions keep the prefix the file stores (`=_xlfn.XLOOKUP(...)`), as openpyxl and ClosedXML return them. Excel shows them without it.

**Checked on 2026-11-03,** with probe workbooks in a scratch folder:

- **Excel 365 (COM):**
  - Serial 0 shows as `1/0/1900` and `1900-01-00 00:00:00`, serial 60 as `2/29/1900`, and serial 2,958,465.99999999 as `9999-12-31 23:59:59.999`. Times are rounded to the millisecond (0.999999999 shows as the next day).
  - In the 1900 system negatives show as `####`. In the 1904 system they show as negative dates (`-1/2/1904`) and negative durations (`-24:00:00`).
  - `[h]:mm:ss` shows 1.5 as `36:00:00`.
  - Shared formulas: a fill down turned `SUM($1:1)` into `SUM($1:2)` and `'Other Sheet'!A1` into `'Other Sheet'!A2`, and left `LOG10(A1)`'s name and `MyName` alone. A fill off the grid stored `#REF!` itself.
  - Excel writes `_xlfn.XLOOKUP` and `_xlfn.SEQUENCE`, and shows them without the prefix. It writes the CR LF of a multi-line formula as is, without `xml:space`.
  - Array formulas store `<f t="array" ref="G1:G3">` in the first cell only, a spilled one with `cm="1"` too. `Range.Formula` gives the master's formula on a legacy array's other cells, and nothing on a spilled array's.
  - Not every fill makes a shared formula: in one probe, Excel wrote every cell's formula in full.
- **ClosedXML 0.105.1:** day 0 is 1899-12-31. Serial 60 reads as 1900-03-01, and 60.5 throws `ArgumentException`. It can't load a 1904 workbook that has a serial of 60 at all.
- **ExcelDataReader 3.9.0:** day 0 is 1899-12-31, serial 60 reads as 1900-02-28, negatives as dates before 1900, and 2,958,466 stays a double.
- **Sylvan.Data.Excel 0.5.8:** a serial below 1 has no date (empty string, `InvalidCastException`), and neither does 60.

#### Internal design

**Core** (each item is also under "Core changes"):

- **`CsvCellKind.Duration`** (6): a number in invariant form in an elapsed-time format. `IsInvariant` covers it, and `RecordBatch.CommitField` flags it as it flags date, boolean and error cells, so that text accessors reach `CellString`.
- **`CsvCellOptions`** (`Internal/CsvCellOptions.cs`), a sealed record with `Date1904`, `SignificantDigits` (`int?`) and `ThrowOnErrorCells`, and `Default` (the 1900 system, 15, false).
- **`CsvRecordSource.CellOptions`:** a virtual property, `CsvCellOptions.Default` unless a source overrides it.
  - The reader reads it through its `recordSource`, which parallel workers have too. So `CsvReader` gets no new field (Phase 0's field-layout lesson).
- **`CsvCells`** (`Internal/CsvCells.cs`) holds every conversion, for the reader and dynamic records alike:
  - `Parse<T>(s, kind, provider, booleans, hide, options)` and `TryParse<T>`: by `T` and kind, as the table says. The rest goes to `CsvValue`, or `ParseChars` on .NET, as now.
  - `Text(s, kind, options, hide)`: the text of a date, duration, boolean or error cell, or `null` when the text is the bytes.
  - The serial arithmetic (both date systems, day 0, serial 60, the range, millisecond rounding), rounding to significant digits, and whole numbers from doubles.
  - Messages leave the value out when `IncludeValuesInExceptions` is off, as `CsvValue`'s do. For example: "The cell holds the error #N/A.", "60 is 1900-02-29 in Excel's 1900 date system, a day that doesn't exist.", "-1 is not a date: Excel's dates are serials from 0 to 2958465.99999999 (9999-12-31).", "1.5 is not a whole number."
- **`CsvReader`:**
  - `ParseCell`, `TryParseCell` and `CellString` call `CsvCells`. They are reached as now, behind `typedCells`.
  - `CaptureHeader` takes a typed cell's text, and an error cell's text under every policy (step 8: `CellHeader` no longer goes through `CellString`'s check of `ThrowOnErrorCells`).
  - `GetChars` gives a typed cell's text.
  - The `ParseExact` overloads send number, date and duration cells to `CsvCells`, through a method that isn't inlined.
  - The new `FieldEquals`.
  - Under `ErrorCells = Error`, `GetString` names the cell (`Data!B3 holds the error #N/A.`), since nothing wraps it there.
- **`CsvDynamicRecord`:**
  - Its values (the indexer, `TryGetValue`, enumeration, `ToDictionary`) give a typed cell's text, and `Parse<T>` calls `CsvCells`.
  - `CsvDynamicSchema` keeps the source's `CsvCellOptions` (null for CSV) in place of its `Cells` flag, and interns by it.
- **CsvHelper.Linq:** `DynamicAccess.TextEquals` calls `reader.FieldEquals`.

**CsvHelper.Excel:**

- **New files:**
  - `ExcelErrorCells.cs` and `ExcelFormulas.cs`.
  - `Internal/Package/NumberFormats.cs`: the built-in table and the code classifier.
  - `Internal/Sheet/IsoDate.cs`: `t="d"` text to a serial.
  - `Internal/Sheet/FormulaShifter.cs`: moving a shared formula's references.
  - `Internal/Sheet/SharedFormulas.cs`: a sheet's shared formulas, by `si`.
- **`StyleTable`:** each cell format's kind (Number, DateNumber or Duration) comes from `NumberFormats`, once per format at load.
- **`SheetCellKind`:** gains `Duration`, mirroring core.
- **`SheetRecordSource.CellOptions`:** the workbook's `Date1904` and the reader options' `SignificantDigits`, and whether `ErrorCells` is `Error`.
- **Scanner:**
  - `t="d"`: the value is decoded and trimmed as now, then `IsoDate` turns it into a serial. Core's `FloatFormatter` (internal, reached through `InternalsVisibleTo`) writes the serial's shortest round-trip text into the field. The style gives the kind, as for a number cell: Number, DateNumber or Duration.
  - `t="e"`: with `ErrorCells = Null`, the cell is valueless, as one without a value is. Otherwise it is an Error field, as now.
  - **`FormulaText`:** in a `c`, the scanner reads `f` (its `t`, `si` and `ref`, and its text through `TryReadText`), which comes before `v`. It writes the formula as the value, and skips the rest of the cell, `v` included, in the resumable `CellRest` mode. With `CachedValue`, `f` is skipped, as in Phase 1. The window rules don't change: a cell is read whole or read again after a refill.
  - **Shared formulas:**
    - The defining cell of a group is stored when its field is committed (once, even when a refill made the scanner read the cell again): its formula's UTF-8 and its row and column. A defining cell without text is stored with none, and a definition of the same `si` replaces the one before.
    - The store counts toward `MaxPartSize`: each group its formula's bytes plus 64, and the room moved formulas are written in. Past it, `InvalidDataException`: "The shared formulas of xl/worksheets/sheet1.xml take more than ExcelReaderOptions.MaxPartSize (N bytes)."
    - A moved formula counts the bytes it has beyond its cell's XML (up to its `f`) toward `MaxUncompressedSize`, and so does a shared string beyond its cell, as gap fields count (Phase 1's decision 5): a short cell repeated can't make gigabytes of fields. Other fields are no longer than their XML. (Added at step 8.)
    - A cell whose `si` no earlier cell defined throws `CsvFormatException`: "it uses shared formula 3, which no cell before it defines". An `si` that isn't an index from 0 throws `CsvFormatException` at the cell, as other attributes do.
    - A moved formula is written into the store's growable room, after its `=`, then added with `AddField`. The room grows until the formula fits, which it always does in five times the defining formula's length.
- **`FormulaShifter`:** one forward pass, no recursion, so nesting depth costs nothing.
  - **Copied as is:**
    - string literals (`"..."`, with `""` inside);
    - brackets, nested, with `'` escapes inside (structured references, workbook indexes);
    - array constants (`{...}`);
    - error literals (`#REF!`, `#N/A` and the others; a lone `#` is the spill operator);
    - numbers (digits, `.`, an exponent);
    - operators and white space.
  - **Words** (letters, digits, `_`, `.`, `\`, `$`, `?`, and non-ASCII letters):
    - a word before `(` is a function;
    - a word before `!` is a sheet name, also as `Sheet1:Sheet3!`;
    - `'...'!` is a quoted sheet name, with `''` inside;
    - `[$]COL[$]ROW` within the grid is a cell;
    - `[$]COL:[$]COL` and `[$]ROW:[$]ROW` are whole columns and rows;
    - anything else is a name.
  - A part without `$` moves modulo the grid's size, so it wraps around the edges as Excel reads it. Each end of a range moves on its own. (The design had a range moved off the grid become `#REF!` as a whole; Excel's answers at step 6 changed it.)
  - A moved formula's references are written in capitals. A row that doesn't move is copied as it is.

#### Testing

**Core** (`CsvHelper.Tests`, with a test record source as Phase 0's `CellHookTests` has):

- **The table:** every target and kind, through `Parse`, `TryParse`, `Parse` with a provider, `ParseExact`, `CsvField`, `CsvDataReader`'s getters, dynamic records, `GetRecords<T>` with generated mappers, `ParallelEnumerate` workers, and header names.
- **Serials:** both date systems around every boundary (0, below 1, 1, 59, 60, 61, the last millisecond of 9999, negatives, NaN and infinities, text that isn't a number), against an independent implementation in the test. That includes every millisecond step across day ends, and random serials compared with `DateTime.FromOADate` corrected by the 1900 rule.
- **Rounding:** 1 to 28 digits, carries (`9.9999999999999999` to 10), large and small exponents, negatives, and the half-way cases.
- **Messages,** with and without values.
- **`FieldEquals`** for CSV fields (quoted, trimmed) and typed cells.

**CsvHelper.Linq.Tests:** `row["Date"] == "2024-01-15"` and `row["Paid"] == "TRUE"` filter typed cells as `row[...]` reads them.

**CsvHelper.Excel.Tests:**

- **`NumberFormats`:**
  - The built-in table and about 150 codes (dates, times, durations, numbers with literals, colors, conditions, locales, `General`, exponents, fractions, text), against Excel's answers.
  - The corpus tool's new `formats` command gets them through COM. For each code, it reads three answers for a number with that format:
    - `Range.Value`'s type, a date when the format shows a day, a month or a year (built, it turned out not to cover a time alone);
    - `CELL("format")`, `D6` to `D9` for Excel's four time formats;
    - the text Excel shows, where an elapsed-time format shows 1.5 days as 36 hours.
  - It also opens a generated workbook with a cell format for every id from 0 to 163, `numFmts` entries redefining built-in ids, and `applyNumberFormat="0"`. It writes `Files/excel-formats.json`, which is committed.
  - Durations are checked against Excel's text: an elapsed-time format shows 1.5 as 36 hours. (ClosedXML, planned for this, reads a time of day as a `TimeSpan` too.)
- **`IsoDate`:** every accepted form, fractions of 1 to 20 digits, the range limits, and rejected forms (offsets, week dates, basic format, `24:00`, February 29 of a common year).
- **`FormulaShifter`:**
  - Unit cases for every token kind, references wrapping at the edges of the grid, and deep nesting.
  - A comparison with ClosedXML's `FormulaA1` on generated shared formulas, then with Excel's answers on the corpus (below).
- **Scanner,** in every one of the 16 sheet modes:
  - `t="d"` cells;
  - each `ErrorCells` policy;
  - `FormulaText`: own, shared (first and other cells), array, data table, an empty `f`, an `f` without text, a missing first cell;
  - the shared formulas' limit.
- **End to end,** sync and async: typed records through `GetRecords<T>`, `Parse`, dynamic records, `CsvDataReader` (`DataTable.Load` and typed getters), `Query<T>()` and `ParallelEnumerate`, in both date systems; the options' validation.

**Corpus:**

- **New `typed` content** in the corpus tool, defined once like `basic`:
  - "Dates": each built-in date id; about 30 custom date, time and duration codes; number codes with literals; values around 0, 1, 60 and 61, the limits, and milliseconds.
  - "Numbers": integers, 17-digit values, exponents, large integers, booleans, and each error value.
  - "Formulas": fills down and right of relative, absolute and mixed references, ranges, whole rows and columns, other sheets' (quoted) references, names, `LOG10`, strings with references in them, a legacy array, a spilled array, a newer function, a fill off the grid, and structured references in a table.
- **Its producers:**
  - Excel: `excel/typed.xlsx`, `typed-1904.xlsx` and `typed-strict.xlsx` (with `t="d"`);
  - LibreOffice, from Excel's file;
  - openpyxl, plus `typed-isodates.xlsx` with `iso_dates`, which writes `t="d"`;
  - XlsxWriter, Apache POI and ClosedXML.
  - The corpus `README.md` records what each writes.
- **Excel's answers:** the tool's new `oracle` command opens every corpus workbook read-only through COM. For each used cell of each worksheet, it writes:
  - `Value2`, and the type of `Value` (date, number, text, boolean, error or empty);
  - `Formula` and `HasArray`;
  - `Text`, with columns widened in memory.
  - The output is `Files/excel-oracle/<file>.json`, committed, and `all` regenerates it. The privacy check covers it. `excel/packageassets-1695.xlsx` is left out (42,000 text cells).
- **Oracle tests:**
  - **Excel's answers:** each cell's kind (date, number, boolean, error, text), its value (the serial, `t="d"` cells included), the text of booleans and errors, and `FormulaText` against `Formula`. Each difference is listed with its reason (for example, a legacy array's other cells, or `_xlfn.`).
  - **ClosedXML:** dates compare as `DateTime` and durations as `TimeSpan`, no longer as serials; `FormulaA1` compares with `FormulaText`.
  - **ExcelDataReader:** dates compare as `DateTime`. Its `t="d"` difference from Phase 1 goes away.
  - **LibreOffice:** unchanged. It exports displayed text, so date cells stay out of the comparison.
- **Phase 1's corpus tests:** fields still compare with their cells through the stored form (`GetSpan`), and the text accessors are checked against the rules above.

**Security and fuzzing:**

- The fuzzer also varies `Formulas`, `ErrorCells` and `SignificantDigits`, and reads typed records as well as strings.
- A sheet of a million shared formula groups stops at `MaxPartSize`.
- A long formula, brackets nested a million deep in a formula, and absurd `t="d"` texts (a year of 10,000 digits, 10,000 fraction digits) are handled without growth or recursion.

#### Benchmark

- **Core gate:** the changes to core and CsvHelper.Linq go through Phase 0's gate, measured as before:
  - A/B against a baseline worktree of `b3f8079f` at `Csv\_bench\base`, with the machine checked first (paged pool, free RAM), the BenchmarkDotNet processes pinned, and pairs interleaved.
  - ReadBenchmarks (its Dynamic category included), the Linq Dynamic benchmarks and NCsvPerf (`CsvHelperLocal` and `_MT`). WriteBenchmarks get one pair as a control: the writer doesn't change.
  - A new CSV class, `FormattedDatesRead`, covers `ParseExact`, which nothing measures today: records with `[CsvFormat]` `DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly` members. It is copied into the baseline for the A/B.
  - If a method moves, its JIT output is compared base against new (IL size, inlinees), as Phase 0 did for the writer.
- **Typed reading:** a new class, `ExcelOrdersRead`.
  - **Data:** a million rows of `OrderRecord.Generate`'s orders (the parallel-write benchmark's), each member in the cell type Excel gives it:
    - dates and times as serials in a date format;
    - `Total`, `Weight`, `Quantity` and `Sku` as numbers;
    - `Paid` as booleans;
    - text, the `Guid` included, as shared strings.
    - (Changed at step 9: the dates' format is `yyyy-mm-dd hh:mm:ss`, and `ShippedAt` is its UTC time. A cell holds a time to the millisecond and no offset, so the orders are those times, as reading gives them back; `ExcelOrders` makes them and their cells.)
  - **Writer:** `ExcelTypedWorkbook`, in Excel's shape as `ExcelTextWorkbook` is. It is cached as PackageAssets is. Excel opens it without a repair (checked through COM), and ExcelDataReader reads the same values.
    - (Changed at step 9: as for `ExcelTextWorkbook`, Excel's shape is a file of Excel's, `excel/orders-1000.xlsx`, which the corpus tool's new `orders` command has Excel enter from the same cells, and a test compares the writer's with it. The numbers' text follows Excel's rule as far as Excel's files show it; see step 9's "As built".)
  - **Categories:**
    - Records: `GetRecords<OrderRecord>`, Sylvan's and ExcelDataReader's typed getters through `IColumnSource`-style builders, and MiniExcel's `Query<T>` if it reads every member;
    - Values: each field through its typed getter, folded into a checksum.
    - (Changed at step 9: the builders are one `Create` over an `IOrderSource` of typed getters, a struct per library, and Values folds the same getters. MiniExcel is left out: its `Query<T>` doesn't read every member. It fills properties only, and `OrderRecord`'s members are fields; with properties, it reads `ShippedAt` without its milliseconds and at the local offset.)
  - Sylvan is the baseline. The same orders as CSV are the reference.
  - One BenchmarkDotNet run, recorded in "As built".
- **Phase 1's benchmark:** a QuickRunner A/B of `ExcelPackageAssetsRead`, base against new, shows the scanner's new `f` check costs nothing measurable.

#### Core changes

Phase 2 changes core, and this lists every change for approval. Each must pass the gate under "Benchmark".

| Change | CSV paths it touches |
| --- | --- |
| `CsvCellKind.Duration`; `IsInvariant`; `CommitField`'s flag | none: record sources only |
| `CsvCellOptions`; `CsvRecordSource.CellOptions` | none |
| `CsvCells`, and the bodies of `ParseCell`, `TryParseCell` and `CellString` | none: behind `typedCells`, not inlined |
| `CaptureHeader` takes a typed cell's text | header reading, once per file |
| `GetChars` gives a typed cell's text | `GetChars` |
| `ParseExact` sends number cells to `CsvCells` | the four public overloads, which generated mappers call for `[CsvFormat]` dates |
| `ParseExact` for `DateOnly`, `TimeOnly` and `TimeSpan`; the generator emits it for them (decision 4) | generated mappers of those members with `[CsvFormat]`: a `ParseExact` from chars in place of `T.ParseExact(GetString(...))` |
| `CsvDynamicRecord` values and `Parse<T>`; `CsvDynamicSchema` keeps `CsvCellOptions` | dynamic records' values: one test of the schema |
| `CsvReader.FieldEquals`, public (decision 3) | none: new |
| CsvHelper.Linq: `TextEquals` calls `FieldEquals` | dynamic-row queries that compare text |
| `CsvValue.ParseExact<T>`'s two overloads are `AggressiveInlining` (added at step 9's gate, approved at its review) | every `ParseExact`: the type dispatch inlines again, as base's did for `DateTime` |

Considered and avoided:

- **A new kind for `t="d"` cells.** The scanner converts them to serials instead. Phase 1's "Core changes" left this open.
- **New fields on `CsvReader`.** The options come through the source.
- **Branches in `GetSpan`, `GetString`'s fast path, `DecodeField` or `ParseSpan`.** Typed cells reach the typed code through the needs-decoding flag and the existing `typedCells` tests.

#### Decisions

Each comes with a recommendation.

1. **Where the conversions live.** Recommended: in core (`CsvCells`), as above. That covers the 1900 rule too, which is Excel's, but core is already in lockstep with CsvHelper.Excel. The JIT folds the `typeof(T)` tests, and nothing is dispatched virtually.
   - Alternative: a converter that CsvHelper.Excel implements, behind non-generic virtual methods per target type. Excel's rules would stay in CsvHelper.Excel, but it adds a dispatch per value and a wider internal interface for the same result.
   - Alternative: the scanner writes texts (ISO dates, `TRUE`) into the buffer. `GetSpan` would then match `GetString`, but every date cell pays for the conversion whether it's read or not, and date targets would have to parse the text back.
2. **Stored form for bytes, text for strings.** Recommended: the byte accessors give the stored form and the string accessors give the text, as "Text accessors and byte accessors" lists. Making `GetSpan` give the text would need a branch in `GetSpan`, which every CSV read loop inlines.
3. **CsvHelper.Linq's text comparisons.** Without a change, `row["Paid"] == "TRUE"` compares `1` with `TRUE` and silently filters the row out. Recommended: a public `CsvReader.FieldEquals(int, ReadOnlySpan<byte>)`, whether a field's text is the UTF-8 text, without making a string. Linq's `TextEquals` calls it. It's a small public API, and useful on its own.
   - Alternative: `InternalsVisibleTo CsvHelper.Linq` in core, and Linq checks the reader's `typedCells`. That ties Linq to core's internals, so its package would need the exact pin too.
   - At approval, the user noted that core, Linq and Excel are always released together, so the alternative's pin would cost little. The recommendation stands: the user chose it, and it keeps Linq on public API.
4. **`[CsvFormat]` on `DateOnly`, `TimeOnly` and `TimeSpan` members.** Today the generator emits `DateOnly.ParseExact(reader.GetString(i), format, ...)`. On a date cell that parses `2024-01-15` with the member's format and fails. Recommended: `CsvReader.ParseExact<T>` takes these three types too (`DateOnly` and `TimeOnly` on .NET 8+), and the generator emits it for them. Date cells then read as `Parse` reads them. CSV gains a `ParseExact` from chars without a string. It's a public behavior change: `ParseExact<T>` stops throwing `NotSupportedException` for them.
   - Alternative: leave it and document that formats on those members need text cells.
5. **A duration kind.** Recommended: `CsvCellKind.Duration`, so that `[h]:mm` cells read as `36:00:00` rather than `1900-01-01T12:00:00`. Timesheets use these formats. Alternative: read durations as date cells, or as plain numbers (`1.5`).
6. **Date cells' text.** Recommended: from the value (a day, a time alone, or both), so no stored information is lost. A date format holding a time shows the time too, which Phase 8's displayed text will not. Alternatives: from the format (a date format gives only the date), or always the full `yyyy-MM-ddTHH:mm:ss`.
7. **Durations' text.** Recommended: Excel's `[h]:mm:ss` (`36:00:00`), which Excel, LibreOffice and pandas read back. Alternatives: .NET's `1.12:00:00`, which `TimeSpan.Parse` reads, or ISO 8601's `PT36H`.
8. **Plain number cells into date targets.** Recommended: read them as serials. That way a date column that lost its format, as some exports write it, still reads. Alternative: date cells only, as Phase 1 behaves (an error).
9. **Day 0 and times alone.** Recommended: 1899-12-31, the day before serial 1. That's what ClosedXML and ExcelDataReader give, and it keeps day arithmetic continuous. Alternative: 1899-12-30, the OLE Automation date that `DateTime.FromOADate` and Excel's COM `Value` give.
10. **Serial 60 (1900-02-29).** Recommended: date targets fail, and the text is `1900-02-29`. Alternatives: 1900-02-28 (ExcelDataReader) or 1900-03-01 (ClosedXML).
11. **`DateTimeOffset` from a serial.** Recommended: offset zero, as planned. It's deterministic, and it matches Phase 4's `DateTimeOffsets = Utc` writes. Alternative: the local offset, as CSV text without an offset gets.
12. **Significant digits.** Recommended: rename `DecimalDigits` to `SignificantDigits`, and apply it to integer targets as well as `decimal`. Then `3.0000000000000004` reads as 3, as Excel shows it. Default 15; null is exact.
    - The cost: a 17-digit ID stored as a number reads rounded (12345678901234567 as 12345678901234600), as Excel shows it. Such IDs belong in text cells, which Phase 4 writes by default.
    - Alternative: integers exact when the text is a whole number, and rounded otherwise.
13. **Error cells.** Recommended: `Text` by default, which matches Excel's CSV export and keeps the information; typed targets fail with the cell named. `Null` makes the cell valueless, and `Error` fails every read of the cell, strings included.
14. **`t="d"`.** Recommended: the scanner converts it to a serial (no new kind), accepting the forms Excel writes, plus `Z`. Other text stays text.
15. **Formula text's form.** Recommended: `=` first, as Excel's formula bar, `Range.Formula` and openpyxl show it, so formulas stand apart from values in a mixed column. The text is as stored, `_xlfn.` included. Alternatives: no `=` (ClosedXML, EPPlus), or Excel's displayed form, which would need `_xlfn.ANCHORARRAY(B1)` turned into `B1#`, `_xlfn.SINGLE` into `@`, and more.
16. **The other cells of an array formula.** Recommended: their values, since the file gives them no formula. Mirroring Excel would need `metadata.xml` to tell spilled arrays (no formula) from legacy ones (the master's formula).
17. **A shared formula whose first cell is missing.** Recommended: `CsvFormatException`, since no producer I know writes one. Alternative: read the cell's value.
18. **Shared formulas' memory.** Recommended: count it toward `MaxPartSize`, as a part held in memory.
19. **Built-in ids redefined in `numFmts`, and `applyNumberFormat="0"`.** Recommended: the definition wins, and `applyNumberFormat` is ignored, unless Excel's answers at step 4 say otherwise; then follow Excel.
20. **Header names.** Recommended: header cells give their text, as other text accessors do.
21. **`CsvDataReader`'s types.** Recommended: it stays string-typed (`GetFieldType` is `string`, values are the text), and its typed getters convert. Typed `GetValue` values (double, `DateTime`, bool) are a possible later option, for `SqlBulkCopy` into typed columns.
22. **Number cells' text.** Recommended: the stored text, as planned: `0.30000000000000004`, POI's `42.0`. Rounding it to 15 digits would cost a parse and a format for every number read as a string, and Phase 8's `Text = Formatted` is the displayed form. Alternative: round it.
23. **Booleans from number cells.** Recommended: only 1 and 0, as the built-in parser reads them. Alternative: Excel's rule, any number other than 0 is true.
24. **Excel's answers as committed files.** Recommended: the `formats` and `oracle` commands write what Excel says through COM, and CI's tests compare with it. Excel itself only runs here.
25. **A new `typed` content set.** Recommended: rather than growing `basic`, whose files and golden records every Phase 1 test uses.
26. **The typed benchmark.** Recommended: `OrderRecord`'s data, which the write benchmarks already generate, with Sylvan as the baseline.

#### Risks

- **JIT sensitivity:** `ParseExact`, `GetChars`, `CaptureHeader` and dynamic values get a test each. The gate measures them. If one costs, the typed path moves behind a separate method or schema, as Phase 0 did for the writer.
- **The formula grammar's corners.** Excel's answers, ClosedXML and the fuzzer cover it. How Excel reads shared formulas at the grid's edges, and moves sheet-qualified references in its fills, was checked through COM at step 6. One corner is left as it is (step 8): a reference right after a number, which Excel can't write (`20A1`), reads as the number's exponent once it moves to column E (`20E1`).
- **Locale formats:** built-in ids 27–36 and 50–58 mean different things in different locales. Excel's answers settle them for an en-US Excel; others may differ.
- **Excel's rules in core:** the 1900 rule and the significant digits live in core's internals, which lockstep releases already cover.
- **Text from values, not formats:** a date format holding a time shows the time in `GetString`, and numbers show all 17 digits. Phase 8 gives the displayed text.
- **Phase 1 behavior changes:** `GetString` of date and boolean cells, and header names from such cells. Nothing has shipped, so nothing breaks for users.

#### Order of work

Each step is reviewable on its own and gets committed when you ask.

1. **Core conversions (M):**
   - `CsvCellKind.Duration`, `CsvCellOptions`, `CsvRecordSource.CellOptions` and `CsvCells`.
   - The reader's paths: `ParseCell`, `TryParseCell`, `CellString`, `CaptureHeader`, `GetChars` and `ParseExact`.
   - Dynamic records.
   - Core tests with a test source.
2. **Public additions (S):**
   - `FieldEquals` and Linq's `TextEquals` (decision 3).
   - `ParseExact` for `DateOnly`, `TimeOnly` and `TimeSpan`, and the generator (decision 4), with the generator's tests.
3. **Core gate (S):** the A/B benchmarks under "Benchmark", with `FormattedDatesRead`. If a later step changes core again, the gate runs again. (Deferred to step 9; step 8 changed `CellHeader`, which it covers. Run at step 9: one loss, `FormattedDatesRead`'s Records, 3.5% and about 2% after a fix the user approved.)
4. **Number formats (S):**
   - `NumberFormats`, `StyleTable`'s kinds and `SheetCellKind.Duration`.
   - The corpus tool's `formats` command and `Files/excel-formats.json`.
5. **Typed cells in the scanner (M):**
   - `t="d"` and `ErrorCells`.
   - The three options and their validation, and `SheetRecordSource.CellOptions`.
   - End-to-end tests.
6. **Formula text (M):** `FormulaShifter`, `SharedFormulas`, and the scanner's `f`, with the tests. How Excel reads shared formulas, at the grid's edges too, is checked through COM.
7. **Corpus and oracles (M):**
   - The `typed` content and its producers.
   - The `oracle` command and Excel's answers for every corpus workbook.
   - The oracle tests.
8. **Security and fuzzing (S).**
9. **Benchmarks (M):**
   - `ExcelTypedWorkbook` and its cross-check, and `ExcelOrdersRead`, with a run on the benchmark machine.
   - The QuickRunner A/B of `ExcelPackageAssetsRead`.
10. **"As built" (S):** the decisions made while building, and the numbers.

#### Gate

- Every row of "What a cell reads as" works through `GetRecords<T>`, `Parse` and `TryParse`, dynamic records, `CsvDataReader`, `Query<T>()` and `ParallelEnumerate`. That holds in both date systems, sync and async, on net10.0, net8.0 and net48.
- Date detection agrees with Excel on every id and code of `excel-formats.json`, and on every number cell of the corpus.
- Dates and durations agree with ClosedXML and ExcelDataReader, each difference documented (serial 60, for one).
- `t="d"` cells read as the serials Excel gives them (but for the Strict workbooks' days before March 1900; see step 7's "As built").
- `FormulaText` agrees with Excel's `Formula` on every formula cell of the corpus, and shared formulas also agree with ClosedXML, each difference documented.
- The three `ErrorCells` policies behave as "Public API" says, and so does CsvHelper.Linq's text comparison.
- The security and fuzz tests pass, `FormulaText` included.
- Core's changes pass Phase 0's benchmark gate.
- The typed benchmark's baseline is recorded.
- CI's pack check still passes.

#### Changes elsewhere

Applied at approval on 2026-11-03:

- **API sketch:**
  - `DecimalDigits` becomes `SignificantDigits` (`decimal` and integer targets).
  - `Formulas` is `CachedValue` or `FormulaText`.
  - `ErrorCells` says what each policy does.
- **Phase 1's "Public API":** the "Later phases add" line gets the new names.
- **Phase 1's "Core changes":** the `t="d"` question is settled (no new kind).
- **Testing:**
  - Excel's answers through COM, committed as files.
  - The `typed` content.
- **Feature coverage:** a row for durations, and "Formula cached value vs formula text" notes shared formulas.
- **Defaults:**
  - `decimal` and integer targets round to 15 significant digits.
  - Error cells read as text.
  - Formulas read their cached values.
  - Date cells' text is ISO 8601.
- **Phase 9:** the typed benchmark joins the baseline it starts from.
- **Risks:** "Lockstep releases" covers CsvHelper.Linq too.

#### As built

**Summary (step 10, 2026-11-04).** Phase 2 is complete. This summary gathers what the steps below record one by one.

- **What it delivers:**
  - **Typed reading through `CsvReader`'s existing methods**, as "What a cell reads as" lists:
    - numbers as numbers, with integer targets taking whole numbers only, and `decimal` and integer targets rounded to `SignificantDigits`;
    - cells in date and time formats as dates, in both date systems, and elapsed-time formats as durations;
    - booleans; error cells by the `ErrorCells` policy; formulas' cached values or their text, shared formulas moved;
    - a cell's text: the stored number, ISO 8601 for dates, `[h]:mm:ss` for durations, `TRUE` or `FALSE`, an error's text.
  - **Public API:**
    - `ExcelReaderOptions.ErrorCells`, `SignificantDigits` and `Formulas`, with the enums `ExcelErrorCells` and `ExcelFormulas`.
    - In core, `CsvReader.FieldEquals`, and `ParseExact<T>` for `TimeSpan`, and for `DateOnly` and `TimeOnly` on .NET 8 and later.
  - **Core**, every change listed in "Core changes":
    - `CsvCells`' conversions, `CsvCellKind.Duration`, and `CsvCellOptions` through `CsvRecordSource.CellOptions`;
    - the typed paths behind `typedCells` (`ParseCell`, `TryParseCell`, `CellString`, `CellHeader`, `CellChars`, `ParseExactCell`, `CellEquals`), dynamic records, and the `ParseExact` dispatch, now inlined;
    - the generator's `ParseExact<T>` for `[CsvFormat]` times and durations, and CsvHelper.Linq's `TextEquals`.
  - **CsvHelper.Excel internals:**
    - `NumberFormats` and `StyleTable`'s kinds;
    - `IsoDate`, for `t="d"` dates and ISO 8601 durations;
    - `FormulaShifter` and `SharedFormulas`;
    - the scanner's typed cells, error policy and formulas, and `ChargeBeyondCell`.
  - **Size:** CsvHelper.Excel has 8,556 lines of library code (6,799 at Phase 1's end), and 11,467 of tests (8,162). Core gained 1,739 lines, 1,106 of them `CsvCells.cs`, and CsvHelper.Tests 1,062.
  - **Tests:**
    - CsvHelper.Excel.Tests: 1,033 on net10.0 and net8.0, and 1,005 on net48, where the benchmark writer's 28 tests don't build (689 on each at Phase 1's end).
    - CsvHelper.Tests: 1,309, 1,304 and 844 (1,287, 1,282 and 823).
    - CsvHelper.Linq.Tests: 112, 110 and 101, unchanged.
  - **Corpus:** 10 new workbooks.
    - The `typed` content: Excel's three (the 1900 and 1904 systems, and Strict), and LibreOffice's, openpyxl's two, XlsxWriter's, Apache POI's and ClosedXML's.
    - Excel's `orders-1000.xlsx`, for the benchmark writer's cross-check.
    - The tests now read 34 workbooks, besides the three CsvHelper.Excel rejects.
    - Excel's answers through COM are committed: `excel-formats.json` (180 codes, and the built-in ids 0 to 163) and `excel-oracle/`, every cell of 33 workbooks.
  - **Benchmarks:** `FormattedDatesRead` (CSV, for the core gate) and `ExcelOrdersRead` (typed cells), whose writer, `ExcelTypedWorkbook`, a test checks against Excel's file.
- **The gate**, item by item:
  - Every row of "What a cell reads as" through `GetRecords<T>`, `Parse` and `TryParse`, dynamic records, `CsvDataReader`, `Query<T>()` and `ParallelEnumerate`, in both date systems, sync and async, on net10.0, net8.0 and net48: met.
    - Step 1's `CellConversionTests` take every kind into every target through every accessor, and through generated mappers sync, async and in `ParallelEnumerate`.
    - Step 5's `TypedReadingTests` do the same over a sheet, in both date systems.
  - Date detection agrees with Excel on every id and code of `excel-formats.json`, and on every number cell of the corpus: met (steps 4 and 7).
    - `NumberFormatsTests` checks the ids and codes.
    - `ExcelAnswersTests` compares every cell of the 33 workbooks. 39 cells differ, and so does Google's pivot table sheet, each listed with one of 11 reasons.
  - Dates and durations agree with ClosedXML and ExcelDataReader, each difference documented: met (step 7).
    - ClosedXML can open no typed workbook, and the test checks each of its exceptions.
    - ExcelDataReader's differences are listed: ids 79 and 80 swapped, serial 60, negatives, `t="d"` as local times, and the rest.
  - `t="d"` cells read as the serials Excel gives them, but for the Strict workbooks' days before March 1900: met (steps 5 and 7). The Strict date system is left as it is, the user's choice.
  - `FormulaText` agrees with Excel's `Formula` on every formula cell, with Excel's display allowed for, and shared formulas with ClosedXML's `FormulaA1` on every workbook it opens but for one array cell (decision 16): met (steps 6 and 7).
  - The three `ErrorCells` policies, and CsvHelper.Linq's text comparison: met (steps 1, 2, 5 and 8).
  - The security and fuzz tests pass, `FormulaText` included: met (step 8). That includes 200,000 inputs on each target, and 5,000,000 formulas through the formula fuzzer.
  - Core's changes pass Phase 0's benchmark gate: met at step 9, after a fix the user approved. `FormattedDatesRead`'s Records is about 2% slower than base, with 65% less allocation. Everything else is within noise.
  - The typed benchmark's baseline is recorded: met (step 9).
  - CI's pack check still passes: run again on 2026-11-04. CsvHelper is `[0.0.0-ci]` in all 5 targets, and net47 has System.IO.Compression.
  - Every test in the solution passed on 2026-11-04, after the fix, with the counts above, and the solution builds with 0 warnings.
- **Decisions made while building** (each step's "As built" has the details):
  - **Conversions** (step 1):
    - `double` and `float` targets read the stored text, as in Phase 1, so they cost nothing new.
    - `TimeOnly` reads serial 60's time of day.
    - A serial that rounds to day 0 is a time alone.
    - Rounding works on the stored decimal text, half away from zero.
    - `CellOptions` is a field load, not a virtual call.
  - **Excel's answers changed date detection** (step 4): only a format's first section counts; a date needs a day, a month or a year in it; a date part wins over elapsed time; and ids 79 and 81 are times, 80 a duration.
  - **Shared formulas wrap around the grid's edges** (step 6), as Excel reads them, rather than becoming `#REF!`. Moved references are written in capitals.
  - **`t="d"`** (step 7): a cell's kind is its style's, and Excel's Strict ISO 8601 durations read as durations.
  - **Security** (step 8; both were the user's choices at review):
    - a header's error cell names its column, a core change to `CellHeader`;
    - shared strings and moved formulas count their bytes beyond their cell toward `MaxUncompressedSize`.
  - **The benchmark** (step 9):
    - The writer follows Excel's rule for numbers' text as far as Excel's files show it: 15 digits when they read back, otherwise 17.
    - MiniExcel is left out, since its `Query<T>` can't read the orders.
    - The `ParseExact` dispatch is inlined, a core change the user approved after the gate found its cost.
- **The numbers** (step 9):
  - On typed cells, CsvHelper.Excel reads a million orders in 2.34 s, 1.04x Sylvan.Data.Excel's time, where ExcelDataReader takes 2.30x. The same orders as CSV read in 0.35 s.
  - Phase 1's benchmark: the scanner's Phase 2 work costs nothing measurable (every method within 1%).
  - Core: `FormattedDatesRead`'s Records about 2% slower with 65% less allocation; the other reads, the writes and NCsvPerf within noise.
- **Left for later:**
  - **Phase 6:** `CsvWriter` writes a dynamic record's stored form, so a sheet's dates copied to CSV come out as serials (step 1, the user's choice).
  - **Phase 8:** the displayed text, which date cells' ISO 8601 text doesn't follow.
  - **Phase 9:**
    - why typed sheets read close to Sylvan's time while text sheets don't (step 9 gives a guess);
    - Phase 1's open items: the scanner's per-cell cost, shared strings returned as one string each, coverage-guided fuzzing, and a zip reader that checks CRCs.
  - **Left as they are, each documented:**
    - Strict workbooks' `dateCompatibility="0"` (step 7);
    - a reference right after a number, which Excel can't write (step 8);
    - an array formula's other cells, which read their values (decision 16).

**Step 1, core conversions** (built 2026-11-03, committed in 1ec418e9):

- **New in core** (`src/CsvHelper/Internal`):
  - `CsvCells.cs`: every conversion of "What a cell reads as", and the text of dates, durations and booleans.
    - `Parse<T>` gives the value, throws, or returns `false` so the caller parses the stored text with the invariant culture, as Phase 1 did. `TryParse<T>` returns `true`, `false`, or `null` for the same fallback.
    - `Text` gives a cell's own text. `ErrorRead` is the exception for reading an error cell under `ErrorCells = Error`.
  - `CsvCellOptions.cs`: `Date1904`, `SignificantDigits` (validated: 1 to 28, or null) and `ThrowOnErrorCells`.
  - `CsvCellKind.Duration` (6).
- **Changed in core:**
  - `CsvRecordSource.CellOptions`.
  - The reader's `ParseCell`, `TryParseCell`, `CellString`, and the new `CellHeader`, `CellChars` and `ParseExactCell`, all not inlined. They are reached from `CaptureHeader`, `GetChars` and the four `ParseExact` overloads behind `typedCells`.
  - `CommitField` flags durations.
  - `CsvDynamicRecord`'s indexer and `Parse<T>`. `CsvDynamicSchema.CellOptions` replaces the `Cells` flag and is part of the interning key.
  - `CsvValue.ParsesExact<T>`.
- **Decisions made while building:**
  - **`CellOptions` is not virtual.** It is a property with a protected setter, so a typed parse loads a field instead of making a virtual call.
  - **New helpers on `CsvCellKind`.** `IsInvariant` went. `IsTyped` covers what `CsvCells` sees, error cells included. `HasText` covers what `CommitField` flags.
  - **`double` and `float` targets aren't converted.** They parse the stored text as Phase 1 did (a date's serial, a boolean's 1 or 0), so they cost nothing new. Error cells fail for them too.
  - **`TimeOnly` reads serial 60's time of day** (12:00 for 60.5): a time doesn't need the day that never existed. `DateTime`, `DateOnly` and `DateTimeOffset` fail on it.
  - **A serial that rounds to day 0 is a time alone, in both date systems.** So serial 0's text is `00:00:00`, not a date.
  - **Rounding to `SignificantDigits`** works on the stored decimal text, half away from zero, not on the binary value.
    - Stored text with no more bytes than the digits needs no rounding. A `decimal` then parses it exactly as before, and an integer in `[-]digits` form takes a fast path.
    - Otherwise a digit reader keeps 40 significant digits, rounds, and builds the `decimal` from a 96-bit integer and a scale.
    - Values smaller than `decimal`'s 28 decimal places round to them (`5E-29` at 1 digit is `1E-28`).
  - **A number cell whose text isn't a number** falls back to the Phase 1 parse, and fails with its usual message.
  - **`NullValues` are compared with the stored form**, as `GetStringOrNull` and `IsNull` already compared them, before any text is made. An error's stored form is its text, so `#N/A` works as a null value.
  - **`TryParse<string>` throws under `ErrorCells = Error`**, as `GetString` does. Its string path goes through `MakeString`, and changing that would add a branch to the CSV hot path. `TryParse` of other types returns `false`.
  - **Messages:**
    - `'1.5' is not a whole number, so it is not a valid Int32.`
    - `'2147483648' is outside the range of Int32.`
    - `'2' is not a valid boolean: a number cell is true when it is 1 and false when it is 0.`
    - `'60' is 1900-02-29 in Excel's 1900 date system, a day that doesn't exist, so it is not a valid DateTime.`
    - `'-1' is not a valid DateTime: Excel's dates are serials from 0 to 2958465.99999999 (9999-12-31).` The 1904 system's limit is 2957003.99999999.
    - `The cell is a date, so it is not a valid Boolean.` (also for durations and booleans).
    - `The cell holds the error #N/A, so it is not a valid Int32.`
    - `Data!B3 holds the error #N/A.` A dynamic record says `The cell` in place of the location.
    - Each has a variant without the value, for when `IncludeValuesInExceptions` is off.
  - **`DateTime.FromOADate` is a millisecond off for some doubles.** It adds its epoch to the milliseconds before truncating them. The tests compare it exactly on serials of whole milliseconds and within a millisecond on arbitrary doubles, and check the millisecond rounding exactly.
- **Left for later (the user's choice, 2026-11-03):**
  - `CsvWriter` writes a `CsvDynamicRecord`'s stored form (`TryGetSpan`). So `WriteRecords(sheet.GetRecords<dynamic>())` writes a sheet's dates as serials and its booleans as 1 or 0.
  - The design doesn't cover it, and the writer is JIT-sensitive. Phase 6's conversion is the natural place.
- **Tests:**
  - **`CellConversionTests`** (18 tests):
    - every kind for every target, through every accessor, generated mappers (sync, async, `ParallelEnumerate`) and `OnRecordError`;
    - both date systems at their limits;
    - every millisecond across six day ends, plus 40,000 random serials against `FromOADate`;
    - rounding at 1 to 28 digits against `decimal` arithmetic (16,800 values), with carries and limits;
    - messages with and without values.
  - `FakeRecordSource.Cells` sets a source's options.
  - **Phase 0 and 1 expectations updated:** boolean cells read as `TRUE`/`FALSE` and dates as ISO 8601 (Phase 1's sheets have no date cells until step 4).
    - `CorpusTests.ReadsEveryWorksheetThroughThePublicApi` compares the stored form through `GetSpan`, then the text.
- **Counts:**
  - CsvHelper.Tests: 1305 (net10.0), 1300 (net8.0), 841 (net48).
  - CsvHelper.Linq.Tests: 112, 110, 101.
  - CsvHelper.Excel.Tests: 689 on each target.
  - The solution builds with 0 warnings.
- **CSV paths changed, for step 3's gate:**
  - the four `ParseExact` overloads, `GetChars` and `CaptureHeader` (a `typedCells` test each);
  - `CsvDynamicRecord`'s indexer (a test of the schema) and `Parse<T>` (a null test in place of a flag);
  - the schema's interning key.

**Step 2, public additions** (built 2026-11-03, committed in 1ec418e9):

- **`CsvReader.FieldEquals(int, ReadOnlySpan<byte>)`**, public: whether a field's text, the string `GetString` would make, is the UTF-8 given.
  - For CSV it is `GetSpan(index).SequenceEqual(utf8)` behind a `typedCells` test.
  - Typed cells go to `CellEquals`, which isn't inlined and compares the cell's ASCII text char by byte.
  - Under `ErrorCells = Error`, it throws for an error cell, as every read does.
- **CsvHelper.Linq:** `DynamicAccess.TextEquals` calls `FieldEquals`, so `row["Paid"] == "TRUE"` matches a boolean cell.
- **`ParseExact<T>`** takes `TimeSpan` on every target, and `DateOnly` and `TimeOnly` on .NET 8 and later.
  - Each parses as its own `ParseExact` does (`TimeSpanStyles.None`, `DateTimeStyles.None`), the call generated mappers made before. Fields of up to 128 bytes are decoded on the stack instead of to a string.
  - Messages leave the value out when `IncludeValuesInExceptions` is off. The generated `T.ParseExact(reader.GetString(i), ...)` used to ignore that option.
  - Typed cells convert as `Parse` does (`ParseExactCell`), so a date cell fills a `[CsvFormat]` `DateOnly`.
  - `ParseExact<TimeSpan>` used to throw `NotSupportedException`. `ReaderTests.ParseExactParsesOnlyDates` became `ParseExactParsesDatesTimesAndDurations`.
- **Generator:** `[CsvFormat]` members emit `reader.ParseExact<T>`:
  - `TimeSpan` always.
  - `DateOnly` and `TimeOnly` only when the referenced CsvHelper is its .NET build. The generator tells by `readerParsesSpans`, the flag that already gates `ISpanParsable` parsing. An application on .NET 6 or 7 gets the netstandard2.1 build, so its mappers keep the type's own `ParseExact`. No test covers that case: it would need the netstandard2.1 build as a reference.
  - Without `[CsvCulture]`, the call takes no provider and the reader's applies, as the generated `provider` local did before.
  - `MappingFeaturesGenerateCSharp9WithoutWarnings` now expects `reader.ParseExact<global::System.TimeSpan>(column3, formats1)`.
- **Tests:**
  - `CellConversionTests` gained:
    - `ParseExactReadsDateCellsForTimesAndDurations`: the cells, and generated mappers through `FormattedCellRecord`;
    - `FieldEqualsComparesTheText`: CSV quoted, trimmed and missing fields; each cell kind; the error policy;
    - `LinqComparesTheTextOfTypedCells`: `==` and `!=` on date and boolean cells, with `Explain` checking that the reader runs the comparison.
  - The Linq test lives in CsvHelper.Tests, whose test source needs core's internals, which CsvHelper.Linq.Tests can't see.
  - `RecordGeneratorTests.FormatsOfTimesAndDurationsAreParsedByTheReader`.
- **Docs:** `docs/pages/reading.md` gives `ParseExact`'s types and a paragraph on `FieldEquals`. `docs/pages/records.md` says `[CsvFormat]` members of these types are read by `ParseExact<T>`.
- **Counts:**
  - CsvHelper.Tests: 1309 (net10.0), 1304 (net8.0), 844 (net48).
  - CsvHelper.Linq.Tests: 112, 110, 101.
  - CsvHelper.Excel.Tests: 689 on each target.
  - The solution builds with 0 warnings.
- **CSV paths changed, for step 3's gate:**
  - Linq's dynamic text comparisons: `FieldEquals` in place of `GetSpan` and `SequenceEqual`.
  - Generated mappers of `[CsvFormat]` `TimeSpan`, `DateOnly` and `TimeOnly` members: `ParseExact<T>` from chars in place of a string. `FormattedDatesRead` should include a `TimeSpan` member too.
- Steps 1 and 2 were committed together on 2026-11-03: their changes share files.

**Step 3, core gate** (deferred, the user's choice, 2026-11-03):

- **`FormattedDatesRead`** (`bench/CsvHelper.Benchmarks/FormattedDatesBenchmarks.cs`, committed with steps 1 and 2): 200,000 rows of a `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan` in exact formats.
  - Records: the generated mapper's `[CsvFormat]` parsing.
  - ParseExact: the reader's `ParseExact<DateTime>` and `ParseExact<DateTimeOffset>`, with Sep's `DateTime.ParseExact` on the same columns as a control.
  - QuickRunner's `verify` gives the same checksums on base and new.
- **The baseline:** a worktree of `b3f8079f` at `Csv\_bench\base`, with its NCsvPerf copy and `FormattedDatesRead` copied in. Steps 4 to 8 plan no core change, so it stays the right baseline.
- **The selection:**
  - 56 read benchmarks: `PackageAssetsRead`, `PackageAssetsReadFromString`, `PackageAssetsReadFile`, `FloatsRead`, `QuotedRead`, `FormattedDatesRead`, and `PackageAssetsLinq`'s Dynamic category, in 2 interleaved pairs.
  - The 15 write benchmarks, as a control pair.
  - NCsvPerf, in 3 interleaved pairs.
- **Filters must name the classes exactly** (`CsvHelper.Benchmarks.PackageAssetsRead.*`). A leading `*` also matched `ExcelPackageAssetsRead`, an hour per run, and `SeparatorPackageAssetsRead`.
- The run was started and then stopped at the user's request. It will run later, overnight or with step 9's benchmarks.
- (Run at step 9, 2026-11-04: see step 9's "The core gate".)

**Step 4, number formats** (built 2026-11-03, committed with steps 4 to 8 on 2026-11-04):

- **`NumberFormats`** (`src/CsvHelper.Excel/Internal/Package/NumberFormats.cs`): `BuiltIn(id)` and `Classify(code)`.
  - `StyleTable` gives each cell format the kind of its format, classified once per id at load.
  - `SheetCellKind.Duration` mirrors core's, and the compile-time check covers it.
  - Number cells in date and time formats are now date cells everywhere CsvHelper.Excel reads them.
- **`SheetRecordSource.CellOptions`** passes the workbook's `Date1904`. The design put this in step 5, but date cells appear here, and without it a 1904 workbook would read in the 1900 system until then. Step 5 adds the other two options.
- **The corpus tool's `formats` command** (`tools/CsvHelper.Excel.Corpus/ExcelFormats.cs`, also run by `all`) writes `Files/excel-formats.json` from Excel 365 (en-US) through COM:
  - 180 codes, 172 of which Excel takes;
  - built-in ids 0 to 163, from a workbook the tool writes;
  - eight cases of `numFmts` redefinitions and `applyNumberFormat="0"`.
  - The corpus README describes it, and the privacy check passes.
- **What Excel's answers changed** (the design's "Date detection" now says so):
  - **`Range.Value` returns a date only when a format's first section shows a day, a month or a year.** A time alone (`h:mm`) and an era, a Buddhist year or a weekday alone (`e`, `b`, `aaa`) come back as numbers. So the tool also asks `CELL("format")`, which flags Excel's four time formats (`D6` to `D9`), and records the text Excel shows, which marks elapsed time.
  - **Only the first section counts:** Excel takes `0;yyyy` and `[<1]h:mm;yyyy-mm-dd` for what their first sections are. The design said every section counts.
  - **A date part wins over elapsed time:** `yyyy-mm-dd [h]:mm` shows the hour of the day, not 1,087,356 hours. The design said a duration wins.
  - **Built-in ids:** 79 and 81 are times (`mm:ss`, `mm:ss.0`), and 80 is a duration (`[h]:mm:ss`). The design had 79 as the duration. In an en-US Excel, 27–31, 36 and 50–58 show as `m/d/yyyy`, 32–35 as `h:mm:ss`, 59–70 as plain numbers, and 23–26 and 82 on as General.
  - **Confirmed:** a `numFmts` definition wins over a built-in id, and `applyNumberFormat="0"` is ignored (decision 19).
  - **Also found:** `[hhh]` is elapsed time too; Excel refuses lowercase `e+` (`0.00e+00`), `0 h` and `[s]0`; `General d` is a date.
  - **The exponent rule:** `E+` or `E-` is an exponent only after a digit placeholder, so `e-m-d` stays a date.
- **Tests:**
  - **`NumberFormatsTests`** (31 per target):
    - every code Excel takes, against its answers;
    - every built-in id against Excel's answers, and against `Classify` of the code Excel shows for it;
    - the redefinition and `applyNumberFormat` cases through `StyleTable`;
    - 27 token cases.
    - The JSON is read with `JsonReaderWriterFactory`, which net48 has as well, so no package was added.
  - **`TimesAndCalendarParts`:** 23 codes that none of Excel's answers flags but that show a time or a date part: `mm:ss`, `h`, `AM/PM` alone, `e`, `b`, `aaa`, and the like. The test lists them, each shown by Excel's text.
  - **Durations are checked against Excel's text** rather than ClosedXML, which reads a time of day as a `TimeSpan`.
  - **`StyleTableTests.GivesEachStyleTheKindOfItsNumberFormat`** replaces Phase 1's "number kind for now" test.
- **Phase 1 tests updated for date cells:**
  - `Workbooks.Orders()`' prices had style 1, the test package's built-in date (14). They no longer have a style.
  - Excel's basic workbook reads its date rows as `2024-01-02`, `2024-01-02T03:04:05`, `03:04:05` and `2024-12-31`.
  - The corpus test works out each date cell's text with .NET's own date arithmetic (`Corpus/CellTexts.cs`).
  - The Open XML SDK oracle compares date cells as numbers (`CorpusFiles.WithoutDates`), since it reads no formats.
  - ClosedXML and ExcelDataReader compare date kinds. Their `TimeSpan`s map to `Duration`.
  - ClosedXML's one new difference: Data!B33, a time of day, in 16 files (`TimesAsTimeSpans`).
  - The oracle tests compare each file's differing cells as a set, not in order.
- **Counts:** CsvHelper.Excel.Tests: 720 on each target. Core and Linq unchanged. The solution builds with 0 warnings.

**Step 5, typed cells in the scanner** (built 2026-11-03, committed with steps 4 to 8 on 2026-11-04):

- **Public:** `ExcelErrorCells` and `ExcelFormulas`, and `ExcelReaderOptions.ErrorCells`, `SignificantDigits` and `Formulas`.
  - Each is validated with the design's messages.
  - `Formulas = FormulaText` is accepted, and step 6 makes it read formulas. Until then it reads cached values.
- **`ExcelReaderOptions.CellOptions(date1904)`** (internal) gives the record source its `CsvCellOptions`: the workbook's date system, `SignificantDigits`, and whether `ErrorCells` is `Error`. Step 4's interim wiring of `Date1904` alone is gone.
- **The scanner** takes the date system and the error policy (optional parameters, so Phase 1's test call sites stand):
  - `t="d"`: `IsoDate` converts the decoded text to a serial. Core's `FloatFormatter` writes it into the field, with room for at least 32 bytes. The cell is a date, or a duration in an elapsed-time style. Text that isn't an ISO date stays text, as in Phase 1.
  - `t="e"` with `ErrorCells = Null`: valueless, as a cell without a value is.
- **`IsoDate`** (`Internal/Sheet/IsoDate.cs`) takes `yyyy-MM-dd`, `yyyy-MM-ddTHH:mm[:ss[.f]]` and `HH:mm[:ss[.f]]`, each with an optional `Z` and XML white space around it.
  - The day counts in the date system: the 1900 system's day 0 is 1899-12-31, and 1900-03-01 is serial 61.
  - The time is worked out in `decimal` from the first 20 fraction digits (the rest must be digits) and parsed once to a `double`.
  - Excel's Strict texts come out the same doubles as the serials of `excel/basic.xlsx` (`45293.127835648149`, `0.12783564814814816`).
  - **Not dates:** offsets, week and ordinal dates, the basic format, `24:00`, leap seconds, invalid days, and days before day 0.
- **Tests:**
  - **`IsoDateTests`** (53 per target): Excel's forms to the bit, both date systems at their limits, 31 rejected forms, 5,000 random texts against `Corpus/IsoSerials.cs` (an independent computation: a regex, `ToOADate`, `decimal`), and texts of 10,000 digits.
  - **`TypedCellScannerTests`**, in every sheet mode: number cells by style, `t="d"` in both systems (an elapsed style, invalid text, a preserved space, a character reference, empty values), and the three error policies with empty rows kept and dropped.
  - **`TypedReadingTests`** (13): `TypedOrder` through generated mappers (sync and async), `Parse` and `ParseExact`, `FieldEquals`, dynamic records, `CsvDataReader` (`DataTable.Load` and typed getters), `Query<T>()` and `ParallelEnumerate`, in both date systems. Also the error policies with `OnRecordError`'s location and `NullValues`, `SignificantDigits` at 15, 17, 2 and null, and the options' checks.
  - The test helpers take a `CellReading` (styles, date system, error policy). `CapturedRecord` shows durations as `duration:`.
- **Phase 1 tests updated:**
  - The scanner's cell-type test reads `t="d"` as `date:45293.12783564815`.
  - The reference model of `SheetScannerComparisonTests` converts `t="d"` with `IsoSerials`. So does the Open XML SDK oracle, whose comparison sets dates to numbers on both sides.
  - The Strict differences listed for ExcelDataReader and LibreOffice (`t="d"` read as text) went.
  - ExcelDataReader has two new differences in `excel/strict.xlsx`: it reads a `t="d"` date-time as a `DateTimeOffset` in the local time zone (B32), and a time alone on today's date (B33).
  - On .NET Framework, `double.ToString("R")` gives 17 digits where CsvHelper writes the shortest form, so `IsoSerials` finds the shortest round-trip text itself.
- **Counts:** CsvHelper.Excel.Tests: 789 on each target. Core and Linq unchanged. The solution builds with 0 warnings.

**Step 6, formula text** (built 2026-11-03, committed with steps 4 to 8 on 2026-11-04):

- **`FormulaShifter`** (`Internal/Sheet/FormulaShifter.cs`): `TryShift(formula, rows, columns, destination, out written)`, one forward pass over the UTF-8, as the design's token rules say. `MaxGrowth` is 5: `A1` grows to at most `XFD1048576`, and `1:1` to `1048576:1048576`.
- **`SharedFormulas`** (`Internal/Sheet/SharedFormulas.cs`): the groups by `si`, `Define`, `TryFind` and `TryGet`. `TryGet` moves the formula into a room that starts at twice the formula's length and doubles until it fits. The groups (each its formula plus 64 bytes) and the room count toward `MaxPartSize`. `XmlWindow.MaxSize` gives the scanner the limit.
- **The scanner** takes `ExcelFormulas` (an optional parameter), and holds a `SharedFormulas` only under `FormulaText`. `TryReadFormula` reads `f`'s attributes and text and decides what the cell reads (`CellFormula`: none, its own, defining, or shared). `AddFormula` writes it. Its class remarks describe formulas.
- **Excel's answers, through COM, on 2026-11-03.** Probe workbooks were written by hand, one case each, and their formulas read back with `Range.Formula`:
  - **Shared formulas wrap around the grid's edges.** Excel opened, without repairing them, groups whose other cells move references past every edge:
    - `A1` moved a column left is `XFD2`, and `XFD1` moved right is `A1`;
    - `B1048576` moved a row down is `B1`;
    - `SUM(1:1048576)` moved down is `SUM(2:1)`, `SUM(B1:XFD1)` moved right is `SUM(C1:A1)`, and neither is put in order.
    - So the design's `#REF!` rule went. `#REF!` is what Excel's own fills store, as each cell's full formula; those cells read as they're stored.
  - **Excel repairs, so takes as corrupt:**
    - a cell outside its group's `ref`, which reads `#N/A`;
    - an `si` defined twice, whose second definition loses its formula;
    - a defining cell without a `ref`, or without text;
    - a cell before its group's defining cell, or with no definition at all;
    - any shared formula with a sheet-qualified reference, `Sheet1!A1` on its own sheet included.
    - Excel's fills write such formulas in full in each cell: other sheets' references, string literals and table columns. In the probes Excel shared only same-sheet references, functions, names and numbers.
  - **Excel takes:**
    - a cell's own text in a shared `f` without `ref`, which it ignores (`C7` read as `A2`);
    - an `si` that skips numbers (7 first);
    - lowercase references, which it shows in capitals, functions too.
  - **Text:**
    - Excel trims `f` as it trims `v`: `<f>  A1 + 1  </f>` is `=A1 + 1`. With `xml:space="preserve"` it keeps the white space in front but drops what follows (`=  A1 + 1`). CsvHelper.Excel keeps both, as the file stores them; Excel never writes either.
    - Excel writes `xml:space="preserve"` on an `f` that starts with a space, and escapes `_x` in formulas as in values (`"_x005F_x0041_"`), so `_xHHHH_` escapes decode.
    - A raw CR LF or LF is a line feed, and `&#13;` makes Excel repair the file.
    - `<f/>`, `<f></f>` and `<f>   </f>` read their values.
  - **Shown differently from the file:**
    - numbers (`1E+5` as `100000`, `.5` as `0.5`);
    - a whole column stored as `C1:C1048576` (`C:C`);
    - newer functions without `_xlfn.`;
    - everything in capitals.
    - Step 7's oracle lists these as differences.
- **ClosedXML 0.105.1** moves references as Excel does within the grid. It writes `#REF!` past an edge where Excel wraps, writes a moved formula's references in capitals, unmoved and absolute ones too, and ignores a cell's own text, as Excel does. CsvHelper.Excel writes references in capitals as both do (the first build kept their case).
- **Tests:**
  - **`FormulaShifterTests`** (47 per target):
    - 18 of Excel's answers;
    - 27 token cases: other sheets, quotes, brackets, arrays, errors, the spill operator, functions, names, numbers, leading zeros, offsets of a whole grid, and unclosed literals;
    - the growth bound, and a million brackets deep.
  - **`SharedFormulaComparisonTests`:** 60 generated groups of every kind of reference, literal and function, some in lowercase, each with up to 12 other cells, all the same as ClosedXML's `FormulaA1`.
  - **`SharedFormulasTests`:** the room's growth, the replacement of a group, and both limits.
  - **`FormulaScannerTests`**, in every sheet mode:
    - own formulas in every cell type;
    - decoding: trimming, `xml:space`, entities, escapes, CDATA, line ends;
    - shared formulas with wrapping and ignored text;
    - arrays, data tables, empty `f`, and a defining cell without text;
    - a missing or later definition, and bad `si` values;
    - `ErrorCells`;
    - the limit, and redefinitions under it.
  - **`FormulaReadingTests`:** end to end, sync and async, through fields, `GetRecords<T>`, dynamic records and `ParallelEnumerate`, with `ErrorCells = Error`.
  - **A look ahead at step 7 (not kept):** every corpus file's formula cells read with `FormulaText` agree with ClosedXML's `FormulaA1`, Excel's shared formulas included. Two exceptions: ClosedXML can't open `excel/strict.xlsx` or `google/64450.xlsx`, and `google/66365.xlsx`'s B2 is an array formula's other cell, which reads its value (decision 16) where ClosedXML repeats the formula.
- **Counts:** CsvHelper.Excel.Tests: 851 on each target. Core and Linq unchanged. The solution builds with 0 warnings.

**Step 7, corpus and oracles** (built 2026-11-03, committed with steps 4 to 8 on 2026-11-04):

- **The `typed` content** (`tools/CsvHelper.Excel.Corpus/Typed.cs`), defined once, as `basic` is:
  - "Dates": 51 built-in ids set by id, 29 format codes, four elapsed-time codes, durations, 16 serials around the date systems' limits shown to the millisecond, and six dates written as dates.
  - "Numbers": integers, 17-digit doubles, the largest and least normal doubles, exponents, booleans, the seven classic errors as constants and as results, `#CALC!`, and texts that look like values.
  - "Formulas": fills down and right, a legacy array, a spill and a reference to it, `XLOOKUP`, a reference copied off the grid, line breaks, and the table `Orders` with a calculated column. The fills are listed apart, for Excel to fill, and the rows hold each cell's formula as the fill gives it, for the other producers.
  - "Other Sheet", and the defined name `Rate`.
  - The spec's cells gained a format id, a formula's result, an array's range and a spill flag; its sheets gained fills, copies and tables; and `CellType.Error` is a constant error.
- **Its producers**, nine files of 10 to 20 KB:
  - Excel: `excel/typed.xlsx`, `typed-1904.xlsx` (which leaves out the dates before 1904, which Excel can't hold) and `typed-strict.xlsx`;
  - LibreOffice, from Excel's;
  - openpyxl (`typed.xlsx`, and `typed-isodates.xlsx` with `iso_dates`), XlsxWriter, POI's XSSF, and ClosedXML.
  - The commands that write them write the basic files again too. Those were put back from git, unchanged in content, so their committed bytes stand.
- **The `oracle` command** (`tools/CsvHelper.Excel.Corpus/ExcelOracle.cs`, also run by `all`) writes `Files/excel-oracle/<file>.json`: 33 files, 2 MB, in 17 seconds. The corpus README describes them. Found while building it:
  - Excel opens the files with calculation manual if a blank workbook set it first, and then shows the stored values: openpyxl's formulas without values are empty.
  - `Range.Formula` fails for a whole range with a text longer than a formula can be, and `Range.Value` for a date past 9999, so both are read per cell.
  - Excel repairs `closedxml/typed.xlsx`. So the command opens a workbook it can't open as it is with repair, and records the repair log's records, not its summary, which names the file's full path.
- **Excel's answers changed CsvHelper.Excel twice** (the design's "ISO dates" now says so):
  - **ISO 8601 durations:** Excel's Strict file writes elapsed-time cells as `t="d"` durations (`PT36H0M0.000S`, `PT2160M0.000S`, `PT129600.000S`), which read as text. `IsoDate` now takes days, hours, minutes and seconds (`P1DT12H`), each a whole number of up to 15 digits, in order, at least one, with a fraction only in the seconds. The tests' `IsoSerials` takes them too.
  - **A `t="d"` cell's kind is its style's**, as a number cell's is: Excel showed a `t="d"` cell in `0;yyyy` as a number, and a probe showed General `t="d"` cells as numbers (45293, and 1.5 for `PT36H`). Step 5 made every `t="d"` date a date cell. Step 5's scanner test now styles its dates and adds cells in General, and `TypedReadingTests`' `t="d"` order date has a date style, as Excel writes one.
- **Excel's answers found, and the test lists:**
  - **Strict workbooks' date system:** `dateCompatibility="0"` is a 1900 system without 1900-02-29 that counts days as OLE Automation dates do; Excel shows serial 1 as 1899-12-31 and 60 as 1900-02-28 there. Its `t="d"` dates read as the same dates in the 1900 system, a serial a day less before March 1900, and 1899-12-30 stays text. Reading such a workbook's plain serials as Excel does would take a third date system in core's `CsvCellOptions` and conversions. At review the user chose to leave it as it is: documented, with no core change.
  - A serial past the last day has no `Value` date, and Excel's 1904 system shows a negative serial as a date before 1904.
  - `#CALC!` and `#SPILL!` are stored as `#VALUE!` with rich value metadata.
  - Excel reads a number past the largest double as `#NULL!`, and a subnormal one as 0.
  - It shows `#VALUE!` in a legacy array's other cell whatever the file stores, and 0 where openpyxl stores nothing.
  - It builds a pivot table's cells that Google Sheets leaves out.
  - COM put a date before March 1900 a day off, and rounded milliseconds to the second.
- **`ExcelAnswersTests`** (34 per target) compares every cell of the 33 workbooks:
  - kind and value, the kind from `Range.Value`, `CELL("format")` and the elapsed text, as `NumberFormatsTests` judges them;
  - the text of booleans, errors, and dates and durations in the millisecond formats, against `CellTexts`, which `CorpusTests` ties to `GetString`: Excel's own date arithmetic (serial 0 as `1900-01-00`, 60 as `1900-02-29`, `0.999999999` as the next day) agrees, but for day 0, which reads as a time alone (decision 9);
  - formulas read as text against `Formula`, with Excel's display allowed for: no `_xlfn.` or `_xlws.`, `H14#`, `[@Qty]`, and no formulas for a legacy array's other cells.
  - 39 cells differ, and Google's pivot table sheet, each listed with one of 11 reasons. Every basic workbook agrees with Excel in every cell, and so do all the shared formulas, moved.
- **ClosedXML and ExcelDataReader** compare dates as `DateTime` and durations as `TimeSpan` (`CorpusFiles.AsDotNet`, through the public reader's `TryParse`), no longer as serials:
  - **ClosedXML** can open no typed workbook. It rejects serial 60 in a date format (in the 1904 system too), a number past the largest double, and the `t="d"` texts of Excel's Strict files and openpyxl. The test checks each exception.
  - Its `FormulaA1` is compared with `FormulaText` on every workbook it opens: they agree but for Google's legacy array's other cell.
  - **ExcelDataReader** differs in typed cells for reasons of its own, each listed:
    - built-in ids 79 and 80 swapped;
    - `e`, `yyyy-mm-dd [h]:mm` and `0;yyyy` classified otherwise;
    - serial 60 as 1900-02-28;
    - negatives as dates;
    - serials past the last day as numbers;
    - unrounded `TimeSpan`s on .NET, while .NET Framework's `TimeSpan.FromDays` rounds;
    - `t="d"` as local `DateTimeOffset`s or text.
    - On .NET Framework, a number past the largest double throws in `double.Parse`, so it gives text there.
  - **LibreOffice:** two more cells, the Strict 1899-12-30 and its own `Err:533`.
- **Also found in the producers** (the corpus README's "What the producers write"):
  - openpyxl and XlsxWriter write 16 significant digits, and LibreOffice and ClosedXML 15 (`0.1+0.2` as `0.3`), where Excel writes 17 and POI the shortest form.
  - ClosedXML wrote `NA()`'s result as a number, which Excel repairs.
  - XlsxWriter writes `#` as it is, which makes Excel repair the file; its users write `ANCHORARRAY()`, as the producer now does.
  - LibreOffice writes `#ref!` and `_xlfn.anchorarray` in lowercase.
  - POI can't parse an undefined name.
- **Tests:**
  - `IsoDateTests`: 87 per target, with 21 rejected and 12 accepted durations, and 2,000 random durations against `IsoSerials`.
  - `CorpusTests` list the typed files, and check every cell's stored form and text through the public API.
  - The Open XML SDK matches every typed workbook exactly.
  - `NumberFormatsTests`' kind rule and its list of times are shared with `ExcelAnswersTests`, and `CorpusFiles.Read` takes `ExcelFormulas`.
- **Counts:** CsvHelper.Excel.Tests: 973 on each target, LibreOffice's included. Core and Linq unchanged. The solution builds with 0 warnings.

**Step 8, security and fuzzing** (built 2026-11-03, committed with steps 4 to 8 on 2026-11-04):

- **Security tests** (`SecurityTests`, each sync and async, through the public API):
  - **A million shared formula groups:** a sheet of a million cells that each define a group of their own. Read as text, the store counts each group's formula and 64 bytes, 66 MB in all, toward a 4 MB `MaxPartSize`, and stops at about the 63,000th group with its error. Read as values, nothing is held, and all million rows read.
  - **A long formula:** 64 MB of formula under a 4 MB part limit stops the window at the limit, whether formulas read as text or as values, since a cell is read whole with its `f`. The reader got no further into the compressed data than twice the limit's share.
  - **A shared formula that grows:** 1 MB of `1:1,` repeated. Moved a row down it reads; moved to the last row it would be 4 MB (`1048576:1048576,`), and the room for it, with the formula the store holds, passes the 4 MB limit with the store's error, after the two records before it.
  - **Nesting:** shared formulas of a million parentheses around a reference, a million brackets nested around one, and a million brackets that never close. Each follower moves in one pass: the references outside brackets move, those inside don't.
  - **Absurd `t="d"` texts** of a million characters: a year of a million digits, a million digits of a second's fraction (of which the first 20 count), a million-digit number of hours, a million zeros of seconds, and a date in a million spaces. The dates read as dates and the rest as text, each in one pass. (The design said 10,000 characters; step 5's tests have those.)
- **The fuzzer** (`FuzzTests`):
  - **Settings:** besides sync or async and the CSV options, each seed picks `Formulas`, `ErrorCells`, `SignificantDigits` (15, 17, null, 1 or 28) and how to read, from numbers of its own (its bits mixed first: `Random` gives seeds near each other nearly the same first numbers, so the read mode at first followed the next seed's mutations).
  - **Three ways to read:**
    - **Strings:** every field through `GetString`, as in Phase 1.
    - **Typed fields:** every field through `GetString`, and the fields of each sheet's first 64 records through `TryParse` as `double`, `decimal`, `long`, `int`, `byte`, `bool`, `DateTime`, `DateTimeOffset` and `TimeSpan` (and `DateOnly` and `TimeOnly` on .NET), which must not throw. `Parse` runs for one of those types in turn, since exceptions are slow: it must give `TryParse`'s value, or throw `FormatException` or `OverflowException` where `TryParse` failed.
    - **Typed records:** `GetRecords<FuzzRecord>` (or `GetRecordsAsync`), a record of those types by index, with `OnRecordError` skipping the records that fail. Each failure must be a `CsvFormatException` around a `FormatException` or an `OverflowException`.
  - Under `ErrorCells = Error`, an error cell's `GetString` may throw the policy's `FormatException`.
  - **Sources:** a fourth synthetic package, of formulas (a shared group, a two-column group, a group whose defining cell has no text, an array, a data table, a structured reference) among dates, `t="d"` dates and a duration, an error and 17 digits. The typed corpus files came in with step 7.
  - **Tokens:** formulas' elements and attributes, the formula grammar's delimiters, references at the grid's edges, styles 1 and 3, and ISO 8601 texts; numbers such as `1E+309`, `4.9E-324`, 60 and 2958466.
  - **Runs:** 3,000 inputs by default, in about 4 s on net10.0, 5 s on net8.0 and 7 s on net48 (Phase 1's took 2 to 3 s). 200,000 inputs passed on each target with the review's changes: 5 min 5 s (net10.0), 7 min 47 s (net8.0) and 9 min 27 s (net48). Each way of reading took a third of them, with the same mix: about 39% read to the end, 30% failed at a sheet with `CsvFormatException`, 9% at a sheet with `InvalidDataException`, and 22% as a workbook with `InvalidDataException`. Before the changes, two runs of 200,000 on each target passed too, the first with the read mode following the mutations, and 0.16% of the inputs stopped at a header's error cell (below), which now read.
- **The formula fuzzer** (`FormulaFuzzTests`): random formulas of the grammar's pieces and stray bytes, moved by random offsets, at and past the grid's edges. A moved formula fits in five times its length, exactly in its own and not in a byte less; unmoved, it is itself; and moved twice, it is moved once by both offsets, which holds only if the shifter reads a moved formula's pieces as it read the formula's. (Where a move rewrites a row or column, in capitals and without leading zeros, and both moves together leave it unmoved, which copies it as it is, the last check is skipped.) 20,000 formulas by default, in 0.2 to 0.4 s; `CSVHELPER_EXCEL_FUZZ_FORMULAS` sets the count. 5,000,000 passed in 65 s on net10.0.
- **What fuzzing found:**
  - **A header cell holding an error, under `ErrorCells = Error`:** `Read()` threw the policy's plain `FormatException` from core's `CaptureHeader` ("Data!E1 holds the error #DIV/0!."), which `Read` doesn't document; it documents `CsvFormatException` for a malformed header. The alternatives were a `CsvFormatException` naming the cell, or documenting it. At review the user chose the recommendation: a header cell is a name, not a value, so it names its column with the error's text under `Error`, as under `Text`. (Under `Null` it is empty, as every error cell is, so the header ends before it.) This is a core change, below.
  - **A reference right after a number** (`20A1`, `$2B1048576`), which Excel can't write, reads as the number's exponent once it moves to column E (`20E1`), so it moves no further. The formula fuzzer found it, twice. Excel's grammar puts an operator after a number, so the shifter is left as it is, and the fuzzer joins its pieces with a space where a number would run into a letter. "Risks" says so.
- **Found reading the code, and measured:** a shared string or a shared formula repeated in many cells multiplied bytes and work that no limit counted. The window bounds a cell's XML, and `MaxUncompressedSize` the XML inflated, but not the fields a cell gives.
  - 3,000 rows each reading one 1 MB shared string give 3.1 GB of fields from a 17 KB zip, under a 16 MB total limit.
  - One row of 300 such cells is a 315 MB record, and allocates 1 GB; core's buffer stops a record at 2 GB ("A single record is too large to buffer").
  - A 1 MB shared formula and 3,000 cells of its group (a 23 KB zip) kept a Release reader busy for 10 s, giving 1.7 GB of fields, before the store's room passed a 4 MB part limit. Under the default 256 MB, a larger formula could keep it busy for days.
  - The shared strings' case is Phase 1's; decision 5 counted gap fields for the same reason.
  - **The fix** (the user's choice at review, of three: this, counting a shared string's or moved formula's every byte, which would make large workbooks reach the limit sooner, or documenting it): a shared string and a moved shared formula count the bytes they have beyond their cell's XML, up to the end of the value, toward `MaxUncompressedSize`, as gap fields count. `SheetScanner.ChargeBeyondCell` is a subtraction and a comparison per shared string cell, and charges nothing for a string shorter than its cell, which ordinary workbooks' strings mostly are. Other fields are no longer than their XML (a `t="d"` serial, at most 24 bytes, can pass its cell by a few). So a reader's fields stay within about the bytes the workbook may inflate to, and the probe's cases now stop within 16 cells under a 16 MB limit. The `MaxUncompressedSize` docs, the scanner's remarks and the design ("Scanner", and Phase 1's "Public API" and decision 5) say so; step 9's A/B of `ExcelPackageAssetsRead` measures it.
  - **Its tests:** `SheetScannerTests` counts both to the byte (a 100-byte string in a 24-byte cell counts 76, a shorter one nothing; a moved formula counts its length less its cell's, and nothing when formulas read as values). `SecurityTests`: a megabyte's shared string read by a million rows, or by a row of 16,384 cells, and a megabyte's shared formula with a million cells in its group, each stop at a 16 MB limit within 16 cells.
- **Mutation check** on net8.0, seven bugs planted one at a time, over the Security, Sheet and Api tests:
  - "shared formula groups not counted": 4 failures;
  - "the room for moved formulas not counted": 3;
  - "rows don't wrap at the grid's edge": 4, the formula fuzzer among them;
  - "every fraction digit read" (a `decimal` overflow): 4;
  - "a `t="d"` text kept with the style's date kind": 1 (`TypedCellScannerTests`; core reads such a field as text, so the fuzzer can't see it);
  - "a `'` in brackets escapes nothing": 0 at first. The one case escaped a balanced pair (`['[A1']]`), so a column name with a bracket of its own, `Table1[x'[A1]` and `Table1[y']A1]`, is now a `FormulaShifterTests` case, which catches it;
  - "a bracket's depth not tracked" (the first `]` ends brackets): 0. It changes nothing a valid formula holds: what follows a closing bracket inside brackets is always another bracketed item.
  - After the review's changes: "shared strings not counted beyond their cells": 6; "moved formulas not counted beyond their cells": 4.
- **Counts:** CsvHelper.Excel.Tests: 999 on each target (20 security cases, 4 scanner cases, the formula fuzzer and the shifter's case are new), all passing on net10.0, net8.0 and net48. CsvHelper.Tests: 1309, 1304 and 844, with the header case added to `ErrorCellsCanFailEveryRead`; CsvHelper.Linq.Tests unchanged. The solution builds with 0 warnings.
- **Core:** one change, flagged and approved at review: `CsvReader.CellHeader` takes `CsvCells.Text` of the cell, or its bytes, without `CellString`'s check of `ThrowOnErrorCells`. It is on the header's path alone, once per file, behind `typedCells`, in a method that isn't inlined. Step 9's deferred gate covers it.

**Step 9, benchmarks** (built 2026-11-04):

- **The orders** (`bench/CsvHelper.Benchmarks`):
  - `OrderRecord` moved from `ParallelWriteBenchmarks.cs` to a file of its own, `OrderRecord.cs`, unchanged but for its XML docs, and `Generate` taking the row count (the parallel write passes `DataGenerator.DefaultRows`). The test project and the corpus tool link it.
  - `ExcelOrders.cs` makes the cells: the header row of member names, then each order's cells. `Id`, `Total`, `Weight`, `Quantity` and `Sku` are numbers in General; `OrderDate` and `ShippedAt` (its UTC time) are serials in `yyyy-mm-dd hh:mm:ss`, which Excel stores as `yyyy\-mm\-dd\ hh:mm:ss`; `Paid` is a boolean; the text, the `Guid` included, is shared strings, and an empty `Notes` (3 orders in 7) has no cell.
  - `ExcelOrders.Generate` gives the orders as the cells hold them: the times to the millisecond, `ShippedAt` at offset zero. The CSV reference is those orders written by `CsvWriter.WriteRecords`.
- **`ExcelTypedWorkbook`** writes rows of strings, doubles, booleans and nulls, each column's number format applying below row 1. The package and the sheet around the cells are `ExcelTextWorkbook`'s, whose helpers it uses (they became `internal`; that file's output is unchanged). Each distinct format is a custom format from 164 with a cell format of its own; a cell in General has no `s`, as in Excel's file.
- **Excel's file:** the corpus tool's new `orders` command has Excel write `excel/orders-1000.xlsx`, the first 1,000 orders entered in one assignment of `Range.Value2`, the date columns formatted first.
  - Entered as typing enters them, text that starts like a number with an exponent, such as the `Guid` `54e5160b-...`, got the scientific format (25 of the 1,000). That comes from entering, not from saving, so the tool clears the text columns' formats after; Excel then drops the unused format when it saves. Setting them to General instead made a cell format of its own, on every text cell.
  - The file joins `CorpusTests`' list, and, like `packageassets-1695.xlsx`, is left out of Excel's answers (12,000 cells). The corpus README describes it, and the privacy check passes.
- **How Excel writes numbers.** Excel's file showed 17 significant digits for most numbers, 15 for about two thirds of the totals, and an exponent for a weight below 0.1 (`5.7447142925787315E-2`). To find the rule, Excel wrote 172,368 chosen numbers through COM (totals in cents, weights at every scale to 1E-9, serials, large numbers, and short decimals from 1E-8 to 1E+7):
  - The text always reads back as the number. When it has 15 digits or fewer, it is exactly the 15-digit rounding, and only when that reads back.
  - Excel sometimes writes 17 digits where 15 read back: 17,345 of the 75,285 such numbers (`36021.370000000003` for 36021.37, but `33002.81`). Nothing tried explains which: not Excel's own parse of the 15 digits (it parses them exactly), not its `TEXT` of 15 digits (exact too), not a threshold on how far the number is from its 15 digits, and not a rounding to 15 digits in double arithmetic. Two numbers as far from their 15 digits, with the same low bits, came out differently (33002.81 short, 33268.56 long).
  - A tie of the 17th digit rounds away from zero (`13463503243.257813`), where .NET rounds to even: 111 numbers.
  - The form: an exponent below 0.01 (`1E-3`), from 0.01 to 0.1 unless there is one significant digit (`0.05`, but `1.23E-2`), and from 1E+16 (`1.2345678901234568E+16`); otherwise none.
  - **The writer's rule:** 15 digits when they read back, else 17, in Excel's form. It gives Excel's text for 154,912 of the 172,368, and for the rest differs only as above. `ExcelTypedWorkbook.Number` documents it.
- **Cross-check** (`Bench/ExcelTypedWorkbookTests`, 28 tests on net10.0 and net8.0; not on net48, where `OrderRecord` needs `Random.NextInt64` and the formatting .NET's digits):
  - Against Excel's `excel/orders-1000.xlsx`: the entries in Excel's order (without its theme and document properties), the sheet from its `dimension` on with each number compared as its value, `sharedStrings.xml` byte for byte, and the `numFmts` and `cellXfs`.
  - The numbers whose text differs are exactly the unexplained case: 314 of the 7,000, among the totals (162), the dates (51 and 58) and the weights (43). Each is Excel's 17 digits against the writer's 15, of the same number. No tie occurs.
  - Both files read through `GetRecords<OrderRecord>`, sync and async, as `ExcelOrders.Generate`'s orders, and through ExcelDataReader as the same values.
  - The Open XML SDK's validator finds no error; a small sheet pins each cell's form (text, number, boolean, a formatted number, no cell for null or empty text) and reads its dates as dates; 19 numbers' texts are Excel's, from its corpus files and the 172,368; numbers that aren't finite throw.
  - Excel opened the writer's 1,000 orders and the benchmark's million read-only through COM without a repair (its `Open` throws when a file needs one): 11,577 values (12,012 cells less the empty notes), and, in 6 s, A1:L1000001 with 11,570,435 values. Numbers, text and booleans come back with their types.
  - **Mutation check** on net10.0, nine bugs planted one at a time, all caught:
    - always 17 digits: 3 failures (the numbers' texts, the sheet);
    - an exponent only below 0.001: 1;
    - booleans as numbers: 5;
    - the header with its column's format: 1;
    - a cell for empty text: 6, the validator's among them;
    - spans per row: 3;
    - no `applyNumberFormat`: 1;
    - `ShippedAt` at its local time: 3;
    - totals as text: 1.
- **`ExcelOrdersRead`** (`ExcelOrdersBenchmarks.cs`): a `ReadBenchmarkBase` whose CSV is the orders', so MB/s is CSV-equivalent.
  - Records: `Sylvan_Records` (the baseline), `CsvHelper_Records` (`GetRecords<OrderRecord>`, the generated mapper), `ExcelDataReader_Records`, and `Csv_Records`, the reference.
  - Values: the same four, each folding every field's typed value into a checksum: the numbers, the dates' ticks, the strings' lengths.
  - Sylvan and ExcelDataReader go through one `Create` and one `Fold` over an `IOrderSource`, a struct per library. Sylvan's getters are used as they are. ExcelDataReader's typed getters unbox the value it read (`GetInt32` is `(int)GetValue(i)`, by its IL), so on a number cell, a boxed `double`, all but `GetDouble` throw: its adapter converts `GetDouble` for the integer and decimal columns, and parses the `Guid` from its string. A date cell has no offset, so the libraries other than CsvHelper make `ShippedAt` at offset zero.
  - MiniExcel is left out (see "Benchmark").
  - The million-order workbook is 114 MB, cached as `orders-1000000-<hash>.xlsx`; the generator's hash now covers the five source files it embeds.
- **Equal work:** in `verify`, the four readers agree in both categories: 1,000,000 orders, hash `199B37C22F244824` (every member, the dates' ticks and offsets, the totals whatever their scale), and checksum `-5419681878775476871`. Since the CSV is written from `ExcelOrders.Generate`, that is also every reader returning the generated orders.
- **QuickRunner:** `verify` hashes lists of orders.
- **The run** (2026-11-04, 10:13–10:20):
  - Tools: BenchmarkDotNet 0.15.8, .NET 10.0.13 (x64, x86-64-v4). Job: `BenchConfig`'s, core 4 pinned, 4 warmups, 12 iterations.
  - One run of the 8 benchmarks took 7 minutes. The machine before: paged pool 3.0 GB, 26.4 GB free; after, the same.
  - The workbook: 113.5 MB, with 487.7 MB of sheet XML, 52.0 MB of shared strings (the million `Guid`s, the header and 15 other texts), and 11,570,435 cells. The CSV is about 191 MB. Standard deviations were at most 2% of the mean but for the CSV reference's Records (5%).

Mean times:

| Library | Records | Values |
| --- | ---: | ---: |
| Sylvan.Data.Excel 0.5.8 (baseline) | 2,251.6 ms (85 MB/s) | 2,193.1 ms (87 MB/s) |
| CsvHelper.Excel | 2,341.0 ms, 1.04x slower | 2,270.9 ms, 1.04x slower |
| ExcelDataReader 3.9.0 | 5,178.0 ms, 2.30x | 5,005.7 ms, 2.28x |
| CsvHelper, the same orders as CSV | 347.7 ms | 293.3 ms |

Allocated per operation, in BenchmarkDotNet's MB, Records / Values: Sylvan 248 / 110, CsvHelper.Excel 411 / 273, ExcelDataReader 3,038 / 2,900, CsvHelper on CSV 261 / 123.

- **What the numbers say:**
  - On typed data, CsvHelper.Excel is within 4% of Sylvan, against 1.36–1.53x on Phase 1's text data. Nothing here measured why. A guess for Phase 9: two thirds of these cells are numbers, booleans and serials, which need no shared-string lookup, where all of Phase 1's cells were shared strings.
  - CsvHelper.Excel's 163 MB more than Sylvan's is in its strings: it makes a string of each text field, as for CSV, where Sylvan returns its table's (Phase 1's finding).
  - Reading from the workbook takes CsvHelper 6.7x the CSV time for Records and 7.7x for Values: about 200 ns per cell, near Phase 1's 160 ns for text cells.
  - ExcelDataReader allocates 2.9 GB, about 250 bytes per cell.
- **Phase 1's benchmark, base against new** (2026-11-04, 09:42–10:13): QuickRunner's `ExcelPackageAssetsRead` for CsvHelper.Excel's three methods, both variants, in three interleaved pairs (base, new; new, base; base, new), each process pinned to CCD0 and QuickRunner pinning core 4. The machine: paged pool 3.1 GB, 38.9 GB free.
  - Base and new wrote the same workbooks: the content of every entry hashed the same, in both variants. (Their caches have different keys, so each run wrote its own.)
  - Two base runs were noisy: the second was about 10% slower in all six methods, and the first's inline Rows and Strings had medians 15% over their minimums. The third pair, the cleanest, gives new/base of 0.992 (Rows), 1.000 (Strings) and 1.000 (Records) with shared strings, and 1.004, 0.993 and 0.988 inline.
  - The best median of each side's three runs, in ms:

| Strings | Method | Base | New | New/base |
| --- | --- | ---: | ---: | ---: |
| Shared | Rows | 2,681.6 | 2,661.1 | 0.992 |
| Shared | Strings | 2,757.5 | 2,750.8 | 0.998 |
| Shared | Records | 3,411.3 | 3,411.5 | 1.000 |
| Inline | Rows | 3,152.3 | 3,149.9 | 0.999 |
| Inline | Strings | 3,230.7 | 3,208.7 | 0.993 |
| Inline | Records | 3,874.6 | 3,914.0 | 1.010 |

  - So the scanner's Phase 2 work (the `f` check, the typed cells' paths, `ChargeBeyondCell` on each shared string) costs nothing measurable here: every method is within 1%, in both directions.
- **The core gate** (2026-11-04, 10:20–11:38, follow-ups and the fix's re-gate to 12:51): step 3's deferred gate, for steps 1 and 2 and step 8's `CellHeader`.
  - Measured as in Phase 0: BenchmarkDotNet against the `b3f8079f` worktree with `FormattedDatesRead` copied in, each process pinned to CCD0 and the job to core 4, pairs interleaved, filters naming the classes exactly. Both NCsvPerf builds were checked against their sources. The machine: paged pool 3.0–3.1 GB and 26.2–26.5 GB free, before and after.
  - **Reads**, 56 benchmarks in 2 pairs: geometric means of new/base 1.006 and 1.001.
    - Six moved more than 3% the same way in both pairs. Four are Sep and Sylvan controls, which don't use CsvHelper: `PackageAssetsRead.Sep_Cols` 1.031, `PackageAssetsReadFile.Sep_Cols` 1.031, `QuotedRead.Sep_Rows` 1.030 and `QuotedRead.Sylvan_Rows` 0.968. `CsvHelper_ExpandoObjectToList` is 0.957, faster; the Expando runs are GC-bound, with standard deviations up to 32 ms of 105 (`CsvHelper_ExpandoObject` read 1.349 in one pair and 0.965 in the other). The sixth is `FormattedDatesRead.CsvHelper_Records`, below.
    - Every other CsvHelper read, the dynamic and Linq ones that steps 1 and 2 touched included, is within 3% or moved in opposite directions in the two pairs.
  - **`FormattedDatesRead.CsvHelper_Records` loses about 3.5%**, step 2's change to the generated mappers:
    - BenchmarkDotNet's pairs gave 1.000 and 1.064. QuickRunner, 4 more pairs: 1.049, 1.067, 1.027, 1.096. Then 12 rounds in rotating order, Records alone: medians of 52.80 ms base and 54.72 ms new (+3.6%), means +3.3%, best runs +2.3%.
    - Allocation falls from 35.9 MB to 12.5 MB per operation, since the `DateOnly`, `TimeOnly` and `TimeSpan` members no longer make a string. `CsvHelper_ParseExact` moved 1–2%, within noise.
    - It isn't the parsing. A micro-benchmark of the three columns alone, over the same 200,000 rows, put `reader.ParseExact<T>` at 31.75 ms against base's `GetString` plus `T.ParseExact` at 33.28. Zeroing the 128-char stack buffer costs about 0.2 ns a field.
    - It is inlining. `CsvValue.ParseExact<T>`, the type dispatch behind every `ParseExact`, was 100 bytes of IL in base: exactly the most the JIT inlines without `AggressiveInlining`. Step 2's three types made it 226, so it stopped inlining. The mapper's Tier 1 code now calls it for each of the five members, where base's called `DateTimeExactCore` and `DateTimeOffsetExactCore` directly. (JIT method summaries and disassembly, from QuickRunner's process, base and new.)
    - **The fix, a core change the user approved at step 9's review:** `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on the two `CsvValue.ParseExact<T>` overloads, with a remark saying why. An instantiation's `typeof` tests fold to one call, so inlining adds only that call. Tried first in a scratch copy: in the same 12 rounds, Records came to 53.34 ms (+1.0% on base), means +1.7%, best +1.4%.
    - **After the fix:** every test passes (CsvHelper.Tests 1309, 1304 and 844; CsvHelper.Linq.Tests 112, 110 and 101; CsvHelper.Excel.Tests 1033, 1033 and 1005), and the solution builds with 0 warnings. In QuickRunner's process, the mapper's Tier 1 code calls `DateTimeExactCore`, `DateTimeOffsetExactCore`, `DateOnlyExactCore`, `TimeOnlyExactCore` and `TimeSpanExactCore` directly (1,561 bytes, 47 inlinees, as in the scratch copy).
    - **Re-gate** (2026-11-04, 12:36–12:43): `FormattedDatesRead` in BenchmarkDotNet, 4 interleaved pairs, measured as above, with 3.1 GB paged pool and 39.5 GB free. Records: 1.037, 1.023, 1.023 and 1.002, a mean of 1.021 (base 52.4–53.4 ms, new 53.5–54.5). `CsvHelper_ParseExact` 0.989 and the Sep control 1.000.
    - **The remaining 2%** wasn't pinned down. A scratch copy without step 1's `typedCells` test in each `ParseExact` came to +1.8% in 12 QuickRunner rounds, against +3.2% for new in the same rounds. But QuickRunner's sessions differ by about 2% for the same code (+1.0% for the fix's copy earlier, +3.2% for new here), so that difference can't be read. A first try at those rounds was disturbed by something else on the machine and was discarded.
    - Only `FormattedDatesRead` uses `ParseExact`.
  - **Writes**, the control (the writer didn't change, nor any file it uses): 15 benchmarks in 2 pairs, geometric means 1.015 and 1.001. `PackageAssetsWrite.CsvHelper_WriteAsync` read 1.067 and then 1.017: base 6.73 and 6.51 ms, new 7.19 and 6.62. Sylvan's writes moved 8–13% in the first pair.
  - **NCsvPerf**, 3 pairs, new/base:
    - `CsvHelperLocal`: 0.991, 1.033 and 0.989 (mean 1.004).
    - `CsvHelperLocal_MT`: 1.020, 1.009 and 1.007 (1.012), with standard deviations of 1.8–10.2 ms on 206–216 ms.
    - The controls' means: Sep 0.983, Sep_MT 0.970, Sylvan 1.003, RecordParser 0.996.
  - So the gate passes. With the fix, `FormattedDatesRead`'s Records is about 2% slower than base, the same way in all 4 pairs, within the ±3% Phase 0 accepted, and it allocates 65% less.

### Phase 3: choosing what to read, and messy sheets (L)

Status: the detailed design below was written and approved on 2026-11-04, with every recommendation accepted. "Changes elsewhere" was applied to the rest of the plan at approval. Steps 1 to 3 were built and committed on 2026-11-04, step 4 built that day and committed on 2026-11-05, steps 5 to 7 built and committed on 2026-11-05, step 8 built on 2026-11-05 and committed on 2026-11-06 with the review's fix, and step 9 written and committed on 2026-11-06. The phase is complete. "As built", at the end of this phase, records the steps, and opens with step 9's summary and the gate re-checked: every item is met but the default path's "every A/B method within 1%", which isn't plainly met, and the user called the phase complete with that item as it stands. Step 2 changed one rule of the design: with formulas read as text, rows above an area and cells outside its columns are read for their shared formulas' definitions. Step 3 widened what is hidden to match Excel, checked through COM again: a `<col>` without a width, or with one that isn't a number, hides its columns, and a custom default row height of 0 hides the rows a sheet lacks. Step 4 made merged cells as strict as Excel, checked through COM: a `ref` Excel can't open throws, and one-cell ranges count in the overlap check before they are dropped. Step 5 changed how the header is found: a scanner of its own reads the rows the header can be in, over a second stream of the sheet's part, and the reader's scanner then reads the rows after it, rather than one scanner pausing after the header row and narrowing its columns; a row given (`At`) is the header even when hidden. Step 6 asked Excel about every corpus workbook's structures over the whole grid rather than a window, and found that Excel opens no workbook with a name of a chartsheet, which openpyxl writes (CsvHelper.Excel still reads such a name, as the review decided; see step 6's "As built"). Step 7 found that each defined name searched every sheet for the sheet of its block, so that opening took the names times the sheets, and made it one lookup. Step 8 put the report benchmark's regions outside its table, since Excel merges no cells in a table. Its A/B of the default path found two methods 1–2% slower; a bisect placed the cost in the scanner without a change to undo. The review took the one candidate, splitting `FlushGaps` so that its test inlines again, and BenchmarkDotNet pairs put both methods within the noise, Shared Rows on one clean pair (see step 8's "As built"). It makes the phase L rather than M: besides the outline, it covers the other ways Excel marks rows and columns hidden, rows the file leaves out of a merged range, Excel's answers for the new structures, a `messy` corpus set, and a benchmark of the new paths. The Excel behavior it relies on was checked through COM on 2026-11-04 (see "Checked on 2026-11-04").

Scope, as planned:

- **Workbook metadata:**
  - Sheets: the dimension. Name, index, visibility and kind already come in Phase 1.
  - Defined names, workbook- and sheet-scoped. Static references only; anything with a formula is not a range.
  - Excel Tables from each sheet's table parts: name, `ref`, header row count and totals row count.
- **Read targets:**
  - All sheets, as one reader per sheet.
  - A named range.
  - A Table by name, using its header and excluding the totals row.
  - An explicit range like `B5:F200`.
- **Header-row detection:** scan the first N rows for the required column names of `T` (from `CsvRecordMapper.TryGetMembers<T>`), skip the title and logo rows above them, and clip to the header's columns. (The design probes `T`'s mapper instead; see "Core changes".)
- **Hidden rows and columns:** skip hidden rows (`<row hidden="1">`) and hidden columns (`<cols>`, which comes before the data). Logical column indexes are remapped.
- **Merged-cell fill:** fill down and across from the top-left cell. `<mergeCells>` comes *after* `<sheetData>`, so turning this on costs a second inflate pass. Document that cost.

#### Public API

Phase 3 adds seven options, the `ExcelHeaderRow` that one of them takes, and the workbook's metadata: a range type, tables, defined names, and sheets' dimensions. Reading still returns a plain `CsvReader`.

```csharp
public sealed record ExcelReaderOptions
{
	// Phases 1 and 2: Default, Sheet, SheetIndex, Csv, MaxPartSize, MaxUncompressedSize, ErrorCells, SignificantDigits, Formulas.

	public string? Range { get; init; }
	public string? Table { get; init; }
	public string? DefinedName { get; init; }
	public ExcelHeaderRow? HeaderRow { get; init; }
	public bool SkipHiddenRows { get; init; }
	public bool SkipHiddenColumns { get; init; }
	public bool FillMergedCells { get; init; }
}

public sealed class ExcelHeaderRow
{
	public static ExcelHeaderRow At(int row);
	public static ExcelHeaderRow Find<T>(int maxRows = 20);
	public static ExcelHeaderRow Find(IEnumerable<string> names, int maxRows = 20);

	public override string ToString();
}

public readonly struct ExcelRange : IEquatable<ExcelRange>
{
	public ExcelRange(int firstRow, int firstColumn, int lastRow, int lastColumn);

	public int FirstRow { get; }
	public int FirstColumn { get; }
	public int LastRow { get; }
	public int LastColumn { get; }
	public int RowCount { get; }
	public int ColumnCount { get; }

	public static ExcelRange Parse(string text);
	public static bool TryParse(string? text, out ExcelRange range);

	public bool Equals(ExcelRange other);
	public override bool Equals(object? obj);
	public override int GetHashCode();
	public override string ToString();

	public static bool operator ==(ExcelRange left, ExcelRange right);
	public static bool operator !=(ExcelRange left, ExcelRange right);
}

public sealed class ExcelSheet
{
	// Phase 1: Name, Index, Visibility, Kind, ToString.

	public ExcelRange? Dimension { get; }
	public IReadOnlyList<ExcelTable> Tables { get; }
}

public sealed class ExcelTable
{
	public string Name { get; }
	public ExcelSheet Sheet { get; }
	public ExcelRange Range { get; }
	public int HeaderRowCount { get; }
	public int TotalsRowCount { get; }
	public IReadOnlyList<string> Columns { get; }

	public override string ToString();
}

public sealed class ExcelDefinedName
{
	public string Name { get; }
	public ExcelSheet? Scope { get; }
	public bool Hidden { get; }
	public string RefersTo { get; }
	public ExcelSheet? Sheet { get; }
	public ExcelRange? Range { get; }

	public override string ToString();
}

public sealed class ExcelWorkbook
{
	// Phase 1: Open, OpenAsync, Sheets, Date1904, OpenReader, OpenReaderAsync, Dispose, DisposeAsync.

	public IReadOnlyList<ExcelTable> Tables { get; }
	public IReadOnlyList<ExcelDefinedName> DefinedNames { get; }
}
```

**What a reader reads.** A reader reads an area of one sheet: the whole sheet, or the area that `Range`, `Table` or `DefinedName` gives.

- **The whole sheet** (none of the three): as in Phases 1 and 2, field `i` is column `i` from `A`, and the first record is the header.
- **`Range`:** cells in Excel's A1 form.
  - `B5:F200`, `B5` (one cell), `B:F` (whole columns) and `5:200` (whole rows), with or without `$`, letters of either case, and the corners in either order (`F200:B5`).
  - A sheet's name in front chooses the sheet, quoted as Excel quotes it: `Orders!B5:F200`, `'Q1 Orders'!B5:F200`. Without one, the range is on the sheet chosen as in Phase 1.
- **`Table`:** an Excel table by its name, ignoring case. The area is the table's columns and its data rows: those below its header row and above its totals row.
  - With `HasHeader`, the header record is the table's column names, whether or not its header row shows (decision 6). Without it, the reader reads the data rows alone.
- **`DefinedName`:** a defined name whose `Range` is set (see "Metadata").
  - `Data` is the workbook's name `Data`, or else the one sheet's name `Data`. `Messy!Data` is the sheet Messy's own, as Excel's Name Box writes it.
  - Excel's built-in names go without their `_xlnm.` prefix: `Messy!Print_Area`.
- **Choosing:**
  - At most one of the three can be set.
  - `Table`, `DefinedName` and a `Range` with a sheet's name choose their sheet. A sheet given as well (`OpenReader`'s argument, `Sheet` or `SheetIndex`) throws `ArgumentException`.
- **In an area:**
  - Field 0 is the area's first column. Cells outside its columns are not fields, and their values are not decoded.
  - Rows above the area are skipped. Reading stops after its last row: nothing of the sheet after it is inflated.
  - The rest is as for a whole sheet: empty rows follow `SkipEmptyLines`, rows are padded to the first record's width, `LineNumber` is the sheet's row and `Location` the cell.
  - With `SkipEmptyLines = false`, record `n` is the area's row `n`, up to the last row of the area that the file has.

**The header row (`HeaderRow`):**

- **`null`** (the default): the area's first record is the header when `HasHeader`, as before. Nothing is clipped.
- **`ExcelHeaderRow.At(row)`:** that sheet row is the header, and it must be in the area. Rows of the area above it are skipped, and the columns are clipped to the header's: from its first cell with a value to its last. (Added at step 5:) The row is the header even when it is hidden and hidden rows are skipped: it is named.
- **`ExcelHeaderRow.Find<T>(maxRows)`:** the header is the first row, of the area's first `maxRows` rows (20 by default), that has every column `T` requires. Rows above it are skipped and the columns clipped, as for `At`.
  - A row has them when `T`'s mapper finds them there, as `GetRecords<T>` would: `TryValidateHeader<T>` on a reader whose header is that row. So alternate names, nested records, `NormalizeHeaderName` and header matching all apply, and optional members are not required.
  - `T` needs a mapper (`[CsvRecord]`); otherwise `OpenReader` throws `InvalidOperationException`, as `GetRecords<T>` does.
  - `Find<dynamic>` takes the first row with a value: dynamic records require no columns.
- **`ExcelHeaderRow.Find(names, maxRows)`:** the same, for a row that has every name, each matched as `TryGetFieldIndex` matches it: for dynamic records and reading by name. An empty list takes the first row with a value, clipped.
- **Candidates** are the area's rows with a value, after hidden rows are skipped and merged cells filled (when those options are on). A typed cell counts as its text, as a header name does (a date names its column `2024-01-15`). Rows count by number: the area's first `maxRows` rows, whether the file has them or not.
- **When:** `OpenReader` (or `OpenReaderAsync`) reads the sheet up to the header row, so a sheet without one fails there. (Changed at step 5:) It reads them with a scanner of its own, before the reader reads them again, so they are inflated twice, and the search's read counts toward `MaxUncompressedSize`.
- **Not found:**
  - `OpenReader` throws `KeyNotFoundException`, naming the rows searched and the row that came closest, with what it lacks, as header validation names columns: "No row of Orders!1:20 has the columns that Order requires. The closest, row 4, lacks Order.Total ('Total')."
  - For `At`, a header row without a value throws `CsvFormatException` naming the row.
- **Not with:** `Table` (a table's header is its column names), or `Csv.HasHeader = false`. Either throws `ArgumentException`.

**Hidden rows and columns** (`SkipHiddenRows` and `SkipHiddenColumns`, both `false` by default, so hidden cells read as in Phases 1 and 2):

- **A hidden row** (checked through COM, below):
  - `hidden="1"` or `hidden="true"`;
  - or `ht="0"` with `customHeight` set (without `customHeight`, Excel ignores the height);
  - or, in a sheet whose `sheetFormatPr` has `zeroHeight`, a row that the file doesn't have. Excel writes that for "hide every row below", with each visible row in the file, empty or not. (Changed at step 3: so does a `sheetFormatPr` with `customHeight` and a `defaultRowHeight` of 0, as Excel reads it.)
  - Rows that a filter hides, and rows of a collapsed outline, are hidden rows too, so `SkipHiddenRows` reads what Excel shows.
  - (Changed at step 3:) Excel also hides a row whose height is too small to show a pixel (0.5 hid a row on the machine checked, 0.74 didn't). That depends on the screen, and is not reproduced.
- **A hidden column:**
  - a `<col>` with `hidden` set, or with `width="0"`; (changed at step 3) or without a width, or with one that isn't a number, which Excel reads as 0;
  - or, when `sheetFormatPr` has `defaultColWidth="0"`, a column that no `<col>` covers.
  - Where `<col>` ranges overlap, the last one that covers a column decides.
  - Excel also hides a column so narrow that it rounds to no pixels (below about 0.07 characters in Calibri 11). That depends on the workbook's font, and is not reproduced.
- **How Excel reads the values** (added at step 3, checked through COM): it decodes references, then reads strictly. A boolean is true only as `1` or `true` (not `TRUE`, ` 1 ` or `yes`). A number has no white space and no `+` (`-0`, `.0`, `0.` and `0e+0` are 0, and so is an empty value). Excel ignores a value it can't read, so it hides nothing, except a column's width, which is then 0.
- **What skipping does:**
  - A hidden row is no record, so record `n` is no longer row `n`; `LineNumber` is still the row.
  - A hidden column is no field: the fields are the area's visible columns, in order. `Location` still names the cell, so field 2 of row 9 is `Messy!E9` when `D` is hidden.

**Merged cells** (`FillMergedCells`, `false` by default):

- Each cell of a merged range reads as its top-left cell, value and kind, whatever it stores itself. That is what Excel shows. Excel's own files have nothing in the other cells, but other producers' can, and Excel's `Value2` gives what they store (decision 14).
- The fill comes first: a top-left cell above or left of the area, or in a hidden row or column, still fills the area's visible cells. Rows the file leaves out inside a merged range are records once they are filled.
- A range whose top-left cell has no value fills with nothing.
- **Cost:** `<mergeCells>` comes after `<sheetData>`, so `OpenReader` reads the sheet's part through once to find it, before the reader reads it again. That second inflate counts toward `MaxUncompressedSize`, and the ranges toward `MaxPartSize`. "Benchmark" measures its time.
- Overlapping ranges, which Excel can't open, throw `CsvFormatException` from `OpenReader`. (Changed at step 4, from COM checks:) so do the `ref` values Excel can't open: none, empty, with white space around it, with a sheet's name, a list, or a cell outside the grid. A one-cell range inside another overlaps it, as Excel reads it. So does XML that can't be read anywhere in the sheet, which the first read finds.

**Metadata:**

- **`ExcelRange`:** a block of cells.
  - Rows and columns are 1-based, as Excel's `Range.Row` and `Range.Column` number them (`A` is 1).
  - `ToString()` gives Excel's form: `B5:F200`, `B5`, `B:F` for whole columns, `5:200` for whole rows.
  - `Parse` and `TryParse` take `Range`'s forms, without a sheet. The constructor and `Parse` throw for a block outside the grid.
- **`ExcelSheet.Dimension`:** the used range the file states (`<dimension ref>`), which Excel keeps: cells with only formatting count, and an empty sheet is `A1`.
  - `null` when the sheet states none (the Open XML SDK, SpreadCheetah, LargeXlsx and Google Sheets write none), when it isn't a worksheet, or when the start of its part can't be read; reading the sheet then says why.
  - It is read on first access, from the start of the sheet's part, and then kept (decision 17). A disposed workbook throws `ObjectDisposedException`, unless it was read before.
- **`ExcelSheet.Tables`** and **`ExcelWorkbook.Tables`:** the tables, by sheet in tab order, and in each sheet in the order of its relationships.
- **`ExcelTable`:**
  - `Name`: the table's `displayName`, which formulas use. COM's `ListObject.Name` gives the part's `name` attribute instead; Excel writes the two alike.
  - `Range`: the table's `ref`: its header, data and totals rows.
  - `HeaderRowCount` (1, or 0 when the header row is hidden) and `TotalsRowCount` (0 or 1).
  - `Columns`: the column names, with `_xHHHH_` escapes decoded.
  - `ToString()`: its name.
- **`ExcelDefinedName`:**
  - `Name`, without `_xlnm.` for Excel's built-in names (`Print_Area`, `_FilterDatabase`).
  - `Scope`: the sheet whose own name it is (from its `localSheetId`, the sheet's index in tab order, chartsheets counted), or `null` for the workbook's.
  - `Hidden`, and `RefersTo`: the formula as the file stores it, without `=`.
  - `Sheet` and `Range`, when `RefersTo` is one block of cells on a worksheet of the workbook:
    - a sheet's name (as `Range` takes it), `!`, then a cell or a block, with or without `$`, in either case, with white space around it;
    - a relative reference reads as stored, which is relative to `A1`: Excel stores relative names that way, and shows them so while `A1` is the active cell;
    - not ranges: constants, formulas, unions (`A1,C3`), intersections, 3-D references (`Sheet1:Sheet3!A1`), other workbooks' (`[1]Sheet1!A1`), `#REF!`, table references (`Sales[Qty]`), and a chartsheet's.
  - `ToString()`: `Data`, or `Messy!Local` for a sheet's own.
- **Reading them:** `Open` reads the defined names with the workbook part, and each worksheet's relationships and table parts. `MaxPartSize` bounds each part, and `MaxUncompressedSize` counts them. Only `Dimension` is read later.

**Reading all sheets:** one reader per sheet, as Phase 1 already allows: a loop over `Sheets` that opens each worksheet's reader in turn (decision 19). The docs give the loop, and a test reads every sheet of a workbook that way.

**Messages** (`ArgumentException` unless noted; the parameter is `options`, or the argument that chose the sheet):

- From `Validate`:
  - "ExcelReaderOptions.Range, ExcelReaderOptions.Table and ExcelReaderOptions.DefinedName can't be set together."
  - "ExcelReaderOptions.Range, \"B5:\", is not a range of cells, such as B5:F200, B:F or 5:200."
  - "ExcelReaderOptions.Table chooses its own sheet, so ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't be set with it." The same for `DefinedName`, and for a `Range` that names its sheet.
  - "ExcelReaderOptions.HeaderRow can't be set with ExcelReaderOptions.Table: a table's header is its column names."
  - "ExcelReaderOptions.HeaderRow needs ExcelReaderOptions.Csv.HasHeader."
- From `OpenReader`:
  - "ExcelReaderOptions.Table chooses its own sheet, so no sheet can be given with it." (and for the other two)
  - "The workbook has no table named \"X\". Its tables are \"Sales\" (Tables!A1:B4), \"NoHeader\" (Tables!D6:E6)." Or "The workbook has no tables."
  - "The workbook has no defined name \"X\"."
  - "The defined name \"Data\" is a name of several sheets: Messy!Data, Tables!Data. Give one of them, such as Messy!Data."
  - "The defined name \"Dyn\" refers to OFFSET(...), which is not a block of cells on a worksheet."
  - "The header row, 3, is not in the range B5:F200." (`ArgumentOutOfRangeException`)
- From `ExcelHeaderRow`: "A row is from 1 to 1048576.", and "maxRows must be from 1 to 1048576." (`ArgumentOutOfRangeException`); a null list or name (`ArgumentNullException`, `ArgumentException`).

#### Checked on 2026-11-04

Excel 365 (16.0 build 20527) through COM, with probe workbooks in a scratch folder. Excel made and saved some of them; the rest were written by hand and opened read-only (`Workbooks.Open` throws when a file needs repairing).

- **Hidden rows:**
  - A hidden row is `hidden="1"`. Excel also hides `hidden="true"`, and `ht="0"` with `customHeight="1"`. `ht="0"` alone, and `defaultRowHeight="0"`, hide nothing.
  - Hiding every row below 19 wrote `zeroHeight="1"`, with each of rows 7 to 19 as an empty `<row>`. A hand-written `zeroHeight` sheet shows the rows it has, unless they are hidden, and hides every row it lacks.
  - A filter wrote `hidden="1"` on the rows it hides (with `filterMode="1"` and an `autoFilter` after `sheetData`). A collapsed group wrote `hidden="1"` and `outlineLevel` on its rows, then `collapsed="1"` on the row after.
- **Hidden columns:**
  - Hiding column `D` wrote `<col min="4" max="4" width="0" hidden="1" customWidth="1"/>`. Excel also hides `hidden="true"`, `width="0"` without `hidden` (with or without `customWidth`), and a width that rounds to no pixels (0.0001).
  - Hiding every column from `H` wrote `<col min="8" max="16384" ... hidden="1"/>` and `defaultColWidth="0"`. A hand-written `defaultColWidth="0"` hides every column that no `<col>` covers.
  - Of two overlapping `<col>` elements, the later decides for the columns it covers.
- **Hidden cells' values,** checked again at step 3 with three rounds of hand-written files:
  - Rows: `hidden` hides as `1`, `true` or `&#49;`; `TRUE`, `True`, ` 1`, `1 `, `true `, `on`, `2`, `-1` and `yes` hide nothing. `customHeight` reads the same way. With `customHeight="1"`, an `ht` of `0`, `0.0`, `-0`, `.0`, `0.`, `0e+0`, `&#48;` or empty hides the row; ` 0`, `0 `, `+0`, `0x0`, `0,0` and `abc` hide nothing; `0.3` to `0.5` and `1e-9` hide it, `0.74`, `0.75` and `1e+0` don't. `customHeight` without `ht` hides nothing, and `hidden="0"` doesn't show a row whose height is 0.
  - Rows the file lacks: `zeroHeight` as `1`, `true` or `&#49;` hides them, and `0` doesn't; `TRUE`, ` 1` and `yes` make a file Excel can't open. A `defaultRowHeight` of `0`, empty or `0.2` hides them with `customHeight` set (`1` or `true`), and nothing without it or with `customHeight="yes"`; `abc` and ` 0` make a file Excel can't open, and `-1` hides nothing.
  - Columns: `hidden` reads as a row's does (`yes`, ` 1`, `TRUE` and `2` hide nothing). A `<col>` without `width` (with only `style`, or only `customWidth`) hides its columns, as does a width of `abc`, ` 9 `, `+9`, empty, `-0`, `0.0` or `1e-9`; `-1`, `9.`, `.5e1` and `&#57;` don't.
  - A `defaultColWidth` of `0`, `0.0`, empty or `0.0001` hides the columns no `<col>` covers; ` 0 `, `abc` and `+0` hide nothing, and `-1` makes a file Excel can't open.
  - Excel can't open a file whose `<col>` has no `min` or `max`, a `min` of 0 or past 16,384, a `min` after its `max`, a `min` with white space, or a `max` of `2.0`; nor one with `<cols/>`, or with `<cols>` after `sheetData`. It takes a `max` past 16,384 as 16,384, `<col>` elements out of order, and two `<cols>` elements.
- **Merged cells:**
  - Excel writes a merged range's other cells as empty cells with the top-left cell's style, and the ranges in `<mergeCells>`, after `sheetData` (and after `autoFilter`, before `pageMargins`).
  - A hand-written file with values in a range's other cells opens. `Value2` and `Text` give each cell's own value, while the sheet shows the range as its top-left cell. A range whose top-left cell is empty reads the same way.
  - Excel can't open a file whose ranges overlap, even repairing it. It accepts a one-cell range.
  - (Added at step 4, with 28 hand-written files opened read-only:) Excel can't open a `mergeCell` without `ref`, or whose `ref` is empty, has white space around it (` A1:B2 `, or a tab by reference), names a sheet (`S!A1:B2`), is a list (`A1:B2 C3:D4`), or names a cell outside the grid (`A1:XFE2`, `A0:B2`). Nor a one-cell range inside another (`B2` or `B2:B2` with `A1:B2`), the same range twice, `mergeCells` before `sheetData`, two `mergeCells`, or `<mergeCells/>`.
  - It takes lowercase (`a1:b2`), `$`, reversed corners (`B2:A1`), whole columns (`A:B`) and rows (`1:2`), references by entity (`&#65;1:B2`), leading zeros (`A01:B02`), and `A1:XFD1048576`, which it shows as `1:1048576`. It ignores elements inside a `mergeCell`, other elements inside `mergeCells`, and a wrong `count`. Ranges that touch are fine.
- **Defined names:**
  - A sheet's own name has a `localSheetId`: the sheet's index in tab order, chartsheets counted (a name of the second sheet, after a chartsheet, was 1).
  - Built-in names are `_xlnm.Print_Area`, and `_xlnm._FilterDatabase`, hidden, which COM shows as `Messy!Print_Area`.
  - A name to a deleted sheet is stored as `#REF!`. A relative name is stored relative to `A1`: `=Messy!B2`, defined with `C3` active, was stored `Messy!B2`, and COM showed it moved by the active cell.
  - Excel reads hand-written names in lowercase (`s!$a$1`), with their corners reversed, with white space or parentheses around them, and as an intersection (`S!$A$1:$B$2 S!$B$1:$B$3` is `B1:B2`). It can't open a file with a name that has no sheet (`$A$1:$B$2`, as the workbook's or a sheet's), or one that starts with `=`.
  - (Added at step 6, with the corpus and variants of openpyxl's file:) Excel can't open a file with a name whose `localSheetId` is a chartsheet's, even by repairing it, whatever the name refers to. It stores a name to a deleted sheet as `#REF!`, which COM shows as `=#REF!#REF!`. `RefersToRange` gives a range for a union (two areas), an `OFFSET` formula and a table reference (`Sales[Qty]` is the column's data cells). COM's `Names` also lists a hidden `_xlfn.X` name of `#NAME?` for each newer function a formula stores with its prefix, which the file doesn't store.
- **Tables:**
  - A table part (`xl/tables/table1.xml`) is related from its sheet (type `table`) and listed in the sheet's `<tableParts>`, after `sheetData`. To Excel, a relationship without a `<tablePart>` is no table.
  - With a totals row: `ref="A1:B4" totalsRowCount="1"`, and an `autoFilter` without the totals row. With the header row hidden: `headerRowCount="0"`, a `ref` without that row, the header cells gone from the sheet, and the names only in `tableColumns`.
  - Excel can't open a table whose header cells differ from its column names: another text, a number for the name `1`, or an empty cell.
  - Formulas use `displayName`: `=ROWS(Shown)` works and `=ROWS(Internal)` is `#NAME?`. COM's `ListObject.Name` is the `name` attribute and `DisplayName` the other. Excel writes them alike.
  - (Added at step 6:) Turning on a table's totals row (`ShowTotals`) inserts cells in the table's columns, moving the cells below it down. Excel shows a structured reference to the table a cell is in without the table's name: `Sales[Qty]` in Sales' totals row shows as `[Qty]`.
  - (Added at step 8, building the report benchmark's workbook:) Excel merges no cells in a table: `Range.Merge` over two of a table's cells fails with `0x800A03EC`, and `ListObjects.Add` over merged cells unmerges them, without a word when alerts are off. Making a table widens each column whose header doesn't fit beside its filter button (22 of the package assets data's 25), and a table over cells with a number format gets a differential format of it (`<dxf><numFmt numFmtId="30" formatCode="@"/></dxf>` for text) for its header row (`headerRowDxfId`) and for each such column (`dataDxfId`, numbered from the last column).
  - (Added at step 8:) Excel writes every cell of a range given a format, an empty one as a cell with only its style, and gives the cells of a merged range a cell format of their own, a copy of the one they had, which it writes on each cell the range covers. A new workbook's page margins are 0.7, 0.75 and 0.3 inches, where a CSV opened in Excel has 0.75, 1 and 0.5.
- **Dimensions:** Excel's `<dimension ref>` covers cells with only formatting and the empty rows it writes (`A1:F19`, where the last value was in row 18); an empty sheet is `A1`. In the corpus, the Open XML SDK, SpreadCheetah, LargeXlsx and Google Sheets write none, and ClosedXML, openpyxl and MiniExcel write `A1:A1` for an empty sheet.

#### Internal design

**New files:**

- `ExcelRange.cs`, `ExcelTable.cs`, `ExcelDefinedName.cs` and `ExcelHeaderRow.cs`: the public types.
- `Internal/Package/TablePart.cs`: a table part's parser.
- `Internal/Sheet/AreaReference.cs`: parsing `[sheet!]area`, for `Range`, defined names, tables' and merged ranges' `ref`.
- `Internal/Sheet/SheetArea.cs`: what a reader reads: its sheet, its rows and columns, and where its header comes from.
- `Internal/Sheet/SheetLayout.cs`: fields from columns: the area's bounds, hidden columns and the header's clip, and the field-to-column map that `Locate` uses.
- `Internal/Sheet/HiddenColumns.cs`: a 16,384-bit set, from `<cols>` and `sheetFormatPr`.
- `Internal/Sheet/MergedCells.cs`: the ranges, the read that finds them, and the fill state the scanner keeps. (Changed at step 4: the fill state is `Internal/Sheet/MergedFill.cs`.)
- `Internal/Sheet/HeaderSearch.cs`: the search, with the capturing target it fills and the one-record source its probe readers read.
- `Internal/Sheet/SheetDimension.cs`: reads `<dimension>` from the start of a sheet's part.

**Opening** (`ExcelWorkbook.LoadAsync`):

- **Defined names:** `WorkbookPart` reads `definedNames` too.
  - Each `definedName` gives its `name`, `localSheetId` and `hidden`, and its text, decoded as a formula's is (`XmlText`, trimmed of XML white space, escapes decoded).
  - A name without `name`, or a `localSheetId` that isn't a sheet's index, throws `InvalidDataException` naming the attribute, as Phase 1's step 3 made the other attributes strict. Of two names with one name and scope, the first counts.
- **Tables:** for each worksheet with a part, its relationships part, if it has one, and the part of each `table` relationship that exists.
  - A table part's root is `table`, by local name, so either namespace and any prefix. `displayName` (else `name`) and `ref` are required; `headerRowCount` defaults to 1 and `totalsRowCount` to 0; `tableColumns/tableColumn/@name` give the names.
  - A value that isn't one (a `ref` that isn't a block, counts past the `ref`'s rows) throws `InvalidDataException`.
  - Tables are found from the relationships, not from `<tableParts>`: that comes after `sheetData`, so finding it would inflate every sheet at `Open`. Excel ignores a table relationship that `<tableParts>` doesn't list, and no producer writes one (see "Risks").

**Choosing what to read** (`OpenReader`):

1. `Validate` checks the new options' combinations, and parses `Range`.
2. The target becomes a `SheetArea`: the sheet; its first and last rows and columns (the whole grid for a sheet); and where its header comes from: the first record (as before), a table's names, or a search (`ExcelHeaderRow`).
   - A table's area is its `ref` less its header and totals rows. The record of its names gets the header row as its line when the table shows one, else 0, which `Locate` gives no location.
   - A table without data rows reads its header alone.
3. With `FillMergedCells`, `MergedCells.LoadAsync` finds the ranges (below).
4. The scanner is created over the sheet's part, with the area, the hidden-state options and the ranges.
5. With a `HeaderRow`, `HeaderSearch` runs the scanner up to the header row (below). With a table, the scanner holds the record of its names.
6. It returns `new CsvReader(source, Csv)`, as before. A failure after the part is opened disposes it.

**The scanner:**

- **The fast path stays.** A whole sheet with nothing skipped or filled has no layout (`null`), and the scanner runs Phase 2's code, with one test of the layout per row and per cell. The QuickRunner A/B under "Benchmark" measures that test.
- **Before `sheetData`,** the scanner reads `sheetFormatPr` (`zeroHeight`, `defaultColWidth`, and from step 3 `customHeight` and `defaultRowHeight`) and `cols` when hidden rows or columns are skipped, and skips them otherwise, as now. `<cols>` comes before `sheetData` in the schema and in every producer here, so the layout is final before the first row.
- **Rows:**
  - A row above the area, or hidden while hidden rows are skipped, or above the header row once the search has found it, is skipped whole (`TrySkipTo`), its cells unread. The exception is a row holding the top-left cell of a merged range that the area needs. (Changed at step 2: with formulas read as text, a row above the area is read for its shared formulas' definitions, and nothing of it is written, so that a group's cells in the area read their formula; see step 2's "As built".)
  - A row past the area's last row ends the sheet (`Done`), so nothing after it is inflated.
  - A row without `r` is read up to its first cell's reference, which gives its number, before it is judged.
  - Missing rows (`SkipEmptyLines = false`) start at the area's first row and leave out hidden ones. In a `zeroHeight` sheet, every missing row is hidden.
- **Cells:** `SheetLayout.FieldOf(column)` gives the field, or -1 for a column outside the area or hidden. A cell of no field is skipped as the rest of a cell is (`CellRest`), its value not decoded, unless it is a merged range's top-left cell, or (changed at step 2) holds a shared formula's definition while formulas read as text. Gap fields and padding work on fields as they work on columns now; the order checks still use columns.
- **`SheetLayout`:**
  - `FieldOf` is `column - firstColumn` within the bounds, or an `int[]` over the area's columns when hidden columns are skipped. `ColumnOf(field)` serves `Locate`.
  - It is final before the first record is written (the header search narrows it in `OpenReader`), and immutable after, so `ParallelEnumerate`'s workers read it without locks.
- **A held first record** (a table's names, or the header row the search found, clipped) is written as the first record of the first fill, with its kinds.

**Merged cells:**

- **Finding them** (`MergedCells.LoadAsync`, sync or async): a second stream of the sheet's part, with its own `XmlScanner`.
  - It checks the root, skips `sheetData` (`TrySkipTo`, whose skipping of text keeps the window small), reads each `mergeCells/mergeCell/@ref`, and stops after `</mergeCells>`, or at the root's end.
  - A `ref` that isn't a block throws `CsvFormatException` for the sheet, as its other content does. One-cell ranges are dropped. (Changed at step 4: after the overlap check, since Excel refuses a one-cell range inside another; and a `ref` with white space around it is no block either.)
  - Each range counts 16 bytes toward `MaxPartSize`: "The merged cells of xl/worksheets/sheet1.xml take more than ExcelReaderOptions.MaxPartSize (N bytes)." (Changed at step 4: 20 bytes, its bounds and its place in the order the ranges end.)
  - The ranges are sorted by first row, then column, and a sweep over them finds overlaps: "Sheet Messy can't be read at byte N of xl/worksheets/sheet1.xml: its merged cells A1:B2 and B2:C3 overlap." (N is the offset of `mergeCells`.)
- **Filling** (the scanner's `MergedCells` state):
  - Ranges become active at their first row and end after their last, in row order. The active ones are kept by column.
  - When the scanner reads a range's top-left cell, in any row, even one it otherwise skips, it copies the cell's field (bytes and kind) into the state's byte store.
  - In a row, cells and fills merge in column order. A cell inside an active range, other than its top-left, is skipped unread. Each fill is the top-left cell's field, added at its column's field (through the layout), and counts its bytes toward `MaxUncompressedSize`, as a shared string beyond its cell does (Phase 2's step 8). (Changed at step 4: at least 1 byte, as an empty field does, and the top-left cell's own field is counted as any cell is, not again.)
  - A row the file lacks inside an active range with a value is written as a record of its fills. So are rows after the last one the file has, while ranges go on. Each such record counts as a missing row does. (Added at step 4: with empty rows kept, the rows between those are empty records, up to the last filled row.)
  - The store holds one field per active range; a range that ends frees its field.

**The header search** (`HeaderSearch.RunAsync`, sync or async):

- It fills a capturing target, one row per fill (full after each record), through the scanner's own `Fill` or `FillAsync`, over the area's first `maxRows` rows by number.
- Each row with a value becomes a probe: a `CsvReader` over a one-record source of its fields and kinds, with the reader's options, so typed cells name their columns as `CaptureHeader` makes them. The probe is asked `TryValidateHeader<T>` (`Find<T>`), or `TryGetFieldIndex` for each name (`Find(names)`).
- `ExcelHeaderRow` holds that test as a delegate, which `Find<T>` makes. So the options stay non-generic, and nothing is reflected.
- The first row that passes is the header. The layout's columns narrow to its first and last fields with a value, the row (narrowed) is held for the first fill, and the scanner goes on after it.
- It keeps the row with the fewest missing columns, for the message. A probe reader is made per candidate, at most `maxRows` of them, each with the reader's buffer.
- `At(row)` reads up to that row and takes it, without a probe.
- (Changed at step 5:) The search has a scanner of its own, over a second stream of the part, whose area is the rows it tries (or the row given) in the area's columns, so it stops at the first row after them. Once it has the header, the reader's scanner is created over the rows after it, in its columns, holding it as its first record, as a table's names are held. So the reader's scanner never changes its layout, width or merged-cell state once it has begun; the rows up to the header are read twice. See step 5's "As built".

**Location:** `SheetRecordSource.Locate` maps a field to its column through the layout. With no layout, the field is the column, as now. The scanner's own errors already name cells by column.

**Dimension:** `SheetDimension.ReadAsync` opens the part, reads the root and its children up to `dimension` (its `ref`) or `sheetData` (none), and closes it: a few KB inflated. `ExcelSheet.Dimension` calls it synchronously on first access, under the workbook's options, and keeps the result. Any exception but `ObjectDisposedException` makes it `null`.

**Validation:** `ExcelReaderOptions.Validate`, which every `Open` and `OpenReader` runs, checks the combinations under "Public API" and parses `Range`. Looking up sheets, tables and names happens in `OpenReader`, which has the workbook.

#### Testing

**Unit tests:**

- **`ExcelRange` and `AreaReference`:** every form (cells, blocks, whole columns and rows, `$`, case, reversed corners, white space, quoted sheet names with `''` in them), round trips through `ToString` over random ranges, and rejections (past `XFD` or row 1,048,576, `A0`, empty parts, unions, 3-D references, `#REF!`, functions, a leading `=`).
- **`TablePart`:** Excel's shape, a hidden header row, totals, escaped names, the strict namespace and prefixes, missing and invalid attributes, a DOCTYPE, and the part limit.
- **Defined names in `WorkbookPart`:** every name of the probes above, scopes with a chartsheet first, hidden and built-in names, duplicates, and an invalid `localSheetId`.
- **`HiddenColumns`:** ranges, overlaps, `defaultColWidth`, `width="0"`, the boolean forms, and invalid `min` and `max`.
- **`MergedCells`:** finding the ranges (after `sheetData`, with prefixes, and stopping after `</mergeCells>`, checked with a read-tracking stream), sorting, overlaps, and the limit.

**Scanner,** in every one of the 16 sheet modes:

- **Areas:** rows and columns clipped, gap fields from the area's first column, padding, `SkipEmptyLines` both ways, the early stop (a read-tracking stream shows nothing read past the refill that holds the area's last row), and rows without `r`.
- **Hidden cells:** hidden rows in each form, `zeroHeight` with rows present and missing, and hidden columns in each form, with gaps and padding.
- **Merged cells:** fills across, down and both; a top-left cell outside the area, in a hidden row or column, or without a value; other cells' values dropped; rows the file lacks, filled; ranges past the last row; one-cell ranges; overlaps; fills counted to the byte.
- **A held first record:** a table's names, and the header the search found.
- **Comparison:** the 100 generated sheets of `SheetScannerComparisonTests`, read again with random areas, hidden rows and columns, and merged ranges that don't overlap, against the reference model, which gains the same rules.

**End to end,** sync and async, on net10.0, net8.0 and net48:

- Each target (`Range` with and without a sheet, `Table`, `DefinedName` by both lookups), each `HeaderRow`, and the three switches, through `GetRecords<T>`, dynamic records, `CsvDataReader`, `Query<T>()` and `ParallelEnumerate`, with `Location` checked through the layout, in errors and in `OnRecordError`.
- Every sheet of a workbook read in a loop.
- The metadata: `Tables`, `DefinedNames`, and `Dimension` (stated, missing, unreadable, a chartsheet's, a disposed workbook's).
- The options' validation, and every message.

**Corpus:**

- **A `messy` content set** (`tools/CsvHelper.Excel.Corpus/Messy.cs`), defined once, like `basic` and `typed`:
  - "Report": a title merged across, a note, an empty row, the header at `B4` with a hidden column, rows with a region merged down them, a merged block, a hidden row, a collapsed group, and a totals line.
  - "Tables": a table with a totals row, one with its header row hidden, one with a line break in a column name, and one away from `A1`.
  - "Filtered": a filter that hides rows, and every row below the data and every column right of it hidden (`zeroHeight`, `defaultColWidth="0"`).
  - A chartsheet before "Tables", so that sheets' names count it.
  - Defined names: the workbook's `Data`; a sheet's own name, and one of the same name on another sheet; a relative name, a union, a constant, a formula, a 3-D reference, whole columns and whole rows, a name to a sheet that was deleted, a hidden name, a table reference, `Print_Area` and `Print_Titles`.
- **Its producers:** Excel (`excel/messy.xlsx`, through COM, which builds each feature as a user would); LibreOffice, from Excel's file; openpyxl, XlsxWriter, Apache POI and ClosedXML, each writing what it can. The corpus `README.md` records what each writes.
- **Excel's answers:** the `oracle` command also records, for every corpus workbook:
  - each worksheet's hidden rows and columns, within its used range and a margin, and each merged cell's `MergeArea`;
  - the workbook's names (`Name`, `RefersTo`, the address of `RefersToRange` or none, `Visible`) and tables (`Name`, `DisplayName`, `Range`, `ShowHeaders`, `ShowTotals`, and the column names).
- **Oracle tests:**
  - **Excel's answers:** every table, name, hidden row and column, and merged range of every workbook, against the metadata and against reads with each option: skipping hidden cells reads exactly the visible ones, and a filled cell reads its range's top-left cell. Each difference is listed with its reason.
  - **ClosedXML** (merged ranges, hidden rows and columns, tables, defined names) and **ExcelDataReader** (merged cells, and hidden rows as a height of 0), with their differences listed.

**Security and fuzzing:**

- A merged range `A1:XFD1048576` around a 1 MB top-left value stops at `MaxUncompressedSize` within a few cells.
- A million merged ranges stop at `MaxPartSize`. A million ranges down a column of rows the file lacks are counted as missing rows are.
- `<cols>` of a million entries, a million defined names, and a table part of a million columns stay within the limits, in linear time.
- A header search over a sheet of a million missing rows stops after `maxRows` rows.
- The fuzzer also varies `Range`, `Table` and `DefinedName` (taken from the workbook's own), `HeaderRow` (`At` a random row, `Find` of a random row's names) and the three switches. Its mutations gain `<cols>`, `<mergeCells>`, `<definedNames>` and table parts.

#### Benchmark

- **No core change,** so no core gate (see "Core changes").
- **The default path:** a QuickRunner A/B of `ExcelPackageAssetsRead` (both variants) and `ExcelOrdersRead`, CsvHelper.Excel's methods, base against new, measured as Phase 2's step 9 measured the scanner: a baseline worktree of `1cd1a0af` at `Csv\_bench\base`, the machine checked first, each process pinned, and three interleaved pairs. Every method within 1%.
- **The new paths:** a new class, `ExcelReportRead`, over the PackageAssets data (shared strings) laid out as a report: two title rows (the first merged across), the header at `B4`, a hidden column, a hidden row in every 50, the region of each 100 rows merged down them, and a table over the data. `GetRecords<PackageAsset>` with:
  - `Range` over the data, the reference; and `Range` over its first 1,000 rows, for the early stop;
  - `Table`;
  - `HeaderRow = ExcelHeaderRow.Find<PackageAsset>()`;
  - `SkipHiddenRows`, and `SkipHiddenColumns`;
  - `FillMergedCells`; and `FillMergedCells` on the plain PackageAssets workbook, which has no ranges: the second inflate alone.
  - The generator writes the report in Excel's shape, checked against a file of Excel's as the others are. The existing variants' bytes don't change.
  - (Changed at step 8, from COM checks:) the table is over the hidden column and the data, not the regions: Excel merges no cells in a table, and unmerges them when it makes a table over merged cells. So `Table` reads one column fewer than `Range`.
- One BenchmarkDotNet run, recorded in "As built".

#### Core changes

None. Phase 3 uses what core has:

- the record source, its batch and `CsvCells.Text`, through `InternalsVisibleTo`;
- the public `TryValidateHeader<T>` and `TryGetFieldIndex`, for the header search.

Considered and avoided:

- **`CsvRecordMapper.TryGetMembers<T>` for the header search,** as the outline said. It describes `T`'s members only when projections are generated, which needs CsvHelper.Linq or `[CsvRecordProjections]`, and it leaves nested records, alternate names, matching and `NormalizeHeaderName` to the caller. Probing the mapper covers all of it, for every mapper.
- **A header search in core,** such as a hook before the header is captured: the source finds the header itself.
- **A public record-source API:** still not wanted (architecture decision 3).

If building turns up a need, it is flagged first and gated, as Phase 2's were.

#### Decisions

Each comes with a recommendation.

1. **Where targets are given.** Recommended: options (`Range`, `Table` and `DefinedName`, as strings), so that `ExcelReader`'s one call reads them too, and no new `OpenReader` overloads. Alternative: overloads taking an `ExcelTable`, an `ExcelDefinedName`, or a sheet and an `ExcelRange` (six more methods with the async forms).
2. **A target with a sheet.** Recommended: `Table`, `DefinedName` and a `Range` that names its sheet choose their sheet, and a sheet given too throws, even the same one. Alternative: accept a sheet that agrees.
3. **A sheet's name in `Range`.** Recommended: accepted, as Excel's Name Box and formulas write ranges (`'Q1 Orders'!B5:F200`). Alternative: cells only, with the sheet from `Sheet`.
4. **Looking up a defined name.** Recommended: the workbook's name, else the only sheet's name of that name (an error lists them when several sheets have one); `Sheet!Name` for a sheet's own; built-in names without `_xlnm.`, as Excel shows them. Alternative: workbook names only, with sheets' names reached through `DefinedNames`.
5. **Which names are ranges.** Recommended: one block on a worksheet, with relative references as stored (relative to `A1`), in the forms Excel tolerates (case, reversed corners, white space). Not ranges: unions, intersections, 3-D and other workbooks' references, `#REF!`, constants and formulas. Alternatives: compute intersections and parentheses, as Excel does; resolve table references (`Sales[Qty]`) to a table's columns, later if asked.
6. **A table's header.** Recommended: the table's column names, always, with `HasHeader`. Excel opens no file whose header cells differ from them, so the two agree wherever Excel reads; the names also head a table whose header row is hidden, which has no header cells. Alternative: the header row's cells, and no header when the row is hidden.
7. **A table's name.** Recommended: `displayName`, which formulas use, for `ExcelTable.Name` and for `Table`. Alternative: match the `name` attribute too, which COM's `ListObject.Name` gives; Excel writes them alike.
8. **Width in an area.** Recommended: Phase 1's rule, unchanged: rows are padded to the first record's width, which is now at most the area's. A table's header has every column, so its rows have its width. Alternative: pad every row to the area's width (`B5:F200` always 5 fields), as Excel's CSV export of a range would; whole rows would then be 16,384 fields.
9. **Rows in an area.** Recommended: reading stops after the area's last row, and with `SkipEmptyLines = false` the rows after the file's last row in the area are not padded on (`B:F` would otherwise be 1,048,576 records). Alternative: pad them on when the area's last row isn't the sheet's.
10. **The header search.** Recommended: `ExcelHeaderRow`, with `At`, `Find<T>` and `Find(names)`, probing `T`'s mapper (`TryValidateHeader<T>`) so that every mapper works, and 20 rows by default. Alternatives: `TryGetMembers<T>` (needs projections; see "Core changes"); a predicate over a row's texts; another default.
11. **When the header is searched.** Recommended: in `OpenReader`, which then fails with `KeyNotFoundException`, as header validation does, before any record is read. Alternative: at the first read, keeping `OpenReader` from reading the sheet (it reads merged ranges there anyway).
12. **Clipping.** Recommended: a header from `HeaderRow` clips the columns to its first and last values; without `HeaderRow`, nothing clips, so field `i` stays column `i` (Phase 1's decision 8). Alternative: clip by default, which would change Phases 1 and 2's reads.
13. **What is hidden.** Recommended: every form Excel hides (`hidden` in either form, `ht="0"` with `customHeight`, the rows a `zeroHeight` sheet lacks, `width="0"`, `defaultColWidth="0"`, and the last `<col>` deciding), with skipping off by default: hidden data is still data, and a filter's hidden rows would vanish. Alternatives: `hidden` alone; skipping on by default.
14. **Merged ranges' other cells.** Recommended: they read as the top-left cell whatever they store, as Excel shows the range. Alternative: fill only the empty ones, as `Value2` gives a cell's own value.
15. **Overlapping merged ranges.** Recommended: `CsvFormatException` from `OpenReader`, since Excel can't open such a file. Alternative: the first range wins.
16. **Finding merged ranges.** Recommended: a second read of the sheet's part in `OpenReader`, counted toward the limits, with its cost measured and documented. Phase 9 can make the skip over `sheetData` cheaper (a raw search for its end tag). No alternative avoids reading the sheet twice, since the ranges come last.
17. **`Dimension`.** Recommended: read on first access, kept, and `null` when the sheet states none or its start can't be read. Alternatives: read every worksheet's at `Open` (an entry opened per sheet), or leave dimensions out.
18. **Tables and names at `Open`.** Recommended: both read at `Open` (names with the workbook part; tables from each worksheet's relationships, small parts), and tables found from relationships rather than `<tableParts>`. Alternative: read them on first use.
19. **Reading all sheets.** Recommended: nothing new: a reader per sheet in a loop, documented and tested. Alternatives: an `ExcelWorkbook.Worksheets` list, or an `OpenReaders()` that opens each worksheet's reader in turn and disposes the one before.
20. **`ExcelRange`.** Recommended: a `readonly struct` with 1-based rows and columns, as Excel numbers them. Alternative: 0-based columns, as CsvHelper counts fields.
21. **The corpus.** Recommended: a new `messy` content set, and the `oracle` command extended to the new structures for every workbook, as Phase 2 did for typed cells.

#### Risks

- **The scanner's speed:** the new tests sit on the per-row and per-cell path. The A/B measures them; if one costs, the whole-sheet path becomes a method of its own, as Phase 0 kept the writer's.
- **The scanner's complexity:** areas, hidden cells, fills, filled missing rows and a held first record all meet in placement. The comparison test's reference model is the check.
- **Merged cells cost a second inflate,** and memory for one field per active range, within the limits.
- **Table relationships without a `<tablePart>`** read as tables where Excel has none. No producer writes one.
- **The header search with a lax `T`:** a type with few required columns can match a title row. `Find(names)` and `At` are the remedies, and the docs say so.
- **Tiny column widths** that Excel shows as hidden are not hidden here, since the threshold depends on the font. (Added at step 3:) the same goes for tiny row heights, whose threshold depends on the screen.
- **Relative names** read relative to `A1`, which is what Excel stores, but not what it shows while another cell is active.

#### Order of work

Each step is reviewable on its own and gets committed when you ask.

1. **Ranges and metadata (M):**
   - `ExcelRange` and `AreaReference`; defined names; table parts at `Open`.
   - `ExcelTable`, `ExcelDefinedName`, `ExcelWorkbook.Tables` and `DefinedNames`, `ExcelSheet.Tables` and `Dimension`.
   - Their tests.
2. **Areas (M):**
   - `Range`, `Table` and `DefinedName`, their validation and resolution.
   - `SheetLayout`; the scanner's rows and columns, the early stop and the held first record; `Locate`.
   - The scanner and end-to-end tests, the comparison's areas, and the loop over every sheet.
3. **Hidden rows and columns (S):** `sheetFormatPr` and `cols`, `HiddenColumns`, the layout's map, and the two options.
4. **Merged cells (M):** `MergedCells`, the read that finds the ranges, the fill, and `FillMergedCells`.
5. **Header row (M):** `ExcelHeaderRow`, `HeaderSearch` and the clip.
6. **Corpus and oracles (M):** the `messy` content and its producers, the `oracle` command's new answers for every workbook, and the oracle tests.
7. **Security and fuzzing (S).**
8. **Benchmarks (M):** the A/B of the default path; the report layout and its cross-check; `ExcelReportRead`; a run.
9. **"As built" (S).**

#### Gate

- Every target (a sheet, `Range`, `Table`, `DefinedName`, and every sheet in turn), every `HeaderRow` and the three switches work through `GetRecords<T>`, dynamic records, `CsvDataReader`, `Query<T>()` and `ParallelEnumerate`, sync and async, on net10.0, net8.0 and net48, with errors naming the right cell.
- The metadata agrees with Excel's answers on every corpus workbook (tables, names and their ranges, hidden rows and columns, merged ranges), each difference documented.
- Reads with the switches on agree with Excel's answers: the visible cells, and each filled cell as its range's top-left cell.
- ClosedXML and ExcelDataReader agree, each difference documented.
- The security and fuzz tests pass, the new options included.
- Nothing in core changes; if something must, it is flagged, approved and passes Phase 0's gate.
- The default path costs nothing measurable (every A/B method within 1%), and the new paths' numbers are recorded, the second inflate's among them.
- CI's pack check still passes.

#### Changes elsewhere

Applied at approval (2026-11-04):

- **Status** (top of the plan): Phase 3's design approved.
- **API sketch:**
  - `ExcelReaderOptions`: `Range`, `Table` and `DefinedName` are strings (a `Range` may name its sheet); `HeaderRow` is an `ExcelHeaderRow` (`At`, `Find<T>`, `Find(names)`).
  - `ExcelWorkbook` gains `Tables` and `DefinedNames`, and `ExcelSheet` `Dimension` and `Tables`. `OpenReader` takes a sheet; a table, a name or a range come through the options.
- **Phase 1's "Public API":** the "Later phases add" line for Phase 3 gets these names. Its "Location" says the field-to-column map is `SheetLayout`.
- **Feature coverage:** "All sheets" notes Phase 1's loop.
- **Testing:** the `messy` content; Excel's answers gain tables, names, hidden rows and columns, and merged ranges.
- **Defaults:** hidden rows and columns are read; merged cells aren't filled; no header search and no clipping unless asked.
- **Risks:** "Merged-cell fill needs a second inflate pass" points at Phase 3's measurement.
- **Phase 9:** a cheaper skip over `sheetData` for finding merged ranges.

#### As built

Filled in step by step; the code is the reference.

**Summary (step 9, 2026-11-06).** Phase 3 is complete. This summary gathers what the steps below record one by one. Every gate item is met but one: the default path's "every A/B method within 1%" isn't plainly met (see "The gate"). On 2026-11-06 the user called the phase complete with that item as it stands, rather than after measuring it again.

- **What it delivers:**
  - **Choosing what to read:** an area of one sheet. That is the whole sheet; a `Range`, with or without its sheet's name; a `Table`, its data rows headed by its column names; or a `DefinedName`, the workbook's or a sheet's own. Rows above the area are skipped, and reading stops after its last row.
  - **Finding the header** (`HeaderRow`): a row given (`At`), or the first of the area's first rows that has `T`'s columns (`Find<T>`, which asks `T`'s mapper) or the names given (`Find(names)`). The columns are clipped to the header's.
  - **Messy sheets:**
    - `SkipHiddenRows` and `SkipHiddenColumns` skip every form Excel hides, read as Excel reads it.
    - `FillMergedCells` reads each cell of a merged range as its top-left cell, the rows the file lacks in a range included.
  - **Public API**, as "Public API" designs it:
    - `ExcelReaderOptions`: `Range`, `Table`, `DefinedName`, `HeaderRow`, `SkipHiddenRows`, `SkipHiddenColumns` and `FillMergedCells`.
    - `ExcelHeaderRow`, `ExcelRange`, `ExcelTable` and `ExcelDefinedName`.
    - `ExcelSheet.Dimension` (read on first access) and `Tables`, and `ExcelWorkbook.Tables` and `DefinedNames` (read at `Open`).
    - Reading still returns a plain `CsvReader`.
  - **Internals:**
    - `AreaReference`, for every reference: `Range`, names, tables' and merged ranges' `ref`;
    - `TablePart`, the defined names in `WorkbookPart`, and `SheetDimension`;
    - `SheetLayout` (fields from columns) and `HeldRecord` (a first record written before the rows);
    - `HiddenState` (how Excel reads the attributes that hide) and `HiddenColumns`;
    - `MergedCells` (the read that finds the ranges, with Excel's checks) and `MergedFill` (the fill);
    - `HeaderSearch`;
    - in the scanner: the layout, `sheetFormatPr` and `cols`, the early stop, top-left cells captured wherever they are, fills and filled rows, and `FlushGaps`'s test split from `WriteGaps`.
  - **Core:** no change.
  - **Size:** CsvHelper.Excel has 12,855 lines of library code (8,556 at Phase 2's end), and 17,893 of tests (11,467). The report benchmark's three files are 672 lines.
  - **Tests:**
    - CsvHelper.Excel.Tests: 1,791 on net10.0 and net8.0, and 1,763 on net48 (1,033 and 1,005 at Phase 2's end).
    - CsvHelper.Tests: 1,309, 1,304 and 844, and CsvHelper.Linq.Tests: 112, 110 and 101. Both are unchanged.
  - **Corpus:** 7 new workbooks.
    - The `messy` content (title rows, merged cells, hidden rows and columns, a filter, tables, defined names and a chartsheet), from Excel, LibreOffice, openpyxl (a file Excel can't open), XlsxWriter, Apache POI and ClosedXML.
    - Excel's `packageassets-report-250.xlsx`, for the report writer's cross-check.
    - The tests now read 41 workbooks, besides the three CsvHelper.Excel rejects.
    - Excel's answers, now 39 files, also give every workbook's tables and names, and every sheet's hidden rows and columns and merged ranges, over the whole grid. openpyxl's messy file is answered as refused.
  - **Benchmarks:** `ExcelReportRead`, the PackageAssets data laid out as a report. A test checks its writer byte for byte against Excel's file.
- **The gate**, item by item, re-checked on 2026-11-06:
  - Every target, `HeaderRow` and switch works through `GetRecords<T>`, dynamic records, `CsvDataReader`, `Query<T>()` and `ParallelEnumerate`, sync and async, on the three targets, with errors naming the right cell: met (steps 2 to 5, and step 7's fuzzer).
    - Every target, each `HeaderRow` and each switch is read through `GetRecords<T>`, in readers opened sync and async: a sheet, `Range` with and without its sheet, `Table`, `DefinedName` by each lookup, and every sheet in a loop. The tests are `AreaReadingTests`, `HeaderReadingTests`, `HiddenReadingTests` and `MergedReadingTests`.
    - The other four APIs run over each new path, not over every combination:
      - a range goes through all five with a header search (`FindsTheHeaderBelowATitle`) and with merged cells filled (`FillsMergedCells`);
      - a table goes through all five (`ReadsATable`, and `ReadsATablesShownCells` with both hidden switches);
      - `Find<dynamic>` goes through dynamic records;
      - a defined name becomes the same area as a range, and goes through `GetRecords<T>` and `ParallelEnumerate`.
    - Each class's `NamesTheCellsOfFieldsThatDontParse` checks the cell named after an area, a hidden column, a fill and a clip, in `GetRecords<T>`, `OnRecordError` and `ParallelEnumerate`.
    - The fuzzer reads areas, tables, names, header rows and the switches through records, fields and strings.
  - The metadata agrees with Excel's answers on every corpus workbook, each difference documented: met (step 6).
    - `StructureAnswersTests` covers the 38 answered workbooks. Tables, merged ranges, and hidden rows and columns agree everywhere.
    - 20 names differ, each listed with its reason. Excel's `RefersToRange` gives a range for a union, an `OFFSET` formula and a table reference, which decision 5 doesn't take as ranges. A name to a deleted sheet is `#REF!`, which COM shows as `#REF!#REF!`.
  - Reads with the switches agree with Excel's answers: met (step 6). `StructureAnswersTests` reads every sheet of every workbook, sync and async: with hidden rows and columns skipped, with merged cells filled, and with all three. Each field is the cell Excel shows.
  - ClosedXML and ExcelDataReader agree, each difference documented: met (step 6).
    - `OracleTests` lists ClosedXML's 27 differences, in five messy files, and ExcelDataReader's 6, in four, each with its reason.
    - Merged ranges agree with both everywhere.
  - The security and fuzz tests pass, the new options included: met (step 7, and again here).
    - `SecurityTests`' 12 cases, sync and async, pass in the suite.
    - Step 7 ran the fuzzer's 200,000 inputs before step 8 changed the scanner. The runs were made again on 2026-11-06, on the full Release build, and passed on each target: 1 min 34 s (net10.0), 3 min 9 s (net8.0) and 5 min 16 s (net48).
    - Of net10.0's inputs, 37% read to the end, 22% failed at a sheet with `CsvFormatException`, 9% at a sheet with `InvalidDataException`, and 32% as a workbook, as at step 7. Its reads of areas: 63,649 ranges read and 12,708 threw; 14,398 ranges with a sheet's name read and 3,899 threw; 7,791 tables read and 2,426 threw; 7,255 defined names read and 1,879 threw. Header rows: 58,558 given read and 106 threw; 59,445 searched for read and 131 threw, none for want of the row.
    - The tallies differ a little from step 7's (64,718 ranges read then), since step 8's corpus file joined the fuzzer's sources. The three targets' agree within a few dozen, as at step 7.
  - Nothing in core changes: met.
    - `git diff 1cd1a0af` (Phase 2's end) is empty over `src/CsvHelper`, `src/CsvHelper.Linq`, `src/CsvHelper.Generators`, `tests/CsvHelper.Tests` and `tests/CsvHelper.Linq.Tests`, in the commits and in the working tree. No commit since touches them.
    - So no core gate was needed.
  - The default path costs nothing measurable (every A/B method within 1%): **not plainly met.**
    - Step 8's QuickRunner A/B ran base `1cd1a0af` against new in three interleaved pairs. Six of the eight methods were within 1% (Orders Values at the edge, +1.0%; inline Strings and Records on two pairs, the third disturbed). `ExcelOrdersRead.CsvHelper_Records` was +2.1%, and shared Rows +1.2%.
    - A bisect put the cost in `SheetScanner.cs`, with no change to undo. The review took the one candidate: splitting `FlushGaps` so that its test inlines again.
    - After the split came BenchmarkDotNet pairs, each counted only when both runs' standard deviations were under 2% of their means, on a machine shared with another project's tests:
      - Orders Records is within the noise on three clean pairs (1.008, 0.978 and 0.989).
      - Shared Rows has one clean pair, at +1.2% (1.012). The review accepted it as within the noise, which is 1–2% between sessions.
      - Inline Rows had no clean pair.
      - The other five methods weren't measured after the split.
    - So shared Rows isn't shown to be within 1%, and Orders Records is shown only to be within the noise, about 2%. "Left open" proposes measuring again on a quiet machine.
    - The user's decision (2026-11-06): Phase 3 is complete with this item as it stands. Measuring again is left open.
  - The new paths' numbers are recorded, the second inflate's among them: met (step 8; see "The numbers").
  - CI's pack check still passes: met, run again on 2026-11-06 as CI runs it.
    - The steps were restore, a full Release build (`--no-incremental`), the tests, then the check. The package went to a scratch folder rather than `pack-check`, which `.gitignore` doesn't cover.
    - CsvHelper is `[0.0.0-ci]` in all 5 targets, and net47 has System.IO.Compression.
  - Every test in the solution passed on 2026-11-06 on net10.0, net8.0 and net48, with the counts above. The solution builds with 0 warnings and 0 errors. The logs of these runs, and their scripts, are in `Csv\_bench\results\phase3\step9`.
- **What changed from the design, and why** (each step's "As built" has the details):
  - **Shared formulas outside an area** (step 2): with formulas read as text, rows above an area and cells outside its columns are read for their shared formulas' definitions, and nothing of them is written.
    - Without this, a group's cells in the area would throw, since their definition was skipped unread.
    - A column of formulas filled down from row 2 does that for any range below row 2.
  - **What is hidden** (step 3, from three more rounds of COM checks):
    - Excel decodes a value's references and reads it strictly, and it ignores a value it can't read. A column's width is the exception: a `<col>` without a width, or with one that isn't a number, hides its columns.
    - A custom default row height of 0 hides the rows a sheet lacks, as `zeroHeight` does.
  - **Merged cells as strict as Excel** (step 4, from 28 hand-written files):
    - A `ref` Excel can't open throws when the reader opens, and a one-cell range inside another overlaps it.
    - Each range counts 20 bytes toward `MaxPartSize`, not 16, and the fill state has a file of its own, `MergedFill.cs`.
  - **The header search has a scanner of its own** (step 5):
    - The design paused the reader's scanner after the header and narrowed its layout. That would have re-keyed the merged fill's open ranges mid-sheet.
    - So the search reads the rows it tries over a second stream, and the reader's scanner starts after the header, holding it. The scanner never changes once it has begun, and the rows up to the header are inflated twice.
    - A row given (`At`) is the header even when it is hidden, since it is named.
  - **The corpus** (step 6):
    - Excel was asked about the whole grid rather than a window around the used range.
    - Filtered gained an empty row 6, so that some file lacks a row under `zeroHeight`.
    - Excel opens no workbook with a name of a chartsheet, and openpyxl writes one after a chartsheet. CsvHelper.Excel reads such a name as the file says, the review's choice.
  - **One lookup for each name's sheet** (step 7): each defined name searched every sheet for its block's sheet, so opening took the names times the sheets. A million names after 100,000 sheets took 6 minutes. A dictionary of the worksheets by name makes it one lookup, and about a second.
  - **The report benchmark** (step 8): its regions are outside its table, since Excel merges no cells in a table. So `Table` reads one column fewer than `Range`. The review's `FlushGaps` split is under "The gate".
  - **Smaller decisions**, each in its step:
    - `ExcelRange` stores its bounds from 0, so that its default is `A1` (step 1).
    - A table's header record has one field per column of its `ref` (step 2).
    - With merged cells filled, XML that can't be read anywhere in the sheet throws when the reader opens, without its cell (step 4).
- **What Excel's COM checks taught** ("Checked on 2026-11-04" has every case):
  - **Excel reads its own attributes strictly.** A boolean is `1` or `true`. A number has no white space and no `+`, and an empty one is 0. It ignores what it can't read, a column's width aside. Some forms that look harmless (`zeroHeight="TRUE"`, a `<col>` without `min`, `<cols/>`) make a file it can't open.
  - **More than `hidden` hides a row or column:**
    - a height of 0 with `customHeight`, and the rows a `zeroHeight` sheet lacks;
    - a column's width of 0 or none, and `defaultColWidth="0"`.
    - Excel also hides heights and widths too small to show a pixel. Those depend on the screen and the font, and aren't reproduced.
  - **Excel refuses more than the schema does:**
    - overlapping merged ranges, even when repairing, and a one-cell range inside another;
    - a merged `ref` with white space around it, a sheet's name or a list;
    - a table whose header cells differ from its column names;
    - a name without a sheet, and a name of a chartsheet.
  - **Names:**
    - `localSheetId` counts chartsheets.
    - A relative name is stored relative to `A1`.
    - A name to a deleted sheet is stored as `#REF!`.
    - `RefersToRange` evaluates unions, `OFFSET` and table references, which CsvHelper.Excel leaves as no range (decision 5).
  - **Tables:**
    - Formulas use `displayName`.
    - Turning on a totals row inserts cells.
    - Excel merges no cells in a table, and unmerges them when it makes one over them.
    - Making a table widens its columns and adds differential formats.
  - **Asking Excel takes care.** Right after it opens a workbook with a pivot table, COM answers "busy" while Excel builds it, and a first version of the `oracle` command took that for an answer. The command now retries busy calls, and takes only Excel's own failure to mean "none".
- **The numbers** (step 8):
  - **The new paths,** in BenchmarkDotNet, over a million rows of the report (305 MB as CSV):
    - `Range`: 4.35 s (70 MB/s).
    - Its first 1,000 rows: 4.2 ms, the early stop.
    - `Table` +1.9%, the header search +1.4%, skipping hidden columns +1.3%, and skipping hidden rows 0.3% faster.
  - **Filling merged cells costs its second read:**
    - +44% on the report (6.27 s);
    - +34% on the plain workbook, which has no ranges: 1.18 s to find none.
    - That is 714 MB of sheet XML through the XML scanner's skip, at about 600 MB/s. Inflating alone runs at 5.3–5.6 GB/s.
  - The report reads 24% slower than the plain workbook through `Range`, for 35% more sheet XML.
  - **The default path:** six of eight methods within 1% before the split. After it, Orders Records is within the noise, and shared Rows is +1.2% on one clean pair (see "The gate").
  - **Defined names:** a million after 100,000 sheets open in about a second, where they took 6 minutes before step 7's fix.
- **Left open:**
  - **Measuring the default path again**, which the user chose not to wait for in calling the phase complete, and carried into Phase 9's list on 2026-11-06. That needs a quiet machine and a new baseline worktree of `1cd1a0af` at `Csv\_bench\base`, since step 8's was removed. Proposed, not done:
    - BenchmarkDotNet pairs of all eight methods, with the Sylvan control, interleaved and pinned as at step 8, counting clean pairs only;
    - the worktree checked out with LF line ends, so that both sides' generator sources hash alike and share the cached workbooks.
  - **Step 4's four mutants that weren't run**, left when the run was stopped for low memory, and not run since: "fills from the first field each time", "runs ignore hidden columns", "runs end at the column, not the shown field", and "the option ignored".
  - **Phase 9:**
    - a cheaper skip over `sheetData` for finding merged ranges: the second read takes a third of a plain sheet's time;
    - the scanner's sensitivity to the size and placement of its code, which step 8's bisect met: 1–2% that follows no single change;
    - Phases 1 and 2's open items.
  - **Left as they are, each documented:**
    - a name of a chartsheet, read as the file says (step 6, the review's choice);
    - heights and widths too small to show a pixel, which aren't hidden ("Risks");
    - relative names read relative to `A1`, as stored ("Risks");
    - unions, intersections, `OFFSET` and table references, which aren't ranges (decision 5);
    - a table relationship without a `<tablePart>`, which reads as a table where Excel has none ("Risks");
    - with merged cells filled, XML errors anywhere in the sheet, reported when the reader opens, without their cell (step 4).

**Step 1, ranges and metadata** (built 2026-11-04, committed with steps 1 to 3 that day):

- **New files:**
  - Public: `ExcelRange.cs`, `ExcelTable.cs` and `ExcelDefinedName.cs`.
  - `Internal/Sheet/AreaReference.cs`, `Internal/Package/TablePart.cs` and `Internal/Sheet/SheetDimension.cs`.
- **Changed:**
  - `WorkbookPart` reads `definedNames` (into the new `WorkbookName`), checks them, then reads each worksheet's tables into `WorkbookSheet.Tables`.
  - `XmlElements.ReadTextAsync` reads an element's text through its end tag.
  - `ExcelSheet` gained `Tables` and `Dimension`, and `ExcelWorkbook` `Tables`, `DefinedNames` and the internal `ReadDimension`.
- **Decisions made while building:**
  - **`ExcelRange` stores its rows and columns from 0,** so that its default value is a block, `A1`, rather than an invalid one.
    - The constructor wants each first row or column at or before its last, and throws `ArgumentOutOfRangeException` naming the parameter. `Parse` puts reversed corners in order.
    - `Parse` throws `FormatException`: "\"B5:\" is not a range of cells, such as B5:F200, B:F or 5:200."
    - A leading zero in a row (`A01`) is accepted, as Excel's formulas accept it. The whole sheet's text is `1:1048576`, as Excel's addresses write it.
  - **An unquoted sheet name** is everything before the first `!`, without `' [ ] : , ( ) " # =` or white space. So `Q1 Orders!A1`, which Excel would quote, is not a block, and neither is `#REF!$A$1`.
  - **Defined names:**
    - A name's text is decoded as XML, trimmed of XML white space, then decoded for its escapes, as a cell's formula is. Elements in it are skipped, and a comment splits its text without ending it.
    - An empty `name` is rejected as a missing one.
    - `localSheetId` is checked once the part is read, since the sheets must be known, so its message has no byte offset: "The defined name \"X\" of xl/workbook.xml has the localSheetId 2, which is not a sheet's index: its sheets are 0 to 1."
    - A name's scope can be a chartsheet, since `localSheetId` counts them; `ToString` then names it (`Chart!OfChart`).
    - The `_xlnm.` prefix is matched ignoring case.
  - **Tables:**
    - An empty `displayName` falls back to `name`, as a missing one does.
    - A relationship of type `table` (by its last segment, so the strict URI too) whose part exists names a table. External targets, missing parts and other types are skipped, even when they point at a table part.
    - A sheet's relationships part that can't be read stops `Open`, as every part it reads does.
    - A `tableColumn` without a `name` throws. How many names a table has against its `ref`'s columns is not checked here; step 2 decides how a table's header uses them.
  - **`Dimension`:**
    - `InvalidDataException` and `IOException` from reading the sheet's start make it `null`.
    - A sheet that isn't a worksheet, or has no part, is `null` without reading anything, even after the workbook is disposed.
    - A worksheet's dimension not read before the workbook is disposed throws `ObjectDisposedException`: "The workbook is disposed."
  - Until step 2 adds `Range` and `Table` to the options, the new types' docs name them as text rather than links.
- **Tests** (168 new per target: 1,201 on net10.0 and net8.0, 1,173 on net48; all passing):
  - **`ExcelRangeTests`:** 24 forms, 34 rejections, the constructor's checks, the default, equality, and 5,000 random blocks round-tripped through their text, in both cases.
  - **`AreaReferenceTests`:** 21 blocks with and without sheets (Excel's forms from the probes among them), 24 that aren't one block, and UTF-8.
  - **`TablePartTests`:** Excel's two tables from the probe (totals, a hidden header row), every sheet's tables in relationship order, names and escaped column names, the strict namespace and prefixes, relationships that are no table's, 9 invalid tables, broken parts, and the part limit exactly at and one past.
  - **`DefinedNamesTests`:** Excel's 16 names from the probe, decoding (entities, CDATA, a comment, escapes, white space, an element inside), the first of a name in a scope, prefixed strict names, and 7 invalid names.
  - **`MetadataTests`**, through the public API: tables and names with their blocks; finding a block's sheet as Excel does (case, quotes, chartsheets, missing sheets, no sheet, scopes on chartsheets); the corpus's tables and names (Excel's typed workbooks, LibreOffice's and XlsxWriter's have the table `Orders`, `Formulas!A20:D23`; every other workbook none); dimensions in each form; a dimension read once from the start of a 1.5 MB sheet under a 1 MB limit, and kept after disposal; sheets without parts; and a table part's limit at `Open`.
  - `TestPackage` gained `Table`, `Relate` and `DefinedNames`.
- **Mutation check** on net10.0, 16 bugs planted one at a time, all caught:
  - "reversed corners not put in order": 5 failures;
  - "3-D references read as a sheet's": 1;
  - "built-in names keep `_xlnm.`": 4;
  - "a name's sheet matched with case": 2;
  - "a chartsheet's block is a range": 2;
  - "names repeated in a scope kept": 2;
  - "`localSheetId` not checked against the sheets": 1;
  - "a name's text not trimmed": 2;
  - "only the last text token of a name kept": 2;
  - "a table's name over its display name": 2;
  - "column names' escapes not decoded": 4;
  - "header and totals rows not checked": 2;
  - "every relationship's part read as a table": 46, the corpus's among them, whose drawing parts don't read as tables;
  - "the dimension read past `sheetData`": 2;
  - "the dimension not kept": 2;
  - "whole columns before whole rows in text": 6.
- **Core:** no change. The solution builds with 0 warnings.

**Step 2, areas** (built 2026-11-04, committed with steps 1 to 3 that day):

- **New files:** `Internal/Sheet/SheetLayout.cs` (with `RowPlace`: above, in or below an area) and `Internal/Sheet/HeldRecord.cs`, the first record the scanner writes before the rows. The design had no file for the latter.
- **Changed:**
  - `ExcelReaderOptions`: `Range`, `Table` and `DefinedName`, their checks in `Validate`, and the internal `ChoosesSheet`.
  - `ExcelWorkbook`: `Target` turns the options into a sheet, a layout and a held record; `FindTable` and `FindName` look tables and names up.
  - `SheetScanner`:
    - it takes a layout and holds a first record;
    - `LeaveRow` skips a row above the area or ends the sheet at one below it;
    - `SkipCell` passes over a cell that is no field;
    - fields are placed by the cell's field (`cellField`) rather than its column;
    - `EndRow` returns a `Step`, since a row without cells can end the sheet.
  - `SheetRecordSource.Locate` maps fields to columns through the layout.
  - `AreaReference.TrySplitSheet`, split out of `TryParse`, so that defined names use the same rules for a sheet's name.
- **Decisions made while building:**
  - **Shared formulas defined outside the area** (a design change):
    - With formulas read as text, a group's cells in the area would throw if the group's defining cell were above or left of the area and skipped unread: "it uses shared formula 0, which no cell before it defines". A column of formulas filled down from row 2 does that for any range below row 2.
    - So, with formulas read as text, rows above the area and cells outside its columns are read for their shared formulas' definitions alone. Their values aren't decoded, and nothing of them is written.
    - With cached values, they are skipped unread, as designed. Step 4's merged ranges' top-left cells will use the same path.
  - **Order checks outside the area:** every row's number is still checked, those above the area included. Cells are checked in the rows of the area, outside its columns too; the cells of rows above it aren't read, so their order isn't checked.
  - **A table's header record** has one field per column of its `ref`. A column without a name gets an empty one, and names past the `ref`'s columns are left out.
    - Its line is the header row, or 0 when the header row is hidden: `LineNumber` is 0 for it, and it has no location.
    - A table without data rows is an area whose last row is above its first, and reads its header alone.
  - **Looking up a defined name:**
    - The `_xlnm.` prefix is optional, in any case.
    - A sheet's name in front follows `Range`'s rules: quoted when Excel would quote it.
    - A name whose sheet can't be read (an unclosed quote) is "no defined name".
    - The message for a name of several sheets lists them as `ExcelDefinedName.ToString` writes them.
  - **A sheet given with an option that chooses its own:**
    - Given to `OpenReader`, it throws `ArgumentException` naming that argument (`sheetName`, `sheetIndex` or `sheet`).
    - Given as `Sheet` or `SheetIndex`, `Validate` throws, at `Open` too.
  - **Messages** as designed. A workbook without tables ends the table message with "It has no tables.", as the sheets' message ends.
  - `ExcelWorkbook`'s remarks no longer say that dates read as serials, which hasn't been so since Phase 2.
- **Tests** (33 new per target: 1,234 on net10.0 and net8.0, 1,206 on net48; all passing):
  - **`AreaScannerTests`** (10), in every sheet mode:
    - rows and columns, whole rows and columns, areas past the data;
    - fields from the area's first column, with gaps and padding;
    - missing rows only in the area, and none after the file's last row in it;
    - rows without `r`;
    - nothing read after the first row below the area, which a one-byte stream shows stopping at that row's tag;
    - an area without rows;
    - the held record (the width it sets, empty names, line 0);
    - cells outside the area left undecoded;
    - errors naming their cells, with order checked outside the area;
    - shared formulas defined above and left of the area.
  - **`SheetScannerComparisonTests.MatchesTheReferenceInGeneratedAreas`:** the 100 generated sheets, each with a random area, in all 16 modes with empty rows skipped and kept, against the reference model, which gained the areas' rules.
  - **`AreaReadingTests`** (11), sync and async, through the public API:
    - ranges, with and without a sheet, and whole columns and rows; a range's empty rows kept, with their lines;
    - tables: totals excluded, a hidden header row, without `HasHeader`, dynamic records, `CsvDataReader`, `Query<T>()`, headers fitted to the columns, and the header's line;
    - defined names, by every lookup;
    - every error of choosing, and every check of the options at `Open` and at `OpenReader`;
    - errors naming their cells through the layout, in `GetRecords<T>`, `OnRecordError` and `ParallelEnumerate`;
    - `ExcelReader` with a table; every sheet read in a loop; a range read under a limit its sheet passes.
  - `CellReading` takes a layout and a held record, and `Sheets.Open` passes them.
- **Mutation check** on net10.0, 19 bugs planted one at a time, all caught:
  - "rows above the area read": 20 failures;
  - "rows below the area skipped, not the end": 3, the early stop's tests;
  - "fields not counted from the area's first column": 15;
  - "missing rows from the last row above, not the area's first": 3;
  - "the held record not written": 10;
  - "the held record sets no width": 1;
  - "a table's totals row read": 4;
  - "a table's header row read as data": 8;
  - "a table's names not fitted to its columns": 3;
  - "a hidden header row given the table's first row": 2 (with the header's line, which the first run had no test for);
  - "a sheet's name preferred to the workbook's": 6;
  - "a name of several sheets not reported": 2;
  - "locations ignore the layout": 2;
  - "an option that chooses its sheet allowed with `Sheet`": 2;
  - "a sheet argument allowed with a table": 2;
  - "a range's sheet ignored": 4;
  - "definitions outside the area's columns not read": 1;
  - "rows above the area not read for definitions": 1;
  - "cells outside the area written": 17.
- **Core:** no change. The solution builds with 0 warnings. The scanner's new tests on the per-row and per-cell path are measured at step 8.

**Step 3, hidden rows and columns** (built 2026-11-04, committed with steps 1 to 3 that day):

- **New files:**
  - `Internal/Sheet/HiddenColumns.cs`, as designed: two sets of 16,384 bits, the columns a `<col>` covers and those it hides, so that the last `<col>` over a column decides, and a default for the rest. A `<col>` over many columns fills whole words, so a million `<col min="1" max="16384">` cost a million short fills, not a loop over every column.
  - `Internal/Sheet/HiddenState.cs`, which the design had no file for: how Excel reads the attributes that hide rows and columns (below), and `RowHiding`, the flags of a row's attributes.
- **Changed:**
  - `ExcelReaderOptions`: `SkipHiddenRows` and `SkipHiddenColumns`, with no checks: any value is valid.
  - `SheetScanner`:
    - it takes the two options;
    - before `sheetData`, it reads `sheetFormatPr` (`ReadFormat`) and `cols`, one `<col>` tag at a time across refills (a new `Cols` mode, `ReadColumn`);
    - a row's `hidden`, `ht` and `customHeight` are read with its `r`;
    - `SkipRow`, split out of `LeaveRow`, skips a hidden row as one above the area;
    - `BeginRows`, at `sheetData` or at the end of a sheet without one, makes the layout final and writes the held record;
    - `SetRow` writes missing rows only while `keepsMissingRows` holds, which is false when empty rows are skipped or the sheet hides the rows it lacks.
  - `SheetLayout`:
    - `Without(HiddenColumns)` gives a layout whose fields are the area's shown columns, through a map from columns to fields and one back;
    - `SheetLayout.Sheet` is the whole sheet's layout;
    - `Width` counts the fields.
  - `HeldRecord`: each field has its column, and `WriteTo` leaves out the fields of columns the layout doesn't read, and returns how many it wrote.
  - `ExcelWorkbook` passes the options, and the table's first column to its header record.
- **Decisions made while building:**
  - **What is hidden, as Excel reads it** (a design change, from three more rounds of COM checks; see "Checked on 2026-11-04"):
    - Excel decodes a value's references, then reads it strictly. A boolean is true only as `1` or `true`. A number has no white space and no `+`, and an empty one is 0.
    - A value Excel can't read hides nothing, and isn't an error, since Excel opens the file. A column's width is the exception: without one, or with one that isn't a number, Excel reads it as 0, so the column is hidden. The design had only `width="0"`.
    - A `sheetFormatPr` with `customHeight` and a `defaultRowHeight` of 0 hides the rows a sheet lacks, as `zeroHeight` does. The design had only `zeroHeight`.
    - Heights and widths too small to show a pixel, which Excel hides depending on the screen or the font, are not hidden here (see "Risks").
  - **Files Excel can't open:**
    - A `<col>` without `min` or `max`, with a `min` outside 1 to 16,384, a `max` that isn't an integer, or a `max` before its `min`, throws `CsvFormatException` for the sheet, with the byte. Examples:
      - "Sheet Data can't be read at byte N of xl/worksheets/sheet1.xml: a col has no min." (N is the `<col>` tag's offset, or the wrong value's.)
      - "... the attribute min=\"0\" is not a column's number (1 to 16384)."
      - "... a col's max, 2, is before its min, 3."
    - A `max` past 16,384 is 16,384, as Excel takes it.
    - Other values Excel won't open read as the rest do: an unreadable `zeroHeight` hides nothing, and `<cols/>` hides nothing.
    - `<cols>` after `sheetData` is never reached, as the scanner stops at `</sheetData>`.
  - **Only when asked:** with both options off, `sheetFormatPr`, `cols` and rows' hidden attributes are skipped as before, so nothing new can fail, and the default path has no layout.
  - **A whole sheet read with either option** has a layout, `SheetLayout.Sheet`, so hidden rows and columns take the area's paths.
  - **The map** runs from the area's first column to its last shown one: a sheet whose columns are hidden from `H` maps 7 columns, not 16,384.
  - **The held record is written when the rows begin**, once `cols` is read, rather than at the start of the first fill, so that a table's header leaves out the names of its hidden columns. Its width is then its shown columns'.
  - **A hidden row** is no record once the missing rows before it are written, so record `n` is no longer row `n`, and `LineNumber` is still the row. A row without `r` is judged at its first cell, which gives its number, or at its end when it has no cells. With formulas read as text, it is read for its shared formulas' definitions, as a row above an area is since step 2, and a hidden column's cells go through `SkipCell`, which reads them.
  - **Missing rows in a sheet that hides them** are no records, from the first row on. Rows the file has show unless they are hidden, and an empty one is still a record when empty rows are kept.
- **Tests** (58 new per target: 1,292 on net10.0 and net8.0, 1,264 on net48; all passing):
  - **`HiddenScannerTests`** (11), in every sheet mode:
    - every row form of the COM checks, hidden and shown, against Excel's answer, with either option or none;
    - missing rows around hidden rows, with and without `r`, with and without cells, and in an area;
    - the rows a sheet lacks, under each form that hides them and each that doesn't;
    - every column form, against Excel's answer;
    - `defaultColWidth`'s forms, and Excel's shape for columns hidden from `H`;
    - overlapping, out-of-order, clamped and repeated `<cols>`, and a `<col>` with content;
    - gaps and padding over the shown columns, and in areas;
    - hidden cells left undecoded;
    - a part as Excel writes it with a filter, a collapsed group, a hidden column, columns hidden from `H` and rows hidden below 9;
    - the six `<col>` errors with their bytes, and no error with the option off;
    - shared formulas defined in a hidden row (with and without `r`) and a hidden column;
    - a held record without its hidden columns' names, with rows and without.
  - **`HiddenColumnsTests`:** the bits against a model over 200 seeds of random `<col>` ranges (within a word, across words, at their edges, over the whole sheet), the default, the layout's map for a sheet, an area and no shown column, and 34 numbers as Excel reads them.
  - **`SheetScannerComparisonTests.MatchesTheReferenceWithHiddenCells`:**
    - the generated sheets gain `sheetFormatPr`, `<cols>` and rows' attributes in forms Excel hides and forms it doesn't;
    - they are read with either option or both, with and without a random area, in all 16 modes, with empty rows skipped and kept;
    - the reference model gained the rules, reading numbers with .NET's parsing rather than the scanner's.
    - The other generated sheets are unchanged: the generator draws nothing new for them.
  - **`HiddenReadingTests`** (4), sync and async, through the public API:
    - hidden rows with their lines, and the rows a sheet lacks;
    - hidden columns through dynamic records, a range and a sheet;
    - a table's shown cells through `GetRecords<T>`, `CsvDataReader`, `Query<T>()`, `ParallelEnumerate` and `ExcelReader`;
    - errors naming `Report!E4` for a field 3 or 2 whose column is `E`, in `GetRecords<T>`, `OnRecordError` and `ParallelEnumerate`.
  - `CellReading` takes the two options (`CellReading.Hidden`), and `Sheets.Worksheet` has a form with `sheetFormatPr` and `cols`.
- **Mutation check** on net10.0, 27 bugs planted one at a time, all caught:
  - "a row's `hidden` ignored": 13 failures;
  - "a height of 0 hides without `customHeight`": 2;
  - "booleans read with white space and in capitals": 3;
  - "references not decoded": 4;
  - "a plus sign read": 5;
  - "an empty value not 0": 6;
  - "exponents not read": 8;
  - "a col's width that is no number shows it": 2;
  - "a col without a width shows it": 2;
  - "a later col can't show a column": 4;
  - "a col's middle words not set": 2;
  - "a default width of 0 ignored": 2;
  - "`zeroHeight` ignored": 5;
  - "a custom default height of 0 ignored": 2;
  - "missing rows hidden when only columns are skipped": at first only the comparison test; with an assertion added for it, 2;
  - "a hidden row without `r` read": 3;
  - "a hidden row without cells written": 2;
  - "hidden rows not read for shared formulas": 1;
  - "`cols` not read": 14;
  - "`min` not checked": 1;
  - "`max` not clamped": 2;
  - "the layout keeps hidden columns": 13;
  - "the held record keeps hidden columns' names": 5;
  - "locations ignore the hidden columns": 3;
  - "fields ignore the hidden columns": 16;
  - "rows' hidden state read when not skipping": 9;
  - "no layout for a whole sheet": 14.
- **Core:** no change. The solution builds with 0 warnings. The new tests on the per-row path (a row's other attributes are tested against `skipHiddenRows`, and `EndRow` tests `rowHidden`) are measured with step 2's at step 8; the per-cell path of a whole sheet that skips nothing is unchanged.

**Step 4, merged cells** (built 2026-11-04, committed 2026-11-05):

- **New files:**
  - `Internal/Sheet/MergedCells.cs`, as designed: the read that finds the ranges (`LoadAsync` over the package, `ReadAsync` over a part's scanner), Excel's checks, and the ranges sorted by first row and then column, with their order by last row (`Ends`).
  - `Internal/Sheet/MergedFill.cs`, which the design put in `MergedCells.cs`: the fill state (`MergedFill`), its sets of a bit per field (`FieldBits`), and `MergedCapture`, the `IRecordTarget` that reads a top-left cell into its range's store.
- **Changed:**
  - `ExcelReaderOptions.FillMergedCells`, with no checks.
  - `ExcelWorkbook.OpenReader` finds the ranges first when the option is set, and gives them to the scanner.
  - `SheetLayout.TryGetFields`: the fields a block of columns becomes, which are consecutive since the fields are the shown columns in order; with hidden columns, through a count of the shown columns before each.
  - `SheetScanner`:
    - it takes the ranges, and makes the fill state from the final layout when the rows begin; ranges that fill no field leave none;
    - `ReadsUnwritten` also reads a row above the area, or hidden, when a range that fills fields starts in it;
    - in a cell: a range's top-left cell goes to `CaptureCell`; a field that an open range covers goes through `SkipCell`; any other field writes the fills before it first (`AddFills`);
    - `EndRow` writes the rest of a row's fills;
    - `FlushGaps` writes a missing row that an open range with a value covers as a record of fills (`WriteFilledRow`), and skips the rest of a gap when no open range has a value; `SetRow` keeps track of gaps with fills on even when empty rows are skipped;
    - a new mode, `Tail`, when the rows end or a row below the area comes, writes the rows after the last one read that ranges fill, up to the area's last row.
- **Decisions made while building:**
  - **As strict as Excel** (a design change, from 28 hand-written files opened in Excel; see "Checked on 2026-11-04"):
    - A `ref` Excel can't open throws `CsvFormatException` at the `mergeCell`'s byte: "Sheet Data can't be read at byte N of xl/worksheets/sheet1.xml: the attribute ref=\" A1:B2 \" of a mergeCell is not a block of cells, such as A1:B2." Or "... a mergeCell has no ref."
    - White space around a `ref` is refused before it is parsed, since `AreaReference` trims it.
    - One-cell ranges take part in the overlap check, then fill nothing.
  - **Which `mergeCells`:** the first among the root's children, wherever it is; reading stops after it. Excel opens no file with it before `sheetData`, or with two of them.
  - **The overlap message's byte** is the offset of `mergeCells`. A range's own offset is gone after sorting, and keeping it would cost 8 bytes a range.
  - **XML errors:** the first read passes over every tag of the sheet. So with the option on, XML that can't be read anywhere in the sheet throws from `OpenReader`, as the sheet's `CsvFormatException` without a cell, rather than from the read that reaches it, with its cell. The pass doesn't read attributes, so an attribute that can't be read is still reported by the reader, with its cell.
  - **20 bytes a range** toward `MaxPartSize` (the design said 16): its bounds, an `ExcelRange`, and its place in `Ends`.
  - **Which ranges fill:** those of more than one cell whose rows meet the area's and that have a column that is a field. A range's fields are one run, kept at the run's first field; ranges open at once don't overlap, so neither do their runs.
  - **A row costs its cells and fills, not its open ranges:**
    - Ranges open and close by walking the two orders.
    - Each set of a bit per field (16,384 bits in words of 64, with a summary bit per word) finds the run a field is in (the last run start at or before it) and the next run with a value, in a few word tests.
    - Ranges whose top-left cell has no value aren't in the set the fills are found in.
  - **Counting:** each fill counts its bytes toward `MaxUncompressedSize` before it is written, at least 1 as an empty field does. The top-left cell's own field, in its first row, counts as its cell does: its XML, and a shared string's bytes beyond it. A filled missing row counts 1 more, as a missing row does.
  - **The top-left cell is read wherever it is** (`CaptureCell`): outside the area's columns, in a hidden column, or in a row above the area or hidden, which is then read for it. `CaptureCell` is a copy of `ReadCell`'s value loop, as `SkipCell` is, so that `ReadCell`'s path gains no test; it writes through `MergedCapture`, with the row's field count kept as it was. When the cell's own column is a field, its field is written from the store with the rest of the run.
  - **Cells a range covers** go through `SkipCell`: with formulas read as text, they still define their shared formulas, and their order is still checked.
  - **Rows after the file's last row** in the area are records while a range with a value covers them. With empty rows kept, the rows between those are empty records, up to the last filled row, and none after it. A row below the area ends the reading once the filled rows before it are written.
  - **A sheet that hides the rows it lacks** has no filled missing rows when hidden rows are skipped: they are hidden.
- **Tests** (66 new per target: 1,358 on net10.0 and net8.0, 1,330 on net48; all passing):
  - **`MergedCellsTests`** (35), in every scan mode:
    - Excel's shape, and the order of the ranges' ends;
    - the 10 references Excel opens and the 9 it can't, from the COM checks, and a `mergeCell` without `ref`;
    - what Excel ignores, prefixes and the strict namespace, and sheets without ranges;
    - sorting; 7 overlaps among other ranges (the same range twice, one-cell ranges inside, whole columns and rows); ranges that touch;
    - stopping after `</mergeCells>`: a one-byte stream delivers exactly up to its end, with broken XML after it;
    - the limit, at and one past; and XML errors for the sheet.
  - **`MergedScannerTests`** (18), in every sheet mode:
    - fills across, down and both, other cells' values dropped, and the same sheet without the option; fills in column order among a row's cells;
    - top-left cells left of and above the area, and a range past its end, with formulas as text too;
    - top-left cells in hidden rows and columns, with each option;
    - top-left cells valueless, absent, or an empty shared string;
    - missing rows filled before and after the file's last row, with empty rows kept, and in areas; a sheet that hides the rows it lacks; the area's end after filled rows, one record per fill;
    - ranges that fill nothing: one cell, outside the area, or only in hidden columns;
    - kinds and formulas, with a shared formula defined in a cell a range covers; whole columns and rows; fields past 64 and at the last two columns; a held record;
    - errors' cells after fills;
    - fills counted to the byte (text, empty and shared string top-left cells, filled missing rows), and the limit at and one past.
  - **`SheetScannerComparisonTests.MatchesTheReferenceWithMergedCells`:** the 100 generated sheets with merged ranges (some of one cell, in lowercase, with `$`, or with their corners reversed), with and without hidden cells and areas, in all 16 modes, with empty rows skipped and kept, against the reference model. The model now reads the sheet as a grid in which each cell of a range has its top-left cell's value; the other tests' generated sheets are unchanged.
  - **`MergedReadingTests`** (6), sync and async, through the public API:
    - a report's regions through `GetRecords<T>`, dynamic records, `CsvDataReader`, `Query<T>()`, `ParallelEnumerate`, a whole sheet and `ExcelReader`, and the same without the option;
    - rows the file lacks, with their lines;
    - overlapping ranges refused when the reader opens, and read without the option;
    - errors naming `Report!D7` after a fill, in `GetRecords<T>`, `OnRecordError` and `ParallelEnumerate`;
    - the second read of the sheet counted: a limit the sheet passes once fails with fills;
    - a sheet without ranges, read as without the option.
  - `CellReading` gained `FillMergedCells` and `Merged`; `Sheets` gained `FindMergedCells`, and `ReadPackageAsync` fills and areas.
- **Mutation check** on net10.0: 32 bugs planned; Claude Code stopped the run after 28 because the machine ran low on memory. All 28 were caught:
  - "overlaps not checked": 9 failures;
  - "closed ranges' columns kept by the check": 16;
  - "white space around a ref taken": 2;
  - "ranges not sorted": 10;
  - "reading goes on after the ranges": 1;
  - "the ranges' limit not counted": 1;
  - "XML errors not the sheet's": 1;
  - "a mergeCell without ref skipped": 1;
  - "covered cells read as themselves": 15;
  - "ranges never close": 9;
  - "top-left cells not found": 21;
  - "rows with top-left cells not read when otherwise skipped": 4;
  - "no rows filled after the file's last": 11;
  - "earlier words not searched for a run's start": 1;
  - "later words not searched for a fill": 1;
  - "fills written only at the row's end": 15;
  - "covered cells written": 15;
  - "missing rows not filled": 13;
  - "hidden missing rows filled": 2;
  - "no rows filled after the sheet's rows end": 9;
  - "no rows filled before a row below the area": 3;
  - "filled rows past the area": 5;
  - "rows above the area and hidden ones not read for top-left cells": 4;
  - "the top-left cell's own fill counted": 4;
  - "empty fills counted as nothing": 2;
  - "filled missing rows not counted": 4;
  - "a captured cell changes the row's fields": 4;
  - "missing rows not tracked when empty rows are skipped": 6.
  - Not run yet: "fills from the first field each time" (stopped while it ran; the file was restored and the tests pass), "runs ignore hidden columns", "runs end at the column, not the shown field", and "the option ignored".
- **Core:** no change. The solution builds with 0 warnings on every target. With the option on, a whole sheet that has ranges is read through `SheetLayout.Sheet`; one without any is read as with the option off, after the first read through it. Step 8 measures both.

**Step 5, header row** (built and committed 2026-11-05):

- **New files:**
  - `ExcelHeaderRow.cs`, as designed: `At`, `Find<T>`, `Find(names)` and `ToString`, with the test each search makes held as a delegate.
  - `Internal/Sheet/HeaderSearch.cs`: `SearchArea` (the rows a search reads), `RunAsync` (the search), and in it the one-record capture (`CapturedRow`, `RowCapture`) and the probe readers' source (`HeaderProbe`).
- **Changed:**
  - `ExcelReaderOptions.HeaderRow`, and its two checks in `Validate`. `Range`'s docs say that `HeaderRow` can move its header.
  - `ExcelWorkbook`:
    - `OpenReader` checks a row given against the area before it reads anything, finds the header (`FindHeaderAsync`) once the styles, shared strings and merged ranges are loaded, then opens the reader's scanner over the area the search gives, holding the header;
    - `OpenScannerAsync` opens a scanner of the part, for both;
    - the docs of the eight `OpenReader` overloads list `KeyNotFoundException`, `InvalidOperationException`, the header row's `ArgumentOutOfRangeException`, and `CsvFormatException`, which step 4's merged cells already threw from them undocumented.
  - `HeldRecord.Row`: a record of a row's cells, one per column from a first one.
- **Decisions made while building:**
  - **A scanner of its own** (a design change):
    - The design paused the reader's scanner after the header row, narrowed its layout to the header's columns, and held the row for its next fill. The scanner would then have changed its layout, its width and its merged-cell state after the rows had begun, and `MergedFill` keeps its open ranges by field, so narrowing would have re-keyed them mid-sheet.
    - Instead, the search opens a second stream of the sheet's part, with a scanner whose area is the rows it tries (`SearchArea`: the row given, or the area's first `maxRows` rows), in the area's columns, with the reader's options. It skips rows without a value, and writes one record per fill.
    - Once it has the header, the reader's scanner is created over the area's rows after it, from its first column with a value to its last, holding it as a table's names are held. Rows above that area are skipped, or read for shared formulas' definitions and merged ranges' top-left cells, as since steps 2 to 4, so the scanner itself is unchanged.
    - The cost: the part is inflated twice up to the header row, and to the end of the 64 KB window that holds it. That counts toward `MaxUncompressedSize`, and a search that reads every row reads the sheet twice. Step 8 measures it.
  - **The search reads no row after those it tries:** its area ends at the last of them, so the next row ends its read. A row whose XML can't be read just after them doesn't fail it, and a million rows the file lacks cost nothing.
  - **A row given is the header even when hidden** and hidden rows are skipped, since it is named: its search reads hidden rows. A row given outside the area throws `ArgumentOutOfRangeException` before anything is read.
  - **A value** is a field whose kind isn't empty. The clip takes the header's first and last such fields, so it also drops the padding the search's scanner adds (a title merged across three columns pads a two-field header row to three). A hidden column between the header's fields gets an empty placeholder, which the reader's layout leaves out.
  - **Probes:**
    - Each is a `CsvReader` over a one-record source, with the reader's options and cell options, so a date names its column in the workbook's date system.
    - Each is disposed after its test, which returns its buffer to the pool.
    - `Find<dynamic>` needs no case of its own: core's dynamic mapper requires no columns, so the first row with a value passes.
    - When no row tried has a value, a probe of an empty header tells whether any column is wanted, and throws `InvalidOperationException` for a type without a mapper, as a row's probe would have.
  - **Messages** as designed, with these forms:
    - The rows tried are named as a block in the area's columns: `Messy!1:20` for a sheet, `Data!B2:F21` for a range.
    - Names are quoted as core quotes header names: "No row of Data!1:20 has the columns 'Name', 'Total'. The closest, row 4, lacks 'Total'."
    - A type's columns are named as core names them: "... has the columns that AreaOrderWithTotal requires. The closest, row 4, lacks AreaOrderWithTotal.Total ('Total')."
    - Without values: "No row of Messy!B1:E3 has the columns that AreaOrder requires: none of them has a value.", or "No row of Messy!F1:F9 has a value." when no column is wanted.
    - A row given without a value: "The header row, Messy!3:3, has no value.", a `CsvFormatException` whose `Location` is the row.
  - **`ExcelHeaderRow`:**
    - `ToString` gives the code that makes it: `ExcelHeaderRow.At(4)`, `ExcelHeaderRow.Find<AreaOrder>(20)`, `ExcelHeaderRow.Find(["Name", "Unit Price"], 20)`.
    - `Find(names)` copies the names; a null name throws `ArgumentException`.
    - It has no value equality, so `ExcelReaderOptions` compares it by reference.
- **Tests** (29 new per target: 1,387 on net10.0 and net8.0, 1,359 on net48; all passing):
  - **`HeaderSearchTests`** (11), in every sheet mode, through `Sheets.ReadAfterHeaderAsync`, which runs the search and the read after it as `OpenReader` does:
    - names found below a title, ignoring case and white space; the same row given; no names; ranges;
    - the clip, with a gap in the header, notes either side and empty rows kept;
    - rows tried by number, the closest row, and the rows named in ranges;
    - sheets without values, a row given without one, on a sheet whose name is quoted, and a row whose one cell is a space by reference (a record of an empty field, without a value);
    - a row given outside the area, and the rows each search reads;
    - hidden rows and columns, a hidden row given, and a hidden first column;
    - merged cells filled into the header row, and not filled;
    - typed header cells (numbers, booleans, errors in each policy);
    - a shared formula defined above the header;
    - the reader's `NormalizeHeaderName` in the probes;
    - no row read after those tried, and a million rows the file lacks.
  - **`SheetScannerComparisonTests.MatchesTheReferenceWithAHeaderRow`:** the 100 generated sheets, with and without hidden cells, merged ranges and areas, given a row (mostly one with a value) or a search for a row's texts (sometimes cased otherwise, or with a name no row has), in all 16 modes, with empty rows skipped and kept, against the reference model. The model gained the search: it tries the rows its own rules read and matches names as `TryGetFieldIndex` does. About half the reads find a header; the rest compare their exceptions.
  - **`HeaderReadingTests`** (17), sync and async, through the public API:
    - `Find<T>` in a range, `Find(names)` on a whole sheet, `At`, a defined name and `Find<dynamic>`, through `GetRecords<T>`, dynamic records, `CsvDataReader`, `Query<T>()`, `ParallelEnumerate` and `ExcelReader`;
    - a date in the header naming its column, in both date systems;
    - a hidden row of other names above the header, skipped or not, and given;
    - errors naming `Messy!C6` after the clip, in `GetRecords<T>`, `OnRecordError` and `ParallelEnumerate`;
    - every message when no row will do, a type without a mapper (with and without values), a row given without a value or outside the range, and the workbook read on after them;
    - the options' checks at `Open` and at `OpenReader`;
    - `ExcelHeaderRow`'s checks, texts and copy of the names;
    - the search's read counted: under a limit the sheet passes once, a search that reads every row to find its header fails, and one that finds it in the first row doesn't.
  - `CellReading.With` gives a reading of another area, held record and hidden rows' choice; `Sheets.ReadAfterHeaderAsync` runs the search and the read after it.
- **Mutation check** on net10.0, 20 bugs planted one at a time. 17 were caught by the first run; the other 3 were caught once tests were added for them (marked "after"):
  - "rows tried not bounded by `maxRows`": 7 failures;
  - "a row given outside the area taken": 3;
  - "rows without a value tried": 1 after a row whose one cell is a space by reference (none at first);
  - "the closest is the last of those that lack fewest": 4;
  - "without values, the columns wanted not asked": 3;
  - "empty fields before the header's first value kept": 7;
  - "empty fields after the header's last value kept": 2;
  - "the header's first column from its field, hidden columns ignored": 1;
  - "the reader reads the header row again": 22;
  - "the header's kinds lost": 7;
  - "probes convert typed cells with the default options": 2, the 1904 date system's;
  - "a search's fill takes every record": 22;
  - "a hidden row given skipped": 2 after the public API's hidden-row test (none at first: the scanner tests choose the search's hidden rows in their own helper);
  - "the search reads hidden rows": 2 after the same test;
  - "the header found not held": 24;
  - "`HeaderRow` allowed with `Table`": 2;
  - "`HeaderRow` allowed without `HasHeader`": 2;
  - "`Find<T>` asks for no columns": 6;
  - "`maxRows` past the sheet's rows taken": 1;
  - "a null name taken": 1.
- **Core:** no change. The solution builds with 0 warnings on every target. The reader's scanner is unchanged, so the default path is too; step 8 measures the search's second read.

**Step 6, corpus and oracles** (built and committed 2026-11-05):

- **The `messy` content** (`tools/CsvHelper.Excel.Corpus/Messy.cs`), defined once, as designed, with two additions; the corpus `README.md` describes it:
  - "Report", the chartsheet "Chart" before "Tables", "Tables" and "Filtered", with the names the design lists. The names add an `OFFSET` range (`Dynamic`), which Excel's `RefersToRange` evaluates.
  - (A design change:) "Filtered" has an empty row 6 inside its data. Excel writes it as an empty row, which it shows; XlsxWriter, openpyxl and POI leave it out, and `zeroHeight` then hides it. Without it no corpus file lacked a row under `zeroHeight`, so nothing checked that rule against Excel.
  - The spec's sheets gained merged ranges, hidden rows and columns, collapsed groups, a filter, rows and columns hidden to the grid's end and a chartsheet's data; its tables gained a hidden header row (with the names it then has in no cell) and a totals row's label and sum; a `NameSpec` lists the names. They're nullable, so the basic and typed workbooks' JSON doesn't change.
- **Its producers,** six files of 7 to 19 KB, written by the `excel`, `libreoffice`, `python`, `poi` and `dotnet` commands. Those commands write the basic and typed files again too; those were put back from git, so their committed bytes stand.
  - Excel (`excel/messy.xlsx`) builds it as a user would. Turning on a table's totals row inserts cells in the table's columns, which moved Away's column C down a row, so the producer writes each table's cells and makes the table before writing the rest; ClosedXML does the same, and its producer does too.
  - LibreOffice converts Excel's file. openpyxl, XlsxWriter, POI's XSSF and ClosedXML each write what they can: POI has no filter criteria in its schemas and no way to hide a name, XlsxWriter none to hide a name, ClosedXML none to write `zeroHeight`, and neither POI nor ClosedXML writes a chartsheet. The README's "What the producers write" lists what each wrote.
  - **openpyxl's file is one Excel can't open,** even by repairing it. openpyxl numbers a sheet's own names by worksheet, without the chartsheet, so Tables' `Local` is written with the chartsheet's index. COM checks of variants of the file showed that Excel opens no workbook with a name of a chartsheet, whatever it refers to (see "Checked on 2026-11-04"). ClosedXML throws on it too. CsvHelper.Excel reads it, with that name as the chartsheet's, as step 1 decided, and as the review kept it (below).
- **The `oracle` command** (`ExcelOracle.cs`) also answers, for every workbook, as designed, with these changes:
  - Hidden rows and columns cover the whole grid, not the used range and a margin: `SpecialCells(xlCellTypeVisible)` over every cell gives the shown rows and columns in one call per sheet, whatever hides them. They and the merged ranges are read before the columns are widened, which would show hidden ones.
  - Names also give the sheet of `RefersToRange`, with A1 of the first visible worksheet active, since a relative name is relative to the active cell.
  - A workbook Excel can't open even by repairing it is answered as `refused`, and the command goes on.
  - Just after opening `google/64450.xlsx`, Excel answers "the application is busy" (`RPC_E_SERVERCALL_RETRYLATER`) while it builds the pivot table's cells. A first version took any COM failure of `SpecialCells` for "no visible cell", and once answered that the sheet hid everything. Now busy calls are made again (every 100 ms, up to a minute), only Excel's own failure (`0x800A03EC`) means "no visible cell" or "no range", and a sheet without a visible cell must hide its first row or column, or the command fails. Two runs after the fix gave identical files.
  - The 39 answer files are 2.2 MB. Every cell's answer in the 33 files of 2026-11-03 is unchanged; the command only added the new answers.
- **Excel's answers found** (recorded under "Checked on 2026-11-04"): the chartsheet names; `#REF!` shown as `#REF!#REF!`; `RefersToRange` for unions, `OFFSET` and table references; the hidden `_xlfn.X` names COM adds; a totals row's inserted cells; and a structured reference to a cell's own table shown without the table's name.
- **`StructureAnswersTests`** (new), over the 38 answered workbooks (the benchmarks' two and openpyxl's refused one have no answers):
  - **Tables:** `Tables` against `ListObjects` (sheet, `DisplayName`, `Range`, `ShowHeaders`, `ShowTotals`, columns): they agree in every workbook, and every table's `Name` and `DisplayName` are alike.
  - **Names:** `DefinedNames` against `Names` (the name as `ToString` writes it, hidden, `RefersTo`, the range's sheet and address). Excel's added `_xlfn.` names are left out, each checked to be hidden and `#NAME?`. 20 names differ, each listed with one of five reasons: `Union`, `Dynamic` and `TableRef` have a range to Excel and none to CsvHelper.Excel (decision 5), and `Deleted` is `#REF!` (or LibreOffice's `#ref!`) shown as `#REF!#REF!`, in each of the five messy files Excel opens.
  - **Merged ranges:** `MergedCells` against each cell's `MergeArea`: they agree in every sheet.
  - **Hidden rows and columns:** the rows a read that keeps empty rows gives, less those a read that skips hidden rows gives, of 1 to the file's last row, and the columns a read's layout leaves out, over the whole grid, against Excel's: they agree in every sheet. Rows past the file's last row aren't compared: no read can say whether they're hidden.
  - **Reads with the switches,** sync and async, through the public API, with empty rows kept: hidden rows and columns skipped, merged cells filled, and all three. Field `i` must be the `i`-th column Excel shows and each line a row Excel shows, in order; each field must be the cell Excel shows (each cell of a merged range as its top-left cell, or nothing when that has none), and every such cell must be read. They agree in every sheet of every workbook.
- **`OracleTests`** gained the structures of ClosedXML (merged ranges, hidden rows and columns, tables, defined names) and ExcelDataReader (merged ranges, and hidden rows as a `RowHeight` of 0), against CsvHelper.Excel's, each difference listed:
  - ClosedXML, 27 in the five messy files it opens: it shows the columns of a `<col>` that runs to XFD (Filtered's D:XFD); it keeps `_xlnm.` on `_FilterDatabase`; it reads the print area and titles into its page setup, not its names; and it shows the row 6 that XlsxWriter's and POI's files lack under `zeroHeight`.
  - ExcelDataReader, 6 in four files: it reads a row as hidden only from `hidden="1"`, not the `hidden="true"` that POI and LibreOffice write, and shows the rows a `zeroHeight` sheet lacks.
  - Merged ranges agree with both everywhere, and tables and names with ClosedXML outside the messy files.
- **Changed tests:**
  - `ExcelAnswers` reads the new answers (`ExcelSheetStructure`, `ExcelNameAnswer`, `ExcelTableAnswer`) and `refused`. `ExcelAnswersTests` checks that only `openpyxl/messy.xlsx` is refused, and lists `Tables!B5` of three messy files: Excel shows `SUBTOTAL(109,Sales[Qty])` as `SUBTOTAL(109,[Qty])`, as XlsxWriter and ClosedXML store it. ClosedXML's `FormulaA1` gives the same, listed in `OracleTests`, and ClosedXML's rejection of openpyxl's messy file joins its list.
  - `CorpusFiles` gained `Structures` (CsvHelper.Excel's hidden rows and columns and merged ranges, through its own layers), `Tables`, `TableText` and `NameText`; `Oracles` gained `ClosedXmlStructure`, `ExcelDataReaderStructure` and `OracleStructure`.
  - `CorpusTests` lists the six files' sheets, and `MetadataTests` leaves the messy files to these tests.
- **Tests:** 351 new per target: 1,738 on net10.0 and net8.0, 1,710 on net48; all passing. The LibreOffice tests ran here, the messy files among them.
- **Mutation check** on net10.0: the step adds no library code, so 17 bugs were planted one at a time in steps 1 to 5's, to see whether the corpus and its oracles catch them. Each file was restored and checked against its SHA-256. Failures, with the corpus tests' (`Corpus.*`) among them:
  - "booleans read only as 1, not true": 17 failures, 10 of the corpus (POI's and LibreOffice's `hidden="true"`);
  - "a row's hidden ignored": 46, 26 of the corpus;
  - "no row hidden": 46, 26;
  - "`zeroHeight` ignored": 18, 10, all from Filtered's row 6, which the content gained for it;
  - "a col's hidden ignored": 20, 14;
  - "a col's middle words not hidden": 7, 5 (D:XFD spans 256 words);
  - "a table's totals row count ignored": 23, 10;
  - "a table's header row count ignored": 20, 10;
  - "column names' escapes not decoded": 8, 4 (`First_x000a_line`);
  - "a table's name over its display name": 2, none of the corpus: Excel and every producer write the two alike, so only `TablePartTests` tells them apart;
  - "a name's hidden ignored": 15, 8;
  - "built-in names keep `_xlnm.`": 16, 10;
  - "every sheet's name the first sheet's": 18, 10;
  - "merged ranges not kept": 75, 26;
  - "ranges close before their last row": 32, 10;
  - "a range's run ends at its first field": 29, 10;
  - "a top-left cell's value never marked as one": 33, 10.
- **Decided at review** (2026-11-05, the recommendation):
  - **A name of a chartsheet,** which Excel refuses and openpyxl writes whenever a sheet with its own names comes after a chartsheet, is left as it is: the cells read as Excel would read them, and the name's scope is what the file says. The alternative was to throw `InvalidDataException` at `Open`, as for a `localSheetId` that isn't a sheet's index, which would have made such openpyxl files unreadable.
- **Core:** no change. The solution builds with 0 warnings.

**Step 7, security and fuzzing** (built and committed 2026-11-05):

- **Security tests** (`SecurityTests`, each sync and async, through the public API):
  - **A merged range of the whole grid,** `A1:XFD1048576`, around an inline string of a megabyte in `A1`, under a 16 MB limit, with merged cells filled. Each fill counts its bytes before it is written, so the whole sheet stops in its first row, before any of the 16,383 fills it would add, rather than after a record of 16 GB. Read as `Range = "B:B"`, which the top-left cell is left of, it stops within 16 rows: rows the file lacks, filled after the file's last row, or before a row at the grid's end.
  - **A million merged ranges** take 20 MB as the reader holds them, which a 16 MB part limit stops when the reader opens, with the merged cells' message.
  - **A million merged ranges in rows the file lacks,** down column A between its first and last rows, two columns wide or the grid's width: their top-left cells have no value, so they fill nothing. Skipping empty rows, the records are the file's two rows; keeping them, all 1,048,576 rows are records, 1,048,574 of them empty, and a limit with room for 100,000 missing rows past the other bytes read stops the reader between its 100,000th and 300,000th row, so each counts as a missing row does. The sheet's part (32 or 34 MB) is read twice, in 0.7 s (two columns) and 1 to 2 s (the grid's width, whose overlap check marks 256 words a range).
  - **A million `col` elements** (35 or 37 MB) under a 64 KB part limit, with hidden columns skipped: read a tag at a time, so the window never holds them. Each over every column, hiding and showing them in turn, then one hiding `B`, reads `1|3|4`; each of one column, cycling over the grid and hiding the even ones, reads `1|3`. About 0.5 s.
  - **A million defined names** of Data's cells, every other one Data's own, after 100,000 sheets without parts, in a workbook part of 70 MB: a 16 MB part limit refuses the part before inflating it; under 128 MB, `Open` reads them in about a second, and a reader reads one.
  - **A table part of a million columns** (39 MB) over `A1:XFD3`: a 16 MB part limit refuses it before inflating it; under 64 MB, it is read in under a second, and the table's header is its first 16,384 names, its `ref`'s columns.
  - **A header search over a million missing rows:** a sheet whose one row, the grid's last, holds the header after 64 MB of white space (64 KB deflated). `Find(["Name"])` tries rows 1 to 20, stops at that row's tag and throws "No row of Data!1:20 has the columns 'Name': none of them has a value.", having read at most 32 KB of the sheet's compressed data. `Find(["Name"], 1048576)` finds it, trying no missing row.
  - The cases that take linear time (the merged ranges in missing rows, the `col` elements, the names and the table) fail after a minute (`InLinearTimeAsync`), rather than running for hours if one becomes quadratic.
  - `MergedSheet` writes a sheet with its ranges after `sheetData`, `OtherParts` counts the parts a reader reads besides its sheets (shared with the step 8 case of Phase 1 that counted them inline), and `Build` collects what a callback writes.
- **What the tests found** (a library fix, not core):
  - **Each defined name searched every sheet** for its block's sheet, so opening took the names times the sheets: the million names after 100,000 sheets took 5 min 56 s on net10.0 (the async case was stopped after about 2.5 minutes of its own). A workbook part within the default 256 MB part limit could hold 2.5 million of each, and take about six hours.
  - **The fix:** `ExcelWorkbook` makes a dictionary of its worksheets by name, ignoring case, the first of a name in tab order winning, as the search found it, and `ExcelDefinedName` looks its sheet up there. The case takes about a second. No public API changes.
  - Everything else stayed within its limits as designed: fills count before they are written, merged ranges count toward `MaxPartSize` as they are read, `col` elements set words rather than columns, names are deduplicated through a hash set, table names past the `ref` are left out of the header, and a search's area ends at its last row.
- **The fuzzer** (`FuzzTests`):
  - **Settings:** after Phase 2's, which stay as they were for a seed, one seed in three reads as before. The other two pick:
    - an area: every worksheet whole; or a random block of each (`Range` without a sheet: a few cells near the start, whole columns or whole rows); or, after every worksheet whole, one of the workbook's tables, one of its defined names that is a block, or a random block of one of its worksheets named with the sheet (`Range` with its name, quoted as Excel quotes it);
    - with `HasHeader`, a header row: none, `At` a row of the area with a value, or `Find` of such a row's texts within as many rows as reach it and up to two more;
    - each of `SkipHiddenRows`, `SkipHiddenColumns` and `FillMergedCells`, on or off.
  - **Choosing from the workbook:** the table, name and block come from the opened workbook, by a seed of the settings' own, so choosing never fails: a name is one whose text (`ToString`) finds it (no name before it has its name and scope, ignoring case and `_xlnm.`). The header row comes from a read of the area's first 32 rows with a value, without a header and with the seed's switches; so `Find` of a row's texts must find a header, and a `KeyNotFoundException` would fail the run. None did.
  - **Sources:** a seed that reads a table, or a defined name, starts one time in two from a workbook that has one: the typed and messy files with tables, and the six messy files, whose names refer to cells (the typed files' one name is a constant).
  - **Mutations:**
    - the part is a worksheet half the time, and the shared strings, the workbook part and a table part an eighth each (any part the last eighth), where it was sheets and shared strings three times in four;
    - new tokens: `cols` and `col` with hiding and invalid attributes, rows' and `sheetFormatPr`'s hiding attributes, `mergeCells` and `mergeCell` (a block, the grid, one cell, none), `definedNames` and a name, `localSheetId`, `_xlnm.`, and a table's columns, counts, `ref` and name;
    - a new structural mutation writes markup where the schema puts it: `cols` before `sheetData`, `mergeCells` after it (ranges mostly in rows apart, so that they don't overlap), a hiding attribute on a row or on `sheetFormatPr`; `definedNames` after `sheets`, naming the workbook's sheets (the workbook's or a sheet's, some hidden, some `_xlnm.Print_Area`, some a union, `OFFSET` or `#REF!`); and a table's counts, its `ref`, or a column. Blocks are near a sheet's start or at the grid's edges, in the forms Excel takes (lowercase, `$`, reversed corners) and some it doesn't (`A0:B2`, `A1:XFE2`, a list).
  - **Tally:** the run's output also counts the reads of an area or with a header row given, by how they ended.
  - **Runs:** 3,000 inputs by default, in about 3 s on net10.0 and 5 s on net8.0 and net48. 30,000 on net10.0 passed in 17 s. 200,000 on each target passed: 2 min 1 s (net10.0), 4 min 10 s (net8.0) and 5 min 49 s (net48), with the same tallies within a few dozen. Of net10.0's, 37% read to the end, 22% failed at a sheet with `CsvFormatException`, 9% at a sheet with `InvalidDataException`, and 32% as a workbook with `InvalidDataException` (22% in Phase 2: a mutated table part or workbook part fails at `Open`). Its reads of areas: 64,718 ranges read and 13,008 threw; 14,440 ranges with a sheet's name read and 3,875 threw; 7,970 tables read and 2,223 threw; 7,259 defined names read and 1,901 threw. Header rows: 59,479 given read and 102 threw; 60,600 searched for read and 130 threw, none for want of the row.
- **Tests:** 24 new per target (12 security cases, sync and async): 1,762 on net10.0 and net8.0, 1,734 on net48; all passing. `MetadataTests.FindsTheSheetOfABlockAsExcelDoes` gained two names, for the mutation check (below).
- **Mutation check** on net10.0, 14 bugs planted one at a time, the suite run for each, and each file restored and checked against its SHA-256. 12 were caught by the first run, one once a test was added for it (marked "after"), and one changes nothing a reader sees:
  - "worksheets matched with case": 2 failures;
  - "the last worksheet of a name wins": 2 after `FindsTheSheetOfABlockAsExcelDoes` gained worksheets `Twin` and `TWIN`, whose names differ only in case, and a chartsheet `Plot` before a worksheet `PLOT` (none at first: no workbook had two sheets of a name);
  - "names find chartsheets": 2;
  - "the quadratic search back" (each name searching the sheets in tab order, as before the fix): 2, the million names', each at the one-minute bound;
  - "missing rows not counted": 8, the million merged ranges' among them;
  - "missing rows written when empty rows are skipped, with merged cells": 8;
  - "merged ranges counted at 16 bytes": 2, the million ranges' alone, since `MergedCellsTests` computes its limit from the same constant;
  - "the merged ranges' part limit not checked": 3;
  - "fills counted once per run, not per field": 6, the grid's merged range among them;
  - "the header search reads every row of the area": 9, the search over a million missing rows among them;
  - "rows below the area skipped, not the end": 8, the same among them;
  - "two ranges can't start in one row" (an index out of range): 16, the fuzzer among them, which alone caught it in 3 s;
  - "the header's last field placed a column late" (an index out of range): 28; the fuzzer alone caught it too;
  - "a table's names not fitted to its columns": none. It is equivalent: the held record's `WriteTo` leaves out the fields past the layout's columns, so the header is the same, and only the million names' bytes are held until the reader opens.
- **Core:** no change. The solution builds with 0 warnings.

**Step 8, benchmarks** (built 2026-11-05, the review's fix on 2026-11-06, committed 2026-11-06):

- **New files:**
  - `bench/CsvHelper.Benchmarks/ExcelReport.cs`: the report's layout and content. The test project and the corpus tool link it, as they link `ExcelOrders.cs`, so it has its own `using`s (the tool has no implicit ones).
  - `bench/CsvHelper.Benchmarks/ExcelReportWorkbook.cs`: the writer, built on `ExcelTextWorkbook`'s helpers. `ExcelTextWorkbook.cs` is unchanged.
  - `bench/CsvHelper.Benchmarks/ExcelReportBenchmarks.cs`: `ExcelReportRead`.
  - `tests/CsvHelper.Excel.Tests/Bench/ExcelReportWorkbookTests.cs`: the cross-check.
  - `tests/CsvHelper.Excel.Tests/Files/excel/packageassets-report-250.xlsx`, by the corpus tool's new `report` command.
- **Changed:** `ExcelDataGenerator.PackageAssetsReport`, cached as `packageassets-report-<rows>-<hash>.xlsx`; the bench project's embedded generator sources; the test project's and the tool's links; `CorpusTests`' sheets; `MetadataTests`, for the report's table; the workbooks without Excel's answers (`ExcelAnswersTests.AnsweredWorkbooks`, `ExcelOracle.LeftOut`); the corpus README and the benchmark data's.
- **The report** (`ExcelReport`), with the layout designed:
  - row 1, the title `NuGet package assets` in `B1`, merged across `B1:AB1`; row 2, `From NCsvPerf's PackageAssets.csv`; row 3 empty;
  - row 4, the header from `B4`: `Region`, `Key`, then the 25 names;
  - from row 5, a row per data row: in column `B` the region, in the first of each 100 rows (North, East, South and West in turn), merged down them; in column `C`, the hidden one, the key, the row's number from 1 as a number in General; then the fields, as text;
  - every 50th data row hidden (rows 54, 104, and so on);
  - the table `Assets` over `C4` to the last row.
  - **The regions are outside the table** (a design change, checked through COM): Excel merges no cells in a table, and a table made over merged cells unmerges them (see "Checked on 2026-11-04"). So `Table` reads the key and the fields, 26 columns, where `Range` reads 27.
  - The header row, the regions and the fields have the text format, as every cell of the plain workbook has; the key and the titles are in General.
  - A million rows make 10,000 regions and 20,000 hidden rows: 90.5 MB, with 965 MB of sheet XML (the plain workbook's is 714 MB) and 152 KB of shared strings.
- **Excel's shape,** from the file the `report` command has Excel save, which the writer copies (also under "Checked on 2026-11-04"):
  - Every cell of the formatted block is in the file, an empty field as `<c r="K5" s="1"/>`. The plain workbook, which Excel made from the CSV, has no cell for one.
  - Merging gave the title's cells a new cell format, 2 (a copy of General), and the regions' 3 (a copy of the text format), and wrote each cell a range covers as an empty cell in it.
  - Making the table widened the 22 columns whose header doesn't fit beside the filter button (`ScanTimestamp` to 16.36328125, `RoundTripTargetFrameworkMoniker` to 32.54296875). It added a differential format of the text format for the header row and for each of the 25 text columns, numbered from the last column; the key's column, in General, has none.
  - The hidden column is `<col min="3" max="3" width="0" hidden="1" customWidth="1"/>`, first of the `cols`. A hidden row has `hidden="1"` after its `spans`, which are `2:28` on every row.
  - The page margins are a new workbook's (0.7, 0.75, 0.3), not those of the CSV-opened file. The merged ranges are in the order they were made, and `tableParts` comes last.
  - The table part's revision ids (`xr:uid`, `xr3:uid`) are random, so the writer writes none.
- **The writer refuses** (`NotSupportedException`) what Excel's shape isn't known for: another header (its widths are Excel's for these names), no data row (a table needs one), a row of another width, and a last region of one row (Excel doesn't merge one cell).
- **The corpus file,** `excel/packageassets-report-250.xlsx` (43 KB): NCsvPerf's first 250 lines, which give three regions (the last of 50 rows) and 5 hidden rows, rather than all 1,695 lines. The corpus tool's privacy check passes; its creator, last author and company are empty, and it records no path. It joins `CorpusTests`' list, and, like the other two cross-checks, has no answers from Excel (6,800 cells). `MetadataTests` checks its table.
- **Cross-check** (`ExcelReportWorkbookTests`, 21 tests per target):
  - Against Excel's file, byte for byte: the entries in Excel's order and the content types, without the theme and document properties; the sheet from its `dimension` on; `sharedStrings.xml`; the sheet's relationships; the `cellXfs`, `dxfs` and `tableStyles`; and the table part without Excel's revision ids.
  - Each of the benchmark's reads, sync and async, of both files, gives the records and the header expected: `Range`, its first 100 rows, `Table`, a header search for the 25 names, and the three switches.
  - ExcelDataReader reads the two files alike. The Open XML SDK finds no error in 250 rows or in 1,000.
  - At 1,000 rows, the layout is the benchmark's: 10 regions, 20 hidden rows, 980 records with them skipped, and each region's fill. The four refusals.
- **The existing variants' bytes:** the generator's sources changed, so its hash did, and the cached workbooks were written again. Every entry of the three, by SHA-256, is as before.
- **`ExcelReportRead`** (`ExcelReportBenchmarks.cs`), as designed, in two categories. The method names say the read, since all are CsvHelper.Excel's:
  - Report: `Range` (`B4:AB1000004`, the baseline), `RangeFirstRows` (`B4:AB1004`), `Table`, `HeaderSearch` (`Find<PackageAsset>()` over the whole sheet), `SkipHiddenRows`, `SkipHiddenColumns` and `FillMergedCells`, each over the `Range`.
  - Plain: `Plain` (the plain shared-strings workbook, as `ExcelPackageAssetsRead.CsvHelper_Records` reads it, the baseline) and `PlainFillMergedCells`.
- **Equal work:** in `verify`, every read of the million rows gives the plain workbook's records (hash `A46BD7E4C52574F1`), `RangeFirstRows` 1,000 records and `SkipHiddenRows` 980,000. So QuickRunner marks the Report category's results as differing, as it must. The other two classes agree as at Phase 2's step 9, on the new build.
- **The default path, base against new** (2026-11-05, 20:40–21:22): QuickRunner, CsvHelper.Excel's methods in `ExcelOrdersRead` and in `ExcelPackageAssetsRead` (both variants), base the `1cd1a0af` worktree at `Csv\_bench\base`, new the working tree as of `073e0b9a`. Both builds' bench code and core are the same. Their bytes weren't: the worktree's checkout has CRLF line ends (`core.autocrlf`), so base's embedded generator sources hashed otherwise, and each side wrote its own cached workbooks with the same generator, in setups that aren't timed. Three interleaved pairs (base, new; new, base; base, new), each process pinned to CCD0 and QuickRunner pinning core 4. The machine: paged pool 2.45 GB and 43 GB free throughout. In `verify`, new's reads agree with every library's.
  - Something used about one core from 21:20:31 to the end of the last run, over its inline Strings and Records (CPU 7% against 4–5%), so those two are left out. Its other methods ran before.
  - The best median of each side's three runs, in ms, and new/base per pair:

| Method | Pair 1 | Pair 2 | Pair 3 | Base | New | New/base |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Orders Records | 0.995 | 1.019 | 1.022 | 2,304.9 | 2,353.1 | 1.021 |
| Orders Values | 1.001 | 1.004 | 1.029 | 2,225.6 | 2,248.3 | 1.010 |
| Shared Rows | 1.010 | 1.010 | 1.023 | 2,980.1 | 3,017.0 | 1.012 |
| Shared Strings | 1.003 | 0.992 | 1.020 | 3,071.5 | 3,078.6 | 1.002 |
| Shared Records | 1.009 | 1.002 | 1.018 | 3,733.7 | 3,761.8 | 1.008 |
| Inline Rows | 1.016 | 0.991 | 1.009 | 3,449.8 | 3,419.3 | 0.991 |
| Inline Strings | 1.019 | 0.994 | (disturbed) | 3,496.2 | 3,517.4 | 1.006 |
| Inline Records | 1.009 | 0.982 | (disturbed) | 4,201.4 | 4,185.3 | 0.996 |

  - **Two methods are outside 1%:** `ExcelOrdersRead.CsvHelper_Records`, +2.1%, slower in all three pairs bar the first, and shared Rows, +1.2%, slower in every pair. Orders Values is at the edge (+1.0%). The inline variant and the other shared methods are within 1%.
- **Why** (2026-11-05, 21:25–23:20): the JIT check of Phase 2, then a bisect of scratch copies, each measured against base and new in rotated QuickRunner rounds of `ExcelOrdersRead.CsvHelper_Records` (the largest effect, about 47 s a process), each process pinned as above. The scratch copies are `git archive` exports in the session's scratchpad; nothing in the repository changed.
  - **The JIT check** (`DOTNET_JitDisasmSummary`, then `DOTNET_JitDisasm` of every `SheetScanner` method, in QuickRunner's process). The scanner's Tier-1 code grew from 20,819 to 22,519 bytes over its hot methods: `TryScan` 1,590 to 1,959 (IL 226 to 239), `StartTag` 721 to 932, `EndTag` 992 to 1,193, `StartRow` 799 to 1,015, `EndRow` 953 to 1,147, `ReadCell` 3,542 to 3,847 (one inlinee with PGO data and three single-block inlinees fewer). Two findings in it:
    - `FlushGaps`, which `ReadCell` calls for every cell and `StartRow` for every row, was 98 bytes of IL in base, inlined into both. Step 4's fills made it 183, past the JIT's 100, so new calls it, almost always to find no missing row.
    - Methods whose IL didn't change grew too (`AddSegment` 112 to 124 bytes), because their fields moved. The runtime puts reference fields first, and Phase 3's six (`layout`, `held`, `unwrittenRoom`, `hiddenColumns`, `mergedCells`, `merged`) moved every value field 0x30 later: `lastRow`, `gapNext`, `gapEnd`, `segmentCount`, `rawLength`, `lastColumn`, `fields`, `cellColumn` and `mode` passed 0x7F, so each access takes a 4-byte displacement where base's took 1 (field offsets read through a `DynamicMethod`). A struct field, holding references or not, goes after the primitive fields (checked the same way).
  - **The bisect,** each variant's median of its rounds' medians over base's in the same rounds:

| Variant | What it is | Rounds | Variant/base | New/base in those rounds |
| --- | --- | ---: | ---: | ---: |
| ctl | base's code, with unused fields that give its fields new's offsets | 6 | 1.001 | 1.028 |
| fix | new's code, with Phase 3's six references in one struct field: base's offsets | 6 | 1.032 | 1.028 |
| split | new, with `FlushGaps` an inlined test of `gapNext < gapEnd` and the rest a method of its own | 8 | 1.015 | 1.020 |
| strip | new, without the per-row and per-cell additions of steps 2 to 5 in `StartRow`, `ReadCell`, `EndRow`, `SetRow` and `FlushGaps` (areas don't work in it) | 8 | 1.022 | 1.021 |
| bscan | new's tree with base's `SheetScanner.cs` (new's constructor parameters, refused when set) | 8 | 1.004 | 1.021 |
| enum | new, with the modes `Cols` and `Tail` after `Done`, so the others have base's values | 6 | 1.014 | 1.010 |
| strip2 | strip, with the tag dispatch too as in base (`TryScan`'s `Tail`, `StartTag`'s `cols` and `sheetFormatPr`, `EndTag`'s `cols`, `End`, `BeginRows`) and the modes as in base | 6 | 1.016 | 1.010 |

  - **What it shows:**
    - The cost is in `SheetScanner.cs`: base's scanner in new's tree (bscan) reads as fast as base.
    - It isn't the field layout (ctl is level with base, fix with new), nor mainly `FlushGaps` (split recovers half a percent at most, within the noise), nor the per-row and per-cell tests that a whole sheet runs (strip), nor the modes' values (enum), nor the dispatch (strip2).
    - strip2's hot methods compile to 20,937 bytes, within 0.6% of base's and with base's IL sizes but for `AddValue`'s (692 against 672: it reads `cellField` where it had a `column` argument). Yet it reads 1.6% slower than base, while new's 8% more code reads 1.0–2.1% slower. So the time doesn't follow the code. What is left looks like the code's placement, which Phase 2's step 9 also met: QuickRunner sessions differed by about 2% for identical code.
    - The noise of these rounds is as large as the effect: 1–2% between sessions for the same build (new/base came out 1.010 to 1.028 in the five sets), and single rounds disturbed by up to 18% (new's in round 4 of the ctl set, with the CPU above the others'; rounds 6 to 8 of the strip set, with the CPU at up to 20% and 6 GB less free memory, which something else took). One base round read about 5% faster than any other.
  - **Decided at review** (2026-11-06, the recommendation): take the `FlushGaps` split, then measure the two methods in BenchmarkDotNet pairs, as Phase 2 re-gated `FormattedDatesRead`, and accept them if they're within the noise. The other options were to accept the cost as measured, or to keep base's scanner for a whole sheet that skips and fills nothing, as the design's "Risks" proposed: bscan shows that this works, but it means two scanners to keep in step, and strip2, about what a path specialized within one scanner would compile to, recovers nothing.
  - **The fix** (a library change, not core): `FlushGaps` is now the test alone, inlined into its callers (`[MethodImpl(MethodImplOptions.AggressiveInlining)]`), and the writing is `WriteGaps`, the old body, called only when a row is missing. A remark says why. In QuickRunner's process, `ReadCell` and `StartRow` no longer call `FlushGaps`, only `WriteGaps` on the path of missing rows, and `ReadCell` has base's 54 inlinees with PGO data again. All tests pass (1,791, 1,791 and 1,763), and the solution builds with 0 warnings.
  - **Its mutation check,** three bugs planted one at a time:
    - "the test never writes missing rows": 282 failures;
    - "the test stops the row when no row is missing": the suite hangs, since a reader then asks for the same cell forever; stopped after 588 s, with 614 failures;
    - "the test calls `WriteGaps` when `gapNext` equals `gapEnd`": none, as expected: `WriteGaps` then writes nothing and returns true, an equivalent mutant.
  - **The pairs** (2026-11-06): BenchmarkDotNet as in the new paths' run below, of the two methods and of `ExcelOrdersRead.Sylvan_Records`, a control that doesn't use CsvHelper.Excel; the runs base, new, new, base, base, new, new, base, so four pairs, each parent pinned to CCD0. The results are under `_bench\results\phase3\step8`, in `bdn-pairs` and `bdn-pairs2`.
    - Another project's tests ran on the machine through both sets, their test hosts using up to five cores, and they weren't ours to stop. So a pair counts only when both its runs' standard deviations are under 2% of their means.
    - The first set (00:39–01:12): test hosts throughout, the CPU's mean 7.5–28% a run, the control 2,272–3,426 ms. One pair is clean.
    - The second (04:30–05:02), started after two minutes under 6% without a test host: runs 1 to 4 mostly quiet (mean 6.4–8.5%, the other project's helper processes coming and going), its tests back in run 6 (mean 18.8%), and run 8 above 10% throughout. The paged pool was 1.55–1.62 GB and 31–36 GB free.
    - New/base of the clean pairs, against the default path's A/B before the fix:

| Method | Clean pairs | A/B before the fix |
| --- | --- | ---: |
| Orders Records | 1.008 (first set), 0.978, 0.989 | 1.021 |
| Shared Rows | 1.012 | 1.012 |
| Inline Rows | none | 1.009 |
| Sylvan Records (control) | 1.007 | |

    - **What it shows:** Orders Records is within the noise: its three clean pairs are within 2%, two of them faster than base. Shared Rows has one clean pair, 1.2% slower, as the A/B found before the fix, so the pairs don't show the fix changing it, as the bisect's split variant didn't. With the noise at 1–2% between sessions, it is within it too, and so accepted as decided, on one clean pair. A quiet machine could measure it again.
- **The new paths' run** (2026-11-05, 23:29–23:41):
  - Tools: BenchmarkDotNet 0.15.8, .NET 10.0.13 (x64, x86-64-v4). Job: `BenchConfig`'s, with core 4 pinned, 4 warmups and 12 iterations; the parent pinned to CCD0. Filter `CsvHelper.Benchmarks.ExcelReportRead.*`: 9 benchmarks in 12 minutes.
  - The machine before: paged pool 2.49 GB, 38.0 GB free; after, 2.60 GB and 36.0 GB. The CPU was above 6% only at the start (BenchmarkDotNet's build, up to 24%) and in a few setups between benchmarks (up to 14%).
  - Standard deviations were at most 2.4% of the mean (`Table`), the others 0.6–1.4%. MB/s is of the same data as CSV, 305 MB.

| Read | Mean | Ratio | MB/s | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Report: `Range` (baseline) | 4,347.9 ms | | 70 | 1,135.4 MB |
| `RangeFirstRows` (1,000 rows) | 4.18 ms | 1,040x faster | | 1.28 MB |
| `Table` | 4,431.4 ms | 1.019x slower | 69 | 1,135.4 MB |
| `HeaderSearch` | 4,408.4 ms | 1.014x slower | 69 | 1,135.4 MB |
| `SkipHiddenRows` | 4,335.3 ms | 1.003x faster | 70 | 1,113.1 MB |
| `SkipHiddenColumns` | 4,404.7 ms | 1.013x slower | 69 | 1,135.4 MB |
| `FillMergedCells` | 6,269.9 ms | 1.442x slower | 49 | 1,136.8 MB |
| Plain: `Plain` (baseline) | 3,497.7 ms | | 87 | 1,135.4 MB |
| `PlainFillMergedCells` | 4,679.7 ms | 1.34x slower | 65 | 1,135.4 MB |

  - **What the numbers say:**
    - **The second read is what filling merged cells costs.** On the plain workbook, which has no ranges, finding none takes 1.18 s, a third of the read. That is 714 MB of sheet XML through the XML scanner's skip at about 600 MB/s, where inflating alone runs at 5.3–5.6 GB/s (Phase 1's step 9). On the report, the first read of its 965 MB takes about 1.6 s at the same rate, and the 10,000 ranges' fills and covered cells about 0.3 s more: +44% in all. Phase 9's cheaper skip over `sheetData` (a raw search for its end tag) is where to win it back.
    - **The early stop works:** the first 1,000 of the million rows read in 4.2 ms, opening the workbook and loading its shared strings included.
    - **The rest cost little:** `Table` +1.9% (the noisiest, ±2.4%), the header search +1.4% (the rows up to the header read twice, and a probe per row with a value), and hidden columns +1.3% (`cols` read, and a field looked up per cell). Skipping the hidden rows saves about what reading each row's hiding costs: 0.3% faster, with 2% fewer records allocated.
    - **The report itself** reads 24% slower than the plain workbook through `Range` (4.35 s against 3.50 s), for 35% more sheet XML: every empty field is a cell, and there are the regions' and keys' columns.
    - `Plain`, 3,498 ms, agrees with Phase 1's run of the same read (3,511 ms).
- **Tests:** 29 new per target (the cross-check's 21, and the new corpus file in the corpus tests): 1,791 on net10.0 and net8.0, 1,763 on net48; all passing.
- **Mutation check** on net10.0: 27 bugs planted one at a time, the suite run for each, and each file restored and checked against its SHA-256 (and, after the run, every planted line found back as it was). All 27 were caught. 24 are in the writer and the layout; 15 of those are caught only by the byte-for-byte comparison with Excel's file, which is why the cross-check has it:
  - "covered region cells in the text format": 1 failure;
  - "the title's last covered cell left out": 1;
  - "hidden rows not marked": 5;
  - "hidden after the row's `dyDescent`": 3;
  - "regions merged a row short": 5;
  - "every 50th row from the 49th hidden": 5;
  - "the hidden column without its width of 0": 1;
  - "a widened column's width rounded": 1;
  - "a widened column left at the default width": 1;
  - "the keys in the text format": 1;
  - "empty fields have no cell": 1;
  - "differential formats numbered from the first column": 1;
  - "the header row's differential format not referenced": 1;
  - "the second title's row spans only its cell": 1;
  - "regions named in their second row": 16;
  - "the page margins of a CSV opened in Excel": 1;
  - "the table over the regions too": 5;
  - "merged regions in the text format's own cell format": 1;
  - "no content type for the table": 3, the validator's among them;
  - "the table's relationship relative to the workbook": 5;
  - "one differential format short": 3;
  - "the title merged to the column before the last": 3;
  - "the second title in the text format": 1;
  - "the merged ranges counted without the title's": 1.
  - Three are in the cross-check itself, each making it fail against the right writer, to show that the comparison it weakens is made: "filled regions expected unfilled": 2; "`xr3` revision ids kept": 1; "the left-out content types compared": 1.
- **Core:** no change. The solution builds with 0 warnings. Results, logs and scripts' output are in `Csv\_bench\results\phase3\step8` (`ab`, `jit`, `layout`, `flushgaps`, `strip`, `bscan`, `dispatch`, `bdn`, and the cache's entry hashes before and after).

### Phase 4: writer foundation (L)

Status: the detailed design below was written on 2026-11-06, after the Excel and LibreOffice checks under "Checked on 2026-11-06", and approved that day, with every recommendation accepted; its "Changes elsewhere" are applied. Steps 1 (package and text) and 2 (typed cells) were built and approved that day, and committed together, and step 3 (formulas and limits) was built that day, and approved and committed 2026-11-07; see "As built". Step 4 (formats) was built, approved and committed 2026-11-07: it translates dates' and times' formats only, since `[CsvFormat]` doesn't apply to numbers in CsvHelper, and a number's `[CsvCulture]` is read instead; the user left numbers' formats out of Phase 4, as recommended (see step 4's "As built"). It changes some of the scope as planned, each change noted there: numbers' formats aren't translated; a formatted value can be a typed cell, read back with its member's format; Excel settings as a source of formats, and links from `Uri`, move to Phase 5; a formula holds no cached value, keeps its line breaks outside its strings as LF, and is refused with an operator at its start as well as at its end; `History` is a valid sheet name; and Excel's sheet-name comparison is wider than case. Step 5 (corpus and oracles) was built, approved, with every recommendation, and committed 2026-11-07: Excel's answers about the ten workbooks of the `written` set, each pinned by its parts' SHA-256 rather than its file's, the translation's oracle, and LibreOffice in CI; see its "As built". The branch was pushed that day. CI's first run failed Phase 3's reader fuzzer on its time limit, beside the suite's heaviest tests, not on an input; the fix (the fuzzer runs alone, and CI keeps the tests' results) was approved, built and committed that day, and CI's second run, with step 6, passed, comparing `cultures.xlsx` too; see step 5's "As built". Step 6 (security and fuzzing) was built, approved, with every recommendation, and committed 2026-11-07: the fuzzers found four bugs in CsvHelper.Excel, which are fixed (an escape's `_` closing text before it, names with U+FFFE, two names Excel takes to be one, formats .NET can't format); the two decisions made while building were approved as built (a name's 31 counted as written, formulas' white space preserved); and a .NET bug that corrupts core's CSV writes of some date formats is reported, and left open for a core decision; see its "As built". Step 7 (benchmarks) was built, run, approved and committed 2026-11-07: at its defaults CsvHelper.Excel writes a million rows in 2.05x (text) and 1.56x (orders) SpreadCheetah's time, deflate is about half of it, and the cells' `r` attributes nearly triple the text's file at `Fastest`; see its "As built". Step 8 ("As built") was written, approved and committed 2026-11-07, and Phase 4 is complete: every gate item is met, re-checked that day (the validator over every workbook the tests write, and CI's steps at step 7's commit), and the Zip64 test that "Risks" promised was added; see the summary that opens "As built".

Scope, as planned:

- **Package and sheets:**
  - Sheets stream into zip entries one at a time, because `ZipArchive` allows one open entry in create mode.
  - Styles, workbook, relationships and content types are written last, once all number formats and sheet names are known.
  - A style registry hands out `s` indexes as formats first appear.
- **Cell sink** (fields arrive as CSV text plus type; see Phase 0's "As built"):
  - Number cells: the invariant text of a number type is already `<v>` text (the CSV path formats it with `FloatFormatter`/`Utf8Formatter`), so it is copied as is. A number written with a format or a culture is parsed back with the writer's provider or, failing that, written as a text cell. (The design reads it back with its member's format and culture when `[CsvFormat]` gives them, and keeps it only when it formats to the same text; see "What a field becomes".)
  - Strings are written inline by default (shared strings are an option), with `xml:space="preserve"` when needed.
  - Booleans are written as `t="b"`. Dates are parsed from their `"O"` text and written as serial numbers with a date style; a date written with a format arrives as that text, and becomes a text cell. (The design makes it a date cell when its member's format is known and translatable.) An empty field is an empty cell.
  - The first record is the header when `HasHeader` is set: header styles, frozen rows, autofilter and tables apply to it, and `Columns` options match its names. (Those are Phase 5's; Phase 4 writes the header as text cells.)
  - Other types arrive with their type: `Uri` becomes a link, and `ExcelFormula` (through its converter or `WriteField(object)`) a formula. (Links are Phase 5's: Phase 4 writes a `Uri`'s text. `ExcelFormula` needs no converter: it is formattable.)
- **Formulas:**
  - An `ExcelFormula` value type (formula text plus an optional cached value), written through a built-in converter. (The design has no cached value: Excel and LibreOffice compute formulas on open; see "Formulas" and decision 17.)
  - A `WriteFormula` extension for manual writing.
- **.NET format → Excel number format:**
  - The formats come from Excel settings (`[ExcelColumn(Format)]`, `Columns`) and from `[CsvFormat]` on `T`'s members, read by reflection. A format string passed to `WriteField` does not reach the sink (Phase 0): such a field arrives as its formatted text. (Phase 4 reads `[CsvFormat]` and `[CsvCulture]`; the Excel settings come with Phase 5's `[ExcelColumn]` and `ExcelSheetOptions`.)
  - Precision: a number written with a format arrives rounded to it (`N2` → 2 decimals), and that is the value the cell stores. A column whose format should only change the display uses Excel's `Format` setting rather than `[CsvFormat]`, so its values arrive unformatted. Document this in the guide.
  - Custom numeric formats (`0.00`, `#,##0`, `0%`) mostly map one-to-one. (Not built: `[CsvFormat]` doesn't apply to numbers in CsvHelper, so no number arrives in a format, and the user left numbers' formats out of Phase 4; see step 4's "As built".)
  - Map the standard formats (`N2`, `F2`, `P1`, `C`) and date tokens (`MM` → `mm` for months). Excel reads `mm` as minutes only next to an hour or second token, so a .NET `mm` anywhere else is untranslatable.
  - Anything untranslatable is written as a text cell with the formatted value, which matches CSV output.
- **Quirk handling:**
  - Numbers beyond 15 significant digits follow `LongNumbers` (text by default), so IDs and card numbers don't silently change.
  - Numeric-looking strings stay text (ZIP codes, phone numbers, leading zeros), and `<ignoredErrors numberStoredAsText>` hides Excel's warning triangles.
  - The 32,767-character cell limit.
  - Sheet names: at most 31 characters, none of `: \ / ? * [ ]`, no leading or trailing apostrophe, unique ignoring case, and not `History`. (Excel opens a sheet named `History`, and compares names more widely than by case; see "Sheet names".)
  - Control characters are written as `_xHHHH_`, and a literal `_x` that looks like an escape as `_x005F_`.
  - Dates before 1900 can't be serial numbers, so they follow a policy: text or throw. (Negative durations too: `OutOfRangeDates`.)
  - `DateTimeOffset` follows `DateTimeOffsets`.
  - Row and column limits (1,048,576 × 16,384) throw by default. `RowLimit.NewSheet` continues on a new sheet with the header repeated.
- **Multiple sheets** through `WriteSheet` and `AddSheet`, plus async writes through the entry stream.
- **Compression level** option.
- **Gate:**
  - Files pass `OpenXmlValidator`.
  - Files open in Excel and LibreOffice without a repair prompt.
  - Files round-trip through our reader, ClosedXML and ExcelDataReader.

#### Public API

Phase 4 adds a workbook writer, its options and their enums, and a formula type. A sheet is written through a plain `CsvWriter`.

```csharp
public sealed class ExcelWorkbookWriter : IDisposable, IAsyncDisposable
{
	public static ExcelWorkbookWriter ToFile(string path, ExcelWriterOptions? options = null, FileOptions fileOptions = FileOptions.None);
	public static ExcelWorkbookWriter Create(Stream stream, ExcelWriterOptions? options = null, bool leaveOpen = false);

	public ExcelWriterOptions Options { get; }
	public IReadOnlyList<string> SheetNames { get; }

	public void WriteSheet<[DynamicallyAccessedMembers(Members)] T>(string name, IEnumerable<T> records);
	public ValueTask WriteSheetAsync<[DynamicallyAccessedMembers(Members)] T>(string name, IEnumerable<T> records, CancellationToken cancellationToken = default);
	public ValueTask WriteSheetAsync<[DynamicallyAccessedMembers(Members)] T>(string name, IAsyncEnumerable<T> records, CancellationToken cancellationToken = default);

	public CsvWriter AddSheet(string name);
	public CsvWriter AddSheet<[DynamicallyAccessedMembers(Members)] T>(string name);

	public void Dispose();
	public ValueTask DisposeAsync();
}

public sealed record ExcelWriterOptions
{
	public static ExcelWriterOptions Default { get; } = new();

	public CsvWriterOptions Csv { get; init; } = CsvWriterOptions.Default;
	public ExcelStrings Strings { get; init; }                                      // Inline
	public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Fastest;
	public ExcelRowLimit RowLimit { get; init; }                                    // Throw
	public ExcelLongNumbers LongNumbers { get; init; }                              // Text
	public ExcelDateTimeOffsets DateTimeOffsets { get; init; }                      // Text
	public ExcelOutOfRangeDates OutOfRangeDates { get; init; }                      // Text
}

public enum ExcelStrings { Inline, Shared }
public enum ExcelRowLimit { Throw, NewSheet }
public enum ExcelLongNumbers { Text, Number, Throw }
public enum ExcelDateTimeOffsets { Text, Utc, ClockTime }
public enum ExcelOutOfRangeDates { Text, Throw }

public readonly struct ExcelFormula : IEquatable<ExcelFormula>, IFormattable	// and on .NET ISpanFormattable, IUtf8SpanFormattable, ISpanParsable<ExcelFormula>
{
	public ExcelFormula(string formula);

	public string Formula { get; }

	public static ExcelFormula Parse(string text, IFormatProvider? provider = null);
	public static bool TryParse(string? text, IFormatProvider? provider, out ExcelFormula formula);

	public bool Equals(ExcelFormula other);
	public override bool Equals(object? obj);
	public override int GetHashCode();
	public override string ToString();
	public string ToString(string? format, IFormatProvider? formatProvider);

	public static bool operator ==(ExcelFormula left, ExcelFormula right);
	public static bool operator !=(ExcelFormula left, ExcelFormula right);
}

public sealed class ExcelFormulaConverter : ICsvConverter<ExcelFormula>
{
	public ExcelFormula Read(CsvReader reader, int index);
	public void Write(CsvWriter writer, ExcelFormula value);
}

public static class ExcelWriterExtensions
{
	public static void WriteFormula(this CsvWriter writer, string formula);
}
```

`Members` is what reading `[CsvFormat]` needs: public and non-public properties, fields and constructors.

**Writing a workbook:**

- `ToFile` creates or overwrites the file, which other processes can read while it is written, as `CsvWriter.ToFile` does. `Create` writes to a stream from its position; it need not seek.
- Sheets are written in turn, in tab order (decision 1):
  - `WriteSheet` writes one whole: the header and every record, as `CsvWriter.WriteRecords` writes them, then completes it.
  - `AddSheet` returns a `CsvWriter` for the sheet, for `WriteRecords`, `WriteRecord`, `WriteField` and dynamic records; disposing it completes the sheet. `AddSheet<T>` is the same, with `T`'s formats (see "Formats").
  - Adding a sheet while another's writer is open throws `InvalidOperationException` (decision 2).
- Disposing the workbook writer:
  - completes an open sheet with the records it ended (a later write to that sheet's `CsvWriter` throws `ObjectDisposedException`);
  - writes the workbook's other parts and closes the package;
  - gives a workbook with no sheet one empty sheet, `Sheet1`, since Excel opens no workbook without one (decision 4).
- A failed record (a converter's exception, a limit) is discarded, as `CsvWriter` discards one. The sheet keeps the records before it, and the workbook can still be completed into a file Excel opens. An exception from the stream leaves the file incomplete.
- `SheetNames`: the names given so far, the open sheet's included, with those that `RowLimit.NewSheet` adds.
- Async: `WriteSheetAsync`, `CsvWriter`'s async methods and `DisposeAsync` write through the entry streams asynchronously. Before .NET 10, closing an entry and the package writes synchronously (see "Risks").
- A workbook writer is not thread-safe, as `CsvWriter` isn't. `ParallelWriteRecords` throws `NotSupportedException` on a sheet's writer, as Phase 0 built it, until Phase 9.

**Options:**

- `Csv` applies as for any cell sink (Phase 0's "As built"):
  - These apply: `HasHeader`, `ValidateFieldCount`, `FormatProvider` and `BufferSize`.
  - These don't, and aren't rejected (decision 24): `Mode`, `Separator`, `Quoting`, `ShouldQuote`, `Escape`, `FieldWidths`, `NullValue`, `TrueValue` and `FalseValue`, `NewLine`, `Injection` (text cells are never evaluated) and `WriteByteOrderMark`.
  - `CommentPrefix`: `WriteComment` throws `NotSupportedException`, as Phase 0 built it.
  - `[CsvOptions]` on `T` applies through `Csv = CsvWriterOptions.For<T>()`, as for CSV.
- `Strings`:
  - `Inline` (the default): each text cell holds its string (`t="inlineStr"`), and nothing is kept between rows.
  - `Shared`: each distinct string once, in `xl/sharedStrings.xml`, written at the end. The table holds every distinct string until the workbook is disposed.
- `CompressionLevel`: `Fastest` by default, the "super fast" deflate that Excel's own entries are flagged with (decision 22).
- `RowLimit`, `LongNumbers`, `DateTimeOffsets` and `OutOfRangeDates`: below.
- `ToFile` and `Create` validate the options: a value outside an enum throws `ArgumentOutOfRangeException`, and a null `Csv` `ArgumentNullException`.

**Sheet names** (decision 5; checked through COM, below):

- 1 to 31 UTF-16 code units.
- None of `\ / ? * [ ] :`, no apostrophe first or last, and no control characters (U+0000 to U+001F and U+007F to U+009F). Excel opens names with control characters, written by reference or as `_xHHHH_`, but its tabs can't take them, so the writer is stricter.
- Unique as Excel compares them:
  - Excel refuses names that differ in case, ASCII or not (`Äpfel` and `äpfel`, `straße` and `STRASSE`, `σ` and `ς`, `K` and the Kelvin sign), or by Unicode equivalence (`é` composed and decomposed, `ﬁ` and `fi`).
  - It opens names that differ in width (`A`, `Ａ`), in kana (`ｶ`, `カ`, `あ`, `ア`), in accents (`e`, `é`), in dots (`I`, `ı`, `i`, `İ`), or by a zero-width space.
  - The writer's key is a name's NFKC form, with `ß` and `ẞ` as `ss`, upper-cased invariantly. It agrees with every pair checked but the compatibility forms (`A` and `Ａ` are one name to it), where it is stricter.
- Anything else is allowed: `History`, `TRUE`, `A1`, `R1C1`, `123`, spaces at either end, and `= + ! # $ % & ; , " <`, all of which Excel opens (decision 5).
- An invalid name throws `ArgumentException` from `WriteSheet` and `AddSheet`.
- Names are written escaped for XML, with `_x005F_` before text that reads as an escape (`_xHHHH_`), since Excel decodes escapes in sheet names too.

**What a field becomes.** The sink gets each field's text and type (Phase 0):

| Field | Its text | Cell |
| --- | --- | --- |
| Text: `string`, spans, `char`, `Guid`, enums, `Uri`, any other type | as written | a string; empty: no cell |
| `bool` | `True`, `False` | `t="b"`, `1` or `0` |
| Integers (`sbyte` to `ulong`, `Int128`, `UInt128`) and `decimal` | invariant digits | a number; past 15 significant digits, `LongNumbers` |
| `double`, `float`, `Half` | invariant, shortest round trip | a number; `NaN` and the infinities: text |
| `DateTime` | `"O"` | the serial of its clock time, in a date format by what it holds |
| `DateOnly` | `yyyy-MM-dd` | a serial, `yyyy-mm-dd` |
| `TimeOnly` | `"O"` | a fraction of a day, `hh:mm:ss` |
| `TimeSpan` | `"c"` | days, `[h]:mm:ss` |
| `DateTimeOffset` | `"O"` | as `DateTimeOffsets` says |
| `ExcelFormula` | `=` and the formula | a formula, without a cached value |
| null | empty | no cell |

- **Numbers:**
  - The default invariant text is copied into `<v>` as it is. It is the shortest text that reads back as the value, Excel reads every form .NET writes (`1E-05`, `1.5E+20`, `0.10`, 17 digits, `-0`), and it keeps all 17 digits.
  - Other text, a culture's or a format's, is read back. With its member's format known, see "Formatted values". Without one, it is read with the writer's provider, strictly: a sign, digits without leading zeros, the decimal separator and an exponent, nothing else (decision 8). That keeps `1234,5` (de-DE) a number, and keeps `1,234.50` (`N2`) and `0042` (`D4`) text, whose look a number cell would lose.
  - `NaN`, `Infinity` and `-Infinity` are text cells, since Excel can't open them as numbers (decision 9).
  - **Long numbers** (decision 10): an integer or `decimal` with more than 15 significant digits is a text cell (`Text`, the default), a number that Excel rounds to its 15 digits (`Number`), or throws (`Throw`). Leading zeros and the zeros that end the digits don't count, so `10000000000000000000` has one significant digit. Doubles are numbers whatever their digits: the cell stores the same double.
  - Subnormal doubles are written as they are. Excel reads them as 0; LibreOffice and CsvHelper.Excel keep them.
- **Dates and times** (decisions 11 to 13; serials in the 1900 system):
  - `DateTime`: its clock time, whatever its `Kind` (the offset in a local time's `"O"` text is dropped). 1900-01-01 is day 1, and from 1900-03-01 the serial is one more, past Excel's 1900-02-29, as Phase 2 reads it.
  - Each value is rounded to the millisecond, as Excel shows it and Phase 2 reads it, so a write and a read give the same `DateTime` to the millisecond.
  - The format is by what each value holds: `yyyy-mm-dd` at midnight, `yyyy-mm-dd hh:mm:ss` with a time in whole seconds, and `yyyy-mm-dd hh:mm:ss.000` otherwise.
  - `DateOnly`: `yyyy-mm-dd`. `TimeOnly`: `hh:mm:ss`, or `hh:mm:ss.000` with milliseconds.
  - `TimeSpan`: its days (`TotalDays`), as `[h]:mm:ss`, with `.000` when it has milliseconds. Phase 2 reads that as a duration.
  - `OutOfRangeDates`: a date before 1900-01-01, or a negative `TimeSpan`, which Excel shows as `########`, is a text cell (`Text`, the default) or throws (`Throw`).
  - `DateTimeOffsets`: `Text` (the default) writes the `"O"` text as a text cell; `Utc` writes the serial of `UtcDateTime`; `ClockTime` writes the serial of `DateTime`, the time at its own offset.
- **Booleans:** `t="b"`, `1` or `0`. A `[CsvBooleanValues]` member arrives as its text, a string, and is text.
- **Text:**
  - Escaped as Excel writes text:
    - `&`, `<` and `>` as entities;
    - tab and LF as they are;
    - CR as `_x000D_`, since a literal one reads as LF in Excel and LibreOffice;
    - the other control characters, U+FFFE and U+FFFF, as `_xHHHH_`;
    - `_x005F_` before text that reads as an escape (`_x0041_`, with hex digits of either case).
  - `xml:space="preserve"` when the text starts or ends with a space, tab or LF.
  - Invalid UTF-8 (which only `WriteField(ReadOnlySpan<byte>)` can pass) becomes U+FFFD, as invalid UTF-16 does in core.
  - At most 32,767 UTF-16 code units: Excel opens no file with a longer cell, inline or shared. A longer field throws `InvalidOperationException` naming the cell, and the record is discarded (decision 18).
  - Excel flags text that looks like a number (`00123`, `1.5`, `1E5`) with a warning triangle. When a text cell starts like a number (a digit, a sign or a `.`), the sheet gets `<ignoredErrors>` with `numberStoredAsText` over the cells it wrote (decision 19).
  - Text that would be a formula or an error elsewhere (`=1+1`, `#N/A`, `TRUE`) is only text: Excel never evaluates a text cell.
- **Formatted values** (decisions 15 and 16): a number or date whose member's format is known is read back with that format and the member's culture. It is a typed cell, in that format translated to Excel's, only when the value it reads as formats to the same text again; otherwise it is a text cell, as in CSV. So a cell holds what its text shows (`N2` rounds to two decimals, `yyyy-MM` drops the day), never another value. See "Formats". (As built at step 4: dates and times only; a number's member gives its `[CsvCulture]` alone, since `[CsvFormat]` doesn't apply to numbers.)

**Rows and cells:**

- Record `n` (from 0) is row `n + 1`, so the header, when `HasHeader`, is row 1. Field `i` is column `i` from `A`.
- Every row and cell has its `r`, as Excel writes them. A record without a value has no `<row>`, and an empty field no `<c>`.
- No `dimension` and no `spans`: both need rows the sink hasn't seen (decision 23).
- **Limits:**
  - A record past row 1,048,576 follows `RowLimit`. `Throw` (the default) throws `InvalidOperationException` and discards it.
  - `NewSheet` completes the sheet and goes on in a new one, `Orders (2)`, then `Orders (3)` and so on, the name shortened to fit 31 code units and made unique, with the header repeated (decision 20).
  - A field past column 16,384 (`XFD`) throws, and the record is discarded.
- The header is text cells. Phase 5 makes it bold.

**Column widths** (decision 14):

- Excel shows a number its column can't fit in its format as `########`, and at the default width that is every date. That includes `1/15/2024` in the built-in short date, in Calibri 11 and in Aptos Narrow 11 (checked).
- So the sink holds the records up to the first data record (the one after the header) before it writes `<cols>`, which comes before the rows.
  - Each of that record's cells in a number format other than General gets a width that fits its text, when that is wider than the default.
  - Dates and times in the default formats get the widths Excel's AutoFit gives them (below). Other formats' widths come from the cell's text, through a table of Calibri 11's character widths measured through COM. (As built, every width comes from the table, for 96 DPI: the AutoFit widths below were measured at 144 DPI, and are about 4.5% too narrow at 96 DPI. See step 2's "As built".)
  - A `DateTime` column gets the width of the format with seconds, even when its first value is a date alone. A `TimeSpan` column's width is its first value's text.
- Text, and numbers in General, never show `########`, and get no width. Phase 5's auto-fit widens this to every column, over its first N rows.

**Formulas** (decision 17):

- `ExcelFormula` holds a formula, given with or without its `=`. `Formula` is without it, and `ToString` gives `=` and the formula, which is what a CSV writer writes (as text, so `Injection` applies to it there).
- The constructor checks what makes Excel refuse a file (checked):
  - at most 8,192 characters;
  - outside string literals and quoted sheet names, parentheses, brackets and braces that are balanced;
  - string literals that are closed;
  - no operator at the end.
- Other formulas that Excel can't parse also make the file unopenable, and the docs say so. A formula that Excel parses but can't compute (an unknown function, a missing sheet) opens, as `#NAME?` or `#REF!`.
- The cell is `<f>` alone, without a cached value:
  - Excel computes it on open.
  - LibreOffice computes a formula without a cached value, and shows a cached one as it is, even one that is stale.
  - Readers that don't compute read the cell as empty: CsvHelper.Excel with `Formulas = CachedValue`, and ExcelDataReader. With `Formulas = FormulaText`, CsvHelper.Excel reads `=` and the formula.
- Functions newer than Excel 2007 are stored with a prefix (`_xlfn.XLOOKUP`), and Excel reads one without it as `#NAME?`. A formula is written as given, so it must have the prefix.
- **Writing:** a member of type `ExcelFormula` is written by the generated record writer as a formattable value (`IUtf8SpanFormattable`, and `IFormattable` on the netstandard and .NET Framework builds), with no converter. `writer.WriteFormula("SUM(A1:A3)")` writes one by hand.
- **Reading:** `ExcelFormula` parses `=` and a formula, which `Formulas = FormulaText` reads; other text throws `FormatException`. It is `ISpanParsable<ExcelFormula>` on .NET, for the generated mapper. On targets without `ISpanParsable`, a member needs `[CsvConverter(typeof(ExcelFormulaConverter))]`.
- `default(ExcelFormula)` writes an empty cell.

**Formats** (decisions 15 and 16):

- **Where they come from:**
  - `[CsvFormat]` (its first format, the one the record writer writes) and `[CsvCulture]` on `T`'s members, read by reflection, for `WriteSheet<T>` and `AddSheet<T>`.
  - `AddSheet(name)` has no `T`: its formatted values are read back with the writer's provider, as above.
  - Phase 5 adds `[ExcelColumn(Format)]` and `ExcelSheetOptions.Columns`, which give Excel's format codes directly, for display only.
- **Which column a member's format is for:**
  - A member's column is found by its header name: its `[CsvColumn]` name or its own, after the prefixes of the records it is nested in, and the `NameIndex`th column of that name.
  - The names are the header record's. Without a header, they are the names that `T`'s record writer's `WriteHeader` writes into a capturing sink.
  - A member whose name matches no column, and every member of a type mapped by `[CsvRecordMap]` (whose attributes are on its map), gets no format.
- **Translation:** a .NET format becomes an Excel format code when Excel can show the same text.
  - **Numbers** (not built, and left out of Phase 4 by the user on 2026-11-07: `[CsvFormat]` doesn't apply to numbers in CsvHelper, so no number arrives in a format; see step 4's "As built"):
    - `F`, `N`, `D`, `E`, `P` and `C`, with their precision and the culture's patterns; `C` with the culture's symbol (`[$€-407]`).
    - Custom formats: `0`, `#`, `.`, `,` (grouping and scaling), `%`, `‰`, `E+` and `E-`, up to three sections, and literals, quoted.
    - Not translated: `X`, `R`, and lowercase exponents (`e+`), which Excel refuses.
  - **Dates and times:**
    - The standard formats, as the culture's patterns, and then the custom tokens: `d` to `dddd`; `M` to `MMMM` (as `m` to `mmmm`); `yy` and `yyyy`; `H` and `HH`; `h` and `hh` with `tt`; `m`, `mm`, `s` and `ss`; `f` to `fff` (as `.0` to `.000`); `tt` and `t` when the culture's designators are `AM` and `PM`; the culture's separators; and literals.
    - Names (`MMM`, `dddd`) in a culture other than English get its locale (`[$-40C]`).
    - Not translated: `K` and `z` to `zzz` (offsets); `F`; `ffff` and more (Excel refuses four digits); `y` and `yyy`; `g`; a 12-hour `h` without `tt`; and a month token where Excel would read minutes, next to an hour or a second.
  - **`TimeSpan`:** its default text only. Custom `TimeSpan` formats stay text.
  - Whatever isn't translated stays text.
- **Reading back:** the text is parsed with the format and the culture: dates with `ParseExact`, and numbers with the culture's number styles once the format's literals, percent and per-mille signs and scaling are taken off. Then the value is formatted again with both, and only the same text keeps it.
- **Checked against Excel:** every code the translator gives must pass `FormatCheck`, which refuses what Excel refused (below). The corpus asks Excel how it shows each translated format (see "Testing").

**Package** (decision 3):

- Each sheet streams into its entry, `xl/worksheets/sheetN.xml`, as it is written.
- At the end come `xl/sharedStrings.xml` (with `Shared` strings), `xl/styles.xml`, `xl/workbook.xml`, `xl/_rels/workbook.xml.rels`, `_rels/.rels` and `[Content_Types].xml`. Excel and LibreOffice open that order (checked).
- There is no theme, no document properties (so no author and no dates), no views, no `dimension` and no `calcPr`: Excel opens without them.
- Every entry is dated 1980-01-01, as Excel dates its entries, so a workbook's bytes depend only on what is written.
- Styles: Excel's minimal style sheet in Calibri 11 (decision 6), with a cell format for each number format used, in the order of first use, and custom formats from `numFmtId` 164.

**Messages** (`ArgumentException` for names and formulas, `InvalidOperationException` for the rest):

- "Sheet names are 1 to 31 characters long, and \"Quarterly results 2024 (final draft)\" has 36."
- "A sheet name can't have \\, /, ?, *, [, ] or :, and \"Q1/Q2\" has /."
- "A sheet name can't start or end with an apostrophe: \"'Totals\"."
- "A sheet name can't have control characters, and \"a\\tb\" has U+0009."
- "The workbook already has the sheet \"Data\", which Excel takes \"data\" to be."
- "The sheet \"Raw\" is still being written: dispose its CsvWriter before adding a sheet. A workbook is written one sheet at a time."
- "Cell Orders!C14 has 40,000 characters, and Excel opens no cell of more than 32,767. The record is discarded."
- "The sheet Orders is full at row 1,048,576, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet. The record is discarded."
- "Record 5 has a field in column XFE, past XFD, Excel's last column. The record is discarded."
- "Cell Orders!B7 holds 12345678901234567890, which has 20 significant digits, and Excel keeps 15 (ExcelWriterOptions.LongNumbers is Throw). The record is discarded."
- "Cell Orders!D3 holds 1899-06-01, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw). The record is discarded."
- "The formula \"SUM(A1:A3\" has a parenthesis that isn't closed, and Excel opens no workbook with it."
- "A formula is at most 8,192 characters long, and this one has 9,000."

**Changes to reading:** `WorkbookPart` decodes `_xHHHH_` in sheet names, as Excel does (checked), so that a sheet the writer names `a_x0041_b` reads back with that name (decision 25). This is CsvHelper.Excel's code, not core's.

#### Checked on 2026-11-06

Excel 365 (16.0 build 20527) through COM, with probe workbooks written by hand and opened read-only (`Workbooks.Open` throws for a file Excel can't open), and some converted to CSV with LibreOffice 26.8. The probes, their scripts and the answers are in `Csv\_bench\results\phase4\design`.

- **Package:**
  - Excel and LibreOffice open the parts in a streaming writer's order: the sheet first, `[Content_Types].xml` last.
  - Both open a package with no styles part, rows and cells without `r`, and an empty `sheetData`, and one without `dimension`, views, theme, `calcPr` or document properties.
  - Excel's own files date every entry 1980-01-01 and flag its deflate as "super fast".
- **Numbers in `<v>`:**
  - Excel opens `1E-05`, `1.5E+20`, `0.10`, `1234.50`, 17-digit texts (and reads `0.30000000000000004` and `1.0000000000000002` exactly), `1.7976931348623157E+308`, `-0` (as 0), `1e5`, `.5`, `5.` and ` 5 `. `9007199254740993` reads as the nearest double.
  - It can't open `NaN`, `Infinity`, `+5`, `0x10` or `1,5`.
  - It reads subnormals (`4.9406564584124654E-324`, `2.2250738585072009E-308`) as 0. LibreOffice keeps them.
- **Text:**
  - A tab and an LF are kept as they are. A literal CR reads as LF in Excel and in LibreOffice (XML's line ends), so CR must be `_x000D_`.
  - Control characters as `_xHHHH_` read back, while a character reference to one (`&#1;`) makes a file Excel can't open. `_xFFFE_` reads as U+FFFE.
  - `_x005F_` escapes an underscore whether its hex digits are upper or lower case. `_X0041_` (capital X) and `_x0041` (no closing `_`) are literal.
  - Spaces, tabs and LFs at either end need `xml:space="preserve"`. An emoji takes two code units.
  - The limit is 32,767 UTF-16 code units, in an inline string or a shared one: 32,767 opens, and 32,768 doesn't, counting an emoji as two.
  - With `numberStoredAsText` in `ignoredErrors`, over the used range or the whole grid, `00123`, `1.5` and `1E5` aren't flagged. A date's text (`2024-01-15`) and `TRUE` aren't flagged either way. `=1+1` and `#N/A` in text cells stay text.
- **Dates and widths:**
  - At the default width every date shows `########`: the built-in short date (`1/15/2024`) and short date and time, `yyyy-mm-dd`, and the formats with times. `hh:mm:ss` fits.
  - The widths Excel's AutoFit stores, in Calibri 11: `yyyy-mm-dd` 10.09, `yyyy-mm-dd hh:mm` 15.18, `yyyy-mm-dd hh:mm:ss` 17.82, `yyyy-mm-dd hh:mm:ss.000` 21.45, `m/d/yyyy` 9.45, `m/d/yyyy h:mm` 14.54, `hh:mm:ss` 7.91. In Aptos Narrow 11 they are within 0.1.
- **Formulas:**
  - Excel computes a formula without a cached value on open, and recomputes one with a stale cached value, with or without `fullCalcOnLoad`. LibreOffice computes the first and shows the stale value of the second.
  - Excel can't open a file with a formula it can't parse (`SUM(`, `A1+`), or with one longer than 8,192 characters (8,192 opens).
  - It reads `=A1` (with its `=`) as `A1`, and lowercase as uppercase. An unknown function is `#NAME?`, as is `XLOOKUP` without its `_xlfn.` prefix; a missing sheet is `#REF!`.
- **Number format codes:**
  - Excel can't open a file whose format has an unclosed quote or bracket, five sections, a lowercase `e+`, four digits of a second's fraction, letters that aren't quoted (`0.00 kg`), or an unknown color (`[Purple]`).
  - It opens `‰`, `[$-409]`, `[$€-407]`, conditions, fractions, `*` fills, `[Color10]`, `@`, and an empty code (as General). It cuts a code longer than 255 characters to 255.
- **Sheet names:**
  - 31 UTF-16 code units open, and 32 don't, counting an emoji as two.
  - Excel can't open a name with `\ / ? * [ ] :`, an empty name, or one that starts or ends with an apostrophe.
  - It opens `History`, ` `, ` a`, `a `, `123`, `TRUE`, `A1`, `R1C1`, `=a`, `+a`, and names with `!`, `#`, `$`, `%`, `&`, `;`, `,`, `"` and `<`. It opens a tab, an LF or U+0001 in a name, by character reference or as `_x0001_`, which it decodes.
  - It can't open two names that differ in case (`Data`, `data`; `Äpfel`, `äpfel`), nor `straße` and `STRASSE`, `ﬁ` and `fi`, `σ` and `ς`, `K` and the Kelvin sign, or `é` composed and decomposed.
  - It opens `A` and `Ａ`, `e` and `é`, `ｶ` and `カ`, `あ` and `ア`, `I` and `ı`, `i` and `İ`, and `ab` beside `ab` with a zero-width space between its letters.
- **Rows:** row 1,048,576 opens, and 1,048,577 doesn't.

#### Internal design

**New files:**

```
ExcelWorkbookWriter.cs, ExcelWriterOptions.cs (with the five enums), ExcelFormula.cs, ExcelFormulaConverter.cs, ExcelWriterExtensions.cs
Internal/Writing/
  PackageWriter.cs      the ZipArchive in create mode: one entry at a time, the parts at the end, entries dated 1980-01-01
  PackageParts.cs       the content types, relationships, workbook and styles, written at the end
  SheetSink.cs          the CsvCellSink: rows and cells, the held records, the limits, flushes and completion
  XmlOutput.cs          the UTF-8 buffer a sheet's XML goes into: escaped text, numbers and references
  CellValue.cs          a field's text and type to a cell: its kind, its value and its format (as built: CellKind.cs and CellKinds.cs)
  DotNetText.cs         .NET's own texts read back: "O" dates and times, "c" durations, invariant numbers (and NumberCulture.cs, a culture's signs)
  Serial.cs             dates and times to serials, in the 1900 system
  StyleRegistry.cs      number format codes to cell format indexes
  SharedStringTable.cs  Strings = Shared: each distinct UTF-8 string once
  SheetNames.cs         a name's checks and its comparison key
  ColumnWidths.cs       widths from text, with Calibri 11's character widths
  FormatTranslator.cs   a .NET format and culture to an Excel code, and its text read back
  FormatCheck.cs        whether Excel opens a format code
  FormulaCheck.cs       the formula checks
  MemberFormats.cs      T's [CsvFormat] and [CsvCulture], by header name
  HeaderCapture.cs      a sink that keeps the names a record writer's WriteHeader writes
```

**The sink** (`SheetSink`):

- **Fields:** `WriteField(utf8, type)` counts the column, checks its limit, and finds the field's cell kind from its type. Each column keeps the last type it saw and that type's kind, so that a column of one type costs one reference comparison per field. The kind writes the cell into the record's part of the buffer. Types are matched by reference to `typeof`, and `DateOnly` and `TimeOnly` by name on the netstandard builds, as core does.
- **Records:** a record's first cell with a value writes `<row r="n">` before it. `EndRecord` closes the row, if it has one, and asks for a flush once the buffer passes 64 KB. `DiscardRecord` moves the buffer back to the record's start. Shared strings and styles it added stay; Excel ignores unused ones.
- **Flushes:** the writer calls `Flush` or `FlushAsync`, as its caller is sync or async, which writes the buffer to the entry stream.
- **Held records:** nothing is flushed until the first data record ends (or the sheet does). Then the sheet's start, `<cols>` and `<sheetData>` are written, and the held rows after them.
- **Completing:** `</sheetData>`, Excel's default `pageMargins`, `ignoredErrors` when a text cell needs it, and `</worksheet>`. Then the buffer is flushed, and the entry is closed, so that the workbook can open the next.
- **`NewSheet`:** at row 1,048,577 the sink completes its entry, opens the next sheet's, and writes the held header row (whose cells' `r` are row 1 in every sheet) and `<cols>` again.

**Values:**

- `CellValue` maps a type to a kind once per type: Text, Bool, Integer, Decimal, Float, DateTime, DateOnly, TimeOnly, TimeSpan, DateTimeOffset or Formula.
- **Numbers:** the invariant number grammar (`-?(0|[1-9][0-9]*)(\.[0-9]+)?(E[+-]?[0-9]+)?`) decides, without parsing, that text is the default and goes into `<v>` as it is. The significant digits of integers and decimals are counted over the same bytes. Other text is parsed, strictly, with the writer's provider (or the member's format, below) and written with core's `FloatFormatter`, or as the integer's or decimal's invariant text.
- **Dates:** `DotNetText` reads .NET's fixed `"O"` layouts (`yyyy-MM-ddTHH:mm:ss.fffffff` with nothing, `Z` or an offset; `yyyy-MM-dd`; `HH:mm:ss.fffffff`) and `"c"` (`[-][d.]hh:mm:ss[.fffffff]`) by position. `Serial` turns ticks into milliseconds since 1899-12-30 (less one day before 1900-03-01), which a `double` holds exactly, and divides once by 86,400,000.
- **Text:** `XmlOutput` finds the bytes that need escaping (`&`, `<`, `>`, `_`, the control bytes, and U+FFFE and U+FFFF's `EF BF`) with one `IndexOfAny`, and copies the runs between them. A sink can't tell a field of `WriteField(ReadOnlySpan<byte>)`, whose UTF-8 core doesn't check, from other text (both are `string`), so every text field is checked for valid UTF-8: with `Utf8.IsValid` on .NET, and a loop of its own on the other builds.
- **Shared strings:** a dictionary keyed by UTF-8 bytes, over a byte arena like the reader's, gives each string its index.
- **Styles:** `StyleRegistry` gives each format code a cell format index, 0 for General, in the order of first use.

**Formats:**

- `MemberFormats.For<T>()` reads, once per type, the members with `[CsvFormat]` or `[CsvCulture]`: properties and fields, the base types' included, and the primary constructor's parameters (matched to properties by name), with their header names as "Formats" gives them. It is cached per type.
- `HeaderCapture` is a `CsvCellSink` that keeps the fields of its first record, for a `CsvWriter` created over it (core's internal constructor) to which `CsvRecordWriter.Create<T>().WriteHeader` writes.
- `FormatTranslator.Translate(format, type, culture)` tokenizes a .NET format once per column and gives an Excel code (or none) and a reader of its text. It is cached per column.
- `FormatCheck` refuses what Excel refused (unbalanced quotes and brackets, more than four sections, lowercase `e+`, four or more fraction digits of seconds, letters that aren't quoted or tokens, unknown colors) and codes past 255 characters.

**Package:**

- `PackageWriter` holds the `ZipArchive` (`ZipArchiveMode.Create`, entry names in UTF-8, as the reader opens them), creates each entry with the options' level and the fixed date, and on .NET 10 uses its async entry and disposal methods.
- `PackageParts` writes the fixed XML around the lists: sheet names, relationship ids (`rId1` on for the sheets, then the styles and the shared strings), content type overrides, number formats and cell formats.

**Changes to reading:** `WorkbookPart.ReadSheet` decodes the name's `_xHHHH_` escapes (`XmlText.DecodeEscapes`) after its XML.

#### Testing

**Unit tests:**

- **`SheetNames`:** every name under "Checked on 2026-11-06", accepted and refused, the comparison key's cases, and the escapes it writes.
- **`XmlOutput`:** every control character, CR, U+FFFE and U+FFFF, `_x` texts in both cases and their near misses, entities, `preserve`, each form of invalid UTF-8, and the 32,767 limit with surrogate pairs across it.
- **`DotNetText` and `Serial`:** `"O"` texts of each `Kind`, `DateOnly`, `TimeOnly`, `"c"` durations (negative, with days, with fractions), 1900-01-01 to 1900-03-01, the rounding to milliseconds, and random dates through Phase 2's conversion and back.
- **Numbers:** the invariant grammar, cultures' texts, leading zeros, long numbers' significant digits, `NaN` and the infinities.
- **`FormatTranslator`:** each standard format and custom token, in the invariant culture, en-US, de-DE, fr-FR and ja-JP; what isn't translated; and reading back. Every code it gives passes `FormatCheck`, and Phase 2's `NumberFormats.Classify` finds it the right kind: a date format a date, a duration's a duration, a number's a number.
- **`FormatCheck`** and **`FormulaCheck`:** each refusal and acceptance checked through COM, and brackets inside string literals and quoted sheet names.
- **`MemberFormats`:** its names checked against the record writer's own header for every test type: `[CsvColumn]` names, nested records' prefixes, `NameIndex`, constructor parameters and base types.
- **`StyleRegistry`** and **`SharedStringTable`.**

**End to end,** sync and async, on net10.0, net8.0 and net48:

- `WriteSheet<T>` from `IEnumerable<T>` and `IAsyncEnumerable<T>`; `AddSheet` and `AddSheet<T>` with `WriteRecords`, `WriteRecord` and `WriteField`; dynamic records; `HasHeader` both ways; several sheets; an empty workbook; `ToFile`; a stream that can't seek.
- Each row of "What a field becomes", read back by CsvHelper.Excel's reader to the same values, through `GetRecords<T>`, dynamic records and `CsvDataReader`, with each option: `Strings`, `LongNumbers`, `DateTimeOffsets`, `OutOfRangeDates` and `RowLimit`. An internal row limit lets `NewSheet` be tested without a million rows; one test writes 1,048,577 rows.
- Errors: every message, the discarded record, a sheet completed after a failure, an open sheet at `Dispose`, and `ObjectDisposedException` after.
- Every file these tests write passes `OpenXmlValidator`, and ClosedXML and ExcelDataReader read it to the same values, with their differences listed.

**Round trips:** a seeded property test writes random records of every type (random values, and strings of every class of character) and reads them back, equal by the rules above: dates to the millisecond, long numbers as text that parses back, and so on. A few thousand records per run.

**Corpus and Excel's answers** (decision 26):

- **A `written` content set** (`tools/CsvHelper.Excel.Corpus/Written.cs`, linked into the tests): workbooks that CsvHelper.Excel writes. They hold every type of the table and its edge values, formatted members in several cultures, formulas, long and escaped text, `Shared` strings, `NewSheet`, and names Excel accepts.
- **The corpus tool's `written` command** writes them, opens each in Excel through COM, and records Excel's answers in `Files/excel-written/*.json`: whether it opened; each sheet's name and column widths; and each cell's `Value2`, type, `Text`, `NumberFormat` and `Formula`. With them goes the SHA-256 of the workbook Excel read. (As built: of its parts, since deflate differs between runtimes, with a fingerprint of the culture data `cultures.xlsx` reads; see step 5's "As built".)
- **The tests** write the same workbooks again. A workbook whose SHA-256 differs from its answers' fails with "run the corpus tool's written command", since the writer changed. Otherwise they compare Excel's answers with what was written: values and types exactly, and each formatted cell's `Text` with .NET's text of its value (the translation's oracle). Each difference is listed with its reason.
- **LibreOffice:** a test converts the written workbooks to CSV (`soffice --convert-to csv`) and compares. It is skipped where `soffice` is missing, and CI installs LibreOffice for it (decision 27).

**Security and fuzzing:**

- **Text:** random fields, UTF-8 and UTF-16, of control characters, surrogates, invalid sequences, near escapes and lengths around 32,767, written and read back the same (U+FFFD for what was invalid), each file passing `OpenXmlValidator`.
- **Names:** random names: each accepted one opens in CsvHelper.Excel and passes the validator, and each refused one throws `ArgumentException`.
- **Formats:** random .NET numeric and date formats and values (date and time formats only, since step 4 translates no numbers' formats). The translator gives no code or one that `FormatCheck` passes, and reading back never keeps a value whose text differs.
- **Formulas:** `FormulaCheck` throws nothing but `ArgumentException`.
- **Memory:** with `Inline` strings, a sheet of 32,767-character cells streams in a bounded buffer. A million distinct shared strings take the arena's size and no more.

#### Benchmark

- **No core change,** so no core gate (see "Core changes").
- **Two classes** over the benchmarks' existing data:
  - `ExcelPackageAssetsWrite`: the PackageAssets data, a million rows of text and its header.
  - `ExcelOrdersWrite`: a million `OrderRecord`s, of dates, numbers, booleans and text.
- **Writers:**
  - CsvHelper.Excel with `Inline` and `Shared` strings at `Fastest`, and `Inline` at `Optimal`;
  - SpreadCheetah 1.28.0, LargeXlsx 2.0.2 and MiniExcel 1.46.0, the versions the corpus uses;
  - CsvHelper's CSV writer, as the reference.
  - Each writes to a stream that counts and drops its bytes. The file sizes are recorded too.
- One BenchmarkDotNet run, recorded in "As built". Phase 4 sets no speed target; Phase 9 tunes.
- The benchmarks' own `ExcelStrings` enum gives way to the library's.

#### Core changes

None. Phase 4 uses what core has: the internal `CsvWriter(CsvCellSink, CsvWriterOptions)` constructor and `CsvCellSink`, and `FloatFormatter`, through `InternalsVisibleTo`; and the public `CsvRecordWriter.Create<T>().WriteHeader`.

Considered and avoided:

- **Telling the sink `T` from `WriteRecords<T>`,** through a hook in core's record methods. It would add IL to them, which Phase 0 found costly; `AddSheet<T>` gives the sink `T` instead.
- **Typed values or format strings reaching the sink:** Phase 0's gate turned typed hooks down. Reading the text back keeps core as it is.
- **Columns' formats from the generator** (metadata on `CsvRecordWriter<T>`): architecture decision 5 keeps the generator as it is in v1. Reflection by header name finds them instead.

If building turns up a need, it is flagged first and gated, as Phase 2's were.

#### Decisions

Each comes with a recommendation.

1. **The entry points.** Recommended: `ExcelWorkbookWriter.ToFile` and `Create` (named as `CsvWriter.Create` and `ExcelReader.Create`, rather than the sketch's `ToStream`), `WriteSheet<T>` and `WriteSheetAsync<T>`, `AddSheet` and `AddSheet<T>`. No one-call writer like `ExcelReader`: writing a workbook is already two lines. Alternatives: an `ExcelWriter.ToFile(path)` that returns the one sheet's `CsvWriter`; `ToStream`.
2. **Sheets at a time.** Recommended: one, as `ZipArchive` writes one entry at a time; adding a sheet while another is open throws. Alternative: keep each open sheet in a temporary file and copy it in when it completes.
3. **The package's order.** Recommended: sheets as they are written, the other parts at the end, `[Content_Types].xml` last, which Excel and LibreOffice open; no theme, document properties, views or `dimension`. Alternative: Excel's order, which needs every sheet held in a temporary file until the end.
4. **A workbook without sheets.** Recommended: `Dispose` writes one empty sheet, `Sheet1`. Alternative: throw from `Dispose`.
5. **Sheet names.** Recommended: Excel's rules for files, as checked, but stricter where its tabs are (no control characters) and where Unicode's compatibility forms are (one key for all targets). `History` is allowed, since Excel opens it. Alternatives: Excel's rules exactly, control characters included; also refuse `History`, as Excel's own tab does.
6. **The font.** Recommended: Calibri 11, which every Excel since 2007 has and which LibreOffice replaces with Carlito, of the same widths, so that the widths fit everywhere. Alternative: Aptos Narrow 11, Excel 365's default since 2023, which older Excel replaces with a font of other widths.
7. **Strings.** Recommended: `Inline` by default, as the sketch has it: nothing is kept between rows. `Shared` makes smaller files at the cost of memory. Alternative: `Shared` by default, as Excel writes them.
8. **Numbers' other text.** Recommended: the default invariant text copied as it is; any other text read strictly with the writer's provider, with leading zeros and grouping kept as text. Alternative: every text but the default as text.
9. **`NaN` and the infinities.** Recommended: text cells, since Excel can't open them as numbers, and CsvHelper.Excel reads them back. Alternative: `#NUM!` error cells.
10. **Long numbers.** Recommended: as the sketch has it: more than 15 significant digits in an integer or `decimal` follows `LongNumbers`, `Text` by default; doubles are always numbers.
11. **Dates.** Recommended: serials of the clock time, rounded to the millisecond, each in an ISO-like format by what it holds (a date, seconds, milliseconds). Alternatives: Excel's built-in short date and time (14 and 22), which show in the reader's own locale; one format per column, from its first value.
12. **Out-of-range dates.** Recommended: dates before 1900-01-01 and negative durations both follow `OutOfRangeDates`: `Text` by default, or `Throw`. Alternative: the sketch's two choices for dates alone, and negative durations as numbers Excel shows as `########`.
13. **`DateTimeOffset`.** Recommended: `Text` (the default), `Utc` or `ClockTime`, the time at its own offset. Alternative: the sketch's `Local`, which in .NET means the machine's time zone, not the value's.
14. **Widths.** Recommended: the first data record's typed cells set their columns' widths, so that dates don't show as `########`. Alternative: no widths until Phase 5's auto-fit.
15. **Known formats.** Recommended: from `[CsvFormat]` and `[CsvCulture]` by reflection, for `WriteSheet<T>` and `AddSheet<T>`, matched to columns by header name; a formatted value is typed only when it formats back to its own text. Alternatives: never typed (formatted values as text, as the sketch's fallback); formats from the generator, a core change.
16. **What is translated.** Recommended: the standard and custom numeric formats and date formats Excel can show the same, with a locale tag for names in other cultures; `TimeSpan`'s default only; the rest text. Alternative: numbers only, with formatted dates as text. (At step 4, dates and times only: `[CsvFormat]` doesn't apply to numbers, and the user left numbers' formats out of Phase 4 on 2026-11-07.)
17. **Formulas.** Recommended: no cached value, since Excel and LibreOffice compute formulas on open; a check of what Excel refuses (length, brackets, quotes, an operator at the end); `_xlfn.` prefixes as given. Alternatives: cached values, handed to the sink beside the text (a thread-static set while the value formats); a table of the functions that need the prefix, added by the writer.
18. **Cells past 32,767 characters.** Recommended: throw, since Excel opens no such file. Alternative: an option to cut them to 32,767.
19. **Numbers stored as text.** Recommended: `ignoredErrors` over the written cells when a text cell starts like a number. Alternatives: never; always.
20. **`RowLimit.NewSheet`.** Recommended: `Orders (2)`, `Orders (3)` and so on, as Excel names copies, shortened to 31 code units, with the header and widths repeated.
21. **What waits for Phase 5.** Recommended: a `Uri` is its text, the header is plain, and there is no `ExcelSheetOptions` yet. Alternative: links in Phase 4, as the sketch had them.
22. **Compression.** Recommended: `Fastest` by default, as Excel's own files are deflated; the benchmark measures `Optimal` beside it. Alternative: `Optimal`, `ZipArchive`'s default.
23. **Rows and cells.** Recommended: `r` on each, as Excel writes them; no `dimension` and no `spans`, which a stream can't know. Alternative: no `r`, which Excel and LibreOffice open, to save bytes (Phase 9, which the parallel writer needs anyway).
24. **CSV settings.** Recommended: those that don't apply are ignored, as Phase 1's reader ignores them. Alternative: reject them with `ArgumentException`.
25. **Sheet names' escapes on read.** Recommended: decode them, as Excel does, so that names round-trip.
26. **The corpus.** Recommended: a `written` content set, with Excel's answers pinned to each workbook's SHA-256, and Excel's `Text` as the translation's oracle. Alternative: Excel checked by hand at the end of the phase.
27. **LibreOffice in CI.** Recommended: as the plan's "Testing" says, CI installs LibreOffice for the written workbooks' test, a few minutes per run. Alternative: keep it a local test, skipped without `soffice`.

#### Risks

- **Synchronous I/O before .NET 10:** closing an entry and the package writes synchronously, so a stream that forbids synchronous I/O, such as ASP.NET Core's response body, throws. The docs say to write to a file or a buffer there; Phase 9's own zip writer removes it.
- **Zip64** for a sheet past 4 GB on a stream that can't seek: `ZipArchive`'s support differs between targets. A test of a 4 GB sheet runs on demand.
- **A format for the wrong column,** when a header name matches another member's (a converter's or a map's): the value reads back by another format, and is kept only if it formats to the same text. Otherwise it is text.
- **The translation's fidelity:** Excel shows some formats differently from .NET (its own locale's separators, its names). The oracle lists each difference.
- **Formulas Excel can't parse,** past what the check catches, make a file it can't open.
- **Shared strings' memory** with `Strings = Shared`.
- **Widths** are measured for one font, at 100% zoom.
- **Reflection under trimming:** `T`'s members are annotated. A nested record's members are kept by the generated record writer that reads them, and reading their attributes is suppressed with that reason; the build's trim analysis checks it.
- **Speed** is unmeasured until step 7.

#### Order of work

Each step is reviewable on its own and gets committed when you ask.

1. **Package and text (M):**
   - `ExcelWorkbookWriter`, `ExcelWriterOptions` (`Csv`, `Strings`, `CompressionLevel`), `PackageWriter` and `PackageParts`.
   - `SheetSink` with text cells, `XmlOutput`'s escaping, the 32,767 limit, `SheetNames` and the reader's decoding of names; one sheet at a time, the empty workbook, async.
   - Their tests, round trips through CsvHelper.Excel's reader, `OpenXmlValidator`, ClosedXML and ExcelDataReader.
2. **Typed cells (M):** numbers, booleans, `LongNumbers`, `NaN`, dates, times and durations, `DateTimeOffsets`, `OutOfRangeDates`, `StyleRegistry`, `ColumnWidths` (with Calibri 11's character widths measured through COM), and `numberStoredAsText`.
3. **Formulas and limits (S):** `ExcelFormula`, its parsing and converter, `WriteFormula`, `FormulaCheck`, the row and column limits, and `RowLimit.NewSheet`.
4. **Formats (M):** `FormatTranslator`, `FormatCheck`, `MemberFormats`, `HeaderCapture` and `AddSheet<T>`.
5. **Corpus and oracles (M):** the `written` content set, the corpus tool's `written` command and Excel's answers, the translation's oracle, and the LibreOffice test with its CI step.
6. **Security and fuzzing (S).**
7. **Benchmarks (M):** `ExcelPackageAssetsWrite` and `ExcelOrdersWrite`; a run.
8. **"As built" (S).**

#### Gate

- Every way of writing works on net10.0, net8.0 and net48, sync and async, with errors naming the right cell: `WriteSheet<T>` from `IEnumerable<T>` and `IAsyncEnumerable<T>`; `AddSheet`'s and `AddSheet<T>`'s `CsvWriter` with `WriteRecords`, `WriteRecord` and `WriteField`; dynamic records; several sheets.
- Every file the tests write passes `OpenXmlValidator`.
- Every workbook of the `written` set opens in Excel and LibreOffice without a repair prompt. Excel's answers agree with what was written: values and types, and formatted cells' text with .NET's, each difference documented.
- Written files read back the same through CsvHelper.Excel's reader, ClosedXML and ExcelDataReader, each difference documented.
- The security and fuzz tests pass.
- Nothing in core changes; if something must, it is flagged, approved and passes Phase 0's gate.
- The write benchmark's numbers are recorded.
- CI's pack check still passes, and so does its LibreOffice test.

#### Changes elsewhere

Applied at approval, 2026-11-06:

- **Status** (top of the plan): Phase 4's design approved.
- **API sketch:**
  - `ExcelWorkbookWriter.ToFile` and `Create` (for the sketch's `ToStream`), and `AddSheet<T>`; `ExcelSheetOptions` and `AddSheet(name, options)` come with Phase 5.
  - `ExcelWriterOptions` gains `OutOfRangeDates`; `DateTimeOffsets` is `Text`, `Utc` or `ClockTime`; `CompressionLevel` is `Fastest` by default.
  - `ExcelFormula` holds no cached value.
- **Phase 5:**
  - Hyperlinks from `Uri` members (Phase 4 writes their text).
  - `[ExcelColumn(Format)]` and `ExcelSheetOptions.Columns` as sources of formats, checked by Phase 4's `FormatCheck`.
  - Auto-fit widens Phase 4's widths of typed columns to every column, over the first N rows.
  - The bold header.
- **Phase 9:** "Our own zip writer" also removes the synchronous writes before .NET 10.
- **Feature coverage:** "Number format and width per column" is 4 (format translation and typed columns' widths) and 5; "Writing formulas" is 4, without cached values.
- **Testing:** Excel's answers for written workbooks (the `written` content set), and LibreOffice in CI.
- **Defaults:** strings inline; Calibri 11; `CompressionLevel.Fastest`; dates in ISO-like formats by what they hold; formulas without cached values; long numbers, and dates before 1900, as text; `History` allowed as a sheet name.
- **Risks:** "Zip edge cases" gains synchronous I/O before .NET 10.

#### As built

Filled in step by step; the code is the reference.

**Summary (step 8, 2026-11-07).** Phase 4 is complete. This summary gathers what the steps below record one by one. Every gate item is met, each re-checked on 2026-11-07 (see "The gate").

- **What it delivers:**
  - **Writing workbooks through plain `CsvWriter`s.** `ExcelWorkbookWriter.ToFile` and `Create` start a workbook. `WriteSheet<T>` and the two `WriteSheetAsync<T>` write a sheet of records whole; `AddSheet` and `AddSheet<T>` give a sheet's `CsvWriter`, for records, fields by hand, dynamic records and formulas. Sheets are written one at a time, streamed into the package, to any stream that writes, one that can't seek too. `SheetNames` lists them in tab order, and `Dispose` writes the rest of the package; a workbook without sheets gets `Sheet1`.
  - **Cells:**
    - text, inline (the default) or shared, escaped as Excel writes it, up to 32,767 code units, invalid UTF-8 as U+FFFD, and an empty field as no cell;
    - numbers from their invariant text or the writer's culture's, booleans, and dates, times and durations as serials in ISO-like formats by what they hold, with `LongNumbers`, `DateTimeOffsets` and `OutOfRangeDates`;
    - formulas without cached values (`ExcelFormula`, `WriteFormula`), checked for what makes Excel refuse a workbook;
    - dates and times in their members' `[CsvFormat]` and `[CsvCulture]`, translated to Excel's codes when Excel shows the same text and the text reads back, and text otherwise;
    - widths for the first data record's dates and times (for 96 DPI, from Calibri 11's measured widths), and `ignoredErrors` for numbers stored as text.
  - **Limits:** Excel's columns and rows. Past the last row, `RowLimit` throws, or goes on in a new sheet named as Excel names copies, with the header and widths repeated.
  - **Public API:** `ExcelWorkbookWriter`; `ExcelWriterOptions` (`Csv`, `Strings`, `CompressionLevel`, `RowLimit`, `LongNumbers`, `DateTimeOffsets`, `OutOfRangeDates`); the enums `ExcelStrings`, `ExcelRowLimit`, `ExcelLongNumbers`, `ExcelDateTimeOffsets` and `ExcelOutOfRangeDates`; `ExcelFormula` and `ExcelFormulaConverter`; and `ExcelWriterExtensions.WriteFormula`. The reader decodes `_xHHHH_` in sheet names (decision 25).
  - **Internals,** in `Internal/Writing/`:
    - the package (`PackageWriter`, `PackageParts`), the sink (`SheetSink`), XML (`XmlOutput`) and shared strings (`SharedStringTable`);
    - names (`SheetNames`, and `InvariantUpper` for .NET Framework's missing mappings);
    - typed cells (`CellKind`, `CellKinds`, `DotNetText`, `NumberCulture`, `Serial`, `StyleRegistry`, `ColumnWidths`) and formulas (`FormulaCheck`);
    - formats (`FormatTranslator`, `DateFormat`, `FormatCheck`, `MemberFormats`, `ColumnFormat`, `HeaderCapture`);
    - and `Polyfills/TrimmingAttributes.cs` for the netstandard and .NET Framework builds.
  - **Core:** no change.
  - **Size:** CsvHelper.Excel has 19,397 lines of library code (12,855 at Phase 3's end), and 27,229 of tests (17,893), step 8's Zip64 test among them. The corpus tool's `written` set and command are 1,831 lines, and the write benchmarks 601.
  - **Tests:**
    - CsvHelper.Excel.Tests: 2,718 on net10.0 and net8.0, and 2,670 on net48 (1,791 and 1,763 at Phase 3's end), besides step 8's Zip64 theory, which is skipped unless asked for.
    - CsvHelper.Tests (1,309, 1,304 and 844) and CsvHelper.Linq.Tests (112, 110 and 101) are unchanged.
    - Mutation checks: 418 bugs planted over steps 1 to 6, one at a time. Every one is caught, after the tests each step added, but three that are equivalent (step 4's `Color0`, step 5's two).
  - **Corpus:** the `written` set, ten workbooks CsvHelper.Excel writes (`Written.cs`, which the tests link), with Excel's answers about each (`Files/excel-written`), each pinned by its parts' SHA-256. The corpus tool's `written` command asks Excel.
  - **CI:** LibreOffice 26.8.2.2 is installed for the LibreOffice tests (decision 27), the tests' TRX files are kept, and Phase 3's reader fuzzer runs alone.
  - **Benchmarks:** `ExcelPackageAssetsWrite` and `ExcelOrdersWrite`.
- **The gate**, item by item, re-checked on 2026-11-07:
  - Every way of writing works on net10.0, net8.0 and net48, sync and async, with errors naming the right cell: met (steps 1 to 4).
    - `WorkbookWriterTests`: `WriteSheet<T>`, and `WriteSheetAsync<T>` from an `IEnumerable<T>` and an `IAsyncEnumerable<T>`; `AddSheet`'s writer with `WriteRecords`, `WriteRecord` and fields by hand; dynamic records; and several sheets in tab order, each sync and async.
    - `FormattedCellsTests`: `AddSheet<T>`'s writer with `WriteRecords`, and with fields by hand under the header's names. `TypedCellsTests`, `FormulaCellsTests` and `RowLimitTests` take the same paths with their cells.
    - The messages name the cell ("Cell Notes!B3 has 32,768 characters…", "Cell Orders!D3 holds 1234567890123456789…", "Cell Data!B2 holds 31.12.1899…"), and the row limit's names its sheet.
    - Not every combination is tested: `AddSheet<T>` with `WriteRecord` alone isn't, and it goes through the sink as `WriteRecords` does.
  - Every file the tests write passes `OpenXmlValidator`: met, checked whole.
    - Each writing test class validates its own workbooks (`Written.ValidationErrors`), and step 6's fuzzers validate each of theirs. The `written` set's tests don't validate its workbooks.
    - So on 2026-11-07 a scratch build, its `PackageWriter` changed to keep a copy of each package, ran CsvHelper.Excel.Tests on net10.0 and saved every workbook the tests wrote: 1,507, the `written` set's among them. Two more went to files through `ToFile` and weren't kept.
    - All 1,507 pass the validator, run as `Written.ValidationErrors` runs it, without an error. Under that build, `WriterSecurityTests`' two allocation tests fail, since the copy allocates; they pass in the suite. The patch, the validator and a README are in `Csv\_bench\results\phase4\step8`.
  - Every workbook of the `written` set opens in Excel and LibreOffice without a repair prompt, and Excel's answers agree with what was written, each difference documented: met (step 5).
    - Excel opened all ten as they are; `AnswersForEveryWorkbook` checks that none was refused or repaired.
    - `ExcelReadsWhatWasWritten` compares every cell's value, type, shown text and format, and lists each difference with its reason.
    - LibreOffice converts all ten, here and on CI, and `LibreOfficeShowsWhatExcelShows` lists its differences.
  - Written files read back the same through CsvHelper.Excel's reader, ClosedXML and ExcelDataReader, each difference documented: met (steps 1 to 4). `Written.cs`'s helpers read with all three, and the tests list the differences. ClosedXML doesn't decode an inline string's escapes and reads its LF as CR LF. Neither decodes sheet names' escapes. ExcelDataReader reads formula cells as empty, since they have no cached value; ClosedXML computes them.
  - The security and fuzz tests pass: met (step 6, and again in the run below and in CI's run 42421201910).
  - Nothing in core changes: met. `git diff f382244b` (Phase 3's end) is empty over `src/CsvHelper`, `src/CsvHelper.Linq`, `src/CsvHelper.Generators`, `tests/CsvHelper.Tests` and `tests/CsvHelper.Linq.Tests`, and no commit since touches them. So no core gate was needed.
  - The write benchmark's numbers are recorded: met (step 7).
  - CI's pack check still passes, and so does its LibreOffice test: met.
    - CI's run 42421201910, of step 6's commit, passed both.
    - At step 7's commit (`ea46b409`, not pushed), the steps ran as CI runs them on 2026-11-07: restore; a full Release build (`--no-incremental`), with 0 warnings and 0 errors; every test, with TRX files, all passing in the counts above, the LibreOffice tests among them; and the pack check, into a scratch folder. CsvHelper is `[0.0.0-ci]` in all 5 targets, and net47 has System.IO.Compression. The script and its logs are in `Csv\_bench\results\phase4\step8`.
- **The Zip64 test that "Risks" promised** (`Zip64Tests`, added at step 8): it wasn't built at step 1 or after, and step 8 found it missing.
  - It writes a sheet of a million rows of four 1,100-character cells, 4,617,444,904 bytes of XML (4.30 GiB), through `ToFile` (a stream that seeks) and to a stream that can't seek, each sync and async. Then `ZipArchive` must open the file and inflate the sheet's entry to its stated length, past 4 GiB, and CsvHelper.Excel must read the million rows back, some compared whole.
  - It runs on demand, with `CSVHELPER_EXCEL_ZIP64=1` (`Zip64TheoryAttribute`), and is skipped otherwise, so the suite and CI don't pay for it.
  - Run 2026-11-07, all four cases passed on every target. The sheet deflates to 99.6 MB on .NET 10, 70.0 MB on .NET 8 and 72.2 MB on .NET Framework (the stream that can't seek adds 84 bytes of data descriptors). Writing took 3.6–3.8 s, 4.9–5.0 s and 25–28 s, and reading back 1.0–1.7 s, 1.1–1.7 s and 3.6–3.8 s. The log is `zip64.log` in `Csv\_bench\results\phase4\step8`.
  - So the risk is answered for these targets: `ZipArchive` writes Zip64 sizes for a sheet past 4 GiB, to a stream that seeks or not, sync or async.
- **What changed from the design, and why** (each step's "As built" has the details):
  - **Numbers' formats aren't translated** (step 4, the user's decision). `[CsvFormat]` doesn't apply to numbers in CsvHelper, so no record writer writes a number in a format the sink could know. A number member's `[CsvCulture]` is read instead. Translating them would need a core change.
  - **A formatted value can be a typed cell** (decision 15 as built, step 4): it is read back with its member's format, and kept only when it formats to the same text, so that Excel shows what .NET writes.
  - **Widths for 96 DPI, from a measured table** (step 2), rather than the design's AutoFit widths, which were measured at this machine's 144 DPI and are 4.5% too narrow at 96. Widths that fit at 96 DPI fit at higher DPIs too.
  - **Formulas** (steps 3 and 6):
    - the checks also refuse an operator at the start, and control characters outside strings, tabs included;
    - line breaks outside strings become LF, since Excel refuses a CR between tokens;
    - an empty formula is refused, and `Parse("")` is `default`;
    - white space at either end is kept, with `xml:space="preserve"` on `f`.
  - **Sheet names** (steps 1, 3 and 6): Excel's comparison is wider than case (normalization, compatibility forms, and Unicode's caseless match, done twice); `History` is allowed; lone surrogates are refused; copies are named as Excel names them; and a name's 31 code units are counted as written too, so that every file passes the validator.
  - **Messages drop "The record is discarded"** (step 1): core discards a record in the record methods, but not for a field written by hand.
  - **Step 1 took the column limit** from step 3, and the benchmarks' `ExcelStrings` from step 7.
  - **The corpus** (step 5):
    - each workbook is pinned by its parts' SHA-256, since deflate's output differs between runtimes;
    - `cultures.xlsx` carries a fingerprint of the culture data it reads, and is compared only where the data matches, as on CI;
    - Excel's widths are checked at the answers' DPI.
  - **CI** (step 5's first run): Phase 3's reader fuzzer runs alone, since its time limit measured the tests beside it on CI's four cores; the tests' TRX files are kept.
  - **Four bugs the fuzzers found** (step 6):
    - an escape's start before an escaped character, which Excel misread too;
    - names with U+FFFE, which `string.Normalize` refuses;
    - `I` U+0307 beside `i` U+0307, which Excel takes to be one name;
    - formats .NET can't format.
  - **The Zip64 test** that "Risks" promised was missing until step 8.
  - **Smaller decisions**, each in its step:
    - a null `Csv` throws `ArgumentException`;
    - U+007F to U+009F are written as they are;
    - .NET Framework's zip gets a stream that counts what it writes;
    - `ToFile`'s stream has a 64 KiB buffer;
    - built-in format ids where Excel saves them;
    - English names tagged `[$-409]`, and `t` as `A/P`;
    - genitive months, dates without a year and adjacent tokens aren't translated;
    - widths over 84 sample dates.
- **What Excel's COM checks taught** (each step's "Checked through COM" has every case):
  - **Text:**
    - Excel decodes `_xHHHH_` in text, names and formulas, with hex digits of either case.
    - It misreads `_x0041` before an escaped character, as the writer wrote it until step 6.
    - It reads U+007F to U+009F as they are.
  - **Sheet names:**
    - Excel counts a name decoded: it opens one of 31 characters decoded, 43 as written.
    - Its comparison is wider than case.
    - It opens `History`, and names with U+FFFE, U+FFFF and U+FDD0.
    - It refuses a file with `I` U+0307 beside `i` U+0307, or `İ` beside `i` U+0307, and opens one with `i` beside `İ`.
  - **Numbers and dates:**
    - A subnormal double reads as 0, and the 1900 system has its 1900-02-29.
    - A later value wider than its column's first shows `########`.
    - Numbers in General at the default width show in scientific notation, never `########`.
  - **Formulas:**
    - Excel refuses unclosed brackets, strings and quoted names; operators at either end; a CR, a tab or a control character between tokens; and more than 8,192 code units.
    - It opens `=A1` as `A1` and 256-character strings, and shows `#REF!` for a missing table.
    - `LEN` counts an emoji as one character.
  - **Formats:**
    - The 83 codes Excel opens and the 32 it refuses became `FormatCheck`.
    - It reads `m` as minutes after `h` across quoted text.
    - It shows a tagged `A/P` as the whole designator, months in the nominative, ja-JP's `mmm` as a number, and de-DE's March as `Mrz`.
  - **Widths:** `ColumnWidth` depends on the screen's DPI. Calibri 11's digit is 7 pixels at 96 DPI and 11 at 144.
  - **LibreOffice** differs where listed:
    - serials before 61 are a day earlier;
    - CR LF reads as LF;
    - formulas' escapes aren't decoded;
    - `TimeSpan.MaxValue` shows as `#FMT`, and `A/P` in lower case.
- **The numbers** (step 7):
  - At its defaults CsvHelper.Excel writes a million rows of the package assets in 1.28 s, and of the orders in 1.50 s. That is 2.05x and 1.56x SpreadCheetah 1.28.0's time, about LargeXlsx's, and faster than MiniExcel's, allocating 7.3 KB a workbook.
  - Deflate is about half of the time. The cells' `r` attributes nearly triple the package assets' file at `Fastest`: 139.2 MB, against 47.7 MB without them.
  - `Optimal` costs 3.4x the time for 57% of the size.
- **Left open:**
  - **.NET's UTF-8 formatting of an escaped non-ASCII literal** (step 6): core's CSV writer writes `yyyy\年` as `2024t`. A workaround is a core change behind Phase 0's gate. Step 6's review left it open; on 2026-11-07 the user chose the recommendation: a core workaround after Phase 4, before Phase 5 (Phase 5's first item), without a report to dotnet/runtime.
  - **Phase 5:**
    - links from `Uri`;
    - Excel settings and `[ExcelColumn(Format)]` as sources of formats, numbers' among them;
    - auto-fit over the first N rows, which ends the `########` of a later value wider than its column's first;
    - the bold header;
    - `FormatCheck`'s `[DBNum3]` and backtick.
  - **Phase 9** (its "Phase 4's write baseline"):
    - dropping the `r` attributes (decision 23's alternative);
    - the XML's own cost, about 2.4–2.5x SpreadCheetah's;
    - our own zip writer, which removes the synchronous writes before .NET 10.

**Step 1, package and text** (built and approved 2026-11-06, committed with step 2):

- **New files:**
  - Public: `ExcelWorkbookWriter.cs` (`ToFile`, `Create`, `Options`, `SheetNames`, `WriteSheet<T>`, the two `WriteSheetAsync<T>`, `AddSheet`, `Dispose`, `DisposeAsync`), `ExcelWriterOptions.cs` (`Default`, `Csv`, `Strings`, `CompressionLevel`) and `ExcelStrings.cs`.
  - `Internal/Writing/`:
    - `PackageWriter.cs`: the `ZipArchive` in create mode, an entry per part, dated 1980-01-01, opened and closed sync or async (`OpenAsync` and `DisposeAsync` on .NET 10);
    - `PackageParts.cs`: the declaration, the parts after the sheets (shared strings streamed in 64 KB pieces), and the sheets' part names;
    - `SheetSink.cs`: the `CsvCellSink`, with text cells, the 32,767 limit and the column limit;
    - `XmlOutput.cs`: the pooled buffer, text escaping (`AppendText`), UTF-8 checks and lengths;
    - `SharedStringTable.cs`: an arena and an open-addressing table keyed by each string's escaped XML;
    - `SheetNames.cs`: the checks and the key;
    - `InvariantUpper.cs` (`!NET` only): see below.
- **Changed:**
  - `WorkbookPart.ReadSheet` decodes `_xHHHH_` in sheet names after their XML (decision 25).
  - The benchmarks' own `ExcelStrings` enum is gone and `ExcelTextWorkbook` and `ExcelDataGenerator` use the library's. This was step 7's plan, but it had to come now: in the tests' `CsvHelper.Excel.Tests.Bench` namespace, `ExcelStrings` now resolves to the library's. The generator's cache key changes with its source, so the cached benchmark workbooks are written again on the next run.
- **Decisions made while building:**
  - **The record methods discard a failed record, and the sink's messages don't say they do.**
    - Core discards the record when `WriteRecords`, `WriteRecord` or a dynamic record fails, but not when a field written by hand throws: the record stays open, as for CSV.
    - So the messages end without "The record is discarded" (for example "Cell Notes!B3 has 32,768 characters, and Excel opens no cell of more than 32,767.").
    - A field that throws leaves no cell. Its column still counts, as the writer's field count does.
  - **The column limit came with this step,** not step 3, since a field past `XFD` would otherwise get a reference Excel can't read: "Record 1 has a field in column XFE, past XFD, Excel's last column." The row limit and `NewSheet` stay with step 3.
  - **A null `Csv` throws `ArgumentException`,** as the reader's options do, rather than the design's `ArgumentNullException`. `ExcelWriterOptions.Validate` also calls `CsvWriterOptions.Validate`, so a bad CSV option fails at `ToFile` or `Create` rather than at the first sheet. `CompressionLevel` is checked against the values of the target (`SmallestSize` exists on .NET only).
  - **The enums are in files of their own,** as the reader's are, not in `ExcelWriterOptions.cs`.
  - **Sheet names can't have lone surrogates** (a rule the design didn't list), since a name is written as UTF-8.
  - **U+007F to U+009F are written as they are in text:** XML allows them, and Excel reads them (checked through COM: `a`, U+007F, U+0080, U+0085 or U+009F, `b`). Only the C0 controls but tab and LF are escapes. Names still refuse them.
  - **.NET Framework:**
    - Its `ToUpperInvariant` lacks 216 of .NET's mappings, among them final sigma to capital sigma, µ, ſ and Georgian Mtavruli. They were found by comparing the two over every UTF-16 code unit, in Windows PowerShell 5.1 (.NET Framework 4.8.1) and pwsh 7 (.NET 10.0.14). `InvariantUpper` adds them on the netstandard and .NET Framework builds, so the key is the same on every target and `σας` and `ΣΑΣ` are one name there too.
    - Its `ZipArchive` reads the position of a stream that can't seek, which such streams can't tell. `PackageWriter` wraps one in a stream that counts what it writes (`CountingStream`), as .NET's zip does itself.
    - Its UTF-8 decoder replaces some invalid sequences with fewer U+FFFDs: `ED A0 80` gives two, where .NET gives three. Invalid UTF-8 becomes U+FFFD as the runtime decodes it, and the test expects each runtime's own result.
  - **`ToFile`'s stream has a 64 KiB buffer,** where `CsvWriter.ToFile` has none, since the zip writes its headers in small pieces.
  - **The parts are written as designed:**
    - `<sheetData>` is written even when empty, and Excel's default `pageMargins` (0.7, 0.75, 0.3) follow it.
    - The styles are Excel's minimal style sheet in Calibri 11, without a theme's color or scheme.
    - The shared strings' `count` is the cells of the records kept. Strings that a discarded record added stay in the table.
- **Checked through COM and LibreOffice** (probes written with the built library, in the session's scratchpad):
  - Excel opens a workbook of the escape-heavy texts (in two columns of a sheet named `Text`, inline and shared), six sheets named `Q&A <1>`, `"Quoted"`, `a_x0041_b`, `History`, 日本 and ` spaced `, and an empty workbook. It reads every cell and name as written: CR, CR LF, the controls, `_x0041_` and its near misses, U+FFFE and U+FFFF, the emoji and the 32,767-character cell. `a_x0041_b` is written `a_x005F_x0041_b`, which Excel decodes. Excel flags `00123` and `1.5` as numbers stored as text, which step 2's `ignoredErrors` hides.
  - LibreOffice opens all three. It reads each cell as written but CR LF, which it gives as LF. It stops exporting at `Q&A <1>`, whose CSV file name Windows refuses.
- **The oracles' differences** (listed in the tests):
  - ClosedXML reads an inline string without decoding its escapes, and with LF as CR LF; it reads shared strings as written.
  - Neither ClosedXML nor ExcelDataReader decodes the escapes of sheet names, which Excel does.
  - ExcelDataReader reads every text as written.
- **Tests** (193 new on net10.0 and net8.0, 192 on net48: 1,984 on net10.0 and net8.0 and 1,955 on net48, all passing), in `tests/CsvHelper.Excel.Tests/Writing` with `Written.cs`'s helpers (write sync or async, read back with CsvHelper.Excel, the validator, ClosedXML and ExcelDataReader):
  - **`XmlOutputTests`:**
    - each escape as Excel writes it, read back with the reader's own decoding;
    - every C0 control;
    - U+007F to U+009F as they are;
    - `"` in attributes;
    - 20,000 random texts of underscores, hex digits, controls and the rest, read back as written;
    - invalid UTF-8 of each kind;
    - 50,000 random byte strings against .NET's strict decoder, with the UTF-16 length of the valid ones;
    - `preserve`, and growth past the buffer.
  - **`SheetNamesTests`:** the names Excel opened and refused, with each message; lone surrogates; the pairs Excel takes to be the same, and those it tells apart; and the compatibility forms the key joins.
  - **`SharedStringTableTests`:** first-seen order, and 100,000 strings through the table's growth.
  - **`WorkbookWriterTests`**, sync and async, with inline and shared strings where they matter:
    - `WriteSheet<T>`, `WriteSheetAsync<T>` from both sequences, `AddSheet` with `WriteRecords`, `WriteRecord` and fields by hand, dynamic records, `HasHeader` off, sheets in tab order with `SheetNames`;
    - the tricky texts read back by all three readers, the XML of the escapes, invalid UTF-8;
    - 32,767 code units with an emoji at the end, and one past it: the message, the record discarded, and the workbook still complete;
    - the column limit, gaps for records without a value, a 20,000-row sheet, a record flushed in the middle and kept, one flushed in the middle and discarded, one discarded for its field count;
    - the empty workbook's `Sheet1`, an open sheet completed by `Dispose` (and `ObjectDisposedException` after), the parts' order and dates, the same bytes twice, each compression level;
    - names that need escapes read back, a stream that can't seek, `ToFile` (overwriting, readable while written, closed after), `leaveOpen`;
    - the errors: an open sheet, names Excel takes to be the same, after `Dispose`, comments, a type without a record writer (its sheet still completed), and the options' checks.
  - **`WorkbookPartTests.DecodesEscapesInSheetNames`:** `a_x0041_b`, `_x005F_x0041_`, `_X0041_`, `a_x0009_b`, `_x0041` and an escaped surrogate pair.
  - Every workbook these tests write passes `OpenXmlValidator`.
- **Mutation check** on net10.0, 34 bugs planted one at a time, each file restored and checked by SHA-256. 33 were caught by the first run. The other, "escape-like ignores the closing `_`", is caught after a test was added for it. It escaped `_x0041a` needlessly, which still reads back the same, so only the XML shows it.
  - XML text: `&amp;` 12, `&lt;` 7, `&quot;` 3, `_` never escaped 14, U+FFFE and U+FFFF 6, the escape's high digit 10, the closing `_` 2 after the test, lowercase hex 5, LF not preserved 5, CR written as it is 10, `RemoveFirst` keeping a byte 65, the UTF-16 length as the byte length 13, UTF-8 never invalid 18.
  - The sink: an empty field writing a cell 6, the cell limit off by one 4, the column limit off by one 2, the row not advanced 53, a discard keeping its cells 10, a flush writing a partial record 2 (the test added before the run), references not counted 2, the wrong shared index 14.
  - The table: matching by hash alone 3, rehashing to slot 0 1.
  - The workbook writer: an open sheet not refused 1, duplicates found by name 1, no empty sheet 4, the open sheet not cleared 16.
  - The parts: the styles' `rId` clashing 44, the sheet name not escaped as an attribute 2.
  - Names: a trailing apostrophe allowed 1, no normalization 4, C1 controls allowed 3.
  - The reader: names not decoded 4. Entries not dated: 4.
- **Core:** no change. The solution builds with 0 warnings on every target.
- **Review** (2026-11-06): `ValidateFieldCount` stays on by default, as for CSV, so a sheet written by hand with records of different lengths turns it off (the recommendation).

**Step 2, typed cells** (built, approved and committed 2026-11-06):

- **New files:**
  - Public: `ExcelLongNumbers.cs`, `ExcelDateTimeOffsets.cs` and `ExcelOutOfRangeDates.cs`, the three enums of the options this step adds.
  - `Internal/Writing/`:
    - `CellKind.cs` and `CellKinds.cs`: the design's `CellValue.cs`, renamed, since the tests already have a `CellValue` type, which `CsvHelper.Excel.CellValue` would have hidden in their namespace. `CellKinds.Of` maps a type, or a nullable of it, to its kind, by reference, and by name on the netstandard and .NET Framework builds for `DateOnly`, `TimeOnly`, `Half`, `Int128` and `UInt128`;
    - `DotNetText.cs`: `"O"` dates and times, `"c"` durations and numbers, read by position;
    - `NumberCulture.cs`: a culture's signs and decimal separator as UTF-8, to read its numbers back as invariant text;
    - `Serial.cs`: dates, times and durations to serials, through whole milliseconds;
    - `StyleRegistry.cs`: format codes to cell formats, with the writer's own seven formats (`FixedFormat`);
    - `ColumnWidths.cs`: widths from text, with Calibri 11's table.
- **Changed:**
  - `ExcelWriterOptions` gains `LongNumbers`, `DateTimeOffsets` and `OutOfRangeDates` (each `Text` by default), checked by `Validate`.
  - `SheetSink` writes typed cells, holds the records up to the first data record for `<cols>`, and writes `ignoredErrors`.
  - `PackageParts` writes the registry's `numFmts` and `cellXfs`; `XmlOutput` gains `AppendDouble` (core's `FloatFormatter`); `AddSheet`'s docs describe the typed cells.
- **What a field becomes,** as designed:
  - Integers, `decimal`, `double`, `float` and `Half` are number cells of their invariant text, copied into `<v>` (`0.10`, `1E-05`, `1.5E+20`, `-0`, `5E-324`). `NaN` and the infinities are text cells.
  - `bool` is `t="b"`, `1` or `0`. A sink's writer writes `True` and `False` whatever `TrueValue` and `FalseValue` say, so they don't apply.
  - `DateTime` is the serial of its clock time, in `yyyy-mm-dd`, `yyyy-mm-dd hh:mm:ss` or `yyyy-mm-dd hh:mm:ss.000` by what it holds. `DateOnly` is `yyyy-mm-dd`, `TimeOnly` `hh:mm:ss` or `hh:mm:ss.000`, and `TimeSpan` `[h]:mm:ss` or `[h]:mm:ss.000`.
  - A text in another layout, such as a format's (`yyyy-MM-dd`, `g`, `N2`), is a text cell.
- **Decisions made while building:**
  - **Widths are computed for 96 DPI, all from the table,** rather than the design's AutoFit widths for the default formats.
    - The design's numbers (`yyyy-mm-dd` 10.09, with seconds 17.82, with milliseconds 21.45) were measured on this machine, which runs at 144 DPI (150%). They are about 4.5% too narrow at 96 DPI.
    - A column's unit is a digit's width rounded to whole pixels: 7 at 96 DPI, where Calibri's digit is 7.5 pixels, and 11 at 144 DPI, where it is 11.25. So text takes more units at 96 DPI than at 120 or 144 DPI, and a width that fits at 96 DPI fits at those too. Only 144 DPI was checked.
    - The table was measured through COM: an auto-fit text cell of 10 copies and one of 30 copies of each of 140 characters (ASCII, the signs and letters of numbers and dates in common cultures, and Chinese, Japanese and Korean date characters). The difference between the two widths, in points, gives each character's width; two thirds of it is the width at 96 DPI.
    - Number cells in a format were measured too: a literal format showing `abcdefghij` or `WWWWWWWWWW` auto-fits to the same width as that text in a text cell, so the table holds for them.
    - A width is the text's pixels, rounded up, plus Excel's 5 pixels of padding, in 256ths of a 7-pixel digit, rounded up. A column gets one only when that is more than the default column's 61 pixels.
    - So `yyyy-mm-dd` is 10.71 (`10.71484375`), with seconds 18.71, with milliseconds 22.57, and `hh:mm:ss.000` 12.14. `hh:mm:ss`, and `[h]:mm:ss` under 100 hours, fit the default column.
    - Characters the table lacks are taken as wide as `W`, the widest letter. Chinese, Japanese and Korean characters are 15 pixels, as measured, and so is a surrogate pair.
  - **Widths come from the first data record only,** as designed, so a later value that needs more shows `########`. Checked through COM:
    - a `DateTime` column whose first value is a date at midnight (given the width in seconds) shows `########` for a later value with milliseconds;
    - a `TimeOnly` column whose first value is in whole seconds shows `########` for `23:59:59.999`;
    - so does `TimeSpan.MaxValue` under a shorter first duration.
    - Phase 5's auto-fit over the first N rows narrows this.
  - **Built-in ids:** a code that is one of Excel's built-in formats in every locale gets its id, as Excel saves it: 1 to 4, 9 to 13, 46 (`[h]:mm:ss`), 48 and 49. Other codes get ids from 164. So the plain duration is 46, which Phase 2's reader, ClosedXML and ExcelDataReader all take for a duration. Ids 14 to 22 show in the reader's locale, so a code like theirs stays custom.
  - **The writer's culture:**
    - A culture's number is read with its own signs and decimal separator, and only those: `-1234,5` (de-DE) is a number.
    - Invariant text written under such a culture stays text: `1.234` could be de-DE grouping, and `1234.5` from an invariant `[CsvCulture]` waits for step 4.
    - A culture whose signs and separator are the invariant ones (en-US) is read with the invariant grammar.
    - The signs may be longer than one character (sv-SE's U+2212, ar-SA's letter mark). Random doubles in six cultures, de-DE, fr-FR, sv-SE, ar-SA, fa-IR and en-US, read back to the value that culture's text parses to.
  - **Significant digits** run from the first digit that isn't 0 to the last one, so `12345678901234567890` has 19. `LongNumbers = Number` writes the number's text as it is, which Excel reads as the nearest double: `12345678901234567` reads as 12345678901234568, and Phase 2 reads 15 digits of it.
  - **The ends of the range:**
    - A `DateTime` within half a millisecond of 10000-01-01 is 9999-12-31 23:59:59.999, the last millisecond Excel shows.
    - A `TimeOnly` within half a millisecond of midnight is 23:59:59.999, since a time of day can't be 24:00.
    - A negative duration of less than half a millisecond is 0, and a date half a millisecond before 1900-01-01 rounds to it.
  - **Out of range,** with `Text`: the cell is the field's text as written, `1899-12-31T00:00:00.0000000` or `-01:00:00`. With `Throw`, the messages quote that text and drop "The record is discarded", as in step 1:
    - "Cell Old!A2 holds 1899-12-31T00:00:00.0000000, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw)."
    - "Cell Old!B2 holds -00:00:00.0005000, a negative duration, which Excel shows as ######## (ExcelWriterOptions.OutOfRangeDates is Throw)."
    - "Cell Orders!D3 holds 1234567890123456789, which has 19 significant digits, and Excel keeps 15 (ExcelWriterOptions.LongNumbers is Throw)."
  - **`DateTimeOffsets`:** `Utc` is the clock time less the offset, and `ClockTime` the clock time. Either reads back with offset 0.
  - **`numberStoredAsText`** is one `ignoredError` over `A1` to the last column and row of the records kept, when one of their text cells starts with a digit, a sign or a point. A discarded record's text counts for nothing. Excel's own check (`Errors(3)`) is ignored for every such cell.
  - **Column types** are cached per column, as designed. A column whose type changes from field to field (`WriteField(object)`) gets each field's kind.
- **Checked through COM** (Excel 365 at 144 DPI), with four probe workbooks written by the built library (`s2-typed` with inline and with shared strings, `s2-options` and `s2-clock`, in the session's scratchpad). Excel opens all four, and reads:
  - every typed cell as its value, in its format: `2024-01-15 10:30:00`, `2024-12-31 23:59:59.999`, `1900-01-01` (serial 1), `1900-02-28` (59), `1900-03-01 00:00:00.001`, `9999-12-31 23:59:59`, `23:59:59.999`, `36:00:00`, `24000:00:00.005`, `TRUE` and `FALSE`, `0.30000000000000004`, `1E-05`, `1.7976931348623157E+308`;
  - subnormal doubles as 0, as designed;
  - text where designed: `NaN`, `Infinity`, the long integer `12345678901234567`, `decimal.MaxValue`, `DateTimeOffset`s as text, dates before 1900 and the negative duration;
  - the `Utc` and `ClockTime` offsets as `2024-01-15 08:30:00` and `10:30:00`;
  - de-DE's `-1234,5` and `1,5E+20` as numbers, with its `N2` and `D4` texts as text.
  - Every first data record's value shows in its column. Numbers in General at the default width show in scientific notation (`1.23E+16`), never `########`.
- **LibreOffice 26.8** opens all four and reads the same values, but:
  - serials before 61 are a day earlier there, since its 1900 system has no 1900-02-29 (1900-01-01 shows as 1899-12-31);
  - it shows `#FMT` for `TimeSpan.MaxValue` in `[h]:mm:ss.000`;
  - it keeps subnormal doubles.
- **The oracles** read each typed cell as its type: ClosedXML as `Number`, `Boolean`, `DateTime` and `TimeSpan` (a duration), ExcelDataReader as `Double`, `Boolean`, `DateTime` and `TimeSpan`. Their only differences are step 1's.
- **Tests** (217 new on net10.0 and net8.0, 210 on net48: 2,201 on net10.0 and net8.0 and 2,165 on net48, all passing):
  - **`DotNetTextTests`:**
    - `"O"` texts of each `DateTimeKind` and of offsets from −14:00 to +14:00;
    - 20,000 random dates, times and offsets as .NET formats them;
    - 20,000 random durations, with `MinValue` and `MaxValue`;
    - texts in other layouts, refused;
    - the invariant grammar and its significant digits;
    - cultures' numbers, signs of several characters, what de-DE doesn't write, and 20,000 random doubles in six cultures.
  - **`SerialTests`:** Excel's serials of 1900's first days, the rounding, the ends of the range, and 20,000 random dates written and read back to the millisecond.
  - **`StyleRegistryTests`:** the order of first use, built-in and custom ids, the XML, and the writer's seven formats as Phase 2's `NumberFormats` classifies them.
  - **`ColumnWidthsTests`:**
    - the widths of the writer's formats;
    - about 5% wider than Excel's 144-DPI auto-fit;
    - the default column's edge;
    - characters the table lacks, wide characters and surrogate pairs.
  - **`TypedCellsTests`**, sync and async:
    - every type through the record writer, read back by `GetRecords`, dynamic records and `CsvDataReader`, and by ClosedXML and ExcelDataReader, with the cells' XML and the styles part;
    - .NET's newer types;
    - each choice of `LongNumbers`, `DateTimeOffsets` and `OutOfRangeDates`, and the messages;
    - `NaN`, the infinities, `-0` and subnormals;
    - de-DE's numbers and the texts of formats and other types;
    - a column's type changing;
    - the widths of each kind, from the record after the header (not from a header's typed cells), without a discarded record's;
    - records held until the widths are known;
    - `ignoredErrors` over the used range, but not for a discarded record;
    - `CellKinds` for every type;
    - the options' checks.
  - Every workbook these tests write passes `OpenXmlValidator`.
- **Mutation check** on net10.0: 57 bugs planted one at a time, each file restored and checked by SHA-256. 55 were caught by the first run. The other two are caught after a test was added for each:
  - "widths from every row" measured the header's cells too. The test that discarded the first data record cleared the header's widths with it, so a test with a typed header and no discard was added (`GivesTheHeadersCellsNoWidths`).
  - "a discard keeps the flag" left a discarded record's number-like text to be counted. The test ended after the discard, so no record committed it; it now writes one more record.
  - .NET's texts:
    - the offset's sign 3, `Z` refused 2, any day to 31 3, the time's fraction dropped 21;
    - the duration's days dropped 10, its sign dropped 4, past the range 1, hour 24 allowed 2;
    - leading zeros allowed 4, an exponent without digits 2, every digit significant 8, text past 128 bytes read 1.
  - The culture: the separator not translated 6, the exponent's minus not read 3, every culture taken as invariant 8.
  - Serials: no 1900-02-29 11, half rounded down 4, past the last millisecond 1, a time of 24:00 1, half a negative millisecond kept 2, 1899-12-31 allowed 5.
  - Styles: General by case 1, one index for every fixed format 18, custom ids clashing 3, no built-in 46 5, the `cellXfs` count off by one 4, codes not escaped 1.
  - Widths: pixels rounded down 9, the default's width written 1, 256ths rounded down 8, a surrogate pair counted twice 1, wide characters as wide as `W` 1.
  - Kinds: nullables not unwrapped 1, `decimal` as a double 2, `Half` as text 4.
  - The sink:
    - `False` as text 7, 15 digits taken as too long 8, doubles' digits counted 5;
    - UTC the wrong way 3, the offsets' text option 11;
    - seconds decided by the minute 1, a date's width for a date and time 5;
    - widths from every held row 1 after the test, the header always assumed 1, a discard keeping its widths 1, the start written before the widths 1;
    - a point not taken as starting a number 1, a discard keeping the flag 1 after the test;
    - the used range from the last record's column 6, or from row 1 6;
    - a column's first type cached for good 13, times always whole 2, negative durations as numbers 3, a duration's minutes not taken modulo 60 3;
    - `col` numbered from 0 7, out-of-range values never throwing 5.
  - The options: `OutOfRangeDates` unchecked 1.
- **Core:** no change. The solution builds with 0 warnings on every target.
- **Review** (2026-11-06), the recommendations:
  - The widths for 96 DPI, from the table, are accepted in place of the design's AutoFit widths.
  - Widths stay the first data record's, so a later value that needs more shows `########` until Phase 5's auto-fit, rather than every `DateTime` and `TimeOnly` column getting the width of milliseconds.

**Step 3, formulas and limits** (built 2026-11-06, approved and committed 2026-11-07):

- **New files:**
  - Public:
    - `ExcelFormula.cs`: the constructor, `Formula`, `Parse`, `TryParse`, equality and `ToString`. On .NET it is also `ISpanFormattable`, `IUtf8SpanFormattable` and `ISpanParsable<ExcelFormula>`, implemented explicitly, so the public API is the design's.
    - `ExcelFormulaConverter.cs`, `ExcelWriterExtensions.cs` (`WriteFormula`) and `ExcelRowLimit.cs`.
  - `Internal/Writing/FormulaCheck.cs`: the checks, and the line breaks outside a formula's strings.
- **Changed:**
  - `ExcelWriterOptions` gains `RowLimit` (`Throw` by default), checked by `Validate`.
  - `CellKind.Formula`, which `CellKinds.Of` gives `ExcelFormula` and its nullable.
  - `SheetSink` writes formula cells, applies the row limit and starts new sheets.
  - `ExcelWorkbookWriter` gains `ContinueSheet` and an internal `MaxRows`, and its docs describe formulas and the limits.
  - `SheetNames` gains `CopyStem` and `CopyName`.
- **Checked through COM before building** (Excel 365, build 20527), with `f` elements written by hand. The probes, their scripts and the answers are in `Csv\_bench\results\phase4\step3` (`make_probes.py`, `make_probes2.py`, `make_probes3.py`, `answers*.txt`, `copy_names.*`):
  - **Brackets:** Excel opens brackets of every kind inside strings and quoted sheet names (`LEN("(")`, `'a(b'!A1`, `INDEX({"a","}"},2)`). It refuses, as unopenable, a parenthesis, bracket or brace that isn't closed, one closed by another kind, and a closing one that closes nothing (`SUM(A1))`, `)A1(`, `SUM(A1]`, `SUM(1})`, `A1]`).
  - **Strings and names:** it refuses a string or a quoted sheet name that isn't closed. String literals of 256 characters open: the formula bar's limit of 255 isn't the file's.
  - **Operators at the end:** it refuses every binary operator there (`+ - * / ^ & = < > : ,` and `<>`), and `@` and `#` too (a spill reference is `_xlfn.ANCHORARRAY` in a file), also before white space (`A1+ `, `A1+` and LF, `A1+` and a tab). `A1%`, `SUM(1,)` and white space at the end open.
  - **Operators at the start:** it refuses `* / ^ & < > : , %` there, also after a space or LF. `+A1`, `-A1`, `@A1` and `#N/A` open.
  - **`=`:** `<f>=A1</f>` opens, as `A1`; `<f>==A1</f>` and `<f>=</f>` don't. `<f></f>` and `<f> </f>` open as empty cells.
  - **Escapes:** Excel decodes `_xHHHH_` in a formula as in text, with hex digits of either case: `"a_x0041_b"` is `aAb`, and `_x005F_x0041_` is `_x0041_`. `_x000D_`, `_x0001_` and `_xFFFE_` in a string read back as CR, U+0001 and U+FFFE. A character reference to a control character makes a file Excel can't open, as in text.
  - **White space between tokens:**
    - Only spaces and LF are taken there. A literal CR reads as LF, as XML's line end.
    - A CR from `_x000D_`, a tab (literal or `_x0009_`), U+0001 and a no-break space make a file Excel can't open. A tab or LF at the end is trimmed.
    - Any character opens inside a string.
  - **Length:** 8,192 UTF-16 code units open and 8,193 don't, with emoji counting two.
  - **References:** structured references to a table the workbook doesn't have open as `#REF!` (Excel shows `SUM(#REF!)`). An external reference without its link part (`[1]Sheet1!A1`) makes a file Excel can't open.
  - **Copies' names:** Excel copies `Orders` as `Orders (2)`, then `Orders (3)`, and copies `Orders (2)` as `Orders (3)` and `Orders (9)` as `Orders (10)`. A name of 31 or 30 characters is cut to 27 before ` (2)`.
- **Decisions made while building:**
  - **The design's four checks, plus the start.** `FormulaCheck` also refuses an operator other than `+`, `-` and `@` at the start, also after white space, which Excel refused for each. `=` is one: `==A1` is refused, as Excel refuses `<f>==A1</f>`, though it opens `<f>=A1</f>`. Operators elsewhere (`(*A1)`, `(A1*)`, both refused by Excel) need a tokenizer, which error literals like `#REF!+1` make easy to get wrong, so the docs list them with the other formulas Excel can't parse.
  - **Line breaks outside strings become LF.**
    - Excel refuses a CR between a formula's tokens, and the writer would have to write one as `_x000D_`, since a literal CR reads as LF.
    - A multi-line formula in a C# file with CR LF line ends would make a workbook Excel can't open. So `ExcelFormula` keeps a CR outside its strings as LF (a CR LF as one LF), in the constructor and in `Parse`.
    - So `Formula` can differ from the text given, and a formula written and read back is equal. Inside strings a CR is kept, and written as `_x000D_`.
  - **No control characters outside strings but line breaks.** A tab is refused there too, although Excel opens one at the end. Inside strings anything goes.
  - **An empty formula is refused** (also `=` and white space alone), although Excel opens `<f></f>` as an empty cell: "A formula can't be empty: a cell without one is a null or default(ExcelFormula) value."
  - **Parsing:**
    - Empty text is `default(ExcelFormula)`, so every text `ToString` gives reads back, and an empty cell reads as `default` for a member that isn't nullable.
    - Text without its `=` throws `FormatException` ("A formula's text starts with =, and this one doesn't.").
    - A formula the checks refuse throws `FormatException`, with the constructor's message.
  - **No formats:** `ToString(format, provider)` ignores both, as a formula's text is the same in every culture.
  - **The text is kept with its `=`,** so `ToString`, writing and `Parse` of a string that needs no change allocate nothing more, and `Formula` is a substring.
  - **Brackets as structured references read them:** inside brackets only brackets count, and an apostrophe escapes the character after it (`Table1[a'[b]`, `Table1[it''s]`); quotes and parentheses there are a column's name (`Table1[Price (USD)]`). A doubled quote in a string or a quoted name closes it and opens another, which reads the same, so it needs no case of its own.
  - **Messages** (`ArgumentException` from the constructor and `WriteFormula`, `FormatException` from `Parse`) quote the formula without its `=`, cut to 100 code units and `...` past that, and end with ", and Excel opens no workbook with it.":
    - "The formula \"SUM(A1:A3\" has a parenthesis that isn't closed" (and "a bracket", "a brace");
    - "has a ) that closes nothing", "closes a parenthesis with ]";
    - "has a string that isn't closed", "has a quoted sheet name that isn't closed";
    - "starts with the operator *", "ends with the operator +";
    - "has a tab outside its strings", "has U+0001 outside its strings".
    - The length's is the design's, without the formula: "A formula is at most 8,192 characters long, and this one has 9,000."
  - **The formula cell** is `<f>` alone, escaped as text is (entities, `_xHHHH_`, `_x005F_`). It gets no width and never counts as text that looks like a number.
  - **The row limit costs nothing per field.** `EndRecord` lowers the field limit, Excel's columns, to 0 once the sheet is full, so the next record's first field meets the one test that was already there, and finds the row limit.
  - **`Throw`'s message** names the sheet as a formula does, without "The record is discarded", as in step 1: "The sheet Orders is full at row 1,048,576, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet." (`'Q1 Orders'` for a name that needs quotes). Every field of a record past the limit throws.
  - **`NewSheet`:**
    - **Names:** new sheets are named as Excel names copies, of the full sheet's name. So a full `Orders (2)` goes on in `Orders (3)`, rather than `Orders (2) (2)`, and a name that Excel takes to be one in the workbook is skipped (`Data` goes on in `Data (4)` when `Data (2)` and `DATA (3)` are taken). The stem is cut to fit 31 code units, but not between a surrogate pair's halves.
    - **No I/O in `WriteField`:** the full sheet's end goes into the buffer, with the next sheet's header row after it. The next flush, which `EndRecord` asks for, writes the full sheet, closes its entry and opens the next one's. So the switch is sync or async as the caller is.
    - **What the new sheet repeats:** with `HasHeader`, the header row's XML is kept when it ends, and repeated in each new sheet as row 1. Its shared strings are counted again, and it counts in the sheet's used range and its `ignoredErrors`. So do the columns' widths.
    - **A failed record:** when the record that starts a new sheet fails, the new sheet keeps its header and is in `SheetNames`.
  - **`MaxRows`,** internal on `ExcelWorkbookWriter`, lets tests set a sheet's rows (at least 2) before adding a sheet.
- **Checked through COM after building:** five workbooks written by the built library from pwsh 7 (`write_probes.ps1`; answers in `written-*.txt`). Excel opens all five:
  - `s3-formulas`, with inline and with shared strings: every formula computed, as Excel shows each:
    - `A2+B2` 5, `IF(A2>1,"big","small")` big, `"a_x0041_b"&"<&>"` `a_x0041_b<&>`;
    - the multi-line formula with LF between its tokens and CR LF in its string;
    - `a`, U+0001, `b`, U+FFFE from a string;
    - `_xlfn.XLOOKUP` 3, `'Q1 Data'!A1&"!"` `quoted!`, `SUM({1,2;3,4})` 10;
    - `NOSUCHFUNC(1)` `#NAME?`, `Nope!A1` `#REF!`, `A2%` 0.02, `SUM(1,)` 1.
  - `s3-newsheet`, with inline and with shared strings:
    - `Orders`, `Orders (2)`, `Orders (3)` and `Orders (4)`, each with the header in row 1 and the date column's width;
    - `numberStoredAsText` ignored on `Orders (3)` only, which has `00123`;
    - `abcdefghijklmnopqrstuvwxyz12345` going on in `abcdefghijklmnopqrstuvwxyz1 (2)`, whose `ROW()*10+3` is 23.
  - `s3-lastrow`: 1,048,577 rows with `NewSheet`, `Rows` to row 1,048,576 and `Rows (2)` with the last in A1.
- **LibreOffice 26.8** opens all five, and exports the same values, names, headers and rows, but:
  - it doesn't decode `_xHHHH_` in formulas, as it does in text: `"a_x005F_x0041_b"`, a string's `_x000D_`, `_x0001_` and `_xFFFE_` show as written. Excel's reading wins, since a literal CR would read as LF in Excel and XML can't hold the control characters;
  - it shows a missing sheet as `#NAME?`, and `A2%` as `2.00%`.
- **The oracles:** ClosedXML reads each formula without its `=`, and computes it (`B2*C2` 6, `IF(...)` big, `SUM(D2:D3)` 8.5). ExcelDataReader reads the cells as empty, since they have no cached value.
- **Tests** (163 new on net10.0 and net8.0, 161 on net48: 2,364 on net10.0 and net8.0 and 2,326 on net48, all passing), in `tests/CsvHelper.Excel.Tests/Writing`:
  - **`FormulaCheckTests`:**
    - every formula Excel opened and refused, with each message;
    - control characters outside and inside strings, empty formulas, the length in UTF-16 code units (with emoji, at 8,192 and 8,193);
    - long formulas shortened in messages, but not between a surrogate pair's halves;
    - structured references' brackets and escapes, and line breaks kept as LF outside strings.
  - **`ExcelFormulaTests`:**
    - the text with and without its `=`, line breaks, `default`;
    - the refusals and their parameter, equality and hash codes;
    - `Parse` and `TryParse`, and every text `ToString` gives read back;
    - formats and cultures ignored;
    - on .NET, the span interfaces;
    - CSV: `WriteFormula`, `WriteField(object)`, injection protection (`'=SUM(A1:A3)`), and the converter both ways.
  - **`FormulaCellsTests`**, sync and async:
    - formula cells through the record writer, `WriteFormula` and `WriteField(object)`, with their XML;
    - read back with `FormulaText` (records equal) and `CachedValue` (empty);
    - escapes in formulas read back, shared strings without formulas, the oracles, and `CellKinds`.
  - **`RowLimitTests`**, sync and async, with inline and shared strings:
    - `Throw` from `WriteSheet` and for every field written by hand;
    - `NewSheet` with the header, widths and `ignoredErrors` per sheet, and the shared strings' counts;
    - without a header, and a header wider than its rows in each used range;
    - no sheet when the last row is the last record, a failed record at the switch and after its flush;
    - copies' names with names taken, and flushes across sheets;
    - the option's check;
    - and one test that writes Excel's 1,048,576 rows, then throws for the next, with the message's number in full.
  - **`SheetNamesTests`:** copies' stems and names as Excel gives them.
  - Every workbook these tests write passes `OpenXmlValidator`.
- **Mutation check** on net10.0: 78 bugs planted one at a time, each file restored and checked by SHA-256 (the driver, `mutate.py`, and its results are in `Csv\_bench\results\phase4\step3`). 76 were caught by the first run. The other two are caught after a test was changed or added for each:
  - "the record start not moved" (`recordStart -= count` written as `= 0`): after the flush that wrote the full sheet, the new sheet's header and rows weren't counted as written records, so a record of that sheet that failed then dropped them. The test's failing record was past the limit itself, and started another sheet, which hid the loss. With three rows a sheet it fails on the same sheet.
  - "no closing parenthesis needed" in `CopyStem`: no name had digits after ` (` and another last character, so `Orders (12` and `Orders (1x` were added.
  - The checks:
    - the length limit off by one 2, LF not white space 3, empty not refused 3;
    - `=` allowed at the start 2, `%` there 1, `#` at the end 1, `@` there 1, the start's white space not skipped 2, the end's 2;
    - an apostrophe in brackets escaping nothing 3, brackets in brackets not counted 1, never closed there 5;
    - a quoted name never closing 6, an apostrophe opening none 2, a string ending a character early 27, its last character not copied 4;
    - a closer at depth 0 not refused 4, mismatched closers accepted 3, the opener of `]` as `(` 1, a bracket named a parenthesis 2;
    - CR LF kept as two LFs 5, CRs not counted 5, tabs allowed 4, control characters allowed 5;
    - an unclosed quoted name accepted 1, one unclosed bracket accepted 6, 101 code units shown 1, a surrogate pair cut in a message 1, one CR not normalized 3.
  - `ExcelFormula`:
    - a lone `=` not taken off 2, text with its sign copied 2, the normalized text dropped 4, `Formula` keeping its `=` 3;
    - equality ignoring case 1, a hash of nothing 1, `!=` as `==` 1;
    - empty text not `default` 6, text without `=` parsed 4;
    - UTF-8 bytes kept on failure 1, chars kept 1, the format given as the text 2.
  - The converter reading by the constructor 1, `WriteFormula` writing text 4, formulas as text cells 11.
  - The sink:
    - `=` kept in `f` 10, the formula not escaped 4;
    - the row limit taken for the column limit 33, one row past the limit 33, `Throw` taken for `NewSheet` 33, the field limit off by one 31, not restored 20, the message's number unformatted 1;
    - data from row 1 after the header 12, the header not repeated 12, the first data row kept as the header 12, the header repeated without `HasHeader` 8;
    - the header's shared strings not counted 2, new sheets always flagged 4, a number-like header not flagged 4, the header's last column dropped 4, its row not in the used range 4;
    - the new sheet's part not taken 24, its start not written 24, no flush after a switch 16, the record start not moved 4 after the change;
    - the full sheet's entry not closed 24, the switch never cleared 24, the full sheet's end not marked 24.
  - The workbook writer: taken copy names used 4, the copy's part off by one 24, the test limit not passed 32.
  - Names: a copy's copy keeping its number 6, ten digits taken as a number 1, no closing parenthesis needed 2 after the test, letters taken as digits 1, a copy's name cutting a surrogate pair 1, one of 32 code units 5.
  - The options: `RowLimit` unchecked 1.
- **Core:** no change. The solution builds with 0 warnings on every target.
- **Review** (2026-11-07), the recommendations:
  - The checks gain an operator at the start, and refuse control characters outside strings, tabs included.
  - Line breaks outside strings become LF, and an empty formula is refused.
  - `Parse("")` is `default(ExcelFormula)`.
  - New sheets are named as Excel names copies.
  - The row limit's message drops "The record is discarded", as step 1's do.

**Step 4, formats** (built, approved and committed 2026-11-07):

- **New files:**
  - `Internal/Writing/`:
    - `FormatTranslator.cs`: a .NET date and time format, standard or custom, in a culture, to an Excel code, or none;
    - `DateFormat.cs`: a translation: its code, the reading back of a field's text, and the width of the format's widest text;
    - `FormatCheck.cs`: whether Excel opens a format code, and why not;
    - `MemberFormats.cs`: a record type's `[CsvFormat]` and `[CsvCulture]` by reflection, cached per type, and `MemberFormat`, a member's entry;
    - `ColumnFormat.cs`: a column's member's format and culture in one sheet, with its translation and cell format, made when first used;
    - `HeaderCapture.cs`: the cell sink that keeps the names a record writer's `WriteHeader` writes.
  - `Polyfills/TrimmingAttributes.cs` (`!NET` only): `DynamicallyAccessedMembers` and `UnconditionalSuppressMessage`, which the netstandard and .NET Framework builds lack. Core uses its annotations only under `NET`.
- **Changed:**
  - `ExcelWorkbookWriter` gains `AddSheet<T>`, and `WriteSheet<T>` and both `WriteSheetAsync<T>` go through it. Their `T` is annotated `[DynamicallyAccessedMembers(Members)]`, as the design has it: public and non-public properties, fields and constructors.
  - `SheetSink` keeps the header record's names, finds each column's `ColumnFormat`, reads numbers with the member's culture, and dates and times with the member's format.
  - The project file's comment on trimming: reflection now reads the members of the types `AddSheet<T>` is given.
- **Decision: numbers' formats aren't translated, since `[CsvFormat]` doesn't apply to numbers.**
  - CsvHelper's generator writes a member's `[CsvFormat]` only for types with a `ParseExact`: `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` and other types that have one. On an `int`, a `decimal` or a `double` it warns (CSVH010, "has [CsvFormat], which does not apply"), and the record writer writes the default text (`RecordParser.FormatValue`; `RecordGeneratorTests.WarnsAboutAttributesThatDoNotApply` checks `[CsvFormat("x")] public int X`).
  - So the design's numeric translation (`F`, `N`, `D`, `E`, `P` and `C`, and custom numeric formats) has no source: no record writer writes a number in a format the sink could know, and a format passed to `WriteField` by hand doesn't reach the sink (Phase 0). It isn't built.
  - What reaches the sink is a number member's `[CsvCulture]` alone: its default text in that culture (`1234,5`). The sink reads it with that culture's signs and separator (step 2's `NumberCulture`), so it is a number cell. Step 2's open case, `1234.5` from an invariant `[CsvCulture("")]` under a de-DE writer, is a number now.
  - Translating numbers would need a core change that lets the record writer write a number in its `[CsvFormat]` (reading it back needs no format, since a number cell is a number), which would have to pass Phase 0's gate.
  - Decided at review, 2026-11-07: the user left it out of Phase 4, as recommended. The alternatives were to build the numeric translator now, unused until such a change, or to make that core change. Phase 5's `[ExcelColumn(Format)]` and `Columns` are how a number gets a format, and step 6's fuzzing covers date and time formats only.
  - The step's other decisions, below, stand as built.
- **What is translated,** dates and times as designed, with these decisions:
  - **Standard formats** become the culture's patterns: `f`, `g` and `G` as .NET composes them (a date pattern, a space and a time pattern, checked against .NET's own text in 11 cultures); `R`, `s` and `u` the invariant culture's. `O` is read without a format, as before. Not translated: `U`, which depends on the machine's time zone, and `M`, which has no year. A `DateOnly`'s `R` is `ddd, dd MMM yyyy` and a `TimeOnly`'s `HH:mm:ss`.
  - **Every kind reads back through `DateTime`,** with the pattern: a `DateOnly`'s and a `TimeOnly`'s patterns format a `DateTime` the same, so the netstandard builds read them too.
  - **Separators and other text are quoted** (`dd"."mm"."yyyy`), so that Excel shows the culture's whatever its own locale. Excel reads `m` as minutes after `h` with quoted text between (checked: `hh":"mm`, `hh" "mm` and `hh"x"mm` show `10:30`, `10 30` and `10x30`), and `mmm` as a month even beside an hour (`hh mmm` shows `10 Jan`).
  - **Locale tags:** names get their culture's id (`[$-407]`), and English names too, as `[$-409]` for the invariant culture (the design tagged names other than English only; untagged, a German Excel would show German names). `AM/PM` without names gets `[$-409]`, since Excel shows it in the tag's locale (`[$-411]` gives 午前). A culture without a Windows id (0x1000, 0xC00 or 0x1400, or a `DateTimeFormatInfo` as the provider) gets `[$-409]` when its names are the invariant culture's, and isn't translated otherwise: Excel shows `[$-1000]` names in English.
  - **`t` is `A/P` without a tag,** since Excel shows a tagged `A/P` as the whole designator (checked: `[$-409]hh":"mm" "A/P` shows `10:30 AM`). So `t` isn't translated beside names or `tt`.
  - **Month names .NET writes in the genitive** (with a day of the month, where the culture's genitive names differ: ICU's de-DE `MMM`, pl-PL's and ru-RU's `MMMM`) aren't translated: Excel shows the nominative (checked: `[$-419]d mmmm yyyy` shows `15 Январь 2024` and `[$-415]` `15 styczeń 2024`, where .NET writes `15 января 2024` and `15 stycznia 2024`).
  - **A date without a year** (`dd MMMM`, `dddd`) isn't translated: its text reads back in the current year, not the value's.
  - **Two tokens of one letter side by side** (`%H%H`, `dddd%d`) aren't translated: Excel reads them as one (`hh`).
  - The rest as designed: eras, offsets, `F`, four or more fraction digits, `y`, `yyy` and five or more, a 12-hour hour without `tt` or a 24-hour one with it, other designators, a month where Excel reads minutes and minutes where it reads a month, `f` not after `s.` or `ss.`, and calendars other than the Gregorian.
- **Reading back:** `DateTime.TryParseExact` with the pattern, then `ToString` must give the same text (names in another case, `jan 2024`, parse but format otherwise). A format without a date holds the time of day, a serial below 1. A date before 1900-01-01 follows `OutOfRangeDates`, and the message quotes the formatted text: "Cell Data!B2 holds 31.12.1899, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw)."
- **Widths** are the widest text over 84 sample dates (every month, seven days in a row, at 11:58:58.888), rather than the first value's text, so that a column of `MMMM` fits September after May. The evening isn't sampled: `AM` is wider than `PM` in Calibri, and Excel shows no other designators.
- **What a member's format does,** by kind:
  - `DateTime`, `DateOnly`, `TimeOnly`: a format other than `O` or empty is translated; a format that isn't, or a text that doesn't read back, is a text cell. So is .NET's default text in such a column (written by hand): only the member's format reads it.
  - `DateTimeOffset`: a format's text shows a clock time without its offset, so it is a date cell only with `ClockTime`; `Text` and `Utc` make text cells.
  - `TimeSpan`: a format other than `c`, `t` or `T` is a text cell, even when its text is in `c`'s layout (`hh\:mm\:ss` drops the days).
  - Numbers: a `[CsvCulture]`'s signs; a column whose member has a format but no culture reads numbers with the writer's.
- **`MemberFormats`** finds the members as the generator does (`RecordParser`):
  - `[CsvRecord]` types only;
  - the constructor marked `[CsvConstructor]`, else the visible parameterless one, else the one visible constructor with the most parameters, not a copy constructor; its parameters by their own names (`when` for a property `When`), with the attributes of the property or field they set as a fallback;
  - then the properties and fields of the type and its base types, the derived type's first and a hidden member once, unless `[CsvIgnoreBase]`: accessible from the type (a base type's private ones aren't), visible or with `[CsvColumn]`, not `[CsvIgnore]`, not set by the constructor, and settable (a property's setter visible or with `[CsvColumn]`; a field neither `readonly` nor `const`);
  - a member with `[CsvConverter]` has no format, and a record type behind one isn't read;
  - nested records (`[CsvRecord]` types, nullable structs included) after their prefixes, which add up;
  - a collection's columns from `Index` to `EndIndex`, and an indexed member at its index; the others by name, ordinally, and `NameIndex`;
  - a culture that doesn't exist leaves its member out (its record writer throws anyway).
  - Not seen: an assembly's `[assembly: CsvConverter]` of a nested record type. A member it converts is read as nested, and gets its members' formats under prefixed names its one column doesn't have.
- **The columns' names:** with `HasHeader`, the header record's (the first sheet's; `NewSheet`'s sheets keep its formats); without, `HeaderCapture`'s, none when `T` has no record writer or one that can't be created (writing its records throws then, as before).
- **Trimming:** `MemberFormats` reads base types and nested record types, which `T`'s annotation doesn't reach. Those reads are suppressed (IL2067, IL2072, IL2075) with the reason: the generated record writer reads those members, so the trimmer keeps them. The build's trim and AOT analysis gives no warning.
- **Checked through COM** (Excel 365, build 20527, at 144 DPI); the probes, scripts and answers are in `Csv\_bench\results\phase4\step4`:
  - **Format codes,** each in a workbook written by hand (`make_probes.py`, `answers.txt` to `answers4.txt`): these became `FormatCheck`'s grammar.
    - Excel opens `0 €`, `0 £`, `0 ¤`, `0°`, `0‰` (without scaling), a no-break space, `0 |`, `0 &`, `0 ^~{}`, `0 5`, `0""`, `0";"`, `[Color56]`, `[red]`, `[$€]`, `[$-7]`, `[$-40c]`, `[$-12345678]`, `[DBNum1]` and `[DBNum2]`, `[mm]:ss`, `[ss]`, `0;;0`, `@ @`, `0.0.0`, `general`, `General" x"`, `0.0E+0`, `##0.0E-0`, `dd.mm.yyyy`, `mmm d, yyyy`, `YYYY-MM-DD` (as lower case), `aaa` and `aaaa` (weekdays), `bbbb` (the Buddhist year), `ggge`, `yyyy e`, `[$-411]e-m-d`, `ss.0`, `hh:mm.000`, `dd.0` and `mm.00`.
    - It refuses `0\`, `0_` and `0*` at the end, `0:00`, `[Color0]`, `[Color57]`, `[ > 100]`, `[hm]`, `[foo]`, `0.00E00`, `E+0`, `0.00E+`, a date letter beside a digit placeholder (`0 g`, `0 y`, `0 e`, `0 h`, `0 s`), `dd @`, `dd %`, `dd ?`, `yyyy #`, `0 @`, `General 0`, `[Red0`, `ss.0000` and five sections.
    - It opens these as other codes, which the check refuses: `aa` (as `\a\a`), `0 A/P`, `[$-ZZZ]0` (as `[$]0`), `[>abc]0` (as `[>0]0;General`) and `[$-123456789]0`. The check also refuses `[DBNum3]` and a backtick, which Excel opens as they are: forms that weren't in the first rounds, and that no translation needs. Phase 5 can accept them.
  - **Display,** of codes the translator gives (`display-wide` in `answers2.txt`, and `answers4.txt`): quoted separators, `T`, `ss.000`, `\"`, the German, French, Japanese, Russian and Polish names, `AM/PM` untagged and under `[$-409]`, `[$-407]`, `[$-40C]`, `[$-411]` and `[$-809]`, and `A/P` with and without a tag, as above.
  - **Written by the built library** (`make_probeapp.py`, `written-answers.txt`, `written-compare-excel.txt`): 136 formatted columns, of 105 `DateTime` formats (14 standard formats in five cultures, 23 custom ones, and 12 custom ones in six cultures), 13 `DateOnly`, 13 `TimeOnly` and 5 `DateTimeOffset` with `ClockTime`, each with three values. Excel opens both workbooks. 404 of the 408 cells show .NET's text, 367 of them typed (the others are formats that aren't translated, as text), and none shows `########`. The 4: ja-JP's `MMM` (Excel's `[$-411]mmm` shows `1`, .NET writes `1月`), three times, and de-DE's March (Excel `Mrz`, ICU `Mär`). Step 5's oracle lists such differences.
- **LibreOffice 26.8** opens both, and exports 402 of the 408 as .NET writes them (`written-compare-lo.txt`): it shows `A/P` in lower case, and `AM/PM` under `[$-407]` as `vorm.` and `nachm.`.
- **The oracles:** ClosedXML reads formatted dates as `DateTime`, and a time of day as a `TimeSpan`; ExcelDataReader reads them as `DateTime`, a time of day on 1899-12-31.
- **.NET Framework's culture data** (Windows' own, where .NET has ICU's): ja-JP's long date has no day name, de-DE has no designators (so nothing with `tt` is translated there) and its abbreviated genitives are its nominatives, and en-GB has `AM` and `PM` rather than `am` and `pm`. The tests expect each runtime's data.
- **Tests** (276 new on net10.0 and net8.0, 274 on net48: 2,640 on net10.0 and net8.0 and 2,600 on net48, all passing), in `tests/CsvHelper.Excel.Tests/Writing`, with `IsExternalInit.cs` for net48's positional record:
  - **`FormatCheckTests`:** the 83 codes Excel opened and the 32 it refused, with each reason; the forms it opens as other codes, and conditions not checked, refused; the 255-character limit; quotes, escapes and brackets skipped; the messages.
  - **`FormatTranslatorTests`:**
    - the standard formats in the invariant culture, en-US, de-DE, fr-FR and ja-JP, read back;
    - 64 custom formats, translated or not;
    - separators, locale tags and names Excel shows otherwise, each kind's standard formats;
    - every code passing `FormatCheck` and classified as a date by Phase 2's `NumberFormats.Classify`, over 8 cultures;
    - reading back: what each text shows, 1900's first days, texts the format doesn't give, two-digit years;
    - 300 random values for each of 17 formats that translates, in each of the five cultures (10 to 17 of them per culture);
    - the widest text's width.
  - **`MemberFormatsTests`:** each test type's columns checked against its record writer's own CSV fields (every formatted column holds its member's format's text) and its entries: names, `[CsvColumn]` names, private, internal, ignored, read-only and converted members, `NameIndex` with a gap, nested records one and two deep with a nullable struct, a positional record, constructors (a copy constructor, a parameterless one, `[CsvConstructor]`), base types and `[CsvIgnoreBase]`, indexed collections, a mapped type, header names written by hand, the cache, and `HeaderCapture`.
  - **`FormattedCellsTests`**, sync and async:
    - `WriteSheet<T>` of every kind: the cells' XML, the styles, the widths, and the validator;
    - read back by `GetRecords<T>` through the members' formats, and by ClosedXML and ExcelDataReader;
    - without a header; with a header written by hand, by its names; a text the member's format doesn't read; `AddSheet` without `T`;
    - each `DateTimeOffsets` choice, `OutOfRangeDates` both ways, cultures' numbers both ways, `NewSheet`'s sheets, .NET's newer types;
    - .NET's default formats (`O`, empty, `c`, `t`) and a custom duration in `c`'s layout, a number in a date member's column, and an asynchronous sequence.
  - Every workbook these tests write passes `OpenXmlValidator`.
- **Mutation check** on net10.0: 138 bugs planted one at a time, each file restored and checked by SHA-256 (the driver, `mutate.py`, and its results, `mutations.jsonl` and `mutations-rerun.jsonl`, are in `Csv\_bench\results\phase4\step4`). 134 were caught by the first run. Of the other four, three are caught after a test was added for each, and one can't be:
  - "a month where Excel reads minutes" (`Part.Month when !minutes` without its guard): the tests' cases, `HH:MM` and `MM:ss`, have no year, so the rule for dates without a year refused them first. `yyyy HH:MM` and `yyyy MM:ss` were added.
  - "`[CsvIgnoreBase]` ignored": the base type's members got entries, under names its header doesn't have, so its columns were right. The test checks its entries now.
  - "members read without `[CsvColumn]`": a property needs a setter that is visible or has `[CsvColumn]` anyway, so only a private field shows it, and no test type had one. `NamedFormats` has one now.
  - "`Color0` allowed" (the number's bound lowered to 0) is equivalent: a color's number can't start with `0`, so `Color0` is refused either way.
  - The checks:
    - the length limit off by one 1, five sections 2, an unclosed quote 1 and bracket 1 taken as closed at the end, a trailing escape 3;
    - fraction zeros after any zero 11, zeros after a point not fractions 21, an exponent's digits not counted 3;
    - in dates, `#` 2, `0` 6, four fraction digits 3, `%` 1, `@` 1 allowed; `:` in numbers 1; `@` 1 and `General` 1 with digit placeholders;
    - an exponent without digits after 2, in lower case 1, before any digit 1, without its sign 1, an era's dash taken for one 1;
    - `General` 5, `AM/PM` 8 and `A/P` 2 not tokens, `aa` a token 1, `b` not a date letter 1, elapsed time of mixed letters 1;
    - colors by case 1, `Color57` 1, a condition without its number 2, its point without digits 1, text after it 1, a locale of nine hex digits 1, its hex unchecked 1;
    - a backtick 1 and letters past ASCII 1 allowed, a quote's end read as another quote 85.
  - The translator:
    - `R` in the culture's formats 3 and names 1, `f` without its time 6, `g` with the long time 16, `u` as `s` 6, a `DateOnly`'s `R` with a time 3, a `TimeOnly`'s without seconds 1, any calendar 1;
    - `%%` read 1, `%` keeping its letter's run 1, a quote's escape not read 1, an unclosed quote read 1, an escape at the end not refused 2, runs counted as one 81;
    - the time separator as the date's 27, the date separator as `/` 1;
    - a date without a year 12, a 12-hour hour without its designator 1, a 24-hour one with one 1, any designators 1, `t` beside names 2, `AM/PM` without a locale 7, `t` as `AM/PM` 1;
    - the fraction's point kept 4, adjacent letters merged 3, a fraction after minutes 1 or any text 2;
    - minutes only before seconds 26 or after hours 1, a month where Excel reads minutes 2 after the test, minutes where it reads a month 2, one neighbor only 45;
    - `yyy` as `yyyy` 2, `HHH` as `hh` 2, `sss` as `ss` 1, abbreviated months 5 and days 5 as full;
    - genitives ignored 1 or compared the other way 2, the invariant culture's own id 11, names without a culture tagged 0 1 or taken as English 1, a quote in text not escaped 1.
  - `DateFormat`: text not formatted again 1, a time of day as a date 21, one month 1 or one day 1 sampled.
  - `ColumnFormat`: `O` 2, `c` 8 and `t` 2 read as formats, an empty format kept 2, the culture's numbers dropped 9, every member with a culture 1, the culture ignored for dates 11.
  - `MemberFormats`:
    - types without `[CsvRecord]` read 1, constructor parameters skipped 1 or without their properties' attributes 1, base types skipped 1, `[CsvIgnoreBase]` ignored 1 after the test;
    - private base members 1, protected members 1, members without `[CsvColumn]` 1 after the test, ignored members 1, constructor-set members 1, private setters 1 and read-only fields 1 read; converted members' records read 1;
    - nested prefixes dropped 2, outer prefixes dropped 1, nullable structs not unwrapped 1, collections as one column 1, a collection's last column dropped 1;
    - `NameIndex` ignored 1, names ignoring case 1, the first of a name always 1, the copy constructor taken 1, the parameterless constructor not first 1, `[CsvConstructor]` ignored 1.
  - `HeaderCapture`: captured names dropped 10, a missing record writer throwing 1.
  - The sink:
    - header names dropped 19, formats not found at the header's end 19, names from a header without one 21;
    - numbers in the writer's culture 11, formatted dates by the default path 15, formatted `DateOnly` 2 and `TimeOnly` 2 as text;
    - a formatted offset typed with `Utc` 1, an offset's format ignored 1, durations' formats ignored 2;
    - a formatted date before 1900 written 2, text that doesn't read back typed 2, formatted dates' widths dropped 2, the translation's style dropped 10, the last column's format dropped 5.
  - The workbook writer: `WriteSheet` 8, `WriteSheetAsync` 6 and an asynchronous sequence 1 without `T`'s formats, no names without a header 2, no member formats 21.
- **Core:** no change. The solution builds with 0 warnings on every target.

**Step 5, corpus and oracles** (built, approved and committed 2026-11-07):

- **New files:**
  - `tools/CsvHelper.Excel.Corpus/Written.cs`, the `written` content set, which the tests link (as `Corpus/WrittenSet.cs`):
    - `Written`: `Workbooks`, `Write` and `WriteAsync` (into memory), `PartsHash` and `CultureData`;
    - `WrittenWorkbook` (a name, a description, the options, the sheets, and the test's row limit), `WrittenSheet` (rows of values, the header first, written by `T`'s record writer through `WriteSheet<T>`, or field by field through `AddSheet`) and `WrittenColumn` (a header's name, its member's format and culture);
    - the record types, `WrittenNumber` to `WrittenCultureNumbers`, of public properties.
  - `tools/CsvHelper.Excel.Corpus/ExcelWritten.cs`: the `written` command.
  - `tests/CsvHelper.Excel.Tests/Files/excel-written/*.json`: Excel's answers, one file per workbook.
  - `tests/CsvHelper.Excel.Tests/Corpus/`: `WrittenAnswers.cs` (the answers' loader), `WrittenOracle.cs` (what each cell should be, from the values written, and the codes and widths of the workbook's parts) and `WrittenAnswersTests.cs`.
- **Changed:**
  - The tool: `written` in its commands and in `all` (after `oracle`); references to CsvHelper.Excel and the generator; `ExcelOracle`'s `WriteCell`, `Grid` and `RepairRecords` internal, for `written`.
  - `Corpus/LibreOffice.cs` converts the written workbooks too, as this runtime writes them, and its conversion takes any workbooks.
  - `ExcelAnswersTests.Shown` (a formula as Excel shows it) is internal, for the new tests.
  - `.github/workflows/ci.yaml` installs LibreOffice before the tests (below).
  - `Files/README.md`: "Excel's reading of written workbooks".
- **The set,** ten workbooks, each with the default options but those named:
  - `types.xlsx`: every type of "What a field becomes" that .NET Framework has, at its edges, and a sheet whose later values are wider than its first;
  - `types-net.xlsx` (.NET only): `DateOnly`, `TimeOnly`, `Half`, `Int128`, `UInt128`, and dates' and times' formats in the invariant culture;
  - `options.xlsx`: `LongNumbers = Number` and `DateTimeOffsets = Utc`;
  - `text.xlsx` and `text-shared.xlsx`: escaped texts (every C0 control, CR, CR LF, `_x0041_` and its near misses, U+FFFE, U+FFFF, a lone surrogate, invalid UTF-8 through `WriteField(ReadOnlySpan<byte>)`), white space at the ends, numbers stored as text, and 32,767 code units, inline and shared;
  - `formulas.xlsx`: step 3's formulas, by hand and by a record writer, each with the text Excel should show in a `Shows` column of its row;
  - `names.xlsx`: 18 sheet names Excel opens;
  - `newsheet.xlsx`: `RowLimit = NewSheet` at three rows a sheet, with shared strings: `Orders` to `Orders (4)`, and the 31-character name's copy;
  - `formats.xlsx` (`DateTimeOffsets = ClockTime`): 13 standard and 36 custom date and time formats in the invariant culture, translated or not, offsets and durations in formats, and a formatted date before 1900;
  - `cultures.xlsx` (.NET only, `ClockTime`): the standard formats with patterns of their own in en-US, de-DE, fr-FR and ja-JP, 14 custom formats of names, designators and separators in seven cultures, `DateOnly`, `TimeOnly` and offsets in cultures' formats, and numbers in de-DE, fr-FR, sv-SE, ar-SA, fa-IR and the invariant culture, named, by `[CsvCulture]`.
  - Each formatted column holds step 4's three sample values (a morning, an evening and a late night, with milliseconds).
- **Decisions made while building:**
  - **Each workbook is pinned by its parts' SHA-256, not its file's.** The hash is over each entry's name and inflated bytes, with their lengths, in the package's order. Deflate's output differs between runtimes (zlib-ng on .NET 9 and later, zlib on .NET 8, .NET Framework's own), so a file written on net8.0 or net48 isn't the file Excel read, while its parts are. A test checks that the same parts at another compression level hash the same, that the hash is the layout described, and that one sheet fewer changes it. Measured with one workbook written by hand (a header and 2,000 rows of text and numbers, at `Fastest`) on each runtime: its files are 43,362 bytes on .NET 10.0.13, 30,468 on .NET 8.0.33 and 33,141 on .NET Framework 4.8, three different SHA-256s, and its parts hash the same on all three. (At zlib-ng's fastest level, .NET 10's file is 42% larger than .NET 8's; step 7's benchmark records the sizes.)
  - **The workbooks aren't committed;** the tests and the LibreOffice conversion write them again.
  - **Culture data:** `cultures.xlsx` is the one workbook whose formats read culture data other than the invariant culture's, which comes from the runtime (ICU on .NET, of the version Windows ships on Windows; Windows' own data on .NET Framework, which differs, so the workbook is .NET only anyway). Its answers carry `CultureData`, a fingerprint of that data: each culture's id, calendar, patterns, separators, designators, names and genitive names, and numbers' signs and separators. On a runtime whose fingerprint differs (another ICU, as CI's runner may have), the workbook isn't compared and the tests say so in their output. So that a change to the fingerprint itself can't leave it uncompared everywhere, the answers record the operating system they were made on (`RuntimeInformation.OSDescription`), and on that system a different fingerprint fails. On this machine, net10.0 and net8.0 have the same data, and both compare it.
  - **The oracle's rules are the test's own,** from the values the set wrote, and not the writer's code: significant digits from the first digit that isn't 0 to the last; floats as their shortest text (tried digit by digit, since .NET Framework's `ToString` isn't the shortest); serials from 1899-12-31 before 1900-03-01 and from 1899-12-30 after, of the value rounded to the millisecond, half up, and Excel's last millisecond at most; the writer's date formats by what the rounded value holds; durations in whole hours; a formatted value read back by its own type's parser (`DateOnly.TryParseExact` for a `DateOnly`: a `DateOnly`'s `R` is `ddd, dd MMM yyyy`, which `DateTime`'s `R` doesn't read), a time of day alone for a format without a date. A formatted value is a typed cell of the value its text reads back as when its format is one Excel shows the same and the text reads back, from 1900-01-01, as a value that formats to the same text; otherwise it is a text cell of the text. Excel's text of it is .NET's either way. Which formats Excel can't show the same, the test lists by the plan's rules (`M` and dates without a year, offsets, `F`, four fraction digits, `y`, hours and designators that don't agree, a month where Excel reads minutes, an era, genitive months): 11 columns of `formats.xlsx` and 4 of `cultures.xlsx`. (The first version accepted either kind for any formatted value, which the mutation check showed couldn't catch a wrong rule for dates before 1900; see below.)
  - **The formatted cells' counts are pinned,** so that the translation's oracle compares what it should: `formats.xlsx` 125 typed of 161 formatted, `cultures.xlsx` 198 of 210, `types-net.xlsx` 21 of 21. The others are formats that aren't translated, a date before 1900, and two values of `ddd MMMM yyyy` whose weekday isn't the 1st's, so that the text reads back as no date. The first value of that column, Monday 2024-01-15, reads back as Monday 2024-01-01, which shows the same text, so it is a date cell of the 1st, as decision 15 says.
  - **Formulas' expected text is in the workbook,** in a `Shows` column of the formula's row, and a sheet with a formula must have one.
  - **The row limit is set by reflection,** since the tool can't see `ExcelWorkbookWriter.MaxRows`, which is internal (the tests see it through `InternalsVisibleTo`).
  - **Excel opens the written set with calculation automatic,** where the `oracle` command sets it manual: the writer's formulas have no cached values. And it leaves the columns at the widths written, where `oracle` widens them, so that a value its column is too narrow for shows as `########`.
  - **Widths at the answers' DPI:** Excel's `ColumnWidth` depends on the screen's DPI, which the answers record (144 here). The test converts each written width as Excel does: the width in maximum digit widths to whole pixels (`Truncate(((256 × w + Truncate(128 / digit)) / 256) × digit)`), less the padding, in digits, to two decimals; Calibri 11's digit is 7 pixels at 96 DPI and 11 at 144, the padding 5 and 7, and a column without a width 64 and 96 pixels. That gives Excel's 21.91 for 22.57, 18.09 for 18.71 and 8.09 for the default. Other DPIs fail the test with a message.
  - **`names.xlsx` goes through LibreOffice's `.xlsx`,** since LibreOffice names a CSV file after its sheet and stops exporting at `Q&A <1>`. The test reads the converted workbook with CsvHelper.Excel and compares its names and cells.
  - **LibreOffice in CI** (decision 27): the CI job downloads LibreOffice 26.8.2.2, the version checked here, from The Document Foundation's archive, checks it against the SHA-256 the archive publishes (`ff4b9114...e5307a`, 375,934,976 bytes), and installs it with `msiexec /qn` before the tests. So CI also runs Phase 1's LibreOffice tests of the corpus. It first ran in run 42388231857 (below).
  - **Excel 365's `LEN` counts an emoji as one character,** not two: the first answers said so, and the set's expected text was wrong.
- **What Excel answered** (Excel 365, build 20527, at 144 DPI): it opened all ten as they are. It reads every cell as written but:
  - a subnormal double, as 0 (step 2);
  - ja-JP's abbreviated month (`[$-411]mmm`) as its number alone, three times, and ICU's de-DE March, `Mär`, as `Mrz` (step 4);
  - a later value wider than its column's first, as `########` (decision 14): a date with milliseconds after a date alone, `TimeSpan.MaxValue` after an hour, and 1900-01-01 after a date before 1900, which is text, so its column gets no width.
  - Every formula computes to the text in its `Shows` column; every number format is the code written; every width is the width written, converted.
- **LibreOffice 26.8.2.2** shows Excel's text but where listed in the test: serials before 61 a day earlier; CR LF as LF, and also a lone CR in text that has an LF; `_xHHHH_` in formulas undecoded; a missing sheet as `#NAME?`; a percent's result as `2.00%`; `A/P` in lower case; `AM/PM` under `[$-407]` as `vorm.` and `nachm.`; `#FMT` for `TimeSpan.MaxValue`; values Excel shows as `########`; and ja-JP's and de-DE's months as .NET writes them. It keeps every sheet name of `names.xlsx`.
- **.NET Framework** writes the same parts as .NET for the eight workbooks it has.
- **Tests** (44 new on net10.0 and net8.0, 36 on net48: 2,684 on net10.0 and net8.0 and 2,636 on net48, all passing), in `WrittenAnswersTests`:
  - `AnswersForEveryWorkbook`: an answers file for each workbook of the set, the .NET-only two on .NET Framework too, each opened as it is, at a DPI the tests know; only `cultures.xlsx` reads culture data.
  - `WritesTheWorkbookExcelAnswered`, for each workbook: its parts, written synchronously and asynchronously, are those Excel read; otherwise the message says to run the `written` command.
  - `ExcelReadsWhatWasWritten`: each cell's value, type and shown text against the oracle, and its number format against the code written, every difference listed with its reason.
  - `ExcelShowsTheWrittenWidths`, `CountsTheFormattedCells`, `PinsTheParts` and `FingerprintsTheCultureData`.
  - `LibreOfficeShowsWhatExcelShows`: text cells by their whole text (Excel's `Text` stops at 8,221 characters), numbers in General by value, to 14 digits, since the two show their own digits, and the rest by the text shown, every difference listed.
- **Mutation check** on net10.0, each bug planted alone, its file restored and checked by SHA-256 (the drivers, `mutate.py` and `mutate_tool.py`, and their results are in `Csv\_bench\results\phase4\step5`):
  - **Which tests:** each bug ran the tests that compile against its file, `WrittenAnswersTests`, and `OracleTests` too for `LibreOffice.cs`. The whole suite takes about 12 minutes a bug in Debug, mostly LibreOffice's conversion of the corpus, so 56 bugs would have taken 11 hours. The baseline and the first bug ran with the whole suite (`mutations-full-suite.jsonl`): the bug failed the same 11 tests there.
  - **First run** (`mutations-first.jsonl`): 54 of 56 bugs caught. "Formatted dates before 1900 typed" lived: the oracle took a formatted value as text or typed, so Excel's text cell agreed with a wrong rule. The oracle now says which each value must be (above), and catches it. "The width's 128ths rounded" lived too (below).
  - **Second run** (`mutations.jsonl`), of 61 bugs, five of them new, in the typed-cell rule: 59 caught. The two that live are equivalent for the set's content:
    - "the width's 128ths rounded" (`Truncate(128 / 11)`, 11, as `Round`, 12): the two give other pixels only for a width within 11/256 of a pixel under a pixel's edge, and none of the set's is; the writer's widths come from measured text, which can't aim at one.
    - "a date's text that formats otherwise kept" (the read-back's round trip dropped): every formatted text of the set is .NET's own, and such a text, when it reads back, reads back as a value that formats to it. The writer's check is for text written by hand in a formatted column, which `FormattedCellsTests` covers.
  - **Failing tests per bug** (second run):
    - The content set: the parts hash without names 11, without the parts' lengths 11, without the parts 11, lengths most significant first 11; the culture data unsorted 1, with duplicates 1, without month names 4 (the guard on the answers' system); the row limit never set 4; formats ignored 5, cultures ignored 4, the invariant culture listed 4; header names upper-cased 7; UTF-8 written as an object 4; a hand sheet's columns unnamed 3 (no `Shows` column).
    - The oracle:
      - lone surrogates kept 2, an empty string a cell 2, booleans shown in .NET's case 1, default formulas a cell 1;
      - `Utc` as the clock time 1, `ClockTime`'s formats as text 4, `t` and `T` not durations 1;
      - 15 digits too long 3, `LongNumbers` ignored 1, trailing zeros significant 1, `NaN` a number 2, the infinities numbers 2, floats as their doubles 1;
      - 1899-12-31 a date 2, midnight with its time 3, milliseconds by the second 2, half a millisecond rounded down 1, past Excel's last millisecond 2, 1900-02-29 not counted 2, a time of 24:00 1, times always whole 1, any negative duration text 1, hours modulo a day 2;
      - formatted times as dates 3, formatted dates before 1900 typed 1, `DateOnly` 1 and `TimeOnly` 1 read as `DateTime`, every format translated 2, an untranslated format's culture ignored 1, typed without a translation 2, typed though not read back 1;
      - 144 DPI's padding 10 and default column 10, a column's last not widened 6, built-in formats as General 2, sheet names undecoded 2, the header not repeated 1, a new sheet's rows with the header's 2.
    - The tests and the loader: the culture check inverted 4, LibreOffice's General to 16 digits 2, LibreOffice's text against Excel's `Text` 2, a typed time taken for a date 3, widths at 96 DPI 10, Excel's text defaulting to empty 3.
    - LibreOffice: every written workbook exported as CSV 10.
  - **The `written` command,** which only Excel runs (`mutate_tool.py`): each bug planted in `ExcelWritten.cs`, the command run into a scratch folder, its answers put in a scratch build of the tests, and the written set's tests run there; the repository's answers untouched (`mutations-tool.jsonl`). The baseline's fresh answers pass all 44 tests, and all 6 bugs are caught: calculation manual 4 (formulas uncomputed), the columns widened as `oracle` widens them 13, widths from column B 6, the DPI taken as 96 10, another workbook's hash 9, no culture data 5.
- **Core:** no change, and none to CsvHelper.Excel. The solution builds with 0 warnings on every target.
- **Review** (2026-11-07), the recommendations:
  - Each workbook is pinned by its parts' SHA-256, not its file's, and the workbooks aren't committed.
  - `cultures.xlsx` carries its culture data's fingerprint: it isn't compared on a runtime with other data, and the system the answers were made on fails a fingerprint that differs.
  - Excel's widths are checked at the answers' DPI, converted as Excel converts them.
  - Each planted bug runs the tests that compile against its file, rather than the whole suite.
  - The oracle says whether each formatted value is a typed cell or text, from the formats the test lists as untranslated; the two bugs that live are equivalent for the set's content.
  - The CI step installs LibreOffice 26.8.2.2 from The Document Foundation's archive, checked by its SHA-256; it runs once the commits are pushed.
- **CI's first run** (run 42388231857, 2026-11-07): the branch's first push, so the first run of any CsvHelper.Excel test on CI's `windows-latest`, of 4 vCPUs.
  - LibreOffice 26.8.2.2 installed in 1 m 47 s, its SHA-256 checked. The LibreOffice tests ran on every target: none was skipped, and the totals are this machine's (2,684, 2,684 and 2,636).
  - CsvHelper.Tests and CsvHelper.Linq.Tests passed on all three targets. CsvHelper.Excel.Tests passed on net8.0, and failed one test on net10.0 and on net48: Phase 3's `FuzzTests`, with an input "still running after 5 s" (seeds 1965 and 163). The pack check didn't run.
  - Whether CI compares `cultures.xlsx` can't be told from the run: the console shows only failing tests' output, and both outcomes pass there (only the answers' system fails a fingerprint that differs).
  - **Why it failed:** the time limit measured the tests beside the fuzzer, not the input. Each of the two inputs reads in under 0.3 s alone. On this machine pinned to 4 logical CPUs, the whole suite as CI runs it failed the same way in both runs, on net48 (seeds 23 and 5, under 0.4 s alone). Timed, seed 5 waited 7.3 s for a thread of the pool, 8 busy at its minimum of 4 (.NET Framework's pool adds threads slowly while the CPU is busy), then ran 7.1 s through 29 full garbage collections of the tests beside it, the suite's heaviest readers.
  - **The fix** (approved and committed 2026-11-07): `FuzzTests` runs alone, in `TimedCollection` (`DisableParallelization`), after the tests that run in parallel, and an input out of time says how long it waited for a thread and ran, and the full collections since it was queued. CI's Test step writes TRX files, which an `Upload Test Results` step keeps even when tests fail (`if: always()`): about 2.6 MB zipped, with every test's output, such as the fuzzers' tallies and a workbook that isn't compared.
  - **Checked:** pinned to 4 logical CPUs, the suite passed on every target, the fuzzer running last with no test beside it, in 3.1 s on net10.0, 3.9 s on net8.0 and 5.2 s on net48. In the two runs of the experiment before it, no input waited more than 1 ms for a thread, and the slowest read in 72 ms.
- **CI's second run** (run 42421201910, 2026-11-07): the fix and step 6, pushed together. Every step passed, the pack check too, in 10 minutes, LibreOffice's install in 1 m 14 s.
  - CsvHelper.Excel.Tests passed 2,718, 2,718 and 2,670 tests, this machine's totals, with the LibreOffice tests (54, 54 and 52); CsvHelper.Tests and CsvHelper.Linq.Tests passed too. `FuzzTests`, alone, took 5.4 s on net10.0, 10.9 s on net8.0 and 13.4 s on net48 for its 3,000 inputs.
  - **`cultures.xlsx` is compared on CI:** the TRX files have every test's output, and no test said a workbook isn't compared, so the runner's data of the workbook's cultures is the data Excel's answers were made with, and its four tests compared it on net10.0 and net8.0.

**Step 6, security and fuzzing** (built, approved, with every recommendation, and committed 2026-11-07):

- **New files,** in `tests/CsvHelper.Excel.Tests/Security/`:
  - `TextFuzzTests.cs`, `SheetNameFuzzTests.cs`, `FormatFuzzTests.cs` and `FormulaCheckFuzzTests.cs`: the fuzzers, below, seeded as Phase 3's `FuzzTests` is. Each input's seed is a number from `CSVHELPER_EXCEL_FUZZ_SEED` on (1 by default), a failure names its seed, and a run stops at five. Their counts: `CSVHELPER_EXCEL_FUZZ_TEXTS` (500 workbooks), `CSVHELPER_EXCEL_FUZZ_NAMES` (500 workbooks), `CSVHELPER_EXCEL_FUZZ_FORMATS` (3,000 formats, and a workbook for each 20) and `CSVHELPER_EXCEL_FUZZ_FORMULA_CHECKS` (20,000 formulas, and a workbook for each 200).
  - `WriterFuzz.cs`: what they share (settings, text made of code units, text shown for a failure).
  - `WriterSecurityTests.cs`: the memory tests.
- **Changed:**
  - CsvHelper.Excel: the four bugs below, fixed; the two decisions below; `MemberFormats`' constructor internal, and an internal `ExcelWorkbookWriter.AddSheet(string, MemberFormats)`, so that the format fuzzer writes columns of formats no type has (as `MaxRows` is internal for the row limit's tests).
  - `XmlOutputTests` and `SheetNamesTests`: cases for the fixes and the decisions; `XmlOutputTests`' random text has `_x0041` and `_x00e9` among its pieces.
  - Core: no change.
- **The fuzzers:**
  - **Text** (`WritesAndReadsBackRandomText`): workbooks of one to six rows of one to six fields, each written as a string, a span or UTF-8 bytes, or null. Their pieces: letters, numbers, white space, CR, LF and CR LF, the CSV writer's specials, entities and markup, `_x0041_` and its near misses (`_X0041_`, `_x004_`, `_x0041`, `_x005F_`, `_xD800_`), the C0 and C1 controls, U+FFFE, U+FFFF, U+FDD0, the BOM, accents composed and not, and surrogates paired, lone and reversed. The UTF-8 fields add sequences of their own: lone continuation bytes, overlong forms, encoded surrogates, past U+10FFFF, cut, and the valid ones at their edges. One field in 14 is a run of one character to 32,765–32,769 code units, now and then ending in a surrogate pair across the limit. Inline or shared, sync or async; a field met before, again now and then.
    - Each workbook passes the validator, and reads back through CsvHelper.Excel's reader as written, with U+FFFD for what was invalid (the text's UTF-8 round trip). A field past 32,767 throws `InvalidOperationException` with its count and leaves no cell. With shared strings, the part counts a reference per text cell and holds each distinct text once.
    - 500 workbooks of 6,207 fields: 179 refused for their length, 208 within three code units of it, and 1,181 with invalid UTF-16 or UTF-8.
  - **Names** (`AcceptsOrRefusesRandomSheetNames`): workbooks of one to eight names, random or alike to one added: the same name, its NFC or NFD form, its ASCII letters in the other case (but before a combining mark), in upper or lower case, or longer. The pieces: words, Excel's own names, white space, punctuation, the seven forbidden characters, apostrophes, escapes, copies' suffixes and their near misses, controls, invisible characters and non-characters, letters in their cases (ß and ẞ, σ and ς, the Kelvin sign, the dotted and dotless I with U+0307, DŽ, Dž and dž, Ω and the Ohm sign), compatibility forms, scripts, and surrogates; lengths 1 to 35, now and then cut through a pair.
    - The test's own rules (1 to 31 code units and 31 as written, none of `\ / ? * [ ] :`, no apostrophe first or last, no control characters or lone surrogates): a name they refuse throws `ArgumentException` for `name`; one they accept is added, or refused with a message that names a sheet the workbook has. A name alike to a sheet but for normalization or ASCII case is refused, and no two of the workbook's sheets are alike in case, as they are or once normalized.
    - Each sheet has one to seven records at two rows a sheet (`MaxRows`, `RowLimit = NewSheet`), so copies are named too: each passes the rules and keeps the longest start of the sheet before it that fits. The workbook passes the validator, and CsvHelper.Excel reads its names and each sheet's records across its copies.
    - 500 workbooks: 570 names added, 251 copies (8 with their stem cut), 1,410 names refused by the rules and 272 as alike to a sheet.
  - **Formats** (`TranslatesRandomFormatsOrLeavesThemText`, `WritesRandomFormatsAsTheirText`): random .NET formats: the standard ones and letters that are none (`Q`, `K`), and custom ones of one to six pieces (every token in its lengths, separators, literals quoted and escaped, `%`, letters that aren't tokens, quotes and escapes left open, CJK). In 25 cultures (the invariant, Gregorian ones of each kind of names and designators, and Hijri, Persian and Thai Buddhist ones), for `DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly`, with values at Excel's edges (1899-12-30 and 31, 1900-01-01, 1900-02-28 and 03-01, 9999-12-31's last tick, half a millisecond before midnight) and away from them, of every `DateTimeKind` and offset.
    - The translator throws nothing. It gives no code, or one that `FormatCheck` passes, that the reader takes for a date's, and whose width measures; and none where the test's own reading of the format says Excel can't show it (another calendar, a day or a month without a year, an offset, an era, `F`, four or more fraction digits, a year of one, three or five digits, `U`).
    - Reading a value's text back never keeps a value whose text differs: the cell's value, as the member's type, formats to the field's text, as a `DateTime` of no kind and in UTC and a `DateTimeOffset` at two offsets, since the cell keeps neither. So it is for the text in upper and lower case, which .NET parses and doesn't write. A time of day never reads back as before 1900.
    - End to end: workbooks of one to six columns of random formats and cultures, and one to five rows of fields of the column's kind or another, in its format or another, under each `DateTimeOffsets`. Each cell is a date of the translation's code and the serial read back, or a text cell of the field's text, or none for an empty one, as the translation says; the workbook passes the validator.
    - 3,000 formats: 393 translated, 1,699 not as the rules say, 908 not for other reasons; 1,286 values typed, and 154 left text (122 before 1900, 32 not read back); 150 workbooks of 147 date cells and 686 text cells.
  - **Formulas** (`ChecksRandomFormulasConsistently`, `WritesAcceptedFormulasAsTheirText`): random formulas of the grammar's pieces (references, names and functions, strings and quoted names open and closed, structured references and their escapes, arrays, every bracket, operators, white space, CR and LF, tabs, controls, escapes, lone surrogates), parentheses up to 4,096 deep, and lengths around 8,192, with or without `=`.
    - `FormulaCheck` throws nothing. The constructor throws nothing but `ArgumentException` for `formula`, exactly when the check finds a problem, with its message. `Parse` throws `FormatException` where `TryParse` fails, and gives the constructor's formula otherwise.
    - An accepted formula is its text again, has no CR outside its strings, and keeps the relations the checks imply: with `)`, `(`, a quote or an apostrophe more, an operator at either end, or a tab or U+0001 after it, it is refused; in parentheses, with CR LF after it (as LF), padded with spaces to 8,192 or with a string of anything appended with `&`, accepted; padded past 8,192, refused.
    - The accepted ones, written to workbooks (`WriteFormula` and the field itself), pass the validator and read back as their text, a lone surrogate as U+FFFD.
    - 20,000 formulas: 4,119 accepted, every relation checked for each, and all written and read back.
  - **Memory** (`WriterSecurityTests`):
    - `StreamsSheetsOfLongInlineCellsInABoundedBuffer`, sync and async: 64 rows of four cells of 32,767 characters, of one and three bytes, of CR (7 as written) and `&` (5): 512 KiB of XML a row, 32 MiB in all, to a stream that counts its bytes and can't seek, at `NoCompression`. What has been written and hasn't reached the stream stays within a flush, two records and 1 MiB: measured, 512 KiB at most (the first data record, held for the widths). On .NET, the last 56 rows allocate under 256 KiB: measured, nothing in Release (18–23 KiB in Debug).
    - `KeepsAMillionDistinctSharedStringsInTheArena`: a million distinct `<t>` elements (13.9 MB) take a table of at most twice their bytes and 32 bytes a string: measured, 33.5 MB (a 16 MiB arena, and 4, 4 and 8 MiB of integers). Growing it allocates at most twice the table and 64 KiB: measured, 67.1 MB.
    - `WritesAMillionDistinctSharedStringsInTheTablesMemory`: the same strings through a workbook allocate at most twice that table and 8 MiB: measured, 67.3 MB.
- **Found and fixed,** the first three in Excel too (checked through COM on 2026-11-07, from C# probes, since PowerShell hung in the session; the probes and their answers are in `Csv\_bench\results\phase4\step6\probes`):
  - **An escape's start before an escaped character** (the text fuzzer, at once). `_x0041` before a character written as an escape (a control, CR, U+FFFE, U+FFFF) became `_x0041_x0000_`, whose `_` closed it into an escape. CsvHelper.Excel read `A` and `x0000_`, and so did Excel: by hand, a name `_x0041_xFFFF_z` read `AxFFFF_z` and a cell `_x0041_x0001_b` read `Ax0001_b`. `XmlOutput` now escapes that `_` too (`_x005F_x0041_x0000_`), in cells, shared strings, formulas, names and number formats; Excel reads the name as written. `XmlOutputTests`' random test made its text of single characters, and missed it.
  - **A name with U+FFFE threw `ArgumentException` for `strInput`** (the name fuzzer). `string.Normalize` refuses non-characters: U+FFFE on .NET (ICU), and every one on .NET Framework (U+FFFF, U+FDD0, U+1FFFE too); unassigned code points and new emoji it takes. The key now normalizes the runs between them (they compose with nothing) and keeps them, the same on every target. Excel opens names with U+FFFE, U+FFFF and U+FDD0.
  - **`I` U+0307 beside `i` U+0307** (the validator, in the name fuzzer, which compares names in case): the key took them to be different, since NFKC composes the first to `İ` and not the second, so the writer wrote both, and Excel refuses that workbook. So it does `İ` beside `i` U+0307, while it opens `i` beside `İ`. The key is now NFKC and upper case, twice, as Unicode's caseless match does it.
  - **Formats .NET can't format** (the format fuzzer): `tt%"o'clock"'it\'s']'it\'s'` (`%` before a quote) was translated, and its width threw `FormatException`. The sink never measures it (`TryRead` fails first on such a pattern), but the translator now refuses what .NET can't format, by formatting a value with it.
- **Decisions made while building,** each recommended, and approved as built:
  - **A name's 31 counted as written too.** An `_` that would start an escape is written `_x005F_`, and U+FFFE and U+FFFF `_xFFFE_` and `_xFFFF_`, 7 code units each, and the schema limits the attribute to 31: the validator failed `_x0041_` and 24 letters. Excel counts the decoded name, and opens such names (31 decoded, 37 and 43 written). Recommended: refuse them, as the writer is stricter than Excel for control characters, and cut copies to fit (`SheetNames.CopyName`), so that every file passes the validator. Alternative: accept them, and let the validator's error stand for them.
  - **Formulas keep white space at either end,** with `xml:space="preserve"` on `f`, as text cells have it. Readers trim `f` otherwise, as Excel does (Phase 2): `= #N/A!` read back as `=#N/A!`, and `=A1` LF as `=A1`. Excel writes the attribute on a formula that starts with a space, and with the writer's it keeps the white space in front and drops what follows; every probe formula computed. The validator takes it, and the written set has no such formula, so its parts are unchanged. Alternative: no attribute, and the fuzzer expects formulas trimmed.
- **Found, not fixed: .NET's UTF-8 formatting of an escaped non-ASCII literal.** On .NET 8 and 10, `DateTime`'s and `DateTimeOffset`'s `IUtf8SpanFormattable.TryFormat` writes a non-ASCII character escaped with a backslash (also inside quotes) as its low byte: `\é` as E9, which isn't UTF-8, and `\年` as `t`. With a surrogate pair it throws `ArgumentOutOfRangeException`. `ToString` writes them whole, and quoted or bare literals are fine. CsvHelper's CSV writer formats there: `WriteField(date, "yyyy\\年")` writes `2024t`, and CsvHelper.Excel gets the same bytes (as U+FFFD, after its UTF-8 check). A workaround (such a format through UTF-16) is core's, and needs Phase 0's gate, so it is the user's to decide; step 6's review left it open, with no recommendation made for it. Decided 2026-11-07, as recommended then: a core workaround after Phase 4, behind Phase 0's gate (see Phase 5's first item). The format fuzzer leaves such characters unescaped on .NET.
- **Runs beyond the defaults** (net10.0, after the fixes): 6,000 text workbooks, 15,000 name workbooks, 90,000 formats with 4,500 workbooks: no failure.
- **Tests:** 34 new on each target (2,718 on net10.0 and net8.0, 2,670 on net48), all passing, with the whole solution's. Beside the suite, the text fuzzer takes 12 to 21 s, the names' 7 to 18 s, the formulas' and the formats' end to end up to 14 s (on .NET Framework), the rest under 7 s; none has a time limit, so none needs `TimedCollection`.
- **Mutation check** on net10.0, each bug planted alone, its file restored and checked by SHA-256 (the driver, `mutate.py`, and its results are in `Csv\_bench\results\phase4\step6`):
  - **Which tests:** each bug ran step 6's fuzz and security tests that reach its file, and the file's own unit tests, so that the results say which kind caught it. The suite's other tests reach these files too, but these are the ones that compile against them.
  - **First run** (`mutations.jsonl`), 44 bugs: 41 caught, 36 of them by the fuzzers or the memory tests. Three lived:
    - "a copy's name taken twice" and "a text that formats otherwise kept": their files' unit tests (`RowLimitTests` for `ExcelWorkbookWriter.cs`; `FormatTranslatorTests` and `FormattedCellsTests` for `DateFormat.cs`) weren't in the run;
    - "copies cut between a pair's halves": it needs a stem that ends in a surrogate pair and is one code unit past 31 as written, which the fuzzer rarely makes.
  - **Then:** `SheetNamesTests` has that stem; the formula fuzzer checks `#` and `@` at the end; the format fuzzer's rules have a day or a month without a year, and it reads each text back in upper and lower case too (.NET parses either, and writes one); the two files' runs have their unit tests. The second run (`mutations-second.jsonl`) reran the eight bugs that lived or that only unit tests caught, the third (`mutations-third.jsonl`) `DateFormat.cs`'s two: every one caught, the baseline passing.
  - **In all, 44 of 44 caught, 39 by the fuzzers or the memory tests.** The five only unit tests catch:
    - "non-characters dropped from the key" takes `a` U+FFFE and `a` U+FFFF to be one name: a refusal stricter than Excel's, which the name fuzzer allows by design (the writer may be stricter, never less); `SheetNamesTests` checks the key.
    - "names of 32 accepted": the written length refuses them all the same, with its own message.
    - "copies cut between a pair's halves": the new case.
    - "a copy's name taken twice": the fuzzer seldom adds a sheet named as a later sheet's copy; `RowLimitTests` does.
    - "months before seconds taken for months": whether Excel reads `mm` as minutes is Excel's to say, which a .NET oracle can't; `FormatTranslatorTests` and step 5's corpus check the codes.
  - **Failing tests per bug** (the last run of each):
    - `XmlOutput.cs`: an escape's start before an escaped character left as it is 9; CR written as it is 13; U+FFFE and U+FFFF written as they are 7; `<` written as `&gt;` 5; `f` not a hexadecimal digit 1; cells counted in bytes 10; U+FFFE one code unit as written 4; a trailing LF not preserved 3; `_X` taken for an escape's start 3.
    - `SheetSink.cs`: invalid UTF-8 written 1; 32,767 code units refused 3; formulas never preserved 1; shared references counted per record 1; flushed only at a sheet's end 3; formatted dates before 1900 typed 1; formatted offsets typed under `Utc` 1; formulas not escaped 1.
    - `SharedStringTable.cs`: equal hashes taken for equal strings 2; the arena grown by 4 KiB 2; the starts grown by 64 3.
    - `SheetNames.cs`: U+007F to U+009F allowed 4; an apostrophe last allowed 2; the written length unchecked 2; normalized and upper-cased once 4; non-characters dropped from the key 1; copies cut by characters alone 2; copies cut between a pair's halves 1; names of 32 accepted 2. `ExcelWorkbookWriter.cs`: a copy's name taken twice 4.
    - `FormatTranslator.cs`: other calendars translated 2; formats .NET can't format translated 1; offsets translated 3; three-digit years translated 2; months before seconds taken for months 2; dates without a year translated 5.
    - `FormulaCheck.cs`: tabs allowed outside strings 5; CRs kept outside strings 3; a string's end searched from its quote 13; nesting past 64 on the stack 2; `#` allowed at the end 2.
    - `ColumnFormat.cs`: a translation kept across kinds 1; a style kept across kinds 1. `DateFormat.cs`: a text that formats otherwise kept 2; times of day as dates 22.
- **Core:** no change. The solution builds with 0 warnings on every target.

**Step 7, benchmarks** (built, run, approved and committed 2026-11-07):

- **New:** `bench/CsvHelper.Benchmarks/ExcelWriteBenchmarks.cs`: `ExcelPackageAssetsWrite` and `ExcelOrdersWrite`, their base `ExcelWriteBenchmarkBase`, `CountingStream` (it counts the bytes and drops them, and can't seek, as a response body can't, so that every writer streams its package; `verify` has it keep them), and `OrderRowsDataReader`, the orders for MiniExcel.
- **Changed:**
  - `BenchConfig.cs`: a Written column (`WrittenSizeColumn`, through `WrittenSizeConfig` on the two classes), the size of what each benchmark writes, from one more run of it in the host process; the MB/s column knows the new base.
  - `QuickRunner`: `verify` reads each write benchmark's output back as records, a workbook through CsvHelper.Excel's reader and CSV through CsvHelper's, so that a class's writers must agree; `quick` shows their MB/s.
  - The project references SpreadCheetah 1.28.0 and LargeXlsx 2.0.2, the corpus's versions; it had MiniExcel 1.46.0.
  - The design's last item, the benchmarks' `ExcelStrings` giving way to the library's, was done at step 1.
  - CsvHelper.Excel and core: no change.
- **The writers,** each to a `CountingStream` of its own:
  - CsvHelper.Excel with inline strings at `Fastest` (its defaults), with shared strings, and with inline strings at `Optimal`. The package assets go through `AddSheet`'s `CsvWriter`, field by field, as `PackageAssetsWrite` writes CSV; the orders through `WriteSheet<OrderRecord>`, with `DateTimeOffsets = Utc`, so that `ShippedAt` is a date, as the others write it.
  - SpreadCheetah (the baseline), LargeXlsx and MiniExcel with their defaults: SpreadCheetah and LargeXlsx at their fastest level, MiniExcel at the one it has. SpreadCheetah fills one array of `DataCell`s per row, through its asynchronous methods, which are all it has; LargeXlsx writes cell by cell; MiniExcel takes a data reader, of strings or of the orders' typed values, since it fills properties and `OrderRecord`'s members are fields. In the orders, `CustomerId` is its string, `ShippedAt` its UTC date, and the dates are in `ExcelOrders.DateFormat` (MiniExcel's in its own).
  - CsvHelper's CSV writer, the same data as CSV, as the reference; its size is the MB/s column's.
- **Checked:** `verify` read every writer's output back the same: 1,000,000 records of hash `A46BD7E4C52574F1` from each package assets workbook and the CSV, and 1,000,000 orders of hash `199B37C22F244824` from each orders workbook and the CSV.
- **The run:** BenchmarkDotNet 0.15.8, .NET 10.0.13, a Ryzen 9 9950X3D pinned to core 4, 4 warm-ups and 12 iterations. The paged pool was 2.5 GB, with 37 GB free; total CPU, logged every 4 s, was above 12% only while the host set up a class or measured the Written column, and in two lone samples. The reports, the runner, the CPU log and the probes below are in `Csv\_bench\results\phase4\step7`. Allocated counts 1 KB as 1,024 B, as BenchmarkDotNet does; Written counts 1 MB as 10⁶ B.
  - `ExcelPackageAssetsWrite` (25 text columns; the CSV is 306.0 MB):

    | Writer | Mean | Ratio | MB/s | Written | Allocated |
    |---|---:|---:|---:|---:|---:|
    | SpreadCheetah | 624.0 ms | baseline | 490 | 58.6 MB | 11.2 KB |
    | CsvHelper.Excel | 1,276.5 ms | 2.05x slower | 240 | 139.2 MB | 7.3 KB |
    | CsvHelper.Excel, shared | 1,376.7 ms | 2.21x slower | 222 | 100.6 MB | 387.7 KB |
    | CsvHelper.Excel, `Optimal` | 4,392.2 ms | 7.04x slower | 70 | 79.0 MB | 7.3 KB |
    | LargeXlsx | 1,438.8 ms | 2.31x slower | 213 | 195.4 MB | 103.3 KB |
    | MiniExcel | 3,957.8 ms | 6.34x slower | 77 | 195.2 MB | 9.29 GB |
    | CsvHelper, CSV | 136.3 ms | 4.58x faster | 2,246 | 306.0 MB | 440 B |

  - `ExcelOrdersWrite` (numbers, dates, a boolean and text; the CSV is 191.0 MB):

    | Writer | Mean | Ratio | MB/s | Written | Allocated |
    |---|---:|---:|---:|---:|---:|
    | SpreadCheetah | 960.5 ms | baseline | 199 | 132.1 MB | 91.6 MB |
    | CsvHelper.Excel | 1,497.4 ms | 1.56x slower | 128 | 176.1 MB | 7.3 KB |
    | CsvHelper.Excel, shared | 1,662.3 ms | 1.73x slower | 115 | 175.3 MB | 160.0 MB |
    | CsvHelper.Excel, `Optimal` | 4,754.9 ms | 4.95x slower | 40 | 115.1 MB | 7.3 KB |
    | LargeXlsx | 1,424.7 ms | 1.48x slower | 134 | 176.1 MB | 91.7 MB |
    | MiniExcel | 2,461.2 ms | 2.56x slower | 78 | 189.2 MB | 4.08 GB |
    | CsvHelper, CSV | 123.7 ms | 7.77x faster | 1,544 | 191.0 MB | 464 B |

- **What it shows:**
  - At its defaults, CsvHelper.Excel writes a million rows in 1.28 s (the package assets) and 1.50 s (the orders): 2.05x and 1.56x SpreadCheetah's time, about LargeXlsx's, and faster than MiniExcel's. It allocates 7.3 KB a workbook at either level; with shared strings, its table besides (388 KB for the package assets' distinct strings, 160 MB for a million distinct `CustomerId`s). SpreadCheetah's and LargeXlsx's 92 MB in the orders are `CustomerId`'s strings, one per row; MiniExcel boxes every value.
  - `Optimal` costs CsvHelper.Excel 3.4x its time for 57% of the size (the package assets), and 3.2x for 65% (the orders), so `Fastest` stays the default (decision 22).
  - **Deflate is half of the time.** Measured alone (each sheet's XML from its written workbook through `DeflateStream`, in one buffer, on core 4, the best of three; `probes\deflatetime`):

    | Sheet XML | Size | `Fastest` | `Optimal` |
    |---|---:|---:|---:|
    | Package assets, CsvHelper.Excel's | 1,090.2 MB | 139.2 MB in 642 ms | 78.9 MB in 3,880 ms |
    | The same without `r` | 887.6 MB | 47.7 MB in 299 ms | 27.3 MB in 972 ms |
    | Package assets, SpreadCheetah's | 1,226.9 MB | 58.6 MB in 370 ms | 33.6 MB in 1,254 ms |
    | Orders, CsvHelper.Excel's | 569.4 MB | 176.1 MB in 888 ms | 115.1 MB in 4,118 ms |
    | The same without `r` | 421.0 MB | 128.1 MB in 682 ms | 78.5 MB in 2,811 ms |
    | Orders, SpreadCheetah's | 448.6 MB | 132.1 MB in 704 ms | 81.5 MB in 2,859 ms |

  - So the rest, making the XML (the CSV writer's text, the sink's cells, escaping and references), takes about 635 ms for the package assets and 610 ms for the orders, where SpreadCheetah's takes about 255 ms (its time less its deflate's).
  - **The cell references cost the most** (decision 23). `r` on each cell and row (`<c r="Q123456">`) breaks deflate's matches: without them the package assets' sheet deflates to 47.7 MB instead of 139.2 MB at `Fastest`, a third, in 299 ms instead of 642 ms, and at `Optimal` to 27.3 MB in 972 ms instead of 79.0 MB in 3,880 ms. The orders, whose numbers and Guids compress less anyway, go from 176.1 to 128.1 MB. SpreadCheetah writes rows' numbers and no cells' references by default; its sheet is bigger (an empty text cell for each empty field) and deflates to 58.6 MB. LargeXlsx and MiniExcel write the references as CsvHelper.Excel does, and their files are as big. The figures as written match the writer's files to the byte, which checks the method (`probes\rattr` strips the attributes from a stream; `probes\sizes` saves each writer's workbook).
- **Gate:** "the write benchmark's numbers are recorded" is met. Phase 4 sets no speed target; Phase 9 starts from these numbers (see its "Phase 4's write baseline").

### Phase 5: presentation (M)

Status: the detailed design below was written on 2026-11-08, after the Excel and LibreOffice checks under "Checked on 2026-11-08", and approved that day, with every recommendation accepted. Its first item, core's UTF-8 date literals, was built, approved and committed that day (`146257b1`); see its "As built". Step 1 (settings) was built that day, and approved, with every recommendation, and committed 2026-11-09; see "As built" at the end of this phase. Step 2 (styles and the header) was built, approved, with every recommendation, and committed 2026-11-09: the bold header, the frozen pane, the title and the columns' options show. Step 3 (auto-fit) was built, approved, with every recommendation, and committed 2026-11-09: columns are as wide as their held cells, as Excel's AutoFit makes them, and no typed cell shows `########` in Excel. Step 4 (filters, merges and tables) was built, approved, with every recommendation, and committed 2026-11-10: filters with their hidden names, tables whose header Excel's own rule names, and merged cells open in Excel and LibreOffice, and `NewSheet` repeats the filter and the table. Step 5 (links) was built, approved, with every recommendation, and committed that day: `Uri` fields are links in the `Hyperlink` style, as the policy says, within Excel's limits. Step 6 (corpus and oracles) was built, approved, with every recommendation, and committed 2026-11-11: ten presentation workbooks in the `written` set, with Excel's answers about their look and its own AutoFit, ClosedXML and LibreOffice compared beside them, and a writer fix found by LibreOffice (a header cell in General says so); see its "As built". Step 7 (security, fuzzing and AOT) was built, approved, with every recommendation, and committed 2026-11-11: fuzzers of presentations and of Excel's format codes, opened in Excel on demand, which found six writer bugs (fixed) and gave `FormatCheck` Excel's rules; durations past Excel's last serial as text; the links' memory measured; and the NativeAOT check in CI; see its "As built". Step 8 (benchmarks) was built, approved, with every recommendation, and committed 2026-11-12. With Phase 4's look the writer writes Phase 4's bytes: as fast for the orders, and 2.4% faster for the package assets. The defaults and every setting a sheet writes once cost under 1%. A column's format or wrap costs 2–6%, and a sheet's 65,530 links 2.1%. See its "As built".

Scope, as planned:

- **Before this phase: core's UTF-8 date literals** (found at Phase 4's step 6; the user's decision, 2026-11-07, as recommended):
  - On .NET 8 and 10, `DateTime`'s and `DateTimeOffset`'s UTF-8 `TryFormat` writes a non-ASCII character escaped with a backslash in a custom format (also inside quotes) as its low byte, and throws `ArgumentOutOfRangeException` for a surrogate pair; `ToString` writes them whole. Core's `CsvWriter.WriteField<T>` with a format, which the record writers call for `[CsvFormat]`, formats there: `yyyy\年` writes `2024t`, and `yyyy\é` a lone byte E9. The probes are in `Csv\_bench\results\phase4\step6\probes`.
  - The workaround: a format with a backslash before a non-ASCII character is formatted through UTF-16 and transcoded to UTF-8. The check is a scan of the format, made only when one is given, so that the default path and every other format are unchanged.
  - First check which other types' UTF-8 formatting does the same (`DateOnly`, `TimeOnly`, `TimeSpan`, and the `WriteField(object)` and `TextWriter` paths), and that the netstandard and .NET Framework builds, which have no `IUtf8SpanFormattable`, aren't affected.
  - It is a core change, so it passes Phase 0's gate, with care for `WriteField`'s sensitivity to added IL. Reporting the bug to dotnet/runtime wasn't chosen.
  - **As built** (built 2026-11-07; gated, approved and committed 2026-11-08, the user accepting the cost below):
    - **What the checks found:** every type .NET formats as a date, time or duration does it: `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan`, whether the writer writes to a `Stream` or a `TextWriter` (both format into its UTF-8 buffer), and in the record writers' `[CsvFormat]`. Numbers' formats aren't affected, nor is reading. `WriteField(object)` writes dates in their default format, which has no literal, so it isn't affected. The netstandard and .NET Framework builds format through `ToString`, so they aren't either.
    - **The fix,** in `CsvWriter.cs`: both .NET `WriteField<T>` overloads that take a format call `TryFormatField` where they called the value's `TryFormat`, and nothing else in them changes. `TryFormatField` is inlined, and for every type but the five it folds to that same `TryFormat`. For the five, a format longer than one character that has a character past ASCII and a backslash (`NeedsUtf16`) goes to `TryFormatThroughUtf16`, which isn't inlined. It formats as UTF-16 into 256 characters on the stack, or pooled arrays of twice the length until the text fits, and transcodes to UTF-8. The test is wider than the bug (a backslash anywhere, not only before the character), which is safe, since the UTF-16 path is right for every format.
    - **Two versions failed the gate first,** as `WriteField`'s sensitivity to added IL predicted:
      - An `else if` for dates in `WriteField` grew `WriteField<float>`'s IL from 481 to 524 bytes and changed what the floats' loop inlined: `FloatsWrite` +11–12%, and dates in custom formats +7.5%.
      - All dates through one helper that isn't inlined kept floats level, but added a call to each date in its default format: +16–30%.
    - **Tests:** `WriterTests.WritesEscapedLiteralsPastAsciiAsToStringDoes` writes dates, times and durations in formats with escaped literals past ASCII (CJK, `é`, an emoji's surrogate pair, inside quotes, after an escaped backslash, several at once, and 30,000 in one format) and in plain formats. It writes each through a stream, a `TextWriter`, a nullable and a de-DE provider, and checks it against `ToString`. `WritesRecordsWithEscapedLiteralsPastAsciiAsToStringDoes` does the same for a record's `[CsvFormat]`s. Both fail without the fix. Phase 4's format fuzzer no longer skips such formats on .NET (`WrittenFormat` is gone), and without the fix it fails at seed 112. Every suite passes on every target, with 0 warnings: core 1,311 (.NET 10), 1,306 (.NET 8) and 846 (.NET Framework); CsvHelper.Excel 2,718, 2,718 and 2,670, each with the 4 GiB Zip64 test skipped as opt-in; CsvHelper.Linq 112, 110 and 101.
    - **Benchmark:** `FormattedDatesWrite`, beside `FormattedDatesRead`: 200,000 records' dates, times and durations through `[CsvFormat]` (Records), through `WriteField(value, format)` (Fields), without a format (Default), and in `yyyy\年MM\月dd\日` (Escaped).
    - **The gate** (Phase 0's: base `69907e65`, with the new benchmark copied in, against new; `Csv\_bench\results\phase5\utf8-dates`):
      - BenchmarkDotNet, base/new/new/base: `FloatsWrite` −1.1%; `PackageAssetsWrite` within ±1% for CsvHelper; dates without a format −0.6%; dates in custom formats +2.3% through Fields, and +8.0% through Records, whose two new processes disagreed by 7% (+10.9% and +5.1%).
      - So all four were run again in 24 fresh QuickRunner processes, base/new/new/base six times: Records +4.0% (medians 27.39 and 28.48 ms; every new process took 28.0–29.2 ms, every base one 27.1–28.2 ms), Fields +1.7%, Default +0.3%.
      - NCsvPerf, whose reading the fix doesn't touch, as a control over six pairs: single-threaded +1.0% overall, with each side's runs anywhere from 350 to 375 ms (the first pair's +5% didn't repeat); multithreaded level.
    - **The cost:** about 1.1 ns for each field written in a custom date or time format: 4% of a record of five such fields, and 1.7% through `WriteField`. Everything else is level. A format that needs UTF-16 takes 26–36% longer than before, when its output was wrong.

- **Attributes and options:** `[ExcelColumn]`, `[ExcelSheet]` and `ExcelSheetOptions.Columns` for formats, widths, wrap and hidden columns. `[ExcelColumn(Format)]` and `Columns`' `Format` give Excel's format codes directly, for display only, beside Phase 4's formats from `[CsvFormat]`, and Phase 4's `FormatCheck` checks them. They are how a number gets a format, since `[CsvFormat]` doesn't apply to numbers (Phase 4's step 4).
- **Auto-fit:** `<cols>` comes before the rows, so auto-fit buffers the first N rows to estimate widths, then writes them. It widens Phase 4's widths, which cover the first data record's typed cells, to every column over the first N rows. Without auto-fit, widths come from attributes, options and Phase 4's typed columns only.
- **Header and view:**
  - Bold header style (Phase 4 writes the header as plain text cells).
  - Frozen header row (`<sheetViews>`, before the rows).
  - Autofilter, written after the rows once the range is known.
- **Hyperlinks** from `Uri` members, collected while streaming and written after the rows with their relationships. Phase 4 writes a `Uri`'s text.
- **Merged cells** on the sheet writer (e.g. a title row above the header), written after the rows.
- **Excel Table output:** a table part with a style, row banding and filter buttons.
- **Column default styles** (`<col style>`), so formats also apply to cells users add later.
- **From Phase 4** (its "Left open"): `[ExcelColumn(Format)]` and `Columns` as sources of formats, numbers' among them; `FormatCheck`'s `[DBNum3]` and backtick. And from Phase 1: the NativeAOT publish check that the attribute reading brings.

#### Public API

Phase 5 adds a sheet's options, two attributes, a table's and a column's options, a link policy and a merge method. Phase 4's methods that add a sheet gain an optional `ExcelSheetOptions`.

```csharp
public sealed class ExcelWorkbookWriter
{
	// Phase 4's, each with the sheet's options: null for the record type's [ExcelSheet], else ExcelWriterOptions.Sheets.
	public void WriteSheet<T>(string name, IEnumerable<T> records, ExcelSheetOptions? options = null);
	public ValueTask WriteSheetAsync<T>(string name, IEnumerable<T> records, ExcelSheetOptions? options = null, CancellationToken cancellationToken = default);
	public ValueTask WriteSheetAsync<T>(string name, IAsyncEnumerable<T> records, ExcelSheetOptions? options = null, CancellationToken cancellationToken = default);
	public CsvWriter AddSheet(string name, ExcelSheetOptions? options = null);
	public CsvWriter AddSheet<T>(string name, ExcelSheetOptions? options = null);
}

public sealed class ExcelWriterOptions
{
	// Phase 4's, and:
	public ExcelSheetOptions Sheets { get; init; } = ExcelSheetOptions.Default;
	public ExcelLinks Links { get; init; } = ExcelLinks.Web;
}

public enum ExcelLinks
{
	Web,	// http, https and mailto URIs are links, others text
	All,	// every URI is a link: file:, other schemes and relative ones too
	None,	// URIs are text, as in Phase 4
}

public sealed class ExcelSheetOptions
{
	public static ExcelSheetOptions Default { get; }
	public string? Title { get; init; }
	public bool BoldHeader { get; init; } = true;
	public bool FreezeHeader { get; init; } = true;
	public int FreezeColumns { get; init; }
	public bool AutoFilter { get; init; }
	public ExcelTableOptions? Table { get; init; }
	public int AutoFit { get; init; } = 100;
	public double MaxAutoFitWidth { get; init; } = 60;
	public IReadOnlyDictionary<string, ExcelColumnOptions>? Columns { get; init; }
}

public sealed class ExcelTableOptions
{
	public string? Name { get; init; }
	public string? Style { get; init; } = "TableStyleMedium2";
	public bool BandedRows { get; init; } = true;
	public bool FilterButtons { get; init; } = true;
}

public sealed class ExcelColumnOptions
{
	public double? Width { get; init; }
	public string? Format { get; init; }
	public bool Wrap { get; init; }
	public bool Hidden { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ExcelColumnAttribute : Attribute
{
	public double Width { get; set; }	// 0 for none
	public string? Format { get; set; }
	public bool Wrap { get; set; }
	public bool Hidden { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ExcelSheetAttribute : Attribute
{
	// ExcelSheetOptions' settings but Columns, which members' [ExcelColumn] give, and the table's as three.
	public string? Title { get; set; }
	public bool BoldHeader { get; set; } = true;
	public bool FreezeHeader { get; set; } = true;
	public int FreezeColumns { get; set; }
	public bool AutoFilter { get; set; }
	public bool Table { get; set; }
	public string? TableName { get; set; }
	public string? TableStyle { get; set; } = "TableStyleMedium2";
	public int AutoFit { get; set; } = 100;
	public double MaxAutoFitWidth { get; set; } = 60;
}

public static class ExcelWriterExtensions
{
	public static void WriteFormula(this CsvWriter writer, string formula);	// Phase 4's
	public static void MergeCells(this CsvWriter writer, string range);
}
```

**Where the settings come from** (decision 2): a sheet's are the `options` argument; else, for `AddSheet<T>` and `WriteSheet<T>`, `T`'s `[ExcelSheet]`; else `ExcelWriterOptions.Sheets`. Each is taken whole, not merged with the others. A column's are its entry in `Columns`, by its header name, else its member's `[ExcelColumn]`, also whole. They are checked when the sheet is added, and a bad one throws `ArgumentException`, as a bad sheet name does.

**What a sheet gets** (decision 1 for the defaults):

- **A title** (`Title`, none): row 1 holds it, in bold Calibri 14 (Excel sizes the row on open), merged across the header's columns once the header is known (no merge for a header of one column), and the header and the records follow from row 2. The sheet's `CsvWriter` doesn't see row 1: its first record is still the header, so `WriteRecords` writes it (decision 3). 1 to 32,767 characters.
- **The header:** the writer's first record, with `HasHeader`. Without `HasHeader` there is none (decision 11): `BoldHeader` and `FreezeHeader` do nothing, `AutoFilter` and `Table` throw, and `Columns` match the names that `T`'s record writer writes in its header, as Phase 4's formats do.
- **A bold header** (`BoldHeader`, on): the header's cells in bold Calibri 11; not in a table, whose style sets its header.
- **A frozen header** (`FreezeHeader`, on): the rows through the header stay in view as the sheet scrolls, and the first `FreezeColumns` columns too (`sheetViews` with a frozen pane, before the columns).
- **An autofilter** (`AutoFilter`, off): filter buttons on the header, over it and every row below (`autoFilter` after the rows, and Excel's hidden `_xlnm._FilterDatabase` name for it, as Excel writes it). With a table, the table's buttons are the filter (decision 9).
- **A table** (`Table`, none): the header and the rows below it are an Excel table, named `Name` or after the sheet (decision 8), in `Style` (one of Excel's 60 built-in styles, `TableStyleLight1` to `21`, `TableStyleMedium1` to `28` and `TableStyleDark1` to `11`, in any case; another name throws, since Excel shows it unstyled; null for no style), with banded rows and filter buttons as asked. Excel refuses a workbook that breaks its rules (checked):
  - A name is unique in the workbook ignoring case; it starts with a letter, `_` or `\`, then has letters, digits, `_`, `.`, `?` and `\`; at most 255 characters; and it isn't a cell reference (`A1`, `XFD1048576`, `R1C1`), `R` or `C` followed by a digit (`R2D2`), `R`, `C`, `RC`, `TRUE` or `FALSE`. A name given that breaks them throws.
  - The header's cells are text equal to the table's column names, unique ignoring case, at most 255 characters. An empty one is named `Column` and its number, a number cell becomes its text, and a repeated one gets a number (`Name2`), as Excel does when it makes a table (decision 10); a longer one throws. (As built at step 4: Excel's exact rule, as probed, with one number for the whole header, so `[Name, Name, Name2]` is `Name, Name3, Name2`; a typed field is named by its text as written; see its "As built".)
  - A table has a row below its header: a sheet with only a header gets a table over it and one empty row. No merged cell may be in a table.
  - With `RowLimit.NewSheet`, each sheet gets its own table, named with a number after the first (`Orders2`), and its own title. (As built at step 4: and its own filter; the number takes a `_` before it when the name would read as a cell reference, `AB_2`.)
- **Auto-fit** (`AutoFit`, 100 rows; 0 turns it off): the header and the first 100 records are held, as Phase 4 holds through the first data record, and each column gets the width of its widest cell's text, at most `MaxAutoFitWidth` (60 characters; 1 to 255). The text's width is measured as Phase 4's dates are (Calibri 11's measured widths, for 96 DPI, and Excel's padding): the header in bold when it is, with its filter button when it has one; a text's widest line; a number in General as Excel shows it, in at most 11 characters besides its sign; dates and times in their formats; booleans as `TRUE` and `FALSE`; a number in a column's format as decision 5 says. Formulas, the title and merged cells count nothing. (Changed at step 4: cells merged across two columns or more count nothing, and a range down one column counts each of its cells, as Excel's AutoFit does; see its "As built".) A column can come out narrower than the default, as with Excel's AutoFit (decision 4). Without auto-fit, Phase 4's widths of the first data record's dates stand.
- **Columns** (by `Columns` or `[ExcelColumn]`):
  - `Width`: Excel's `ColumnWidth`, in characters (0 to 255), which beats auto-fit.
  - `Format`: Excel's format code, checked by Phase 4's `FormatCheck`, which now takes `[DBNum3]` and a backtick too (decision 13). The column's numbers, booleans and text are written in it, and its dates, times and durations in place of Phase 4's format by what each holds or of the member's translated `[CsvFormat]` (whose text is still what the cell's value is read from). A number in a format gets a width (decision 5).
  - `Wrap`: text wraps at the column's width; Excel and LibreOffice size the rows on open.
  - `Hidden`: the column is hidden.
  - A column with a `Format` or `Wrap` gets it as its default style (`<col style>`) as well, so that cells users add later take it. Every cell written in such a column carries the style itself: Excel gives a column's style only to cells not in the file, and LibreOffice to written cells without one too (checked).
  - A name in `Columns` that the header doesn't have throws when the header record ends, which discards it as any failed record (decision 12).
- **Links** (`ExcelWriterOptions.Links`): a `Uri` field (Phase 0 passes its type) is a text cell of its `OriginalString`, as in Phase 4, with a link to it, in Excel's `Hyperlink` cell style (underlined, `#0563C1`), when the policy takes its scheme (decision 6). The link's target is an absolute URI's `AbsoluteUri` (escaped) or a relative one's text, with one relationship for each distinct target. A sheet holds at most 65,530 links, since Excel refuses a workbook with more; later URIs are text without one (decision 7). (Changed at step 5: a target's escaped letters past ASCII are written as the letters, since Excel reads a file link's escapes byte by byte; a target longer than 8,192 characters, which Excel refuses, is text without a link; and the header's URIs are text; see its "As built".)
- **Merged cells** (`MergeCells("A1:C3")` on a sheet's writer): written after the rows, in the sheet being written. A range has two cells or more, within Excel's grid, written or not; one that overlaps another throws when added (Excel refuses it). So does one in a table's columns at or below its header, since the table reaches every row written below it: when added, or, for ranges added before the header, when the header record ends. (Added at step 4: under a title, a range in row 1 throws too; and a header record that fails for a range before it is discarded, while the range stays; see its "As built".)

#### Checked on 2026-11-08

Excel 365 (16.0 build 20527) through COM, with probe workbooks written by hand and opened read-only, at 144 DPI; and LibreOffice 26.8, converting each to `.xlsx`, whose parts say what it kept. The probes, their scripts and the answers are in `Csv\_bench\results\phase5\design` (`make_probes.py` to `make_probes4.py`, `probe.ps1`, `measure.ps1`, `lo_report.py`; `answers.txt` to `answers4.txt`, `lo-answers.txt` and `measure.txt`).

- **Views and the header:**
  - A frozen header row (`pane` with `state="frozen"`), with and without its `selection`, two rows, and a row and a column, open in Excel without the workbook's `bookViews`. LibreOffice keeps them.
  - A bold header is a second font and a cell format, which both keep.
  - A title in bold Calibri 14, merged over the header, above a frozen header and a filter or a table: Excel sizes its row on open (18.5 points).
- **The autofilter:** Excel opens `autoFilter` without the `_xlnm._FilterDatabase` name, and over the header alone. It refuses one written after `pageMargins`, so the worksheet's elements keep the schema's order. LibreOffice opens all of them, and writes the name.
- **Links:**
  - A link's relationship needs `TargetMode="External"`: Excel refuses one without. Two links may share a relationship.
  - Excel opens `https`, `mailto`, `file:///C:/Temp/report.txt` (shown as `C:\Temp\report.txt`), `file://server/share/report.txt` (as `\\server\share\report.txt`), a relative path, `javascript:` and a target of 8,000 characters, kept whole. It splits `#frag` into the link's sub-address itself. Spaces and accented letters open escaped or not.
  - A sheet of 65,530 links opens; with 65,531 Excel refuses the workbook. LibreOffice opens both.
  - The `Hyperlink` cell style (built-in 8) shows in Excel as its own; LibreOffice keeps its font.
- **Merged cells:** Excel refuses overlapping ranges and a range in a table. It opens a range of one cell (LibreOffice drops it) and one in a filter's range.
- **Tables,** without a theme part (Excel draws them in its default theme's colours):
  - Excel refuses header names that differ from their cells, that repeat (in any case), that are empty or a number cell, or that are longer than 255 characters; a table name used twice in a workbook (in any case); a table of a header alone (a header and an empty row opens); a table under the sheet's own autofilter; and the names that "A table" above rules out. It opens `XFE1`, `A0`, `ABCD1`, `XFD`, `SUM`, `_1`, `\Orders`, `Q1.Orders`, `a?b`, CJK, an emoji, `Print_Area`, and the sheet's own name.
  - It opens header names with `[`, `#` and `'`, a line break (raw or as `_x000a_`), a table without a style, and one without its `autoFilter` (no buttons). It shows an unknown style's name as no style, and takes a built-in's in any case.
  - LibreOffice opens every one of them, renaming repeated headers (`Name2`), and keeps the tables and their styles.
- **Columns:**
  - A column's `style` applies in Excel only to cells not in the file: a written cell without `s` is General. LibreOffice gives such a cell the column's style.
  - Hidden columns open with a width of 0 or not. A width of 256 opens (as 255.36), and Excel's `ColumnWidth` takes at most 255.
  - Wrapped text: Excel sizes the row on open (130.5 points for a long text) without `ht`. Text with a line break and no wrap stays one line high.
  - `[DBNum3]0` and a backtick (`` 0` ``) open in Excel and LibreOffice.
- **Widths,** measured in a workbook whose Normal style is Calibri 11 (`measure.ps1`):
  - Bold Calibri 11 has regular's widths but for 20 of the 131 characters measured, each about 0.7 pixels wider at 96 DPI: `"`, `&`, `,`, `K`, `P`, `V`, `X`, `Z`, `` ` ``, `j`, `k`, `t`, `{`, `}`, `°`, `’`, Greek alpha, Cyrillic zhe, Thai ko kai and `年`. Regular's widths match Phase 4's table.
  - Excel's AutoFit gives a header with a filter button 12.5 points more (17 pixels at 96 DPI).
  - In General, AutoFit shows a number in at most 11 characters besides its sign: `0.000123457`, `3.141592654`, `-1234567.891`, `123456.1235`.

#### Internal design

**New files:**

- Public: `ExcelSheetOptions.cs`, `ExcelTableOptions.cs`, `ExcelColumnOptions.cs`, `ExcelLinks.cs`, `ExcelColumnAttribute.cs` and `ExcelSheetAttribute.cs`.
- `Internal/Writing/`:
  - `SheetLayout.cs`: a sheet's settings, resolved and checked once when it is added, and its columns' (width, style, hidden), resolved by header name when the header record ends. (Built as `SheetSettings.cs` at step 1, since the reader's `SheetLayout` is in the same namespace; see its "As built".)
  - `AutoFit.cs`: the held cells' widths (bold, filter buttons, lines, General's 11 characters), and the columns' widths from them.
  - `NumberFormatWidth.cs`: a number's width in an Excel format code (decision 5).
  - `SheetLinks.cs`: a sheet's links: each distinct target's relationship, the cells, the policy and the cap.
  - `MergedRanges.cs`: a sheet's ranges, checked as they are added against an index sorted by row, so that many ranges cost `O(n log n)`.
  - `TableWriter.cs`: a table's part, and `TableNames`: Excel's rules, and the workbook's names in Excel's comparison (`SheetNames.Key`'s). Phase 1's `Internal/Package/TablePart.cs` reads tables.
- **Changed:**
  - `SheetSink`: writes the title row and offsets rows below it; holds through the auto-fit rows; writes a column's style on its cells and links' cells; writes after the rows `autoFilter`, `mergeCells`, `hyperlinks`, `pageMargins`, `ignoredErrors` and `tableParts`, in the schema's order; and, once its entry is closed, the sheet's relationships part and its table's part. With `NewSheet`, each new sheet repeats the title, the header, the title's merge and the table.
  - `StyleRegistry`: a cell format is a number format, a font (regular, bold, the title's or a link's) and wrap; the `Hyperlink` cell style's `cellStyleXfs` and `cellStyles` entries come with the first link.
  - `PackageParts`: the fonts, the workbook's `definedNames` for filters, and content types for tables.
  - `ColumnWidths`: bold widths, and a cap.
  - `MemberFormats`: reads `[ExcelColumn]` beside `[CsvFormat]` and `[CsvCulture]`, and `[ExcelSheet]` per type, in the same pass and cache.
  - `FormatCheck`: `[DBNum3]` and a backtick.
  - `ExcelWorkbookWriter`: the options, table names across sheets, and each open sheet's sink by its writer, for `MergeCells` (a `ConditionalWeakTable`, so no core change).
  - `ExcelWriterExtensions`: `MergeCells`.
- **The per-cell path:** a column's style index sits beside its kind, in Phase 4's per-column arrays, and a cell writes `s` when it is set. A link is a cell of `CellKind.Uri`, which `CellKinds` gives `Uri`'s type, as it gives `ExcelFormula`'s. After the held rows, a sheet without column formats or links costs per cell what Phase 4's does, but that test.

#### Testing

- **Unit:** `SheetLayoutTests` (where settings come from, and their checks; built as `SheetSettingsTests`), `AutoFitTests` (text, bold, buttons, line breaks, General numbers, dates, the cap), `NumberFormatWidthTests` (against Excel's widths for the codes they model), `TableNamesTests` (each name the probes asked about, as Excel answered), `MergedRangesTests` (overlaps, the grid, many ranges), `SheetLinksTests` (each policy's schemes, targets, shared relationships, the cap), and `StyleRegistryTests` (fonts, wrap, the `Hyperlink` style).
- **Through the public API,** sync and async: each setting alone and with the others; `[ExcelSheet]` and `[ExcelColumn]` against options; `RowLimit.NewSheet` with a title, a table and auto-fit; a failed record among the held rows; `MergeCells` on a CSV writer and on a closed sheet.
- **Read back:** our reader, ClosedXML and ExcelDataReader read the same records as without presentation; ClosedXML reports the panes, filters, tables, merges, links, widths and styles written.
- **`OpenXmlValidator`** on every workbook the tests write, as Phase 4's check does.
- **Excel's answers:** the `written` set gains presentation workbooks (each setting, and all together), with Excel's answers committed as step 5's are: whether it opens, the panes, filter, tables, merged areas, links, widths, styles and each cell's `Text`, none `########` after auto-fit. LibreOffice opens them in CI.
- **Fuzzing:** random settings and data (titles, header names under a table, URIs, merges, formats): every workbook passes the validator, and opens in Excel when it runs on demand; table names and header renames against `TableNames`' rules.
- **AOT:** a NativeAOT publish, in CI, of a small app that writes a sheet of a type with `[ExcelSheet]` and `[ExcelColumn]`, with no warnings, and its workbook read back (decision 14).

#### Benchmark

- `ExcelPackageAssetsWrite` and `ExcelOrdersWrite` at the new defaults, and with Phase 4's look (no bold, no freeze, no auto-fit), against Phase 4's step 7 numbers.
- New methods: `Presentation` (a title, a bold frozen header, a filter, auto-fit over 1,000 rows and the numbers in formats), `Table`, and `Links` (a column of generated `https` URIs).

#### Core changes

None. A `Uri` field already reaches the sink typed (Phase 0), the sink writes the title row before the writer's first record, and `MergeCells` finds its sheet through the workbook writer's table.

#### Decisions

1. **The defaults.** Recommended: a bold header, a frozen header and auto-fit over 100 rows on; a title, a filter and a table off; links for http, https and mailto. A sheet reads well when opened, for the cost of holding 101 records and measuring them. Alternative: everything off, Phase 4's look unless asked.
2. **Where settings come from.** Recommended: the argument, else `[ExcelSheet]`, else `ExcelWriterOptions.Sheets`, each whole; a column's from `Columns`, else `[ExcelColumn]`, each whole. Alternative: nullable settings, merged one by one.
3. **Rows above the header.** Recommended: `Title`, which the sink writes and merges, so that `WriteSheet` and `WriteRecords` still write the header. Alternative: a `HeaderRow` setting with rows written by hand above it, which then needs `WriteHeader<T>()` by hand, since `WriteRecords` writes a header only into an empty writer (`NeedsHeader`).
4. **Auto-fit's widths.** Recommended: as Excel's AutoFit (narrower than the default when the text is), with the filter button, capped at 60 characters. Alternative: never narrower than the default.
5. **Widths of formatted numbers.** Recommended: `NumberFormatWidth` estimates a number's text in the section of a code for its sign (digits, grouping, decimals, percent, exponent and literals), erring wide; codes it doesn't model (fractions, conditions, `[DBNum]`) take General's width and their literals'. It sizes the first data record without auto-fit and the held rows with it, so that a formatted number doesn't show `########`. Alternative: no width, until Phase 8 renders codes exactly.
6. **Which URIs are links.** Recommended: `Web` (http, https, mailto), so that data can't plant links to `file:` shares (which can send Windows credentials when clicked) or to other schemes' handlers; `All` for internal reports. Alternative: `All` by default.
7. **Past 65,530 links in a sheet.** Recommended: text, as the URIs before them, without a link. Alternative: throw.
8. **A table's default name.** Recommended: the sheet's name made valid (`_` for each character Excel refuses, and `_` first when needed), with a number after it while the workbook has it. Alternative: Excel's `Table1`, `Table2`.
9. **A table with `AutoFilter`.** Recommended: the table's buttons (`FilterButtons`) are the filter, and no sheet filter is written, which Excel refuses beside a table. Alternative: throw.
10. **Header cells a table can't have.** Recommended: as Excel names them when it makes a table (an empty one `Column3`, a number its text, a repeat `Name2`), so that the workbook opens, changing the header's text in those cells. Alternative: throw.
11. **Without `HasHeader`.** Recommended: `BoldHeader` and `FreezeHeader` do nothing, `AutoFilter` and `Table` throw, and `Columns` match `T`'s record writer's names. Alternative: the first row is styled, frozen and filtered as a header anyway.
12. **A `Columns` name the header doesn't have.** Recommended: throw when the header record ends, so a typo shows at once. Alternative: ignore it.
13. **`FormatCheck`'s `[DBNum3]` and backtick.** Recommended: accept them, as Excel and LibreOffice open them. Alternative: keep refusing.
14. **AOT.** Recommended: the NativeAOT publish check in CI that Phase 1 promised for the attribute reading. Alternative: the trim and AOT analyzers alone, as now.
15. **`[ExcelSheet(Name)]`.** Recommended: no `Name`, since a sheet's name is always given. Alternative: overloads without a name, taking it from the attribute or the type.
16. **`MergeCells`.** Recommended: an extension on the sheet's `CsvWriter`, taking `A1:C3`. Alternative: a method of `ExcelWorkbookWriter` for the open sheet.

#### Risks

- **Holding 101 records** delays a sheet's first write and costs memory for wide rows (a hundred columns of long text is megabytes). `AutoFit` sets it, and 0 turns it off.
- **Widths are estimates:** for fonts other than Calibri 11 (Excel's own default, Aptos Narrow 11, is within 0.1 for dates, Phase 4), other DPIs, and codes `NumberFormatWidth` doesn't model.
- **Excel's table rules** were found by probing, not in a specification: one missed makes a workbook Excel refuses. The fuzzer, with Excel's answers, looks for them.
- **The default path changes** (decision 1); the benchmarks measure what it costs.

#### Order of work

1. **Settings (S):** the public types, `SheetLayout` (built as `SheetSettings`), `MemberFormats` reading `[ExcelColumn]` and `[ExcelSheet]`, and `FormatCheck`'s two forms.
2. **Styles and the header (M):** `StyleRegistry`'s fonts and wrap; the bold header, frozen panes, the title row and its merge; columns' formats, wrap, widths, hidden and default styles.
3. **Auto-fit (M):** held rows, `AutoFit`, bold widths and `NumberFormatWidth`.
4. **Filters, merges and tables (M):** `autoFilter` and its name, `MergeCells`, `TableWriter` and `TableNames`, and `NewSheet`'s repeats.
5. **Links (S):** `SheetLinks`, the relationships, the `Hyperlink` style, the policy and the cap.
6. **Corpus and oracles (M):** the presentation workbooks in `written`, Excel's answers, LibreOffice and ClosedXML.
7. **Security, fuzzing and AOT (S).** (Added at step 6's review: durations of Excel's last serial or more are written as text, as `OutOfRangeDates` says; see step 6's "Left open".)
8. **Benchmarks (S).**
9. **"As built" (S).**

#### Gate

- Every workbook the tests write passes `OpenXmlValidator`, and the `written` set's open in Excel and LibreOffice without a repair prompt, with Excel's answers matching what was written.
- No typed cell shows `########` after auto-fit, in Excel's `Text`.
- Records read back as without presentation, through our reader, ClosedXML and ExcelDataReader.
- With Phase 4's look, the write benchmarks are within 1% of Phase 4's; the defaults' and each setting's costs are recorded.
- The NativeAOT publish has no warnings.

#### Changes elsewhere

Applied at approval, 2026-11-08:

- **Status** (top of the plan): Phase 5's design approved.
- **API sketch:** `ExcelSheetOptions` as designed (with `Title`, `FreezeColumns`, `MaxAutoFitWidth`, a table's options in `ExcelTableOptions`, and a column's in `ExcelColumnOptions`); `[ExcelSheet]` without `Name`; `ExcelWriterOptions.Sheets` and `Links`; `MergeCells`.
- **Defaults:** a bold, frozen header; auto-fit over 100 rows, at most 60 characters; links for http, https and mailto.
- **Phase 8:** `NumberFormatWidth` models part of what displayed text renders.
- **Phase 9:** auto-fit's held rows.

#### As built

Filled in step by step; the code is the reference.

**Step 1, settings** (built 2026-11-08, approved, with every recommendation, and committed 2026-11-09):

- **New files:**
  - Public: `ExcelSheetOptions.cs`, `ExcelTableOptions.cs`, `ExcelColumnOptions.cs`, `ExcelLinks.cs`, `ExcelSheetAttribute.cs` and `ExcelColumnAttribute.cs`, as the API sketch has them. The options are sealed records, as `ExcelWriterOptions` is, so that `with` makes a variant. `[ExcelColumn(Width)]` is 0 for none, since an attribute can't take a `double?`.
  - `Internal/Writing/`:
    - `SheetSettings.cs`: the design's `SheetLayout`, renamed, since the reader's `SheetLayout` is in the same namespace. A sheet's options, resolved and checked once when it is added, and its columns' options by the header's names (`ColumnsFor`), which the sink calls from step 2.
    - `TableNames.cs`: Excel's rules for a table's name, as the probes found them; the design put it in `TableWriter.cs`. The workbook's names and the default name (decision 8) come with step 4.
- **Changed:**
  - `ExcelWriterOptions` gains `Sheets` (`ExcelSheetOptions.Default`) and `Links` (`Web`), which `Validate` checks.
  - `ExcelWorkbookWriter`: `WriteSheet<T>`, both `WriteSheetAsync<T>`, `AddSheet` and `AddSheet<T>` take `ExcelSheetOptions? options = null`, resolved and checked before the sheet is added, so that a sheet whose settings throw isn't added. `WriteSheetAsync<T>` takes it before the cancellation token, so a token passed by position now needs its name; no caller in the repository passed one so.
  - `SheetSink` holds its `SheetSettings`, unused until step 2. So the step changes no workbook: the parts' hashes pinned at Phase 4's step 5 still match.
  - `MemberFormats` reads `[ExcelColumn]` beside `[CsvFormat]` and `[CsvCulture]`, and `[ExcelSheet]` per type, in the same pass and cache. A member with `[ExcelColumn]` and neither of the others gets an entry; `Columns()` (Phase 4's formats) skips it.
  - `FormatCheck` accepts `[DBNum3]` and a backtick (decision 13).
- **Where settings come from** (decision 2): the options given, else the record type's `[ExcelSheet]`, else `ExcelWriterOptions.Sheets`, each whole. A column's: its entry in `Columns`, by the header's name, else its member's `[ExcelColumn]`, each whole. Every column of a name gets its entry. A `Columns` name the header doesn't have throws when the header record ends (decision 12), naming every such name in ordinal order; without a header, the names are the record type's, and a sheet with neither throws for any name.
- **Checks:** options Excel wouldn't open throw `ArgumentException` (`options`) when the sheet is added, or from `Validate` for `Sheets`, saying what is wrong and what Excel takes: a title of 1 to 32,767 characters, `FreezeColumns` 0 to 16,383, `AutoFit` 0 to 1,048,575, `MaxAutoFitWidth` 1 to 255, a width 0 to 255, a format `FormatCheck` takes, a table's name and built-in style, and `AutoFilter` or a table without `HasHeader` (decision 11).
- **Decisions made while building,** approved at review as built:
  - **A type's attributes** that Excel wouldn't open throw `InvalidOperationException` naming the type and the member ("The [ExcelColumn] of Order.Amount has the width 300, ..."), since the type isn't an argument.
  - **A table without a style keeps the bold header:** only a styled table sets its header's look.
  - **`Columns` is copied when a sheet is added, and the copy checked:** a dictionary is mutable, so `Sheets.Columns` changed after `Validate` throws when the next sheet is added rather than reaching it unchecked.
  - **An empty workbook's `Sheet1`** ignores `Sheets`: it has nothing to show, so it shows no title.
  - **`[ExcelColumn]` on a member written by a converter** applies to its one column; on a member that nests a record it throws, since that record's members have the columns; on a collection, every index gets it.
  - **`[ExcelSheet]`** is read from base types (`Inherited`) and from types that aren't `[CsvRecord]`; its `TableName` without `Table = true` throws.
  - **Table names are stricter where Excel wasn't asked:** characters past ASCII other than letters below the supplementary planes, a supplementary-plane character first, `A01`, `A1048577`, `FALSE`, and names that start with `_xlnm.`.
  - **`Columns` names match ordinally,** as the members' formats do, and an empty `Format` throws (`null` is none).
  - **The XML docs describe the finished behavior:** the defaults they give (bold, frozen, auto-fit) take effect in steps 2 and 3.
- **Tests:** `SheetSettingsTests` (where settings come from, each check at its limits and past them, the attributes, `Columns` against the header, the copy, the empty workbook, and sheets added sync and async through every method, the validator over the workbooks they write), `TableNamesTests` (each name the probes asked about, as Excel answered, and the stricter forms), `MemberFormatsTests.ReadsExcelColumnsBesideFormats`, and `FormatCheckTests`' `[DBNum3]` and backticks. Every CsvHelper.Excel suite passes, with 0 warnings: 2,834 tests on .NET 10 and on .NET 8, and 2,786 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in.

**Step 2, styles and the header** (built, approved, with every recommendation, and committed 2026-11-09):

- **New file:** `Internal/Writing/ColumnStyle.cs`, a styled column's cell formats:
  - Its default: the column's `Format` and `Wrap`, which its `<col>` carries.
  - Each data cell's: the column's format, else the cell's own, with the column's wrap. The last one is cached, since a column's cells mostly share one.
- **Changed:**
  - `StyleRegistry`:
    - A cell format is a `CellXf`: a number format's id, a font and wrap. The design's name for it clashed with the Open XML SDK's `CellFormat` in the tests.
    - Fonts (regular; bold; and the title's, bold Calibri 14) are written in the order of first use. So a workbook without a bold cell or a title has Phase 4's one font.
    - An `<xf>` sets `applyFont`, and `applyAlignment` with `<alignment wrapText="1"/>`, only when it uses them.
    - `NumberFormatId` gives a code's id without adding a cell format.
  - `PackageParts` writes the registry's fonts.
  - `ColumnWidths`:
    - `Default` is 9.140625, Excel's 8.43 characters at 96 DPI.
    - `FromCharacters` converts a `Width` in characters as Excel does: characters × 7 + 5 pixels (× 12 below 1 character), truncated to 256ths of 7 pixels.
  - `SheetSink`:
    - Writes the title in row 1, merged over the header's columns, and the header and records from row 2.
    - Makes the header bold.
    - Writes `sheetViews` with a frozen pane over the rows through the header and the `FreezeColumns`.
    - Resolves the columns' options when the header record ends, or when the sheet is added without a header. It writes their `<col>`s (width, default style, hidden) merged with the measured widths, and styles each data cell by its column.
    - With `RowLimit.NewSheet`, each new sheet repeats the title, its merge, the header and the frozen pane.
  - `MemberFormats.HasExcelColumns` and `SheetSettings.HasColumns`, so that a sheet holds its header's names only when a column has options.
  - `ExcelWorkbookWriter`:
    - Without a header, `AddSheet` and `AddSheet<T>` resolve the columns by `T`'s names when the sheet is added.
    - The exception docs name `Columns`.
- **Decisions made while building,** approved at review as built:
  - **Header cells are bold in their own number format.** They take neither the column's `Format`, which could replace their text with a text section, nor its `Wrap`.
  - **A title without a header** merges over the first record's columns. A title over one column, or over an empty sheet, isn't merged.
  - **`NewSheet` repeats the title, its merge and the frozen pane from this step.** The design listed `NewSheet`'s repeats under step 4; the table and the filter follow there.
  - **A styled column without a `Width` is written 9.140625 wide,** and a hidden column without one too, since Excel hides a `<col>` without a width (Phase 3's step 3). That is the default at 96 DPI. At this machine's 144 DPI, Excel shows such a column 8.55 characters wide against the default's 8.09.
  - **Core doesn't discard a record when the sink's `EndRecord` throws.** `Dispose` then ended the record again, and that second error hid the first: `Columns` checked against an empty header. The sink takes an end with no fields as a record it already discarded, since core never ends a record without one. No core change.
  - **A date in a column with a `Format` takes the column's format alone.** Its own isn't added to the styles. Its width is still Phase 4's measured width until step 3.
  - **Phase 4's tests that pin exact XML, and the corpus's `written` workbooks, write in Phase 4's look:** `BoldHeader` and `FreezeHeader` off, and `AutoFit` 0. So their XML, Excel's answers and the parts' pinned hashes stand. Step 6 adds the new look's workbooks.
- **Checked in Excel** (16.0 build 20527, through COM, on 2026-11-09):
  - Every workbook opens without a repair, and the header is bold.
  - The frozen panes hold. For a wide split, Excel's `SplitColumn` reports one less than written, but its first scrollable column is one past the frozen ones (`XFD` for 16,383), so the freeze is exact.
  - The title is merged, and its row is 18.5 points on open.
  - Wrapped rows are sized on open (43.5 points).
  - Hidden columns are hidden, and a column's default style applies to cells added later.
  - The workbooks, the scripts and Excel's answers are in `Csv\_bench\results\phase5\step2`.
- **Tests:**
  - `SheetLookTests`, sync and async, covers:
    - the defaults, and Phase 4's look;
    - the title, with a header and without, and its merge;
    - the freeze, to 16,383 columns;
    - the columns' options and attributes, and a member's date in its column's format;
    - a `Columns` name the header lacks, and the record type's names without a header;
    - `NewSheet`'s repeats.

    Each workbook goes through the validator and ClosedXML. It is read back by CsvHelper.Excel, ClosedXML and ExcelDataReader as Phase 4's look reads.
  - `StyleRegistryTests`' fonts and flags, and `ColumnWidthsTests`' `FromCharacters`.
  - Every CsvHelper.Excel suite passes, with 0 warnings: 2,875 tests on .NET 10 and on .NET 8, and 2,827 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in. The corpus's pinned hashes still match.
- **The per-cell cost** (a flag test, and a check for a style) is measured at step 8.

**Step 3, auto-fit** (built, approved, with every recommendation, and committed 2026-11-09):

- **New files:**
  - `Internal/Writing/AutoFit.cs`: each held cell's width, as text, as a typed cell, or a formula's; the header's filter buttons; a discarded record's cells dropped; and the columns' widths from them. General's 11 characters, which the design put here, are in `NumberFormatWidth`.
  - `Internal/Writing/NumberFormatWidth.cs`: General's text (`General`) and its width as Excel's AutoFit measures it (`GeneralPixels`). And a format code's text of a number, a date, a time, a duration, a boolean or a text: as Excel shows it in en-US, or in its widest form, which sizes a column (decision 5). Its rules are the probes' (below), in its remarks.
- **Changed:**
  - `ColumnWidths`:
    - bold Calibri 11's widths (`CharPixels`, and `TextPixels` with `bold`), a line feed (8 pixels at 96 DPI), a tab (7.333) and a CR (0);
    - `Fit`: auto-fit's width of a text's pixels, narrower than the default column too;
    - `OfPixels`: Phase 4's width, none when the default column fits;
    - the fixed formats' pixels, the filter button's 17 pixels, and `Widest`, 255 characters.
  - `DateFormat.Pixels`: its widest text's pixels. `Width` comes from them, so Phase 4's widths are unchanged.
  - `ColumnStyle`: its wrap, and its format's `NumberFormatWidth`.
  - `SheetSink`:
    - Holds the header and the records auto-fit measures, or, without auto-fit, through the first data record, as Phase 4 did.
    - Measures their cells as they are written.
    - Knows the widths when the last held record ends, when the first sheet fills (the next sheets of `NewSheet` take them), or when the sheet completes.
    - Writes `bestFit="1"` on the columns auto-fit sizes.
  - `ExcelSheetOptions`' and `[ExcelSheet]`'s docs of `AutoFit` and `MaxAutoFitWidth`.
- **What a cell measures,** with auto-fit:
  - The header: in bold when it is bold or in a styled table, whose built-in styles make it bold. With `AutoFilter`, or a table's `FilterButtons`, each of its fields has a filter button, an empty one too.
  - Text: on one line, as Excel shows text that doesn't wrap, a line feed 8 pixels and a CR none; or, in a column that wraps, its widest line.
  - Numbers: in General as Excel's AutoFit counts them, every character but a leading minus as a digit (`0.5` as `123`); or in their column's format.
  - Booleans: `TRUE` and `FALSE`, in their column format's text section if it has one.
  - Dates, times and durations: their format's widest text, Phase 4's or their column's format's.
  - A formula: its column is the default width at least. The title: nothing.
  - Text is at most `MaxAutoFitWidth`; typed cells are as wide as they need, to 255 characters.
  - Without auto-fit (decision 5): the first data record's typed cells in their column's format are measured beside Phase 4's dates, times and durations, and give a width only where the default column is too narrow.
- **Decisions made while building,** approved at review as built (every recommendation):
  - **Text that doesn't wrap is measured on one line,** not by its widest line, as the design had it: Excel shows it on one line and fits all of it (checked). A column that wraps takes its text's widest line.
  - **`MaxAutoFitWidth` caps text only.** Numbers, booleans, dates and times get the width they need, to 255 characters, since Excel shows one too narrow as `########`, and text is cut or spills.
  - **A formula's column is at least the default width.** The design had formulas count nothing, which would narrow a column of formulas to its header, while their values, unknown, could be dates.
  - **Auto-fitted columns are marked `bestFit="1"`,** as Excel marks its AutoFit's (checked); a column's own `Width` and Phase 4's widths aren't.
  - **A `DateTime` column keeps Phase 4's width of a date and time** when every held value is at midnight, since a later one with a time would show `########`: 18.09 characters, where Excel's AutoFit gives a column of dates 9.45 (at 144 DPI).
  - **General numbers are measured as Excel's AutoFit counts them,** every character but a leading minus as a digit.
  - **`NumberFormatWidth` covers dates and times,** not only numbers: a column's `Format` replaces a date's own format. Month and day names are English, or a locale's (`[$-407]`) as .NET has them, at their widest.
  - **Codes it doesn't model are estimated wider than the design's General width,** which the probes showed narrower than Excel's text for fractions with `?` and for big integers in `[DBNum]`:
    - conditions take their widest section;
    - fractions take a digit for each placeholder besides their whole number's digits;
    - `[DBNum]` takes two East Asian characters a digit, a numeral and a unit, as an East Asian locale shows it. Excel shows digits elsewhere, so a `[DBNum1]0` column of 1E+20 is 90.82 characters wide where Excel's AutoFit gives 22.09;
    - an era's name (`g`) takes one or two East Asian characters.
  - **Booleans and text take a column format's text section,** as Excel shows them (`Paid: TRUE`).
  - **The filter button's room is counted from this step.** The filter and the table come at step 4, so until then a sheet with `AutoFilter` has the room but not the buttons.
  - **With `NewSheet`,** a first sheet that fills before the held records end knows its widths then, and every new sheet takes the first sheet's.
  - **Excel's answers about `NumberFormatWidth` are in its tests,** inline, and Phase 4's `excel-formats.json` is a second oracle. The design's alternative, a corpus tool command writing them under `Files`, wasn't needed.
  - **Phase 4's width tests write in Phase 4's look:** `TypedCellsTests`' four. The streaming security tests run with `AutoFit` 0, since the default holds 101 records (the design's risk); a variant with auto-fit over 8 records bounds the lag by the held rows.
- **What Excel showed** (Excel 365, 16.0 build 20527, through COM, at 144 DPI, on 2026-11-09; the probes, the workbooks' writer, the scripts and the answers are in `Csv\_bench\results\phase5\step3`):
  - **General,** in a column auto-fitted (`general.ps1`, 97 numbers):
    - at most 11 characters besides the sign;
    - decimals while they show as many significant digits as scientific notation would (`0.000012345`, but `1.23456E-05`), and scientific notation otherwise, with as many decimals as fit (`1.23457E+11`, `1.2346E+100`);
    - integers of up to 11 digits whole;
    - every character but a leading minus measured as a digit.
  - **Format codes** (`formats.ps1`: 49 codes over 15 values, 29 date codes over 14 dates, 6 elapsed codes):
    - Rounding is on 15 significant digits, half away from zero (`999.995` shows `1000.00`).
    - With one section, a minus sign shows first, unless the number rounds to zero; the other sections show none of their own.
    - `%` multiplies; commas after the last placeholder divide by 1,000.
    - An exponent is a multiple of the integer placeholders (`##0.0E+0` shows `123.5E+12`), and its zeros are its fewest digits.
    - `?` and `_x` leave a character's width; a fill counts nothing in AutoFit, while its Text repeats it across the column.
    - Booleans and text take the text section.
    - Whole seconds round, and fractions of a second and minutes are cut.
    - Phase 4's answers showed three more rules, which the code follows:
      - the days of the week before 1900-03-01 are a day earlier, as Excel counts them;
      - `p` is wider than `a` in lower case;
      - `[h]` beside a date part is the hour of the day.
  - **Text** (`lines.ps1`): text that doesn't wrap shows on one line, and AutoFit fits all of it, a line feed 6 points wide at 144 DPI and a CR none. Wrapped text's AutoFit shrinks the column to its lines as wrapped.
  - **Widths:** across those probes, ours are never more than half a point below Excel's AutoFit, a pixel of rounding at 144 DPI.
  - **The step's seven workbooks** (`write-workbooks.cs`, `check.ps1`, `answers.txt`):
    - Every one opens without a repair, and no typed cell shows `########` at the widths written. Each shows as many characters as at Excel's own AutoFit, or more.
    - The widths are about 5% wider than Excel's AutoFit at 144 DPI, as Phase 4's are. The exceptions are the text cap, a column's own width, and a formula whose text is wider than the default.
    - Excel's own AutoFit is too narrow for `-1.00E-03` in `0.00E-00`, which shows `#######` there.
    - The title's row is 18.5 points, and wrapped rows grow on open.
- **Tests:**
  - `NumberFormatWidthTests`: Excel's text and AutoFit width of each probe, inline; Phase 4's codes; the widest forms; signs, rounding and placeholders; codes it doesn't model.
  - `AutoFitTests`: lines, bold, ASCII and other text, the cap, typed cells, formulas, filter buttons, a discarded record.
  - `ColumnWidthsTests`: bold Calibri, line feeds and tabs, `Fit`.
  - `SheetAutoFitTests`, sync and async: the defaults, the records held and measured, a failed record, the cap, wrap and line breaks, formulas, filter buttons, a styled table's header, a column's own width, the title, `NewSheet`, cells in their column's format, and without auto-fit. Each workbook goes through the validator and reads back as written.
  - `SheetLookTests` pins the auto-fitted `cols`.
  - Every CsvHelper.Excel suite passes, with 0 warnings: 3,159 tests on .NET 10 and on .NET 8, and 3,111 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in. The corpus's pinned hashes still match.
- **The per-cell cost** (a test of whether cells are measured, on the number, boolean, text and formula paths) is measured at step 8.

**Step 4, filters, merges and tables** (built, approved, with every recommendation, and committed 2026-11-10):

- **New files:**
  - `Internal/Writing/MergedRanges.cs`: a sheet's merged ranges, in the order added, and an index that finds the one a new range overlaps.
    - The index is a set of levels of a power of two ranges each, sorted by first row, with a tree of the greatest last row over each. Adding a range merges full levels into the next, as a binary counter carries.
    - So `n` ranges cost `O(n log n)` to add, and each search `O((log n)^2)` and the ranges whose rows meet the new one's. A range down a whole column doesn't slow every search: 200,001 ranges beside one take about 0.7 s.
  - `Internal/Writing/TableWriter.cs`: a table's columns' names, as Excel names them; its part; and the sheet's relationships part while the table is its only relationship. `TableNames` stays in its own file (step 1).
- **Changed:**
  - `TableNames` keeps the workbook's names, by `SheetNames.Key` (`Existing`, `Add`, `Unique`, `Next`). It makes a sheet's name into a table's (`FromSheetName`, decision 8), and gives a name a number (`Numbered`), cut to fit 255 characters, with `_` before the number when the two would read as a cell reference (`AB_2`).
  - `SheetSink`:
    - Writes `autoFilter` after `sheetData`, over the header's fields (empty ones at its end too), from the header to the last row written, or over the header alone. It tells the workbook, for the filter's name.
    - Writes `mergeCells` next: the title's merge first, then the ranges in the order added.
    - With a table, holds the header's names and writes its cells when the header record ends, named as Excel names a table's columns, before the columns' options are resolved. It writes `tableParts` last, and, once the sheet's entry is closed, the sheet's relationships part and the table's part.
    - With `NewSheet`, each new sheet gets its own filter and table (`Orders2`), and starts without merged ranges but the title's.
    - `MergeCells` checks a range against the title's row, the table's columns and the ranges before it.
  - `ExcelWorkbookWriter`:
    - each open sheet's sink by its writer (a `ConditionalWeakTable`, so no core change);
    - the workbook's table names, a sheet's reserved when it is added;
    - the tables' count, and the filters, for `definedNames`.
  - `ExcelWriterExtensions.MergeCells`.
  - `PackageParts`: `definedNames`, with each filter's hidden `_xlnm._FilterDatabase` (its sheet's `localSheetId`, and its range with the sheet's name quoted as in formulas, `SheetLocation.Quote`); and each table's content type.
  - `XmlOutput`: in an attribute, a tab and a line feed are written as `_x0009_` and `_x000A_`, since XML reads them back as spaces. A `_` that would start an escape before them is escaped too (`ReadsAsEscape` and `IsWrittenAsEscape` take the attribute mode).
  - `AutoFit` takes each cell's row, so that cells merged across columns can be left out.
  - `ExcelRange.Corners()`: a range's two corners, for whole rows and columns too (`B:F` as `B1:F1048576`).
  - `SheetSettings` says where the settings came from (`Origin`, `Given`), for a table name's error.
  - `ExcelTableOptions.BuiltInStyleName`: a built-in style's name in Excel's case.
  - The docs of `AutoFilter`, `Table`, `ExcelTableOptions.Name` and `MergeCells`.
- **Decisions made while building,** approved at review as built (every recommendation):
  - **A table's header names follow Excel's rule exactly,** as probed through `ListObjects.Add`:
    - One number for the whole header, from 1, goes up with each name tried.
    - An empty cell is `Column` and the number.
    - A repeat of a name to its left, ignoring case, takes the next number, 2 at least.
    - A name tried that a column has, its own or one given to a column to its left, is passed over.
    - A name and its number have at most 253 characters.

    So `[Name, Name, Name2]` is `Name, Name3, Name2`. A name can depend on the names to its right, so a table's header cells are written when the header record ends.
  - **A typed header field is named by its text as written** (`True`, `2024-01-15T00:00:00.0000000`), the header's text in CSV. Excel's own table would take its text as shown (`TRUE`, `1/15/2024`).
  - **A header field over 255 characters throws at that field,** as a field that fails does. If the record goes on, its column is named as an empty one is.
  - **Header and table names compare as sheet names do** (`SheetNames.Key`: `ß` as `ss`, full-width as ASCII). That is stricter than Excel: a pair Excel would keep may be renamed, but none it refuses is left.
  - **`NewSheet`'s tables are numbered from 2** (`Orders2`, `Orders3`), as designed, where Excel's copies of a sheet give `Orders3`, `Orders4`. `_` comes before the number only when it is needed (`AB_2`, `R_3`, where Excel's copies give `AB_3`, `R__3`).
  - **A given table name that the workbook has throws:** `ArgumentException` (`options`) for options given, and `InvalidOperationException` for `[ExcelSheet]`'s or `Sheets`', each naming where it came from. So `Sheets` with a table's `Name` serves one sheet only; the message says to leave the name out.
  - **Table names are reserved when the sheet is added,** so a clash throws from `AddSheet`, and a sheet that writes no header still holds its name.
  - **Under a title, a range in row 1 throws:** the title's merge's width isn't known until the first record ends.
  - **A range merged before the header in the table's columns** throws `InvalidOperationException` when the header record ends, and the header is discarded. The range stays, so that sheet gets no table: a programming error, which fails loudly.
  - **`MergeCells` takes whole rows and columns** (`8:9`, `B:C`, written `A8:XFD9`), `$` signs, and corners in either order. These throw `ArgumentException` (`range`): text that isn't a range, one cell, a range that overlaps one before it, and one in row 1 under a title or in the table. On a CSV writer it throws `InvalidOperationException`, and on a closed sheet `ObjectDisposedException`.
  - **Auto-fit leaves out only ranges merged across two columns or more.** The design had merged cells count nothing, but Excel's AutoFit counts every cell of a range down one column, below its first row too (checked).
  - **A tab and a line feed in an attribute are escaped,** with a `_` before them that would start an escape; only table columns' names have them. Excel refused a workbook without the fix (below).
  - **Smaller ones:**
    - Built-in styles are written in Excel's case.
    - A table without a style writes `tableStyleInfo` without a name, which Excel opens unstyled.
    - The sheet's relationships part holds the table's `rId1` alone; step 5's links add to it.
    - A range merged after a full sheet's last record, before the next record, belongs to the full sheet, which is still open.
- **What Excel showed** (Excel 365, 16.0 build 20527, through COM, on 2026-11-10; the probes, the workbooks' writer, the scripts and the answers are in `Csv\_bench\results\phase5\step4`):
  - **Naming** (`rename.ps1` and `rename2.ps1`, `rename.txt` and `rename2.txt`): 38 headers made into tables, with text, empty cells, repeats in any case, white space, line breaks, numbers, booleans, dates and names of 251 to 255 characters; and six tables copied with their sheets, for the numbers Excel gives copies. Their answers gave the rule above.
  - **AutoFit and merged cells:** Excel's AutoFit leaves out a range merged across columns, and counts every cell of one merged down a column.
  - **The step's 16 workbooks** (`writer`, `check.ps1`, `answers.txt`):
    - Every one opens without a repair.
    - Filters and their hidden names are as written. That includes sheets whose names need quotes (`'Q1 Orders'`, `'Bob''s'`, `'A1'`) and one that reads as an escape (`a_x0041_b`).
    - Tables' names, ranges, styles, stripes and buttons are as written, and the renamed header (`Name|Name3|Name2|Column4|2024|True|Two\nLines| Name|NAME5`) stands. A header alone gives `A1:C2`, and under a title `A2:C5`.
    - The default names are `Q1_Orders`, `Q1_Orders2`, `_2024`, `_A1`, `_R2D2` and `Ordersé_` (from `Ordersé€`).
    - `NewSheet` gives `Orders`, `Orders2` and `Orders3`, and each sheet its filter, title and merge.
    - The merges `8:9`, `A4:C4`, `D2:D3` and `E2:E3` stand, and `E2:F3` beside a table. A table with `AutoFilter` shows the table's buttons alone.
    - The widths are about 5% wider than Excel's AutoFit at 144 DPI, as at step 3, merged columns included.
  - **Without the escape fix** (`unescaped`), a column named `_x0041` and a line break made a workbook Excel refuses.
  - **LibreOffice** converts all 16 and keeps the tables, filters, names and merges (`lo_report.ps1`, `lo-answers.txt`).
- **Tests:**
  - `MergedRangesTests`: overlaps at the edges; against a brute-force search over random ranges; 200,001 ranges beside a tall one; auto-fit's cells.
  - `TableWriterTests`: Excel's names for the probes' headers; the cut at 251 to 255 characters; names that compare as sheet names do; a surrogate pair not cut; the part, its options, and the relationships part.
  - `TableNamesTests`: names made from sheets', numbered as Excel numbers a copy's table, cut to take a number, and the workbook's names ignoring case.
  - `SheetTableTests`, sync and async, covers:
    - the filter and its name, under a title in a sheet whose name needs quotes, over a header alone, and none without a header;
    - a table named after its sheet (the parts' order, the relationships, the content type), its options, renamed header cells, a field too long, and a header alone;
    - unique names across the workbook, from options, the attribute and `Sheets`;
    - a table's buttons as the filter, `NewSheet`'s repeats, and the attribute's name.

    Each workbook goes through the validator. ClosedXML reports the tables, and the records read back as Phase 4's look does through CsvHelper.Excel, ClosedXML and ExcelDataReader.
  - `MergeCellsTests`, sync and async: ranges before, while and after the rows; the ranges refused; row 1 under a title; a table's columns before and after the header; a CSV writer and a closed sheet; auto-fit; `NewSheet`.
  - `AutoFitTests` (cells merged across columns), `XmlOutputTests` (tabs and line feeds in attributes), and `SheetAutoFitTests` (a table's empty header named `Column1`).
  - Every CsvHelper.Excel suite passes, with 0 warnings: 3,273 tests on .NET 10 and on .NET 8, and 3,225 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in. The corpus's pinned hashes still match.
- **The per-cell cost** (the row passed to auto-fit's measures, and a test of each record for the header's row) is measured at step 8.

**Step 5, links** (built, approved, with every recommendation, and committed 2026-11-10):

- **New file:** `Internal/Writing/SheetLinks.cs`, the links of the sheet being written:
  - `Target`: the policy's test, and a link's target from a URI's text.
  - Each link's cell and target, and one relationship for each distinct target, numbered in the order of first use.
  - A failed record's links dropped (`EndRecord`, `DiscardRecord`).
  - Excel's limits, `MaxLinks` (65,530) and `MaxTargetLength` (8,192).
- **Changed:**
  - `CellKind.Uri`, which `CellKinds` gives `Uri`'s type, as it gives `ExcelFormula`'s.
  - `SheetSink`:
    - Writes a URI's cell (`WriteUri`): a text cell of its text, in the `Hyperlink` cell style with a link when the policy takes it, below the header and under the cap.
    - Writes `hyperlinks` after `mergeCells`.
    - Streams the sheet's relationships part once its entry is closed: the table's first (`rId1`), then the links'.
    - With `NewSheet`, each new sheet starts without links. A full sheet's parts and the next one's can both wait for a flush, so they are kept in a list.
  - `StyleRegistry`:
    - `CellFont.Link`: Calibri 11, underlined, `#0563C1`.
    - A link's cell formats are in the `Hyperlink` cell style (`xfId="1"`).
    - `WriteCellStyleFormats` and `WriteCellStyles` write that style's entries with the first link. A workbook without links keeps Phase 4's styles part, byte for byte.
  - `PackageParts` writes the cell styles through the registry.
  - `TableWriter.AppendRelationship` writes the table's relationship alone, in place of the whole part.
  - The docs of `ExcelWriterOptions.Links`.
- **Decisions made while building,** approved at review as built (every recommendation):
  - **A target's escaped letters past ASCII are written as the letters.** The design had an absolute URI's `AbsoluteUri`, which escapes them. But Excel reads a target's escapes byte by byte: `file:///C:/Temp/r%C3%A9port.txt` opened as a file whose name has two characters where the URI has `é`, and a relative target kept `%C3%A9` as written. Only the escapes of characters that show as themselves are decoded. These stay escaped: controls, formatting characters (the directions' marks), line and paragraph separators, non-characters, private use and unassigned characters, and every ASCII escape (`%20`, `%2F`, `%23`).
  - **A target has at most 8,192 UTF-16 code units,** its fragment and its escapes as written included: Excel refuses a workbook with a longer one (found at this step). Such a URI is text without a link, as one past the cap is (decision 7).
  - **The cap counts links, not relationships** (checked): 65,531 links sharing one relationship are refused, and 65,531 relationships under 65,530 links open.
  - **A URI in the header row is text,** without a link: the header names the columns, and `NewSheet` repeats its bytes.
  - **A relative target is its text,** as designed, with its C0 controls, DEL, U+FFFE and U+FFFF escaped. Excel decodes `%20` in it, and keeps backslashes and `%5C` as written.
  - **Phase 4's look has no links** (`Links = None`), in the tests' `Written.Phase4` and the corpus tool. The corpus's `WrittenOther` has an `https` URI, which would be a link and change the pinned parts. `TypedCellsTests.WritesTextOfOtherFormatsAndTypesAsText` sets it too.
  - **Smaller ones:**
    - `Web` tests the parsed URI's scheme, so `HTTP://Example.COM` links to `http://example.com/`; white space around the text is trimmed.
    - A failed record's targets get no relationship.
    - A `Uri` through `WriteField(object)` reaches the sink as its `ToString()` text, as it reaches CSV (core's behavior, unchanged), and the link is to what that text parses to.
    - With `All`, `/path` on Linux is an absolute `file:` URI, and on Windows a relative one, as .NET parses them.
- **Left for step 7:** a sheet's distinct targets are held until the sheet ends, about 1 GB at worst (65,530 targets of 8,192 characters), as shared strings are held. Step 7 measures it, and sets a budget if it needs one.
- **What Excel showed** (Excel 365, 16.0 build 20527, through COM, on 2026-11-10; the probes, the workbooks' writer, the scripts and the answers are in `Csv\_bench\results\phase5\step5`):
  - **Probes** (`probes`, `links.ps1`):
    - The cap: `probe-answers.txt`.
    - Relative targets (backslashes, spaces, letters past ASCII, escaped or not, a fragment alone): also `probe-answers.txt`.
    - Absolute targets past ASCII: `probe-absolute-answers.txt`, and .NET's `AbsoluteUri` of each in `absolute-uri.txt`.
    - The targets' lengths: `probe-long-answers*.txt`.
  - **The step's 10 workbooks** (`writer`, `answers.txt`), each opened without a repair:
    - `Web` links `https` and `mailto` (`Hi%20there`), and leaves `file:` and relative URIs text.
    - `All` links a share (`\\server\share\a b.txt`), a Windows path (`C:\Temp\réport.txt`), an escaped relative path (`docs/résumé 1.txt`), `javascript:`, and `#Other!B2`, a jump in the workbook. A fragment is the link's sub-address.
    - Links' cells show the `Hyperlink` style, and in a column with a format and wrap keep both (`@`, wrapped).
    - Links work in a table. With `NewSheet`, each sheet has its own table and links, and the header's URI is text.
    - 65,530 links open, and the next URI is text in Normal. A target of 8,192 characters is a link, and one of 8,193 text.
    - A failed record's link is gone. Letters past ASCII and an emoji open as themselves.
  - **LibreOffice** converts all 10, and keeps every link and the `Hyperlink` style; it writes relative targets against its own folder (`lo_report.ps1`, `lo-answers.txt`).
- **Tests:**
  - `SheetLinksTests`: each policy's targets; letters past ASCII and the escapes kept; controls; the length; relationships by first use; a discarded record; XML in a target; the cap.
  - `SheetLinkTests`, sync and async:
    - `Web`, `All` and `None`;
    - links after a table's relationship, and in a column's format and wrap;
    - the header's URI in each new sheet, and each new sheet's table and links;
    - a failed record, the cap, and a target too long.

    Each workbook goes through the validator. ClosedXML reports the links and their style, and the records read back as without links through CsvHelper.Excel, ClosedXML and ExcelDataReader.
  - `StyleRegistryTests.PutsALinksCellFormatsInTheHyperlinkCellStyle`; `TableWriterTests`' relationship; `TypedCellsTests`' kind of `Uri`.
  - Every CsvHelper.Excel suite passes, with 0 warnings: 3,319 tests on .NET 10 and on .NET 8, and 3,271 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in. The corpus's pinned hashes still match.
- **The per-cell cost** (a `Uri` field's text parsed as a URI, and its link) is measured at step 8.

**Step 6, corpus and oracles** (built, approved, with every recommendation, and committed 2026-11-11):

- **New files:**
  - `tests/CsvHelper.Excel.Tests/Files/excel-written/`: Excel's answers about ten presentation workbooks (below).
  - `tests/CsvHelper.Excel.Tests/Corpus/ClosedXmlLook.cs`: a workbook's look as ClosedXML reads it, its links through the Open XML SDK, in the terms of Excel's answers.
- **Changed:**
  - The content set (`tools/CsvHelper.Excel.Corpus/Written.cs`):
    - `WrittenLook`: a sheet's options, the ranges merged on its writer (before a row, or after the rows), the text Excel shows of each value in a column's Excel format, and the names Excel gives its tables and their columns.
    - `WrittenSheet` takes a look, and keeps its record type's `[ExcelSheet]`. A sheet of records with merged ranges is written by `AddSheet<T>` and `WriteRecords`. `WrittenColumn` keeps its member's `[ExcelColumn]`.
    - Ten workbooks, and their record types (`WrittenLookOrder` to `WrittenReportLine`).
  - The `written` command (`ExcelWritten.cs`) also answers about every workbook:
    - its defined names;
    - each sheet's used range and look: frozen panes, filter, tables, merged ranges, links, hidden columns, the rows Excel sized otherwise than standard, and each cell's look from row 1 to the row below the used range;
    - last, each column's width at Excel's own AutoFit.

    `ExcelOracle`'s `WriteMerged`, `WriteNames` and `ColumnName` are internal, for it.
  - The corpus tool's privacy check (`Privacy.cs`) passes email addresses at the domains reserved for examples (RFC 2606), which the `mailto:` links use.
  - The tests (`Corpus/`):
    - `WrittenAnswers.cs` loads the names, the used range, the look (`SheetLook`, `SheetTable`, `SheetLink`, `WrittenName`) and Excel's AutoFit.
    - `WrittenOracle.cs`:
      - each cell under a title, a table's header as its look names the columns, and a value in a column's Excel format (`InFormat`);
      - each sheet's look (`Looks`) and the filters' names (`FilterNames`), by the plan's "What a sheet gets";
      - the parts' hidden and auto-fitted columns (`WrittenParts`).
    - `WrittenAnswersTests.cs`: four new tests (below), and the cell comparison's new kinds.
    - `LibreOffice.cs` converts every written workbook to `.xlsx` too, for what LibreOffice keeps of the look.
  - The writer (`SheetSink`): a header cell in General, in a sheet whose columns have options, says `s="0"` (below).
  - `Files/README.md`.
- **The set,** ten more workbooks, written with Phase 5's defaults but where named. The first ten keep Phase 4's look, and their parts.
  - `look.xlsx`: the default options; a title over a frozen column; a title over one column; columns frozen without the header.
  - `columns.xlsx`:
    - columns' Excel formats: `#,##0.00`, `0.0%`, `0.00E+00`, a currency in red, `# ?/?`, `000000`, a date's `dd/mm/yyyy hh:mm`, a boolean's text section, `@`;
    - widths, wrap and hidden columns, from `[ExcelColumn]` and from `Columns`, which replaces it whole;
    - a column of a number, text, a boolean, a date and a decimal in one format.
  - `autofit.xlsx`: later values wider than the first, General numbers, text past the cap and with a line break, two records measured of four, a bold header wider than its cells, formulas, and dates at midnight.
  - `filters.xlsx`: filters over the rows; under a title in `Bob's orders`; over a header alone with an empty field at its end; beside a frozen column in `a_x0041_b`.
  - `tables.xlsx`:
    - tables named after their sheets (`Orders`, `Q1_Orders`, `_2024`), in a style, in one given in lower case, or in none;
    - without stripes or buttons;
    - header cells renamed (`Name|Name3|Name2|Column4|2024|True|Two\nLines| Name|NAME5`);
    - over a header alone, under a title, with `AutoFilter`, and from `[ExcelSheet]`.
  - `merges.xlsx`: ranges merged down a column before the header and while the rows are written, down and across after them, past the rows, under a title, and beside a table.
  - `links.xlsx` (`Web`) and `links-all.xlsx` (`All`):
    - `https` with a fragment (twice, one relationship), `mailto` with an escape, `HTTP://Example.COM`, an escaped letter past ASCII;
    - `file:`, relative and `urn:` URIs as text under `Web`;
    - a share, a Windows path, an escaped relative path, a jump to `#Other!B2` and `ftp` under `All`;
    - in a table, in a column's format and wrap, and a header's URI as text.
  - `newsheet-look.xlsx`: `RowLimit = NewSheet` at four rows a sheet, with a title and a table, and with a title, a filter and a frozen column.
  - `presentation.xlsx`: every setting together.
- **Decisions made while building:**
  - **The answers cover every workbook,** Phase 4's too: their look is empty, and their cells' answers are unchanged.
  - **The oracle's rules for links are the test's own:** a jump has no address; a relative URI's address is its text and a file's its local path; another's is its absolute form, with its fragment as the sub-address; escaped letters past ASCII show as the letters.
  - **A value in a column's Excel format shows the text the set gives,** as formulas' expected text is given: the test doesn't format numbers. Its type may be `number`, `date` or `currency` (which Excel's `Value` gives a currency format).
  - **Excel's `NumberFormat` spells `"$"` as `$`,** which the comparison allows (`ExcelCode`).
  - **Excel's AutoFit is the widths' oracle:** each auto-fitted column is as wide as Excel's AutoFit makes it, and at most a tenth and half a character wider. And most columns of a workbook must be wider than Excel's AutoFit, so that answers that copied the widths written can't pass.
  - **ClosedXML and LibreOffice are compared with Excel's answers,** not the oracle's, in the same terms. What LibreOffice does its own way is left out of both sides, by rules the test states; its other differences are listed.
  - **The window is sized before the panes are read** (found while building). An invisible Excel lays out a workbook opened after the first in a window with no room (`UsableHeight` 0), and then reports a frozen split clipped to it: `SplitRow` 1, or 0, for two rows frozen, and another answer from run to run. A window of 800 by 500 points gives the split as written, the same in two runs (`panes.ps1`). The command fails if a window has no room.
  - **Email addresses at reserved domains pass the privacy check:** no one has one.
- **A writer bug, found and fixed:** LibreOffice gives a written cell without a style its column's style, where Excel gives it General (the design's check, at step 2).
  - So the header's cells, which take neither their column's format nor its wrap, took both in LibreOffice when the header isn't bold: a styled table's header (`presentation.xlsx`'s `Notes` wrapped), or one with `BoldHeader` off. A column format with a text section would have shown `Paid: Paid`.
  - Now a header cell in General, in a sheet whose columns have options (`SheetSettings.HasColumns`), says `s="0"`, which LibreOffice takes as General (checked: `header-style`). The check is in the styling branch, which other cells don't take, and other sheets' parts are unchanged: only `tables.xlsx`'s and `presentation.xlsx`'s hashes moved.
  - `SheetLookTests.SaysAHeaderCellIsInGeneralWhenItsColumnsMayHaveStyles` pins it. Without the fix, it fails, and so do the two hashes and `LibreOfficeKeepsTheLook` of `presentation.xlsx`.
- **What Excel showed** (Excel 365, 16.0 build 20527, at 144 DPI, through COM, on 2026-11-11; the probes and scripts are in `Csv\_bench\results\phase5\step6`):
  - It opened all twenty workbooks as they are, and every sheet's look is as its settings say. No look difference is listed:
    - panes, filters and their hidden names, tables (names, ranges, styles, stripes, buttons, renamed columns), merged ranges, links (addresses and sub-addresses), hidden columns;
    - the title's row (18.5 points) and wrapped rows sized taller;
    - the bold header, the title in bold 14, links in the `Hyperlink` style, columns' wrap in their cells, and their format and wrap in the cells not written (the title's row, an empty cell, the row below).
  - Every cell reads as the oracle says, the text of each value in a column's format among them (`" 1/4"` for 0.25 in `# ?/?`, `Paid: TRUE`, `45,306` for a date in `#,##0`), but in three cells:
    - **`TimeSpan.MaxValue`** (`autofit.xlsx`, and `types.xlsx` in Phase 4's look) shows `###################` at auto-fit's width. A probe (`durations.ps1`, `durations.txt`) found that Excel shows no serial of 2,958,466 or more in a date or time format, `[h]:mm:ss` too, at any width: that is the instant after 9999-12-31 23:59:59.999. A value that rounds up to it in its format (2,958,465.9999999884 in `[h]:mm:ss`) shows `###` too. Phase 4 took `types.xlsx`'s case for a width (decision 14); it isn't one. See "Left open".
    - **`newsheet-look.xlsx`'s** `'Orders (2)'!C3` and `'Orders (3)'!A3`: the first sheet fills after two records, before auto-fit's 100 held rows end, and every sheet takes its widths (step 3). Only a row limit far below Excel's, or `AutoFit` near its limit, can do that.
  - Auto-fit's widths are as wide as Excel's AutoFit, or a little wider (about 5%, as at step 3), but where auto-fit counts otherwise by design: the text cap (`Text!B`, `Capped!A`, `columns.xlsx`'s `Options!K`), two held rows, a formula's default width, dates at midnight, a title over one column (which Excel's AutoFit counts), `NewSheet`'s widths, and `TimeSpan.MaxValue`, which Excel's AutoFit fits in 6.27.
- **ClosedXML** reads the same look but a table column's name with `_x000A_`, which it doesn't decode.
- **LibreOffice 26.8.2.2** opens all twenty, and keeps the look but:
  - its own ways, which the comparison leaves out: it gives each cell of a merged range its first cell's look, names a font of its own for East Asian text (`Microsoft YaHei`), wraps text with a line break, and spells codes its own way (`\$#,##0.00;[RED]"-$"#,##0.00`, `yyyy\-mm\-dd`);
  - it writes a file's and a relative path's target against the folder it saves in, and the jump to `#Other!B2` as a link to its own file at the cell, as found at step 5.

  It shows Excel's text but a boolean in a column's number format, which it shows as 1 or 0, and the differences listed before.
- **Tests:**
  - `WrittenAnswersTests`, for each workbook:
    - `ExcelShowsTheLook`: each sheet's look against the oracle's, entry by entry, and the filters' hidden names.
    - `AutoFitsAsWideAsExcel`: each auto-fitted column against Excel's AutoFit.
    - `ClosedXmlReadsTheLook` and `LibreOfficeKeepsTheLook`: ClosedXML's reading of the workbook, and of LibreOffice's `.xlsx`, against Excel's answers.
    - `ExcelReadsWhatWasWritten` also checks the text of values in a column's format, and that no typed cell shows `########` but those listed; `ExcelShowsTheWrittenWidths`, a hidden column's 0.
  - `SheetLookTests.SaysAHeaderCellIsInGeneralWhenItsColumnsMayHaveStyles` (the writer fix).
  - `WrittenAnswersTests` has 164 tests on .NET 10 and on .NET 8 (44 before), and 148 on .NET Framework, which lacks the two .NET-only workbooks. The LibreOffice tests, whose converted workbooks CI makes, take about 10 seconds more.
  - Every CsvHelper.Excel suite passes, with 0 warnings: 3,441 tests on .NET 10 and on .NET 8, and 3,385 on .NET Framework, each with the 4 GiB Zip64 test skipped as opt-in. The first ten workbooks' pinned hashes still match, on every target.
- **Mutation check** on net10.0, each bug planted alone and its file restored, checked by SHA-256. The drivers, `mutate.py` and `mutate_tool.py`, and their results, `mutations.jsonl` and `mutations-tool.jsonl`, are in `Csv\_bench\results\phase5\step6`. Each bug ran `WrittenAnswersTests`, about 15 seconds.
  - **The oracle, the loader, the readers and the content set** (46 bugs): 40 caught.
    - Every wrong rule is caught. That covers the title's merge, merges by hand, frozen rows and columns, the filter beside a table and its range, a header-only table, a style's case, the link policy, a fragment, escapes past ASCII, a file's address, hidden columns, tall rows, a styled table's bold, the `Hyperlink` style, columns' format and wrap in cells not written, codes' spelling, the names' visibility, `[ExcelSheet]`, `Columns` against the attribute, a new sheet's rows, a table's header, a date in a format, and a URI's text.
    - The loader's and ClosedXML's readings are caught too: panes swapped, wrap not read, a link's location, frozen columns.
    - So is the content: a fraction's text, a table's name, a renamed header's order, ranges not merged, and a text's shown text.
    - The six that live remove a check rather than change a rule, so the right answers pass them by construction. For four, a bug the check alone sees is caught: a fraction's text, Excel's wrap not read, the names' visibility, and a text's shown text (planted for it). The other two are equivalent for the set's content:
      - a formatted number's type, behind its value and the pinned parts;
      - LibreOffice's merged ranges left out whole, whose first cells LibreOffice keeps.
  - **The `written` command** (4 bugs, each run through Excel over the repository's answers, which were restored and checked file by file): all 4 caught.
    - the window not sized: `ExcelShowsTheLook` of `links.xlsx` and `links-all.xlsx` fails, and which workbooks clip varies from run to run;
    - no row's height: 7 tests;
    - Excel's AutoFit not run: 10, by the check that most columns are wider than Excel's;
    - no look of the row below the used range: 12.
- **Left open:**
  - **Durations of Excel's last serial or more** (above): the review approved writing them as text, as `OutOfRangeDates` says, as negative durations are. Step 7 does it.
  - Step 5's held targets: step 7.

**Step 7, security, fuzzing and AOT** (built, approved, with every recommendation, and committed 2026-11-11):

- **New files:**
  - `tests/CsvHelper.Excel.Tests/Security/`:
    - `PresentationFuzzTests.cs`: the presentation fuzzer (below).
    - `FormatCodeFuzzTests.cs`: the fuzzer of Excel's format codes as columns take them (below).
    - `LinkMemoryTests.cs`: the memory a sheet's links hold (below).
  - `tests/CsvHelper.Excel.Aot/`: the NativeAOT app (decision 14), in the solution's `tests` folder.
  - `tools/CsvHelper.Excel.Corpus/ExcelFuzzed.cs`: the corpus tool's `fuzzed` command, the fuzzers' "opens in Excel when it runs on demand".
    - It opens in Excel each workbook a fuzzer saved (`CSVHELPER_EXCEL_FUZZ_SAVE`). It reports which Excel repairs or refuses, and the numbers Excel shows as `########`.
    - It opens again, alone in a fresh Excel, each workbook Excel turned down. Only those it turns down again count. In a long run Excel sometimes turned one down while busy, and the next repair log was then taken for that workbook's.
    - Nothing it reads is committed. `ExcelOracle.WhenReady` is internal, for it.
- **Changed:**
  - The writer:
    - durations of Excel's last serial or more, as step 6's review decided (`Serial.PastLast`, `SheetSink.WriteDuration`);
    - a discarded record's link targets (`SheetLinks`);
    - the widest column (`ColumnWidths.Widest`, `SheetSink.AppendColumns`);
    - names alike but for characters Excel ignores (`SheetNames.Key`);
    - `FormatCheck`'s rules and `NumberFormatWidth`'s estimates (below).
  - Public documentation: `ExcelOutOfRangeDates`, `ExcelWriterOptions.OutOfRangeDates` and `Links`, `AddSheet`'s remarks, `ExcelColumnOptions.Width`, `ExcelColumnAttribute.Width` and `ExcelSheetOptions.MaxAutoFitWidth`.
  - `SheetSink.LinkTargets` and `SheetLinks.Targets` (internal), for the memory test.
  - The tests:
    - each change's unit tests: `TypedCellsTests`, `NumberFormatWidthTests`, `SheetAutoFitTests`, `SheetLookTests`, `FormatCheckTests`, `SheetLinksTests`, `SheetNamesTests`, `TableWriterTests`;
    - `TimedCollection`'s documentation, since the memory tests run in it.
  - The corpus:
    - Excel's answers about `types.xlsx` and `autofit.xlsx`, whose `TimeSpan.MaxValue` is now text;
    - the oracle's durations (`WrittenOracle`);
    - two reasons gone from `WrittenAnswersTests` (`LastSerial`, `LibreOfficeFmt`);
    - `Files/README.md`.
  - CI (`.github/workflows/ci.yaml`): a step that publishes the app with NativeAOT and runs it.
  - Core: no change.
- **Durations of Excel's last serial or more** (step 6's "Left open", approved at its review):
  - A duration of 2,958,466 days or more, or one that rounds to it, is text, or throws, as `OutOfRangeDates` says, as a negative one does. The message is "a duration of 2,958,466 days or more, which Excel shows as ########".
  - `TimeSpan.MaxValue` is text in `types.xlsx` and `autofit.xlsx`. Excel's answers were asked again: it shows `10675199.02:48:05.4775807`, in a column 23.64 wide. No other answer changed. Three listed differences went with it, and two LibreOffice ones (its `#FMT`).
  - The oracle had computed the duration's milliseconds as `(ticks + 5,000) / 10,000`, which overflows for `TimeSpan.MaxValue`; the listed difference had hidden it.
- **The links' memory** (step 5's "Left for step 7"):
  - **Measured** (`LinkMemoryTests`, the process's memory, run alone): each distinct target is held once until the sheet ends, about 16,450 bytes for one of 8,192 code units.
    - The test's 2,048 distinct targets under 65,530 links held 34.5 MB.
    - Excel's worst case, run once with `CSVHELPER_EXCEL_LINK_TARGETS=65530`, held 1,078,605,040 bytes (1.0 GiB). That takes 537 million characters of distinct link text written to one sheet.
  - **Found and fixed:** a record that failed after a link kept that link's target, unused. Records that fail, whose links the cap doesn't count, could hold any amount. 2,000 such records, each with an 8,192-character target, held 32 MiB before the fix. Now a discarded record takes the targets it added with it, and the same records hold 17,792 bytes.
- **The presentation fuzzer** (`WritesRandomPresentationsAsTheirOptionsSay`, `CSVHELPER_EXCEL_FUZZ_PRESENTATIONS`, 1,000 by default):
  - **Workbooks:** one to three sheets of random options:
    - titles of text pieces, now and then 32,767 characters;
    - a bold or frozen header, frozen columns, a filter, auto-fit over 0 to 100 records with a random cap;
    - tables with names and styles in any case, banding and buttons;
    - columns' widths, formats from `FormatCheckTests.Opened`, wrap and hidden.
  - **Planted now and then, each one alone:** an option Excel wouldn't open (a title's length, `FreezeColumns`, `AutoFit`, `MaxAutoFitWidth`, a table's name or style, a column's width or format), a table or filter without a header, and an earlier table's given name in other cases.
  - **Headers:** empty, repeated in other cases, numbered as repeats are, white space, line breaks, `_x0041_`, ß and ẞ against `SS`, the Ohm and Kelvin signs, full-width letters, a lone surrogate, a byte order mark, a soft hyphen, a direction's mark; names around 253 and 255 characters; numbers, booleans and URIs.
  - **Data:** text, URIs of every scheme and form (long ones around 8,192), numbers, decimals, booleans, dates at Excel's edges and durations past them.
  - **Each link policy, merged ranges, row limits:** ranges are merged before the header, between records and after them, in every form `MergeCells` takes and as text that is none. `NewSheet` runs at four to seven rows.
  - **Checks:**
    - The validator passes.
    - The workbook reads back through CsvHelper.Excel as the same data in Phase 4's look does, cell by cell, but for a table's header and numbers in a column's format.
    - The test's own rules, from the writer's documentation of Excel's: which options and ranges are refused; each table's name, range, style and columns (renamed by `TableWriter`'s rules, unique by Excel's comparison); the filter and its hidden name; the panes; the merged ranges; the hidden columns and every width within 0 to 255; which cells are links, with `Web` linking nothing but `http`, `https` and `mailto`.
  - **Runs:** the defaults' 1,000 workbooks write about 1,800 sheets, 560 tables, 1,800 links and 640 merged ranges, and refuse what was planted. Seeds 1 to 80,999 passed after the fixes below, and 20,000 more after the last.
- **The format fuzzer** (`ChecksRandomFormatCodesConsistently`, `CSVHELPER_EXCEL_FUZZ_CODES`, 10,000 by default):
  - **Codes:** random codes of the grammar's pieces: placeholders, exponents, fractions, percents, literals quoted, escaped and open, fills, colors, conditions, locales, `DBNum`, elapsed times, every date token, `@`, `General`, sections, and characters that are none of these. Now and then a code is one piece repeated to around 255 characters.
  - **Checks:**
    - `FormatCheck` throws nothing, and a column's options take a code exactly when it passes.
    - Each code that passes measures numbers, dates and text without throwing, in pixels within bounds.
    - Workbooks of a column for each, over numbers, dates, durations, booleans and text, pass the validator and read back.
  - Whether Excel opens what the check passes is Excel's to say, on demand. `CSVHELPER_EXCEL_FUZZ_CODES_PER_WORKBOOK` (20 by default) at 1 gives each code a workbook, so that Excel's repair names its code.
- **Found and fixed by the fuzzers and Excel** (Excel 365, 16.0 build 20527, through COM, on 2026-11-11; the probes, the fuzzers' workbooks, the reports and the scripts are in `Csv\_bench\results\phase5\step7`):
  - **Measuring 9999-12-31's last millisecond threw** (the presentation fuzzer, seed 75). `DateTime.MaxValue` is that millisecond. In a column's format of whole seconds it rounds up to 10000-01-01, and `NumberFormatWidth.DateOf` threw `ArgumentOutOfRangeException` from `AddDays`. Excel shows it as `########` (step 6's probe), and now the estimate gives it nothing.
  - **A column wider than the schema** (seeds 401 and 473): the validator refused `width="255.2109375"`.
    - A column's own `Width` of 255 characters is 255.7109375 as stored at 96 DPI. Excel itself stored 255.6328125 for `ColumnWidth` 255 at 144 DPI (`widths.ps1`, `widths.txt`). Auto-fit capped at that, and a formatted number's width without auto-fit had no cap.
    - The schema's `width` takes 255 at most. So the writer writes 255 at most, which Excel shows as about 254.29 characters.
  - **Names alike but for characters Excel ignores** (Excel repaired 2 of 2,000 presentation workbooks: seeds 2561 and 2988).
    - A table's column named `�Name2` (a lone surrogate written as U+FFFD) beside `Name2` was refused, and so was `�ß` beside `ẞ`.
    - Probes (`table-probes`, `table-rule`) found that Excel ignores, when it compares a sheet's or a table's column's names, the soft hyphen, the combining grapheme joiner, the format characters (the joiners, the directions' marks, the word joiner, the byte order mark), the variation selectors, U+FFFC, U+FFFD and the non-characters. That is at a name's start or inside it; it kept one at the end. It compares U+200B, U+180E, the language tags, U+00A0 and U+3000.
    - So a sheet named `﻿Orders` beside `Orders` was refused too: Phase 4's sheet names had the same gap. A header field that starts with a byte order mark, as a CSV's first field can, is enough.
    - `SheetNames.Key` now drops them wherever they are, by a fixed list, so that every target agrees. It is stricter at a name's end. `﻿Name` and `Name` are one name to the writer: a table renames the second `Name2`, and `AddSheet` refuses the second sheet. With the fix, Excel opened all 2,000 workbooks of those seeds.
    - Phase 4's choice to keep the non-characters in the key changes with it, as Excel's answers say (`SheetNamesTests.ComparesNamesWithNonCharacters`).
  - **`FormatCheck` passed codes Excel refuses.** In the first run of one code a workbook, Excel repaired 201 of the 1,090 random codes the check passed (18%). Each repair logged "Format from /xl/styles.xml".
    - Probes of each kind through COM (`probe/probe.cs`, `codes*.txt`, `answers*.txt`) gave rules. Each was added, stricter where Excel's own form wasn't checked:
      - **Exponents:** an uppercase `E` is an exponent wherever it is (`E`, `Ed`, `E+d` refused), with digit placeholders before and after it, once in a section, and no `,` after it.
      - **Fractions:** a `/` in a number is a fraction's: one, after a placeholder and before a denominator (a placeholder or a number), without a `.`, a `,` or an exponent. In a date it comes after a date token.
      - **B:** `B` in upper case only as `B1` or `B2`, once, before the date's tokens.
      - **Brackets:** one of each kind of bracket in a section. Conditions only in the first two sections and not with `@`. Elapsed time not after an hour, minute or second, and not with `AM/PM`.
      - **Text and `General`:** `@` in the last section only, without `.` or `%`. A fourth section without placeholders, dates, `.` or `%`. `General` once, and without `@` or `.`.
      - **Dates:** no digit outside quotes, and no `.` before the first date token.
      - **Percent signs:** 30 at most.
      - **Length:** Excel opened 83 `=` and 255 `0`, but not 84 `=`, so its limit isn't a count of characters. The check counts 3 for each character outside quotes that shows as itself, 2 for each in quotes and each decimal placeholder, and 1 for the rest, and takes 249 at most. Every refusal probed is over it, and Excel's own longest codes (its accounting formats) are well under it.
    - **The rounds after that** (`fuzzed-code2` to `fuzzed-code7`, fresh seeds each): 11 repairs in 1,704 codes, then 5 in 2,854, 2 in 5,702, 2 in 8,461, 0 in 8,398, and 0 in 2,798 with the final code. Each round's repairs gave the next rules.
    - `FormatCheckTests` has every probe's answer: 113 more codes Excel opened, 107 it refused with the check's reasons, and 21 it opened that the check refuses.
  - **`NumberFormatWidth`'s estimates.** In the first rounds, Excel showed 5,006 numbers as `########` where the estimate said they fit. Excel's answers at a wide column (`codes14.txt` to `codes16.txt`) gave four fixes:
    - **Brackets alone:** a section of a color, `DBNum` or a condition alone shows the number in General (`[Red]` is `[Red]General`; `[DBNum3]` had measured nothing). A locale alone shows nothing, as an empty section does.
    - **Rounding:** a date or time rounds to the second unless its format shows fractions of a second. 23:59:59.6 is the next day in `yyyy-mm-dd` and `00:00` in `hh:mm`, and 9999-12-31's last millisecond can't be shown in `yyyy` or `d`. The estimate had rounded only in formats that show seconds.
    - **Elapsed time alone:** with nothing else but literals, elapsed time counts past the last date (`[h]` shows 2,958,466 days as 71003184). With any other date or time token it stops there (`[h]:mm` shows `########`).
    - **Elapsed time and a date:** after a date part, elapsed time is the time of day in its unit (`d[ss]` shows 1234.5 as `1843200`, `mmmm[mm]` as `May720`). Before one, it is the whole time (`[ss]d` shows 328 as `2833920023`). The estimate had taken every elapsed token beside a date for the hour, minute or second of its clock, which Phase 4's answer of `[h]` agreed with.
- **What Excel showed at the end:**
  - **Presentation workbooks:** of seeds 1 to 2,000 and 2,001 to 4,000 (the second written again after the last fix), Excel opened all 4,000 as they are.
    - It showed 592 numbers of the second set as `########`. 584 come from the sheet's own settings: past the records auto-fit holds (226), without auto-fit past the first record (146), hidden columns (89), a column's own width (42), a sheet `NewSheet` started with the first's widths (37), the estimate saying Excel can't show the value (32), and a range merged across columns, which auto-fit leaves out as Excel's AutoFit does (12).
    - The other 8 are values Excel shows at no width: fractions of large numerators (Excel shows `0/8` of 45306 as `########` at 200 characters), a double of 309 digits at the widest column, and 9999-12-31's last millisecond in `ss.0`.
  - **Code workbooks:** of the last round's 2,798, Excel opened all as they are. It showed 4,896 numbers as `########`. The estimate gives 3,850 of them no text (Excel can't show them), and the others a width too narrow, in codes it doesn't model (below).
- **Decisions made while building** (each recommended, and approved as built):
  1. **No cap on the links' memory.** It is held until the sheet ends, at most 1.0 GiB, for 537 million characters of distinct link text written to one sheet. That is as auto-fit's held records and shared strings are: bounded by Excel's own limits, and grown only by what is written. `Links`' documentation says so, and `None` holds nothing.
     - Alternative: a cap of the targets' characters a sheet (say 64 Mi), past which a URI is text without a link, as past 65,530 links.
  2. **A column is 255 wide at most, as stored:** the schema's limit, about 254.29 characters, where Excel's own widest is 255.63 at 144 DPI. Every workbook then passes the validator, as the gate asks.
     - Alternative: write Excel's width, and let the validator's error stand for a column of 255 characters.
  3. **`FormatCheck`'s length is a count of its own,** stricter than Excel for long codes: Excel opens 255 `0`, 245 `.` and 125 quoted characters, and the check refuses 255 `0` and 125 quoted characters. No code of a realistic length is near it.
  4. **`FormatCheck` refuses some forms Excel opens,** as its rules already did where a form wasn't needed: `B` in upper case but `B1` and `B2` (Excel reads `BB` and `Byyyy` as the Buddhist year), elapsed time after an `m` (which may be a month: `mmmm[mm]`), `AM/PM` with elapsed time in either order, a `,` after an exponent (`0E+0,`), and `General:`.
  5. **The names' key drops the characters Excel ignores wherever they are,** where Excel kept one at a name's end: stricter there, so that no pair Excel takes to be one is written.
  6. **The NativeAOT app keeps NuGet's audit warnings as warnings.**
     - The only package they can name is the SDK's own ILCompiler. SDK 10.0.402's 10.0.13 has an advisory (GHSA-p3jp-9v83-76w9), so the solution's build shows that warning twice on this machine until its SDK is updated. CI installs the newest SDK.
     - Trim, AOT and compiler warnings are errors.
     - Alternative: suppress that advisory in the app's project.
  7. **The presentation fuzzer compares the data with the same data in Phase 4's look,** rather than modelling each cell's text, since every typed cell's text is Phase 4's fuzzers' and the corpus's to check.
  8. **The new fuzzers' defaults** are 1,000 workbooks and 10,000 codes, a second or two each on .NET 10 and two to four on .NET Framework.
- **The NativeAOT check** (decision 14):
  - The app's `Order` is a `[CsvRecord]` with `[ExcelSheet]` (a title, a table named and styled, a frozen column), `[ExcelColumn]` (a width, two formats, a hidden column) and a `Uri`.
  - Published with `PublishAot` for `win-x64`, the trimmer and ILC give no warning. Each one would be an error, one by one: `TrimmerSingleWarn` is off, and a call planted for the check failed the publish with IL2057.
  - The native app writes the sheet, finds its title, merge, panes, widths, hidden column, link, formats and table in the parts, and reads the orders back by the header's row and by `ExcelHeaderRow.Find<Order>()`. Its native build is 4.3 MB.
  - CI's step publishes it from PowerShell, whose environment `vswhere` needs; Git Bash drops `ProgramFiles(x86)`.
- **Tests:**
  - New: the three fuzzers' and memory tests, and the unit tests of each change.
  - Every CsvHelper.Excel suite passes: 3,707 tests on .NET 10 and on .NET 8 (3,441 before), and 3,651 on .NET Framework (3,385), each with the 4 GiB Zip64 test skipped as opt-in. The written set's pinned hashes match but `types.xlsx`'s and `autofit.xlsx`'s, whose answers were asked again.
  - The solution builds with no warning but the AOT app's audit (decision 6).
- **Mutation check** on net10.0, each bug planted alone and its file restored, checked by SHA-256 (`mutate.py`; `mutations.jsonl`, `mutations-second.jsonl`, `-third`, `-fourth`):
  - 53 bugs, all caught: 30 by a fuzzer or a memory test, beside unit tests, and the rest by unit tests alone. The bugs:
    - durations;
    - the estimate's four fixes, and the measuring that threw;
    - the widths' cap;
    - the links' targets and the `Web` policy;
    - every one of `FormatCheck`'s new rules and weights;
    - the names' key;
    - the table names' and columns' numbering;
    - the merges' refusals;
    - four bugs in the presentation fuzzer's own model.
  - The `FormatCheck` rules the fuzzers don't catch are those whose oracle is Excel: in CI the format fuzzer can only check that the check is consistent. Excel's answers are in `FormatCheckTests`, and Excel judges new codes on demand.
- **Left open:**
  - **`NumberFormatWidth` and codes it doesn't model** (Phase 8's exact rendering): in the last codes round, about 1,040 numbers showed `########` where the estimate gave a width. The forms are conditions, elapsed time with sub-second digits past the last date, the system's formats (`[$-F800]`, `[$-F400]`), East Asian names, and an emoji's width. Random codes go far past what columns are given in practice, and the written set's columns still show every typed cell.
  - The `fuzzed` command left Excel processes behind in long runs, three this session. Their workbooks were closed and the answers are whole, but the processes stayed, hidden, until closed by hand.

**Step 8, benchmarks** (built, approved, with every recommendation, and committed 2026-11-12):

- **Changed:**
  - `bench/CsvHelper.Benchmarks/ExcelWriteBenchmarks.cs`:
    - `CsvHelper_Write`, `CsvHelper_WriteShared` and `CsvHelper_WriteOptimal` in both classes are at the new defaults: a bold, frozen header and auto-fit over 100 rows. From now on `CsvHelper_Write` isn't Phase 4's look; `CsvHelper_WritePlain` is.
    - New in both classes:
      - `CsvHelper_WritePlain`: Phase 4's look (`BoldHeader`, `FreezeHeader` and `AutoFit` off).
      - `CsvHelper_WritePresentation`: a title, the bold, frozen header, a filter and auto-fit over 1,000 rows. The orders' adds formats for its five number columns: `Id` and `Sku` `0`, `Total` `#,##0.00`, `Weight` `0.00`, `Quantity` `#,##0` (decision 2).
      - `CsvHelper_WriteTable`: the defaults, with the header and rows in a table in its default style.
    - New in the package assets: `CsvHelper_WriteLinks` and `CsvHelper_WriteLinksAsText` (decision 1).
    - `verify` reads each workbook back by finding its header (`ExcelHeaderRow.Find<T>()`), below a title when there is one.
    - The loops around the writer are Phase 4's, so that the gate times the same code around it.
  - `bench/CsvHelper.Benchmarks/QuickRunner.cs`: a filter that starts with `~` is a regular expression over `Class.Method`, so that a run names methods exactly (for the gate's pairs, below).
  - CsvHelper.Excel and core: no change.
- **Checked:**
  - `verify` read every new method's workbook back as the others: 1,000,000 orders of hash `199B37C22F244824`, and 1,000,000 package assets of hash `A46BD7E4C52574F1`.
  - With Phase 4's look the writer writes Phase 4's bytes exactly: 176,118,335 for the orders and 139,191,310 for the package assets, the same as `69907e65` (Phase 4's last commit) writes.
- **The runs:**
  - BenchmarkDotNet 0.15.8 and .NET 10.0.13, on core 4 of the Ryzen 9 9950X3D, with 4 warm-ups and 12 iterations.
  - The paged pool stayed at 4.40–4.42 GB throughout (it was 2.5 GB at Phase 4's step 7), with 36–38 GB free. During the full run, total CPU had a median of 4.2%, and 2 of 206 samples were above 10%.
  - The scripts, logs, reports and probes are in `Csv\_bench\results\phase5\step8`. Allocated counts 1 KB as 1,024 B, as BenchmarkDotNet does; Written counts 1 MB as 10⁶ B.
- **The gate** (Phase 4's look within 1% of Phase 4's):
  - The first try was BenchmarkDotNet, interleaved base/new/new/base:
    - base was `69907e65`'s `CsvHelper_Write` (Phase 4's look), and new was `CsvHelper_WritePlain`;
    - base was a scratch copy outside the repo.
    - It was inconclusive: the two base processes of the orders disagreed by 6% (1,577.2 and 1,484.9 ms) on the same code.
  - So, as for the UTF-8 dates' gate, the gate used 24 fresh QuickRunner processes, base/new/new/base six times (`quick-pairs.ps1`). Each process timed SpreadCheetah (the control) and CsvHelper.Excel in both classes. Below is the median of each side's 12 process medians, with the range of those medians:

    | Class | Phase 4 (base) | Phase 4's look (new) | Change | SpreadCheetah, base and new |
    |---|---:|---:|---:|---:|
    | Orders | 1,495.3 ms (1,474.3–1,519.7) | 1,487.2 ms (1,476.0–1,520.3) | −0.5% | 955.6 and 955.9 ms |
    | Package assets | 1,278.3 ms (1,265.8–1,353.8) | 1,248.1 ms (1,237.5–1,256.6) | −2.4% | 670.6 and 668.9 ms |

  - **Met:**
    - Phase 4's look is as fast for the orders, and 2.4% faster for the package assets, where every new process was faster than every base one.
    - The control moved 0.0% and −0.3%.
    - What made the package assets faster wasn't traced. Phase 5 rebuilt the sink's cell path around styles (`StartCell`, `StyleOf`).
- **Each setting's cost:**
  - **How it was measured:** BenchmarkDotNet's process per method can't resolve half a percent, so a probe (`probes\settings`) timed them:
    - one process pinned to core 4, at high priority;
    - the benchmarks' data and loops, each setting alone on Phase 4's look;
    - two warm-ups, then 11 rounds in a shuffled order, with a collection before each write;
    - the median of each setting, against Phase 4's look in the same process.
  - **Results:**

    | Setting | Orders | Package assets |
    |---|---:|---:|
    | Phase 4's look | 1,483.3 ms | 1,261.6 ms |
    | Bold header | −0.1% | −0.6% |
    | Frozen header | −0.4% | +0.0% |
    | Auto-fit over 100 rows | −0.3% | −0.2% |
    | Auto-fit over 1,000 rows | +0.2% | +0.6% |
    | Title | −0.1% | −0.5% |
    | Filter | −0.1% | −0.2% |
    | Table | +0.2% | −0.0% |
    | **The defaults** | **−0.4%** | **−0.1%** |
    | Formats of the 5 number columns (of 12) | +6.1% (+90.3 ms) | |
    | Wrap (`Path`) | | +2.0% (+25.6 ms) |
    | Width (`Path`, 40) | | −0.5% |
    | Hidden (`ScanId`) | | +0.5% |
    | 65,530 links, against the same column as text | | +2.1% (+29.2 ms) |

  - **What a sheet writes once costs nothing measurable:** the defaults, and each setting written once per sheet (the header's font, the panes, auto-fit's widths, the title, the filter, the table). Each is within the probe's noise, under 1%.
  - **Per-cell settings cost:**
    - **A column's format** writes `s` on each of its cells. The orders' five formats add 12.9 MB to the file (189.0 MB against 176.1 MB) and 6.1% to the time.
    - **Wrap** writes `s` on its column's cells too: 5,986,060 more bytes of XML. Deflate at `Fastest` happens to compress the sheet 4.6 MB smaller (134,558,450 bytes against 139,189,106), and the time grows 2.0%.
    - **Links:** each of a sheet's first 65,530 URIs is parsed again for its target, and held.
      - In a run of the probe with a link column (`links.txt`), the links cost 29.2 ms (+2.1%) over the same column as text: about 450 ns each. BenchmarkDotNet has +32.0 ms.
      - They allocate 34,560,000 bytes against the text's 138,248, about 525 bytes per link. The field's bytes are copied, decoded, and parsed as a `Uri` for its `AbsoluteUri`.
      - Past the cap, a URI is text and costs what text costs: the link column itself, as text, is +6.7%.
  - **Memory** (BenchmarkDotNet's Allocated):
    - **Phase 4's look:** 10.4 KB per workbook for the orders and 10.1 KB for the package assets, against Phase 4's 7.3 KB. That is about 3 KB for Phase 5's settings and styles, whatever the rows.
    - **The defaults:** about 423 KB for the orders (447 KB in one run of the first try) and 135 KB for the package assets. This is auto-fit's 101 held records:
      - Package assets: 1.0–1.2 KB per held record, about its XML (1,090 bytes a row).
      - Orders: 4.1–4.6 KB per held record, seven to eight times its XML (570 bytes a row). Why wasn't traced.
- **The full run** (BenchmarkDotNet, every method of both classes; `run.ps1`, `bdn`), against Phase 4's step 7:
  - **Caveat:** each method runs in its own process, and CsvHelper.Excel's land 2–4% apart between processes. In the package assets, Phase 4's look took 1,342.4 ms against 1,321.1 for the defaults and 1,287.3 for the table, which the probe puts within 0.6% of each other. The tables are the run's record; the gate and the settings' costs come from the pairs and the probe.
  - `ExcelPackageAssetsWrite`:

    | Writer | Phase 4 | Mean | Ratio | Written | Allocated |
    |---|---:|---:|---:|---:|---:|
    | SpreadCheetah | 624.0 ms | 630.6 ms | baseline | 58.6 MB | 11.2 KB |
    | CsvHelper.Excel (the defaults) | 1,276.5 ms | 1,321.1 ms | 2.10x slower | 139.2 MB | 135.0 KB |
    | CsvHelper.Excel, shared | 1,376.7 ms | 1,315.2 ms | 2.09x slower | 100.6 MB | 515.4 KB |
    | CsvHelper.Excel, `Optimal` | 4,392.2 ms | 4,402.3 ms | 6.98x slower | 79.0 MB | 135.0 KB |
    | CsvHelper.Excel, Phase 4's look | | 1,342.4 ms | 2.13x slower | 139.2 MB | 10.1 KB |
    | CsvHelper.Excel, presentation | | 1,301.7 ms | 2.06x slower | 139.2 MB | 1.01 MB |
    | CsvHelper.Excel, table | | 1,287.3 ms | 2.04x slower | 139.2 MB | 151.8 KB |
    | CsvHelper.Excel, links | | 1,398.2 ms | 2.22x slower | 147.7 MB | 33.0 MB |
    | CsvHelper.Excel, the links as text | | 1,366.2 ms | 2.17x slower | 147.3 MB | 135.0 KB |
    | LargeXlsx | 1,438.8 ms | 1,439.5 ms | 2.28x slower | 195.4 MB | 103.3 KB |
    | MiniExcel | 3,957.8 ms | 3,950.8 ms | 6.27x slower | 195.2 MB | 9.29 GB |
    | CsvHelper, CSV | 136.3 ms | 135.1 ms | 4.67x faster | 306.0 MB | 440 B |

  - `ExcelOrdersWrite`:

    | Writer | Phase 4 | Mean | Ratio | Written | Allocated |
    |---|---:|---:|---:|---:|---:|
    | SpreadCheetah | 960.5 ms | 957.3 ms | baseline | 132.1 MB | 91.6 MB |
    | CsvHelper.Excel (the defaults) | 1,497.4 ms | 1,502.4 ms | 1.57x slower | 176.1 MB | 423.3 KB |
    | CsvHelper.Excel, shared | 1,662.3 ms | 1,660.8 ms | 1.73x slower | 175.3 MB | 160.4 MB |
    | CsvHelper.Excel, `Optimal` | 4,754.9 ms | 4,767.4 ms | 4.98x slower | 115.1 MB | 423.4 KB |
    | CsvHelper.Excel, Phase 4's look | | 1,527.8 ms | 1.60x slower | 176.1 MB | 10.4 KB |
    | CsvHelper.Excel, presentation | | 1,556.5 ms | 1.63x slower | 189.1 MB | 4.55 MB |
    | CsvHelper.Excel, table | | 1,504.9 ms | 1.57x slower | 176.1 MB | 431.9 KB |
    | LargeXlsx | 1,424.7 ms | 1,429.4 ms | 1.49x slower | 176.1 MB | 91.7 MB |
    | MiniExcel | 2,461.2 ms | 2,341.4 ms | 2.45x slower | 189.2 MB | 4.08 GB |
    | CsvHelper, CSV | 123.7 ms | 125.7 ms | 7.62x faster | 191.0 MB | 464 B |

  - Phase 4's "CsvHelper.Excel" rows were in Phase 4's look; this run's are at the new defaults.
  - Shared strings in the package assets allocate 127.7 KB more than at Phase 4: auto-fit's held records.
- **Decisions made while building** (each recommended, and approved as built):
  1. **Links in the package assets, as a 26th column:** each package's NuGet page (`https://www.nuget.org/packages/{Id}/{Version}`), one `Uri` per package and version, built in setup. `LinksAsText` (`ExcelLinks.None`) writes the same column, for what links cost. The orders have no `Uri` member, and adding one would change `OrderRecord`, which the read benchmarks share.
     - Alternative: a record type of its own, with a `Uri`, for the orders.
  2. **The package assets' presentation has no formats,** since every field is text. The orders' has its five number columns'.
     - Alternative: a text format (`@`) on the package assets' columns, which would cost as a format does.
  3. **The gate from fresh-process pairs,** as for the UTF-8 dates, since BenchmarkDotNet's processes disagreed by 6% on the same code. The base's copy got the `~` filter too, outside the repo.
  4. **The settings' probe stays outside the repo,** as earlier probes have.
     - Alternative: a benchmark class with a method per setting, whose BenchmarkDotNet numbers would need the same pairs to read.
- **Left open:**
  - Why Phase 4's look is 2.4% faster for the package assets than at Phase 4 (above). The gate asks for no more than that it isn't slower.
  - The orders' held records allocate seven to eight times their XML: Phase 9's "Auto-fit's held rows".

### Phase 6: conversion (S)

- **`.xlsx` → CSV:**
  - Source field bytes go straight into `WriteField(ReadOnlySpan<byte>)` with no strings created.
  - Dates come out as ISO 8601.
  - One sheet, or all sheets with one file each.
- **CSV → `.xlsx`:** three modes:
  - All text.
  - Per-column type inference from a sample of rows: numbers without leading zeros, ISO dates, `TRUE`/`FALSE`.
  - Typed through `T`.

**v1.0 release: phases 0–6.**

### Phase 7: import templates (M)

- **`ExcelTemplate.Create<T>`:**
  - Header-only sheet, with column default formats.
  - Enum and `bool` dropdowns. Lists up to 255 characters go inline; longer ones use a hidden sheet plus a named range.
  - Number and date validations.
  - Help text shown when a cell is selected, using validation input prompts. These need none of the VML drawing parts that cell comments do.
- **Locking:** sheet protection, with the header locked and data cells unlocked.
- **`ExcelImportResult<T>`:** the records plus every error with its cell address, for "fix these cells" feedback to users.

### Phase 8: displayed text (L)

- **Number-format renderer:**
  - Format sections and conditions (`[>100]`).
  - Digit placeholders `0`, `#`, `?`; thousands separators and scaling; percent, exponent, fractions.
  - Literals, quoted text, `\` escapes, `_` spacing and `*` fill.
  - Date and time tokens, elapsed `[h]`, AM/PM.
  - Locale codes like `[$-409]`.
  - Built-in formats whose output depends on the locale, rendered with the reader's `FormatProvider`.
- **Options it enables:**
  - `Text = Formatted` reading.
  - Formatted output in `.xlsx` → CSV conversion.
- **Test oracle:** where Excel is installed, COM automation exports `Range.Text` for a corpus of values and formats. This feature is where scope creeps most, and real Excel output keeps it honest.
- **From Phase 5:** auto-fit's `NumberFormatWidth` estimates a formatted number's width, erring wide, from the digits, grouping, decimals, percent, exponent and literals of the section for its sign. That is part of what the renderer does exactly, and the renderer replaces it.

### Phase 9: performance (L)

- **Scanner:** SIMD search for `<c`, `<v`, `</row>` and attribute values, the same approach as the CSV scanner.
- **Read pipeline:** an inflate thread, a scan thread and mapping workers. `ParallelEnumerate` already maps record-source batches on workers (Phase 0), but its batches are filled under the read lock, so inflating and scanning run serially there. Inflating one entry is inherently sequential and sets the ceiling.
- **Where Phase 1's baseline points** (step 9's "As built"): on .NET 10, inflate is about 5% of a read of the benchmark's sheet, so the ceiling is far off and the time is in scanning cells and filling records, about 160 ns a cell. With shared strings, Sylvan.Data.Excel is 1.36–1.53x faster and returns its table's own strings, where CsvHelper.Excel makes a new string per field; with inline strings the two are level.
- **Phase 2's typed baseline:** `ExcelOrdersRead` (a million `OrderRecord` rows of dates, numbers, booleans and text) joins `ExcelPackageAssetsRead` as a starting point.
- **Phase 4's write baseline** (step 7's "As built"): `ExcelPackageAssetsWrite` and `ExcelOrdersWrite`. CsvHelper.Excel takes 2.05x and 1.56x SpreadCheetah 1.28.0's time, and deflate is about half of it. The cells' `r` attributes nearly triple the package assets' file at `Fastest` (139.2 MB against 47.7 MB without them), double its deflate's time, and quadruple it at `Optimal`, so dropping them (decision 23's alternative, and parallel writing's need below) comes first; making the XML takes about 2.4–2.5x SpreadCheetah's time besides.
- **Parallel writing:**
  - Generate cell XML in parallel through `ParallelWriteRecords`. Until then, a writer with a cell sink throws `NotSupportedException` for the parallel methods.
  - The style registry must be thread-safe.
  - Omitting `r` attributes means workers don't need absolute row numbers. Verify that every reader we test against accepts that first.
- **Our own zip writer:** async I/O, Zip64, data descriptors and CRC32. It also removes the synchronous writes that `ZipArchive` makes before .NET 10 when an entry or the package closes (Phase 4's "Risks").
  - Then a pigz-style experiment: deflate chunks in parallel and concatenate them after a sync flush.
  - Keep each piece only if it wins on the benchmark machine. The compress flattener showed that a faster stage doesn't always move the end-to-end numbers.
- **Memory:** profile workbooks with huge shared-string tables.
- **Auto-fit's held rows** (Phase 5): a sheet holds its header and first 100 records until their widths are measured, since `<cols>` comes before the rows. Measure what measuring costs, and what holding costs in memory and in time to the first write for wide sheets, and whether a smaller sample fits as well.
  - **Phase 5's step 8:** holding and measuring 100 or 1,000 records cost no time it could measure, under 1%. Each held record allocated about its XML in the package assets (1.0–1.2 KB), and seven to eight times its XML in the orders (4.1–4.6 KB, against 570 bytes). The orders' allocation is the place to start.
- **Finding merged ranges** (Phase 3): a cheaper skip over `sheetData` for the read that finds `<mergeCells>`, such as a raw search for its end tag. Phase 3's step 8 measured that read at a third of a plain sheet's time: 1.18 s for 714 MB of sheet XML through the XML scanner's skip, at about 600 MB/s, where inflating alone runs at 5.3–5.6 GB/s.
- **Measuring Phase 3's default path again** (Phase 3's "Left open", added 2026-11-06):
  - Phase 3's gate wanted every A/B method of the default path within 1%. Shared Rows had one clean pair, at +1.2%, inline Rows none, and five of the eight methods weren't measured after step 8's `FlushGaps` split. Phase 3 was called complete with that item as it stood.
  - BenchmarkDotNet pairs of all eight CsvHelper.Excel methods of `ExcelPackageAssetsRead` and `ExcelOrdersRead`, with the Sylvan control: base `1cd1a0af` (Phase 2's end) against `f382244b` (Phase 3's end), on a quiet machine, interleaved and pinned, counting a pair only when both runs' standard deviations are under 2% of their means.
  - The base is a worktree at `Csv\_bench\base`, checked out with LF line ends, so that both sides' generator sources hash alike and share the cached workbooks.
  - Before Phase 9's own scanner changes, so that they are measured from a known start.

### Phase 10: other formats (XL)

- **Compound-file (CFB) reader:** the shared foundation for the next two.
- **Password-protected `.xlsx`:** MS-OFFCRYPTO agile and standard encryption on read. Encrypted writing is optional.
- **`.xls`** (BIFF8) reader.
- **`.xlsb`** (BIFF12) reader. It's a zip with binary records, so it reuses the Phase 1 container and the cell-kind model. It's also the fastest format to parse.

## Feature coverage

Every feature from the brainstorm, and where it lands.

| Feature | Phase |
| --- | --- |
| Typed cells on read, with no string round trip | 0, 1, 2 |
| Date detection from number formats | 2 |
| Streaming reads with our own scanner | 1, tuned in 9 |
| Read benchmarks vs ExcelDataReader, Sylvan, MiniExcel | 1 (baseline), 9 |
| Sheet by name or index | 1 |
| The sheet list: name, index, visibility, kind | 1 |
| All sheets (a reader per sheet in a loop, which Phase 1 allows), and sheet dimensions | 1, 3 |
| Named ranges | 3 |
| Excel Tables by name | 3 |
| Header-row detection | 3 |
| Raw value vs displayed text | 8 |
| Formula cached value vs formula text, shared formulas moved to each cell | 2 |
| Merged-cell fill, skipping hidden rows and columns | 3 |
| Error-cell policy | 2 |
| Errors that name the cell | 0, 1 |
| Typed cells on write | 4 |
| Writing formulas | 4, without cached values |
| Streaming writes with inline strings, shared strings optional | 4 |
| New sheet at the row limit | 4 |
| Several sheets in one workbook | 4 |
| Number format and width per column | 4 (dates' and times' format translation, and typed columns' widths), 5 |
| Bold header, frozen header row, autofilter | 5 |
| Wrapped text, hyperlinks | 0 (`WriteField(Uri)`), 5 |
| Styled Excel Table output | 5 |
| Auto-fit from the first N rows | 5 |
| Merged cells on write | 5 |
| Import templates | 7 |
| Compression level | 4 |
| Parallel cell XML, parallel deflate | 9 |
| Same attributes, mappers and generator as CSV | 0, 1, 4 |
| `DbDataReader` over a sheet (`SqlBulkCopy`) | 1 |
| CsvHelper.Linq and dynamic records over a sheet | 1 |
| `.xlsx` ↔ CSV conversion | 6 |
| 1900 leap-year bug, 1904 date system | 2 |
| Durations (`[h]:mm`) as `TimeSpan` | 2 |
| Long numbers losing digits | 2 (read), 4 (write) |
| Leading zeros | 4, 6 |
| Cell size and sheet name limits | 4 |
| Control-character escaping | 1 (read), 4 (write) |
| `DateTimeOffset` policy | 2 (read), 4 (write) |
| Sparse rows placed by cell reference | 1 |
| No Excel-library dependency, AOT compatible | 1 |
| Password-protected files | 10 |
| `.xlsb` and `.xls` | 10 |

## Testing

Testing runs through every phase.

- **Unit tests** for each scanner piece: cell references, entities, `_xHHHH_`, rich text, shared formulas, date-format detection, serial-date conversion.
- **Corpus:** files from real Excel (Windows and Mac), LibreOffice, Google Sheets, Numbers, ClosedXML, EPPlus, the Open XML SDK, openpyxl/pandas, Apache POI, MiniExcel and SpreadCheetah. Together they cover prefixes, strict OOXML, inline strings, missing `r` attributes, shared formulas and 1904 dates.
  - `tools/CsvHelper.Excel.Corpus` makes them from one definition of the content. It drives Excel through COM, LibreOffice and Python by command line, and the .NET producers directly.
  - It runs on demand; the tests only read the committed files.
  - Phase 1's "Testing" has the files, the producer versions and how each is made.
  - Content sets: `basic` (Phase 1), `typed` (Phase 2: dates, numbers, formulas), `messy` (Phase 3: title rows, merged cells, hidden rows and columns, filters, tables and defined names) and `written` (Phase 4: workbooks CsvHelper.Excel writes).
- **Excel's answers** (from Phase 2): the corpus tool asks Excel through COM how it classifies number formats and what each corpus cell holds (value, type, formula, text), and commits the answers as JSON files. CI's tests compare with them; Excel itself only runs on demand. From Phase 3 the answers also hold each workbook's tables and defined names, and each sheet's hidden rows and columns and merged ranges. From Phase 4 they cover the written workbooks too, pinned to each workbook's SHA-256, with Excel's text of each formatted cell as the oracle of the format translation.
- **Oracles** (test-only dependencies):
  - The Open XML SDK (DocumentFormat.OpenXml 3.5.1), for raw cell structure and, from Phase 4, `OpenXmlValidator`.
  - ClosedXML 0.105.1 and ExcelDataReader 3.9.0.
  - LibreOffice `--headless --convert-to csv`: a test skipped when `soffice` is missing. From Phase 4, CI installs LibreOffice and runs it over the written workbooks.
- **Round-trip property tests:** random records → write → read → equal.
- **Fuzzing:**
  - Malformed zip and XML input must fail with an expected exception, and never hang or exhaust memory.
  - Seeded mutation fuzzing runs in the test suite, with a time limit per input and small limits. An environment variable allows longer runs.
  - Coverage-guided fuzzing (SharpFuzz) is optional, in Phase 9.
- **Security:** zip bombs, oversized parts, and DOCTYPE/entity expansion.
- **Benchmarks:**
  - Reading: ExcelDataReader, Sylvan.Data.Excel, MiniExcel, Open XML SDK SAX.
  - Writing: SpreadCheetah, LargeXlsx, MiniExcel.
  - The existing CsvHelper tests and benchmarks run unchanged.

## Risks

- **Hot-path cost of the hooks.** Phase 0's gate exists for this.
- **Lockstep releases.** Internal hooks mean every core release must ship a matching CsvHelper.Excel. Core, CsvHelper.Linq and CsvHelper.Excel are always released together (settled 2026-11-03).
- **Producer tolerance.** Excel accepts things the spec doesn't promise, and other producers rely on that. The corpus is the defense.
- **Merged-cell fill** needs a second inflate pass. Phase 3's benchmark measured it: +34% on a sheet without ranges, and +44% on the report (step 8). Phase 9 can make it cheaper.
- **Shared-strings memory** on huge workbooks.
- **Zip edge cases:** Zip64 for sheets over 4 GB, non-seekable streams, and `ZipArchive`'s synchronous central directory on older targets. Before .NET 10, closing an entry and the package writes synchronously, so a stream that forbids synchronous I/O throws (Phase 4); Phase 9's own zip writer removes it.
- **Renderer scope creep** in Phase 8.

## Out of scope

Charts, pivot tables, formula evaluation, editing existing workbooks, a full workbook object model, VBA, images, VML cell comments, and `.ods`.

## Layout

- `src/CsvHelper.Excel`
- `tests/CsvHelper.Excel.Tests`, with the corpus under `tests/CsvHelper.Excel.Tests/Files`
- `tools/CsvHelper.Excel.Corpus`: the corpus generator (not packed, not run in CI)
- `bench/CsvHelper.Benchmarks`: Excel read and write benchmarks, with NCsvPerf's PackageAssets data (and its MIT license) under `Data/`
- `docs/pages/excel.md`, added to `docs/site.json`

## Defaults

Chosen during planning, and still open to change:

- A sheet reads as a plain `CsvReader` and writes as a plain `CsvWriter`.
- Internal core hooks with an exact version pin.
- `decimal` and integer targets round to 15 significant digits (Phase 2).
- Error cells read as their text (Phase 2).
- Formulas read their cached values (Phase 2).
- Date cells' text is ISO 8601, from the value (Phase 2).
- `DateTimeOffset` is written as ISO 8601 text.
- Row and column limits throw.
- Strings are written inline.
- Workbooks are written in Calibri 11, deflated at `CompressionLevel.Fastest` (Phase 4).
- Dates are written in ISO-like formats by what each value holds: a date, seconds or milliseconds (Phase 4).
- A sheet's header is bold and frozen, and its columns are auto-fitted over the header and first 100 records, at most 60 characters wide (Phase 5).
- `Uri` fields with http, https or mailto are links; others are text (Phase 5).
- Formulas are written without cached values (Phase 4).
- Long numbers, dates before 1900 and negative durations are written as text (Phase 4).
- `History` is allowed as a sheet name (Phase 4).
- Excel attributes are read by reflection.
- `MaxPartSize` is 256 MiB and `MaxUncompressedSize` 4 GiB, with no compression-ratio check (Phase 1).
- The default sheet is the first worksheet in tab order, hidden ones included (Phase 1).
- A sheet reader applies `SkipEmptyLines` itself, and with it off emits every row, so record n is row n (Phase 1).
- Rows with a value are padded to the first record's width (Phase 1), within a range's columns (Phase 3).
- Hidden rows and columns are read, and merged cells aren't filled (Phase 3).
- No header search and no clipping unless `HeaderRow` asks: field `i` is column `i` of the sheet or range (Phase 3).
