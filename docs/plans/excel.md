# CsvHelper.Excel plan

Status: approved 2026-11-01. Phase 0 (core hooks) was implemented 2026-11-01, and its writer sink was reworked 2026-11-02 to meet the benchmark gate; see "As built" under Phase 0. Phase 1's detailed design was approved 2026-11-02, with every recommendation accepted. Its steps 1 (skeleton and packaging), 2 (XML layer), 3 (package) and 4 (shared strings) were built 2026-11-02; see "As built" under Phase 1.

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

- **`ExcelWorkbook`**: `Open(path)` and `Open(stream)`. Exposes `Sheets` (name, index, visibility, kind, dimension), `Tables`, `DefinedNames` and `Date1904`. `OpenReader(...)` takes a sheet name, index, `ExcelSheet`, Table or range, and returns a `CsvReader`. Phase 1's "Public API" has the exact signatures and what each phase adds.
- **`ExcelReader`**: `FromFile(path)` and `Create(stream)`, and their async forms. One call returns a `CsvReader` that owns its workbook, for the common case of reading one sheet.
- **`ExcelReaderOptions`**:
  - `Sheet` (a name) or `SheetIndex`, `Range`, `Table`, `DefinedName`: what to read.
  - `HeaderRow`: a fixed row, or detect it from `T` (with a scan limit).
  - `FillMergedCells`, `SkipHiddenRows`, `SkipHiddenColumns`.
  - `ErrorCells`: Text, Null or Error (routes to `OnRecordError`).
  - `DecimalDigits`: significant digits for `decimal` targets (default 15).
  - `Formulas`: CachedValue (default) or FormulaText.
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

Status: the detailed design below was approved on 2026-11-02, with every recommendation under "Decisions" accepted (question 12 updated: Python and LibreOffice are both installed). Steps 1 to 4 of "Order of work" were built on 2026-11-02; "As built", at the end of this phase, records them. The changes the design made to other sections are listed under "Changes elsewhere".

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
- **Options and limits:**
  - The options passed to `Open` belong to the workbook and are the default for `OpenReader`. Options passed to `OpenReader` replace them for that reader; they are not merged.
  - Each call checks the parts it reads against the limits of its own options.
    - `Open` reads the content types, the relationships and `workbook.xml`.
    - `OpenReader` reads the styles and shared strings (on first use, kept by the workbook) and streams its sheet.
  - `MaxPartSize` bounds each part held in memory (content types, relationships, workbook, styles, shared strings) and any single token or cell of a streamed sheet.
  - `MaxUncompressedSize` bounds everything inflated through the workbook, plus the temp-file copy of a non-seekable input.
  - A limit of 0 or less throws `ArgumentOutOfRangeException`.
- **`Csv`** applies as for any record source (Phase 0 "As built"):
  - These apply: `HasHeader`, `SkipEmptyLines`, trimming, `NullValues`, `TrueValues`/`FalseValues` (text cells only), `FieldCountMismatch`, `MissingField`, header matching, `OnRecordError`, `FormatProvider` (text cells only), `PoolStrings` and `BufferSize`.
  - The dialect settings are ignored.
- **Ownership:**
  - A reader from `OpenReader` holds its sheet's entry stream, not the workbook. Disposing the workbook first makes the reader's next read throw `ObjectDisposedException`.
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

- **Phase 2:** `ErrorCells`, `DecimalDigits`, `Formulas`. Date conversion uses `Date1904`.
- **Phase 3:**
  - Options: `Range`, `Table`, `DefinedName`, `HeaderRow`, `FillMergedCells`, `SkipHiddenRows` and `SkipHiddenColumns`.
  - `ExcelSheet.Dimension` and `ExcelSheet.Tables`; `ExcelWorkbook.Tables` and `ExcelWorkbook.DefinedNames`; reading all sheets.
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
Internal/Sheet/
  SheetRecordSource.cs  the CsvRecordSource: Fill, FillAsync, Locate, Dispose
  SheetScanner.cs       the row and cell state machine
  IRecordTarget.cs      where the scanner writes, and RecordBatchTarget, a struct over CsvReader.RecordBatch
  SheetCellKind.cs      a mirror of CsvCellKind (see "Writing into the reader")
  CellReference.cs      A1 references: parsing, column letters, Location text
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
  - White space is kept, with or without `xml:space`.
