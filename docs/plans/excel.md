# CsvHelper.Excel plan

Status: approved 2026-11-01. Phase 0 (core hooks) was implemented 2026-11-01, and its writer sink was reworked 2026-11-02 to meet the benchmark gate; see "As built" under Phase 0. Phase 1's detailed design was approved 2026-11-02, with every recommendation accepted. Its steps 1 (skeleton and packaging), 2 (XML layer), 3 (package), 4 (shared strings), 5 (sheet scanner), 6 (record source and public API), 7 (corpus and oracles) and 8 (security and fuzzing) were built 2026-11-02, and 9 (benchmarks) and 10 ("As built") 2026-11-03. Phase 1 is complete, its gate met; see "As built" under Phase 1, which opens with a summary. Phase 2's detailed design was approved 2026-11-03, with every recommendation accepted. Its steps 1 to 8 were built 2026-11-03, and 9 (benchmarks, with the deferred core gate) and 10 ("As built") 2026-11-04. Phase 2 is complete, its gate met; see "As built" under Phase 2, which opens with a summary. Phase 3's detailed design was approved 2026-11-04, with every recommendation accepted. Its steps 1 (ranges and metadata), 2 (areas) and 3 (hidden rows and columns) were built and committed 2026-11-04, step 4 (merged cells) built that day and committed 2026-11-05, and steps 5 (header row), 6 (corpus and oracles) and 7 (security and fuzzing) built and committed 2026-11-05; see "As built" under Phase 3.

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

using (var sheet = output.AddSheet("Raw", new ExcelSheetOptions { FreezeHeader = true }))	// a CsvWriter
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
- **`ExcelWorkbookWriter`**: `ToFile(path)` and `ToStream(stream)`. `WriteSheet(name, records)` writes a whole sheet. `AddSheet(name, options)` returns a `CsvWriter` for manual or mixed writing.
- **`ExcelWriterOptions`**:
  - `Strings`: Inline (default) or Shared.
  - `CompressionLevel`.
  - `RowLimit`: Throw (default) or NewSheet (continues on a new sheet with the header repeated).
  - `LongNumbers`: Text (default), Number (lossy) or Throw.
  - `DateTimeOffsets`: Text (ISO 8601, default), Utc or Local.
  - `Csv`: a composed `CsvWriterOptions`. CSV-only settings (separator, quoting, fixed width, comments) are ignored or rejected. Injection protection is unnecessary, because text cells are never evaluated.
- **`ExcelSheetOptions`**: `FreezeHeader`, `BoldHeader`, `AutoFilter`, `Table` (name, style), `AutoFit` (rows to sample), `Columns` (per header name: `Width`, `Format`, `Wrap`, `Hidden`).
- **Attributes**: `[ExcelColumn(Width, Format, Wrap, Hidden)]` on members, `[ExcelSheet(Name, FreezeHeader, AutoFilter, Table)]` on the class.

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

