# CsvHelper.Excel plan

Status: approved 2026-11-01. Phase 0 (core hooks) implemented 2026-11-01, and its writer sink reworked 2026-11-02 to meet the benchmark gate; see "As built" under Phase 0.

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

- **`ExcelWorkbook`**: `Open(path)` and `Open(stream)`. Exposes `Sheets` (name, index, visibility, kind, dimension), `Tables`, `DefinedNames` and `Date1904`. `OpenReader(...)` takes a sheet name, index, `ExcelSheet`, Table or range, and returns a `CsvReader`.
- **`ExcelReaderOptions`**:
  - `Sheet`, `Range`, `Table`, `DefinedName`: what to read.
  - `HeaderRow`: a fixed row, or detect it from `T` (with a scan limit).
  - `FillMergedCells`, `SkipHiddenRows`, `SkipHiddenColumns`.
  - `ErrorCells`: Text, Null or Error (routes to `OnRecordError`).
  - `DecimalDigits`: significant digits for `decimal` targets (default 15).
  - `Formulas`: CachedValue (default) or FormulaText.
  - `Text`: Raw (default) or Formatted (Phase 8).
  - `MaxUncompressedSize`, `MaxPartSize`: zip-bomb limits.
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

- **Project:** `src/CsvHelper.Excel`, `tests/CsvHelper.Excel.Tests`, and benchmarks in `bench/CsvHelper.Benchmarks`.
- **Zip container:** `ZipArchive`. Non-seekable input is copied to a delete-on-close temp file.
- **Package parts:**
  - `[Content_Types].xml` and the relationships.
  - `workbook.xml`: the sheet list, hidden state and `date1904`.
  - `styles.xml`: `cellXfs` mapped to number format ids, plus custom `numFmts`.
- **Shared strings:**
  - Stored as one UTF-8 byte arena plus offsets, not .NET strings, so values copy straight into the field buffer.
  - Rich-text runs are joined into one value, and `<rPh>` phonetic runs are skipped.
  - XML entities and `_xHHHH_` escapes are decoded, with `_x005F_` as a literal underscore.
- **Sheet XML scanner:**
  - Forward-only over the inflated bytes.
  - Matches local names, because some producers write prefixes (`x:c`). Accepts both the transitional and strict namespaces.
  - Handles cell types `n`, `s`, `str`, `inlineStr`, `b`, `e` and `d`.
  - Places sparse cells by their `r` reference, and handles rows and cells with no `r` at all.
  - Rejects a DOCTYPE.
- **Sheet choice:** the first sheet, or one by name or index.
- **Zip-bomb guard:** limits on total and per-part uncompressed size.
- **Gate:**
  - `GetRecords<T>`, dynamic records, `CsvDataReader`, Linq and header validation work over a sheet.
  - Errors name the cell.
  - A baseline benchmark against ExcelDataReader, Sylvan.Data.Excel, MiniExcel and the Open XML SDK, using NCsvPerf's PackageAssets data converted to `.xlsx`.

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
  - Sheets: name, index, visibility (visible, hidden, very hidden), kind (worksheet, chartsheet) and dimension.
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
| All sheets, and the sheet list with metadata | 3 |
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
- **Oracles** (test-only dependencies):
  - The Open XML SDK, for reading and for `OpenXmlValidator`.
  - ClosedXML and ExcelDataReader.
  - LibreOffice `--headless --convert-to csv` in CI.
- **Round-trip property tests:** random records → write → read → equal.
- **Fuzzing:** malformed zip and XML input must fail with an exception, never hang or exhaust memory.
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
- `bench/CsvHelper.Benchmarks`: Excel read and write benchmarks
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