- Each `<t>` is decoded with `XmlText`: its XML first (entities, character references, CDATA, line ends), then its `_xHHHH_` escapes.
  - The escapes are decoded over the `<t>`'s whole text, but not across runs, since each `<t>` is a string value of its own.
  - So a surrogate pair whose halves are escapes in two runs reads as two U+FFFD.
- A cell gets its string with `TryGet(index, out span)`, and copies it with one copy and no .NET string: `AddField(span, Text)`. An empty string makes the cell valueless (see "Placement").
- The table loads once per workbook, sync or async, by the first `OpenReader` that needs it. It is immutable afterwards, so readers and workers share it.
- No shared strings part, or one the workbook names but the package lacks, gives the empty table, as for styles. A sheet without `t="s"` cells still reads.
- An arena past `Array.MaxLength` bytes, or more strings than that, throws `InvalidDataException`. An index out of range in a cell throws `CsvFormatException` at that cell.
- Memory is the UTF-8 size of the strings plus 4 bytes each, about half of what a `string[]` costs for ASCII text.

**Sheet scanner:**

- It skips to `<sheetData>` (by local name). It stops at `</sheetData>` and inflates nothing after it; Phase 3's merged-cell fill pays for its own second pass. No `sheetData`, or `<sheetData/>`, gives no records.
- Elements it reads:
  - In `sheetData`, it reads `row` and skips everything else.
  - In `row`, it reads `c` and skips everything else (`extLst`).
  - In `c`, it reads `v` and `is`. `f` is skipped in Phase 1 (Phase 2 reads it). Everything else is skipped.
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
  - A row with a value is padded with Empty fields to the field count of the first record (the header, when `HasHeader`).
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
- **Columns:** a field's column is its index plus the sheet's first column. That is 0 in Phase 1; Phase 3's ranges and hidden-column skipping replace it with a field-to-column map.
- **Format:**
  - `Locate` gives `Orders!C14` for a field and `Orders!14:14` for a whole row (field -1).
  - Sheet names are quoted as Excel quotes them in formulas (`'Q1 Orders'!C14`, `'Bob''s'!A1`). That applies when a name has characters other than letters, digits, `_` and `.`, starts with a digit, or reads as a cell reference.
  - `Locate` uses only immutable data, so it is thread-safe.
- **Scanner errors** (malformed XML, a bad shared string index, cells out of order) name the cell being scanned in `Location`, set `LineNumber`, and put the part name in the message.

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
- Java: none, so Apache POI itself can't run here.
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
| `closedxml/`, `openxmlsdk/` (default namespace, and an `x:`-prefixed variant), `npoi/` (XSSF and the streaming SXSSF), `epplus/`, `miniexcel/`, `spreadcheetah/`, `largexlsx/` | ClosedXML 0.105.1, Open XML SDK 3.5.1, NPOI 2.8.1, EPPlus 4.5.3.3 (the last LGPL version), MiniExcel 1.46.0, SpreadCheetah 1.28.0, LargeXlsx 2.0.2 | the corpus tool. Which of these omit `r` or write inline strings gets checked and recorded |
| `google/`, `numbers/`, `excel-mac/` | Google Sheets, Apple Numbers, Excel for Mac | not possible here: the user exports `excel/basic.xlsx` through them, or a few files come from Apache POI's test-data and SheetJS's test_files (both Apache-2.0) |