Status: the detailed design below was written and approved on 2026-11-04, with every recommendation accepted. "Changes elsewhere" was applied to the rest of the plan at approval. Steps 1 to 3 were built and committed on 2026-11-04, step 4 built that day and committed on 2026-11-05, and steps 5 to 7 built and committed on 2026-11-05; "As built", at the end of this phase, records them. Step 2 changed one rule of the design: with formulas read as text, rows above an area and cells outside its columns are read for their shared formulas' definitions. Step 3 widened what is hidden to match Excel, checked through COM again: a `<col>` without a width, or with one that isn't a number, hides its columns, and a custom default row height of 0 hides the rows a sheet lacks. Step 4 made merged cells as strict as Excel, checked through COM: a `ref` Excel can't open throws, and one-cell ranges count in the overlap check before they are dropped. Step 5 changed how the header is found: a scanner of its own reads the rows the header can be in, over a second stream of the sheet's part, and the reader's scanner then reads the rows after it, rather than one scanner pausing after the header row and narrowing its columns; a row given (`At`) is the header even when hidden. Step 6 asked Excel about every corpus workbook's structures over the whole grid rather than a window, and found that Excel opens no workbook with a name of a chartsheet, which openpyxl writes (CsvHelper.Excel still reads such a name, as the review decided; see step 6's "As built"). Step 7 found that each defined name searched every sheet for the sheet of its block, so that opening took the names times the sheets, and made it one lookup. It makes the phase L rather than M: besides the outline, it covers the other ways Excel marks rows and columns hidden, rows the file leaves out of a merged range, Excel's answers for the new structures, a `messy` corpus set, and a benchmark of the new paths. The Excel behavior it relies on was checked through COM on 2026-11-04 (see "Checked on 2026-11-04").

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

### Phase 4: writer foundation (L)

- **Package and sheets:**
  - Sheets stream into zip entries one at a time, because `ZipArchive` allows one open entry in create mode.
  - Styles, workbook, relationships and content types are written last, once all number formats and sheet names are known.
  - A style registry hands out `s` indexes as formats first appear.
- **Cell sink** (fields arrive as CSV text plus type; see Phase 0's "As built"):
  - Number cells: the invariant text of a number type is already `<v>` text (the CSV path formats it with `FloatFormatter`/`Utf8Formatter`), so it is copied as is. A number written with a format or a culture is parsed back with the writer's provider or, failing that, written as a text cell.
  - Strings are written inline by default (shared strings are an option), with `xml:space="preserve"` when needed.
  - Booleans are written as `t="b"`. Dates are parsed from their `"O"` text and written as serial numbers with a date style; a date written with a format arrives as that text, and becomes a text cell. An empty field is an empty cell.
  - The first record is the header when `HasHeader` is set: header styles, frozen rows, autofilter and tables apply to it, and `Columns` options match its names.
  - Other types arrive with their type: `Uri` becomes a link, and `ExcelFormula` (through its converter or `WriteField(object)`) a formula.
- **Formulas:**
  - An `ExcelFormula` value type (formula text plus an optional cached value), written through a built-in converter.
  - A `WriteFormula` extension for manual writing.
- **.NET format → Excel number format:**
  - The formats come from Excel settings (`[ExcelColumn(Format)]`, `Columns`) and from `[CsvFormat]` on `T`'s members, read by reflection. A format string passed to `WriteField` does not reach the sink (Phase 0): such a field arrives as its formatted text.
  - Precision: a number written with a format arrives rounded to it (`N2` → 2 decimals), and that is the value the cell stores. A column whose format should only change the display uses Excel's `Format` setting rather than `[CsvFormat]`, so its values arrive unformatted. Document this in the guide.
  - Custom numeric formats (`0.00`, `#,##0`, `0%`) mostly map one-to-one.
  - Map the standard formats (`N2`, `F2`, `P1`, `C`) and date tokens (`MM` → `mm` for months). Excel reads `mm` as minutes only next to an hour or second token, so a .NET `mm` anywhere else is untranslatable.
  - Anything untranslatable is written as a text cell with the formatted value, which matches CSV output.
- **Quirk handling:**
  - Numbers beyond 15 significant digits follow `LongNumbers` (text by default), so IDs and card numbers don't silently change.
  - Numeric-looking strings stay text (ZIP codes, phone numbers, leading zeros), and `<ignoredErrors numberStoredAsText>` hides Excel's warning triangles.
  - The 32,767-character cell limit.
  - Sheet names: at most 31 characters, none of `: \ / ? * [ ]`, no leading or trailing apostrophe, unique ignoring case, and not `History`.
  - Control characters are written as `_xHHHH_`, and a literal `_x` that looks like an escape as `_x005F_`.
  - Dates before 1900 can't be serial numbers, so they follow a policy: text or throw.
  - `DateTimeOffset` follows `DateTimeOffsets`.
  - Row and column limits (1,048,576 × 16,384) throw by default. `RowLimit.NewSheet` continues on a new sheet with the header repeated.
- **Multiple sheets** through `WriteSheet` and `AddSheet`, plus async writes through the entry stream.
- **Compression level** option.
- **Gate:**
  - Files pass `OpenXmlValidator`.
  - Files open in Excel and LibreOffice without a repair prompt.
  - Files round-trip through our reader, ClosedXML and ExcelDataReader.

### Phase 5: presentation (M)

- **Attributes and options:** `[ExcelColumn]`, `[ExcelSheet]` and `ExcelSheetOptions.Columns` for formats, widths, wrap and hidden columns.
- **Auto-fit:** `<cols>` comes before the rows, so auto-fit buffers the first N rows to estimate widths, then writes them. Without auto-fit, widths come from attributes and options only, and nothing is buffered.
- **Header and view:**
  - Bold header style.
  - Frozen header row (`<sheetViews>`, before the rows).
  - Autofilter, written after the rows once the range is known.
- **Hyperlinks** from `Uri` members, collected while streaming and written after the rows with their relationships.
- **Merged cells** on the sheet writer (e.g. a title row above the header), written after the rows.
- **Excel Table output:** a table part with a style, row banding and filter buttons.
- **Column default styles** (`<col style>`), so formats also apply to cells users add later.

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

### Phase 9: performance (L)

- **Scanner:** SIMD search for `<c`, `<v`, `</row>` and attribute values, the same approach as the CSV scanner.
- **Read pipeline:** an inflate thread, a scan thread and mapping workers. `ParallelEnumerate` already maps record-source batches on workers (Phase 0), but its batches are filled under the read lock, so inflating and scanning run serially there. Inflating one entry is inherently sequential and sets the ceiling.
- **Where Phase 1's baseline points** (step 9's "As built"): on .NET 10, inflate is about 5% of a read of the benchmark's sheet, so the ceiling is far off and the time is in scanning cells and filling records, about 160 ns a cell. With shared strings, Sylvan.Data.Excel is 1.36–1.53x faster and returns its table's own strings, where CsvHelper.Excel makes a new string per field; with inline strings the two are level.
- **Phase 2's typed baseline:** `ExcelOrdersRead` (a million `OrderRecord` rows of dates, numbers, booleans and text) joins `ExcelPackageAssetsRead` as a starting point.
- **Parallel writing:**
  - Generate cell XML in parallel through `ParallelWriteRecords`. Until then, a writer with a cell sink throws `NotSupportedException` for the parallel methods.
  - The style registry must be thread-safe.
  - Omitting `r` attributes means workers don't need absolute row numbers. Verify that every reader we test against accepts that first.
- **Our own zip writer:** async I/O, Zip64, data descriptors and CRC32.
  - Then a pigz-style experiment: deflate chunks in parallel and concatenate them after a sync flush.
  - Keep each piece only if it wins on the benchmark machine. The compress flattener showed that a faster stage doesn't always move the end-to-end numbers.
- **Memory:** profile workbooks with huge shared-string tables.
- **Finding merged ranges** (Phase 3): a cheaper skip over `sheetData` for the read that finds `<mergeCells>`, such as a raw search for its end tag, if Phase 3's measurement shows the second inflate costing much.

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
| Writing formulas | 4 |
| Streaming writes with inline strings, shared strings optional | 4 |
| New sheet at the row limit | 4 |
| Several sheets in one workbook | 4 |
| Number format and width per column | 4 (format translation), 5 |
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
  - Content sets: `basic` (Phase 1), `typed` (Phase 2: dates, numbers, formulas) and `messy` (Phase 3: title rows, merged cells, hidden rows and columns, filters, tables and defined names).
- **Excel's answers** (from Phase 2): the corpus tool asks Excel through COM how it classifies number formats and what each corpus cell holds (value, type, formula, text), and commits the answers as JSON files. CI's tests compare with them; Excel itself only runs on demand. From Phase 3 the answers also hold each workbook's tables and defined names, and each sheet's hidden rows and columns and merged ranges.
- **Oracles** (test-only dependencies):
  - The Open XML SDK (DocumentFormat.OpenXml 3.5.1), for raw cell structure and, from Phase 4, `OpenXmlValidator`.
  - ClosedXML 0.105.1 and ExcelDataReader 3.9.0.
  - LibreOffice `--headless --convert-to csv`: a local test, skipped when `soffice` is missing, until Phase 4 puts it in CI.
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
- **Merged-cell fill** needs a second inflate pass. Phase 3's benchmark measures it.
- **Shared-strings memory** on huge workbooks.
- **Zip edge cases:** Zip64 for sheets over 4 GB, non-seekable streams, and `ZipArchive`'s synchronous central directory on older targets.
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
- Excel attributes are read by reflection.
- `MaxPartSize` is 256 MiB and `MaxUncompressedSize` 4 GiB, with no compression-ratio check (Phase 1).
- The default sheet is the first worksheet in tab order, hidden ones included (Phase 1).
- A sheet reader applies `SkipEmptyLines` itself, and with it off emits every row, so record n is row n (Phase 1).
- Rows with a value are padded to the first record's width (Phase 1), within a range's columns (Phase 3).
- Hidden rows and columns are read, and merged cells aren't filled (Phase 3).
- No header search and no clipping unless `HeaderRow` asks: field `i` is column `i` of the sheet or range (Phase 3).