- **`basic` content:** the corpus tool (`tools/CsvHelper.Excel.Corpus`, a net10.0 console that is not packed) defines it once:
  - Sheets: "Data", "Hidden" (hidden), "VeryHidden" (very hidden), a chartsheet in second place, "Q1 Orders", "Bob's" and a Japanese name.
  - Values: text, integers, decimals, negatives, `1E-05`, a 17-digit double, booleans, `#N/A` and `#DIV/0!`, empty cells mid-row and at row ends, an empty row, a block that starts in column C, emoji, leading and trailing spaces, a line break, U+0001, a literal `_x0041_`, `&<>"'`, a 32,767-character cell, rich text, a phonetic run (Excel only), and formulas with number, text and error results plus a shared formula down a column.
  - Dates: dates and times in built-in and custom formats.
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
- **Cross-check:**
  - A test compares the generator's 1,695-row output with Excel's `excel/packageassets-1695.xlsx`: the same values through our reader and through ExcelDataReader.
  - The XML shapes get compared once by hand. If Excel adds `s` attributes for the text format, the generator writes them too, so the benchmark runs the style lookup.
- **Class:** `ExcelPackageAssetsRead` in `ExcelReadBenchmarks.cs`.
  - It is a `ReadBenchmarkBase` whose `Generate` returns the CSV text, so its MB/s column is CSV-equivalent throughput, directly comparable with `PackageAssetsRead`.
  - `[Params]` `Strings` is Shared or Inline.
  - Every library reads the same `byte[]` through a `MemoryStream`.

| Library | Version | Rows | Strings | Records |
| --- | --- | --- | --- | --- |
| CsvHelper.Excel | this repo | `Read()` loop | `GetString` per field | `GetRecords<PackageAsset>()` (generated mapper) |
| ExcelDataReader | 3.9.0 | `Read()` loop (skips the header row) | `GetString(i)` | `PackageAsset.Create` through an `IColumnSource` |
| Sylvan.Data.Excel | 0.5.8 | `Read()` loop | `GetString(i)` | `IColumnSource` |
| MiniExcel | 1.46.0 | counts `Query(useHeaderRow: true)` | the row dictionary's values | `IColumnSource` over the row dictionary |
| Open XML SDK SAX | DocumentFormat.OpenXml 3.5.1 | `OpenXmlReader` over the worksheet part, counting `Row` elements | each `Cell`'s attributes and `CellValue` text, with shared strings preloaded into a `string[]` | `IColumnSource` |
| CsvHelper, CSV | this repo | reference only: the same data as CSV, from `PackageAssetsRead` | | |

- **Setup details:**
  - ExcelDataReader on .NET needs `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`, done in the setup.
  - The bench project already references Sylvan.Data.Csv 1.4.4. Check that Sylvan.Data.Excel's Sylvan.Common dependency resolves next to it.
  - Sylvan's and ExcelDataReader's `ExcelDataReader` type names clash, so the file uses aliases.
- **Equal work:** QuickRunner's `verify` mode checks that every library returns the same row count and a checksum of the strings, so no library skips work.
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
- **A cell kind for `t="d"` ISO dates.** Phase 1 reads them as Text. Whether Phase 2 needs a new `CsvCellKind` is Phase 2's decision, and would be a core change then.
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
5. **Gap amplification.** One `<c r="XFD1">` makes 16,384 fields from about 30 bytes of XML. A sheet of such rows could keep a reader busy for minutes while inflating only a few hundred MB. Count each gap and padding field as one byte toward `MaxUncompressedSize`.
6. **Empty rows.** The source applies `SkipEmptyLines` itself: it drops valueless rows, or with `false` emits every row so that record n is row n.
7. **Padding.** Pad rows that have a value to the first record's width.
8. **Columns.** Field i is column i, with no clipping before Phase 3.
9. **Exceptions.** The `InvalidDataException`, `CsvFormatException` and `NotSupportedException` split under "Public API". The alternative is a new public `ExcelFormatException`, which adds a type users must learn for little gain.
10. **Thread safety.** A workbook is single-threaded, and its readers can be used in turn on one thread. Later, if wanted, a workbook opened from a path could give each reader its own `FileStream` and `ZipArchive`, so sheets could be read in parallel.
11. **Metadata now.** Expose `Sheets` (name, index, visibility, kind) and `Date1904` in Phase 1. That pulls part of Phase 3's metadata forward; `Dimension`, Tables and defined names stay in Phase 3.
12. **Installs for the corpus.** Done: the user installed Python 3.14.10 and LibreOffice 26.8.2.2 on 2026-11-02 (see "What this machine has"). Node producers (ExcelJS, SheetJS) come in only if a gap remains.
13. **Google Sheets, Numbers and Excel for Mac.** The user exports `excel/basic.xlsx` through Google Sheets, and through Numbers or Excel for Mac if they have a Mac. That happens at step 7, once the file exists. Otherwise a few files from POI's and SheetJS's corpora, with licenses recorded.
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
- **Inflate bounds read speed,** and .NET 8 and .NET Framework inflate more slowly than .NET 9+.
- **Shared-strings memory** on huge workbooks. The arena halves it, but it stays proportional to the table.
- **Producer tolerance.** Rejecting out-of-order rows and cells follows the spec and the producers I know; if the corpus shows a producer writing them, revisit.
- **Window growth:** a single huge cell can grow the window up to `MaxPartSize`.

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

**Step 1: skeleton and packaging (2026-11-02).**

- **Projects:**
  - `src/CsvHelper.Excel` and `tests/CsvHelper.Excel.Tests` are in `CsvHelper.sln`, under its `src` and `tests` folders. `tools/CsvHelper.Excel.Corpus` joins at step 7.
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
    - white space with and without `xml:space`, and indented parts;
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

### Phase 2: typed reading (M)

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

### Phase 3: choosing what to read, and messy sheets (M)

- **Workbook metadata:**
  - Sheets: the dimension. Name, index, visibility and kind already come in Phase 1.
  - Defined names, workbook- and sheet-scoped. Static references only; anything with a formula is not a range.
  - Excel Tables from each sheet's table parts: name, `ref`, header row count and totals row count.
- **Read targets:**
  - All sheets, as one reader per sheet.
  - A named range.
  - A Table by name, using its header and excluding the totals row.
  - An explicit range like `B5:F200`.
- **Header-row detection:** scan the first N rows for the required column names of `T` (from `CsvRecordMapper.TryGetMembers<T>`), skip the title and logo rows above them, and clip to the header's columns.
- **Hidden rows and columns:** skip hidden rows (`<row hidden="1">`) and hidden columns (`<cols>`, which comes before the data). Logical column indexes are remapped.
- **Merged-cell fill:** fill down and across from the top-left cell. `<mergeCells>` comes *after* `<sheetData>`, so turning this on costs a second inflate pass. Document that cost.

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
- **Parallel writing:**
  - Generate cell XML in parallel through `ParallelWriteRecords`. Until then, a writer with a cell sink throws `NotSupportedException` for the parallel methods.
  - The style registry must be thread-safe.
  - Omitting `r` attributes means workers don't need absolute row numbers. Verify that every reader we test against accepts that first.
- **Our own zip writer:** async I/O, Zip64, data descriptors and CRC32.
  - Then a pigz-style experiment: deflate chunks in parallel and concatenate them after a sync flush.
  - Keep each piece only if it wins on the benchmark machine. The compress flattener showed that a faster stage doesn't always move the end-to-end numbers.
- **Memory:** profile workbooks with huge shared-string tables.

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
| All sheets, and sheet dimensions | 3 |
| Named ranges | 3 |
| Excel Tables by name | 3 |
| Header-row detection | 3 |
| Raw value vs displayed text | 8 |
| Formula cached value vs formula text | 2 |
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
- **Lockstep releases.** Internal hooks mean every core release must ship a matching CsvHelper.Excel.
- **Producer tolerance.** Excel accepts things the spec doesn't promise, and other producers rely on that. The corpus is the defense.
- **Merged-cell fill** needs a second inflate pass.
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
- `decimal` targets round to 15 significant digits.
- `DateTimeOffset` is written as ISO 8601 text.
- Row and column limits throw.
- Strings are written inline.
- Excel attributes are read by reflection.
- `MaxPartSize` is 256 MiB and `MaxUncompressedSize` 4 GiB, with no compression-ratio check (Phase 1).
- The default sheet is the first worksheet in tab order, hidden ones included (Phase 1).
- A sheet reader applies `SkipEmptyLines` itself, and with it off emits every row, so record n is row n (Phase 1).
- Rows with a value are padded to the first record's width (Phase 1).
