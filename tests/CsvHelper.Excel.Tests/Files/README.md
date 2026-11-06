# CsvHelper.Excel's test corpus

Workbooks written by real producers, for the corpus and oracle tests in `tests/CsvHelper.Excel.Tests/Corpus`. The tests only read these files. `tools/CsvHelper.Excel.Corpus` writes them again on demand, on Windows; it never runs in CI. The basic workbooks were written on 2026-11-02, the typed workbooks and Excel's answers on 2026-11-03, the orders on 2026-11-04, and the messy workbooks, with Excel's answers about every workbook's tables, names, hidden rows and columns and merged ranges, on 2026-11-05; the four from Apache POI's test data are under "Downloads".

```
dotnet run --project tools/CsvHelper.Excel.Corpus -c Release -- all
```

`excel`, `packageassets`, `orders`, `report`, `libreoffice`, `python`, `poi` and `dotnet` write one producer's files each, `downloads` fetches the files `downloads.txt` pins, `formats` asks Excel about number formats and `oracle` about every workbook's cells (below), and `check` only checks them.

## Excel's answers

`excel-formats.json` (written 2026-11-03 by `formats`, Excel 365 with an en-US interface) records what Excel says through COM about which number formats make a number a date. `Package/NumberFormatsTests.cs` checks CsvHelper.Excel's rules against it.

- **`codes`:** for each of 180 format codes in `tools/CsvHelper.Excel.Corpus/ExcelFormats.cs` (172 of which Excel takes), set on 45306.5 (2024-01-15 12:00) and on 1.5:
  - whether Excel takes the code, and the code as Excel keeps it;
  - whether `Range.Value` is a date, which it is when the format's first section shows a day, a month or a year, and not for a time alone;
  - `CELL("format")`, which is `D6` to `D9` for Excel's four time formats;
  - the text Excel shows for each value. An elapsed-time format shows 1.5 as 36 hours, 2,160 minutes or 129,600 seconds.
- **`cells`:** a workbook the tool writes, with a cell for each built-in number format id from 0 to 163, which Excel opens and answers for the same way. Also a custom date, cell formats with `applyNumberFormat="0"`, and `numFmts` entries that redefine built-in ids.
- **What it showed:**
  - Ids 79 and 81 are times (`mm:ss`, `mm:ss.0`), and 80 is elapsed time (`[h]:mm:ss`).
  - A `numFmts` entry wins over a built-in id.
  - Excel applies a cell format's number format even with `applyNumberFormat="0"`.
  - Only a format's first section decides (`0;yyyy` is a number).
  - A date part wins over elapsed time (`yyyy-mm-dd [h]:mm` shows the hour of the day).
  - Excel refuses lowercase `e+` in a number (`0.00e+00`).

## Excel's reading of the corpus

`excel-oracle/<file>.json` (written 2026-11-03 by `oracle`, and again on 2026-11-05 with the structures below, every cell's answer unchanged) records how Excel 365 reads each corpus workbook, every one but `excel/encrypted.xlsx` and the benchmarks' cross-checks, `excel/packageassets-1695.xlsx`, `excel/orders-1000.xlsx` and `excel/packageassets-report-250.xlsx`. `Corpus/ExcelAnswersTests.cs` compares CsvHelper.Excel's reading with it, cell by cell, and `Corpus/StructureAnswersTests.cs` its tables, names, hidden rows and columns, merged ranges and the reads that skip and fill them.

- **How:** Excel opens each workbook read-only, with calculation manual, so that it shows the values the file stores rather than computing its own, and with macros disabled. Columns are widened in memory, so that a number's text isn't `####` for want of room.
- **What:** for each cell of each worksheet's used range that has a value or a formula:
  - `value2`: a text, a number, a boolean, or an error's text;
  - `type`, the type of `Range.Value`: `text`, `number`, `date` (for a format that shows a day, a month or a year), `currency`, `boolean`, `error`, or `empty` for a formula without a value;
  - `text`, the text Excel shows, when it isn't `value2`;
  - `numberFormat`;
  - for numbers, `category` (`CELL("format")`) and `elapsed` (the format's text of a day and a half), which tell a time and an elapsed time from a number, as in `excel-formats.json`;
  - `formula` as Excel shows it, `formula2` when that differs, `hasArray` and `hasSpill`.
- **Structures** (added 2026-11-05), for each worksheet, read before the columns are widened, which would show hidden ones:
  - `hiddenRows` and `hiddenColumns`: the runs Excel hides over the whole grid (`8:8`, `D:XFD`), from `SpecialCells(xlCellTypeVisible)` over every cell, so whatever hides them (a filter, a collapsed group, a height or width of 0, `zeroHeight`, `defaultColWidth`) counts as Excel reads it;
  - `merged`: each cell's `MergeArea`, once per range, in the order of their top-left cells.
- **Names and tables** (added 2026-11-05), for the workbook:
  - `names`: each of COM's `Names`, with `Name` (`Report!Local` for a sheet's own), `RefersTo`, the sheet and address of `RefersToRange` when Excel has a range for it, and `Visible`. A1 of the first visible worksheet is made active first, since a relative name is relative to the active cell. Excel lists a hidden `_xlfn.X` name of `#NAME?` for each newer function a formula stores with its prefix, which no file stores.
  - `tables`: each worksheet's `ListObjects`, with `Name`, `DisplayName`, the address of `Range`, `ShowHeaders`, `ShowTotals` and the column names.
- **Repairs:** a workbook Excel opens only by repairing it is answered as repaired, with the records of Excel's repair log under `repaired` (the log's summary, which names the file's full path, is left out). Only `closedxml/typed.xlsx` needs it.
- **Refused:** a workbook Excel can't open even by repairing it is answered with `refused` alone. Only `openpyxl/messy.xlsx` (see "What the producers write").
- **A busy Excel:** just after opening `google/64450.xlsx`, Excel answers `SpecialCells` with "the application is busy" (`RPC_E_SERVERCALL_RETRYLATER`) while it builds the pivot table's cells; the command asks again until it answers. Only Excel's own failure of a call (`0x800A03EC`) counts as "no visible cell" or "no range", and a sheet without a visible cell must hide its first row or column.

## The typed workbook

Defined once in `tools/CsvHelper.Excel.Corpus/Typed.cs`, which `spec` doesn't print; the tool hands it to the Python and Java scripts as JSON. Each producer writes as much of it as its library can.

- **"Dates":** a number in each built-in date, time and duration format id, set by id, which Excel can't do; 29 format codes of dates, times and numbers with literals; four elapsed-time codes; durations around zero and past a day; serials around the date systems' limits (0, 0.5, 1, 59, 60, 61, the last day and millisecond, past it, -1, a day's last nanosecond, a millisecond and less), shown to the millisecond; and dates written as dates, from 1900-01-01 to 9999-12-31.
- **"Numbers":** integers to 2^63, 17-digit doubles, the largest and the least normal double, exponents, booleans, the seven classic errors as constants and as formulas' results (and `#CALC!`), and texts that look like values (`00123`, `1E5`, `TRUE`, `#N/A`, a date, spaces around a number).
- **"Formulas":** data in A1:E5, K and L, and formulas over it:
  - filled down rows 8 to 12, and right rows 14 to 16, so that Excel writes shared formulas: relative, absolute and mixed references, ranges, whole rows and columns, another sheet's quoted references, the defined name `Rate`, `LOG10`, and strings with references in them;
  - a legacy array (`{=A1:A3*2}`), a spill (`SEQUENCE(3)`), a reference to it (`H14#`) and a newer function (`XLOOKUP`);
  - a reference copied off the grid (`#REF!*2`), a formula with a line break, and one with a line break in its result;
  - the table `Orders` with a calculated column (`[@Qty]*[@Price]`) and a total over it.
- **"Other Sheet":** the cells the formulas refer to by a quoted sheet name.

## The messy workbook

Defined once in `tools/CsvHelper.Excel.Corpus/Messy.cs`, handed to the Python and Java scripts as JSON. A report laid out for people, and every structure Phase 3 reads: merged cells, hidden rows and columns, tables and defined names. Each producer writes what it can.

- **Sheets, in tab order:** "Report", "Chart" (a chartsheet, before "Tables" so that sheets' indexes count it), "Tables" and "Filtered".
- **"Report":** a title merged across B1:G1, a note, an empty row, the header at B4 with column D (Code) hidden, regions merged down column B (B5:B7, B8:B10, B11:B12, B13:B14), a block merged over C11:D12 into the hidden column, row 8 hidden (the top-left cell of South's region), rows 13 and 14 in a collapsed group, and a totals line of `SUM` formulas.
- **"Tables":** Sales (A1:C5) with a totals row that labels its first column and sums Qty; NoHeader (E3:F4) with its header row hidden, its names only in the table part; Wrapped (H1:I3) with a line break in a column name; and Away (C8:D10).
- **"Filtered":** A1:C10 under a filter that hides kind `b`, with row 6 empty; every row from 11 hidden (`zeroHeight`) and every column from D. Excel writes the empty row 6, which it shows; a producer that leaves the row out hides it, since `zeroHeight` hides the rows a sheet lacks.
- **Defined names:** the workbook's `Data`; `Local` as the own name of Report and of Tables; a relative name (`Report!B5`), a union, a constant, a formula, an `OFFSET` range, a 3-D reference, whole columns and whole rows, a name to a deleted sheet (Excel makes a sheet, names its cell and deletes it), a hidden name, a table reference (`Sales[Qty]`), and Report's print area and print titles. The filter adds Filtered's `_FilterDatabase` where the producer writes one.

## The basic workbook

Every producer writes the same content, defined once in `tools/CsvHelper.Excel.Corpus/Basic.cs`. `spec` prints it as JSON, which the Python script reads. Each producer writes as much of it as its library can.

- **Sheets, in tab order:**
  - "Data";
  - "Chart", a chartsheet;
  - "Hidden" (hidden);
  - "VeryHidden" (very hidden);
  - "Q1 Orders", a typed table;
  - "Bob's";
  - "日本語".
- **"Data":** a row per kind of value, labelled in column A:
  - text, integers, decimals, negatives, `1E-05` and a 17-digit double;
  - booleans;
  - `#N/A` and `#DIV/0!` from formulas;
  - a gap mid-row, a short row, an empty row (14), and a block from column C;
  - emoji, spaces around a text, a line break, U+0001, a literal `_x0041_`, `&<>"'`, and 32,767 characters;
  - rich text, and a phonetic run;
  - formulas with number, text and error results, and a shared formula down B28:B30;
  - dates, a date and time, a time alone, and a custom date format.

## Privacy

The files are public, and producers record personal data: Excel recorded the Office account's email address as the author, and the folder the file was saved in.

- Excel saves with "remove personal information" set, into `%PUBLIC%\CsvHelper.Excel.Corpus`, and the tool then copies the file, which keeps Excel's bytes.
- The other producers get the author `CsvHelper.Excel.Corpus`.
- The downloaded files were chosen from POI's test data because none of them names a person or holds an email address. One Excel for Mac file was passed over for the email address its threaded comment records.
- Every command ends with a check of every part of every file here: as UTF-8, Latin-1 and UTF-16. It fails on the user's or machine's name, the profile folder, or any email address.

## Files

| Files | Producer | How |
| --- | --- | --- |
| `excel/basic.xlsx`, `date1904.xlsx`, `strict.xlsx` (Strict Open XML), `macro.xlsm`, `template.xltx` | Excel 365, 16.0 build 20527 | COM automation (late bound) builds the workbook, then `SaveAs` with file formats 51, 51 (after setting `Date1904`), 61, 52 and 54 |
| `excel/binary.xlsb`, `encrypted.xlsx` (password `secret`), `legacy.xls` | Excel, the same way | file formats 50, 51 with a password, and 56: the formats CsvHelper.Excel rejects |
| `excel/packageassets-1695.xlsx` | Excel | NCsvPerf's `PackageAssets.csv` opened with `Workbooks.OpenText`, every column as text, and saved. See "Downloads" for the CSV's source. |
| `excel/orders-1000.xlsx` | Excel | The typed benchmark's first 1,000 orders (`bench/CsvHelper.Benchmarks/ExcelOrders.cs`, which the tool links), entered in one assignment of `Range.Value2`: numbers, booleans, text, and dates as serials in `yyyy-mm-dd hh:mm:ss`. The text columns' formats are cleared after, since Excel gives text such as the Guid `54e5160b-...` the scientific format as it would when typed. `Bench/ExcelTypedWorkbookTests.cs` checks the benchmark's writer against it. |
| `excel/packageassets-report-250.xlsx` | Excel | NCsvPerf's first 250 lines laid out as the report benchmark's report (`bench/CsvHelper.Benchmarks/ExcelReport.cs`, which the tool links), built as a user would: the text format on the header, the regions and the fields, the cells in one assignment of `Range.Value2`, a title merged across, the regions merged down each 100 rows, a table over the keys and the fields (Excel merges no cells in a table), and the keys' column and every 50th data row hidden. Making the table widened the columns whose header doesn't fit beside its filter button. `Bench/ExcelReportWorkbookTests.cs` checks the benchmark's writer against it. |
| `libreoffice/basic.xlsx`, `basic-ooxml.xlsx` | LibreOffice 26.8.2.2 | `soffice --headless --convert-to` from `excel/basic.xlsx`, with the "Calc MS Excel 2007 XML" and "Calc Office Open XML" filters, in a throwaway profile |
| `openpyxl/basic.xlsx` | openpyxl 3.1.5, Python 3.14.10 | `python/corpus.py`, in a venv outside the repository with the versions of `python/requirements.txt`, run with `-I` |
| `xlsxwriter/basic.xlsx`, `constant_memory.xlsx` | XlsxWriter 3.3.1 | the same script; the second with `constant_memory` |
| `pandas/openpyxl.xlsx`, `pandas/xlsxwriter.xlsx` | pandas 3.1.0 | the same script: a data frame per sheet, with each engine named |
| `poi/xssf.xlsx`, `poi/sxssf.xlsx` | Apache POI 6.0.0, on Java 25 (Eclipse Temurin 25.0.5+8) | `java/Corpus.java`, run by a JDK unpacked from its zip into `%LOCALAPPDATA%\CsvHelper.Excel.Corpus`, with the jars of `java/jars.txt` from Maven Central, each checked against its pinned SHA-256. XSSF writes the first, and SXSSF, POI's streaming writer, the second. POI's evaluator computes the formulas' results. |
| `google/64450.xlsx`, `google/66365.xlsx` | Google Sheets | downloaded from Apache POI's test data (see "Downloads"). Google Sheets writes no `docProps`, a theme named "Sheets" and Arial as the default font. The first has a pivot table and a chart; the second an array formula. |
| `excel-mac/github-321.xlsx`, `excel-mac/test_conditional_formatting.xlsx` | Excel for Mac ("Microsoft Macintosh Excel", app versions 16.0300 and 15.0300) | downloaded from Apache POI's test data: 17-digit numbers with and without a format, and text and numbers with formulas and conditional formats |
| `closedxml/basic.xlsx` | ClosedXML 0.105.1 | the tool, with formulas evaluated before saving |
| `openxmlsdk/basic.xlsx` | DocumentFormat.OpenXml 3.5.1 | the tool, building each part from the SDK's elements, with Excel's formula results |
| `epplus/basic.xlsx` | EPPlus 4.5.3.3, the last LGPL version | the tool, with its calculation engine for the formulas |
| `miniexcel/basic.xlsx` | MiniExcel 1.46.0 | the tool, from rows of values by column letter |
| `spreadcheetah/basic.xlsx` | SpreadCheetah 1.28.0 | the tool, a streaming writer, with Excel's formula results |
| `largexlsx/basic.xlsx` | LargeXlsx 2.0.2 | the tool, a streaming writer, with Excel's formula results |
| `excel/typed.xlsx`, `typed-1904.xlsx`, `typed-strict.xlsx` | Excel 365 | COM automation builds the typed workbook: values and formats, then the table and the name, then the formulas, `FillDown` and `FillRight` over the fills, and `Range.Copy` for the reference off the grid; `SaveAs` with file formats 51, 51 (after setting `Date1904`) and 61 |
| `libreoffice/typed.xlsx` | LibreOffice 26.8.2.2 | converted from `excel/typed.xlsx` with "Calc MS Excel 2007 XML" |
| `openpyxl/typed.xlsx`, `typed-isodates.xlsx` | openpyxl 3.1.5 | `python/corpus.py`; the second with `iso_dates`, which writes dates as `t="d"` |
| `xlsxwriter/typed.xlsx` | XlsxWriter 3.3.1 | `python/corpus.py`, with `use_future_functions`, the formulas' results given, and the table by `add_table` |
| `poi/typed.xlsx` | Apache POI 6.0.0 | `java/Corpus.java` with XSSF, each formula evaluated on its own |
| `closedxml/typed.xlsx` | ClosedXML 0.105.1 | the tool, with formulas evaluated before saving |
| `excel/messy.xlsx` | Excel 365 | COM automation builds the messy workbook as a user would: each table's cells and then the table (`ListObjects.Add`, then `ShowTotals` and `ShowHeaders`), the other cells, `Merge`, hidden columns and rows, `Group` and `Outline.ShowLevels`, `AutoFilter`, rows and columns hidden to the grid's end, the chartsheet, and the names (`Names.Add`, a worksheet's own for its names) with Report's A1 active; file format 51 |
| `libreoffice/messy.xlsx` | LibreOffice 26.8.2.2 | converted from `excel/messy.xlsx` with "Calc MS Excel 2007 XML" |
| `openpyxl/messy.xlsx`, `xlsxwriter/messy.xlsx` | openpyxl 3.1.5, XlsxWriter 3.3.1 | `python/corpus.py` |
| `poi/messy.xlsx` | Apache POI 6.0.0 | `java/Corpus.java` with XSSF |
| `closedxml/messy.xlsx` | ClosedXML 0.105.1 | the tool, with formulas evaluated before saving |

## What the producers write

These are the shapes the corpus covers; the oracle tests check every cell.

- **Prefixes:** the Open XML SDK, and ClosedXML and MiniExcel through it, write every element with the `x:` prefix. EPPlus writes its rich text runs with a `d:` prefix declared on each run.
- **References:** SpreadCheetah writes rows with `r` but cells without it. Everyone else writes both. LargeXlsx writes them even with `requireCellReferences: false`.
- **Strings:**
  - Shared strings: Excel, LibreOffice, XlsxWriter, pandas with XlsxWriter, POI's XSSF, ClosedXML, the Open XML SDK and EPPlus.
  - Inline strings: openpyxl, pandas with openpyxl, XlsxWriter's `constant_memory`, POI's SXSSF, SpreadCheetah and LargeXlsx. SXSSF also writes an empty shared strings part, and writes rich text as plain text.
  - MiniExcel writes text as `t="str"`, as a formula's result is.
- **Numbers:** POI writes them in Java's form: `42.0`, `1.0E-5`.
- **Formulas:** openpyxl writes them without cached results, so those cells are valueless. LibreOffice writes the shared formula as three, and POI writes it as three too. MiniExcel writes none.
- **Stray text:** POI's SXSSF writes a `>` between an error formula's `</f>` and its `<v>` (`<f>NA()</f>><v>#N/A</v>`). Excel opens the file and ignores it, as every reader here does.
- **Sheets:**
  - Chartsheets: Excel, openpyxl, XlsxWriter and EPPlus. POI can't create one.
  - LibreOffice turns the chartsheet into a worksheet with a chart, and the very hidden sheet into a hidden one.
  - pandas and MiniExcel write every sheet visible.
- **Escapes:**
  - Excel, LibreOffice, XlsxWriter, ClosedXML, EPPlus, MiniExcel and LargeXlsx write a literal `_x0041_` as `_x005F_x0041_`. The Open XML SDK, openpyxl, POI and SpreadCheetah write it as it is, so Excel and every reader read it as "A".
  - MiniExcel writes non-BMP characters as `_xD83D__xDE00_` escapes.
  - SpreadCheetah drops U+0001, POI writes it as `?`, and openpyxl can't write it.
- **Zip:** POI's SXSSF writes a Zip64 data descriptor after each entry. Its local headers say version 45 but have no Zip64 field, and its central directory says version 20. Excel, .NET's `ZipArchive` and its `System.IO.Packaging` read it; .NET Framework's `System.IO.Packaging` (WindowsBase), which the Open XML SDK and ClosedXML use there, rejects it as corrupt.
- **White space:** SpreadCheetah writes a text with spaces around it without `xml:space="preserve"`, so Excel, and CsvHelper.Excel, read it trimmed.
- **Strict:** Excel's Strict workbook writes its dates as `t="d"` ISO 8601 cells.
- **Google Sheets:**
  - It writes numbers in Java's form (`1.0`), `state="visible"` on every sheet, and declares Excel for Mac 2008's `mx` namespace.
  - It writes a relationship with no `Id` or `Target` in a pivot cache's relationships (POI's bug 64450). `System.IO.Packaging`, which the Open XML SDK and ClosedXML use, rejects the package for it; Excel opens it.
  - It writes a pivot table's sheet as empty rows, without the table's cells. Excel builds them from the pivot table when it opens the file, but they aren't in the sheet, so CsvHelper.Excel and every other reader here read the sheet as empty.
- **Typed workbooks:**
  - **Shared formulas:** Excel shares a filled formula from the fill's second cell on, the first keeping its own. It writes in full each cell's formula with another sheet's reference, and `SUM($A$1:A1)`.
  - **Newer functions and errors:** Excel stores `_xlfn.SEQUENCE`, `_xlfn.XLOOKUP` and `_xlfn.ANCHORARRAY(H14)` for `H14#`, and `#CALC!` and `#SPILL!` as `#VALUE!` with rich value metadata. XlsxWriter writes `XLOOKUP` as a dynamic array formula, and its users write `ANCHORARRAY()` for `#` (XlsxWriter writes a `#` as it is, which makes Excel repair the file).
  - **Strict:** Excel's Strict workbook has `dateCompatibility="0"`, a 1900 date system without 1900-02-29 that counts days as OLE Automation dates do: it writes serial 0 as `1899-12-30` and 60 as `1900-02-28`. It writes a cell in an elapsed-time format as an ISO 8601 duration (`PT36H0M0.000S`), even one whose date part wins (`yyyy-mm-dd [h]:mm`), and a serial outside the date range as a number.
  - **Precision:** openpyxl and XlsxWriter write numbers with 16 significant digits, and LibreOffice and ClosedXML with 15, so `0.1+0.2` becomes `0.3`, the largest double runs past it (Excel reads `#NULL!`), and the least normal one becomes subnormal (Excel reads 0). Excel writes 17 digits, and POI the shortest form that round-trips.
  - **ClosedXML** wrote `NA()`'s result `#N/A` as a number, without `t="e"`, which Excel repairs to `#VALUE!` (in the basic workbook it writes `t="e"`), and writes the format id 0 it was given as a format with an empty code. ClosedXML can't open any typed workbook: it rejects serial 60 in a date format (in the 1904 system too), a number past the largest double, and the `t="d"` texts of openpyxl and Excel's Strict files.
  - **COM:** a date handed to Excel through COM before March 1900 lands a day off (1900-01-01 as serial 2), and with its milliseconds rounded to the second: the cells hold what Excel was handed.
  - **openpyxl** writes no value in an array formula's other cells, and can't set a built-in format id outside its table, a dynamic array or a table's formulas. **LibreOffice** writes error literals and `ANCHORARRAY` in lowercase (`#ref!`). **POI** can't parse an undefined name (`NOSUCHNAME`), so that cell is left out.
- **Messy workbooks:**
  - **Excel:**
    - Turning on a table's totals row inserts cells in the table's columns, moving the cells below it down (Away's column C moved a row), so the producer makes each table before writing the cells below it. ClosedXML does the same.
    - It writes a name to a deleted sheet as `#REF!`, which COM shows as `=#REF!#REF!`; a hidden header row as `headerRowCount="0"`, a `ref` without it and no header cells; a line break in a column name as `_x000a_`; and a merged range's other cells as empty cells with the top-left cell's style.
    - "Filtered" gets `zeroHeight`, `defaultColWidth="0"`, `<col>` widths for A:C and a hidden `<col>` for D:XFD, and its empty row 6 as an empty `<row>`.
    - Excel opens no workbook with a name whose `localSheetId` is a chartsheet's, even by repairing it: a constant, another name and another target were all refused (checked through COM with variants of openpyxl's file).
  - **openpyxl** numbers a sheet's own names by worksheet, without the chartsheet before them: it writes Tables' `Local` as `localSheetId="1"`, the chartsheet's, and Filtered's `_FilterDatabase` as Tables'. Excel refuses the file, and ClosedXML throws ("There isn't a worksheet associated with that position"); CsvHelper.Excel reads the names as the chartsheet's and Tables', as the file says. It writes no cells under a merged range but the first, quotes the sheet in the print area and titles (`'Report'!$4:$4`), and leaves out the empty row 6, which `zeroHeight` hides.
  - **XlsxWriter** writes no cells under a merged range but the first (`merge_range` without a format), the totals formula as `SUBTOTAL(109,[Qty])`, as Excel shows it, a line break in a column name as `&#xA;`, and `hide_unused_rows` as `zeroHeight`, leaving out row 6. `define_name` can't hide a name, so `Secret` is left out.
  - **POI:** its schemas (`poi-ooxml-lite`) have no filter criteria, so it writes the filter's range and the rows it hides. Its names can't be hidden, so `Secret` is left out. On saving, it names the columns of a table without a header row after the table's first row (`ann`, `7`), whatever names it was given. It writes booleans as `true` (`hidden="true"`), and leaves out row 6, which `zeroHeight` hides.
  - **ClosedXML** has no way to write `zeroHeight`, so Filtered shows its rows from 11 and the row 6 it leaves out. It writes `collapsed` on a collapsed group's rows rather than the row after, the print titles without `$` (`Report!4:4`), the totals formula as `SUBTOTAL(109,[Qty])`, and every column to XFD in Filtered's rows (`spans="1:16384"`).
  - **LibreOffice** keeps the chartsheet as a worksheet with a chart, writes `#ref!` in lowercase, lists the tables in another order, puts the totals row inside the Sales filter's range, and writes booleans as `true`.
- **Phonetic runs:** the Open XML SDK and ClosedXML write `<rPh>`. Excel can't here: without the Japanese editing language it ignores phonetic readings set through COM. POI has no way to write one.

## Downloads

- **`google/` and `excel-mac/`:** from [apache/poi](https://github.com/apache/poi)'s `test-data/spreadsheet`, commit `81ab52621cb3ecaf9cb96a5a92edcb2e5fd60408` (2026-11-02), unchanged; `tools/CsvHelper.Excel.Corpus/downloads.txt` has each URL and SHA-256. Apache License 2.0, whose text is in the repository's `LICENSE.txt`, and POI's NOTICE: "Apache POI. Copyright 2003-2026 The Apache Software Foundation. This product includes software developed at The Apache Software Foundation (https://www.apache.org/)."
- **`excel/packageassets-1695.xlsx`** and **`excel/packageassets-report-250.xlsx`:** made from `NCsvPerf/TestData/PackageAssets.csv` in [joelverhagen/NCsvPerf](https://github.com/joelverhagen/NCsvPerf), commit `c7bff62e601eb1e2ce97c95a5b54ef94eff4b6ab`: 1,695 records of 25 fields, no header. The repository keeps a copy, which the tool reads, in `bench/CsvHelper.Benchmarks/Data`. MIT License, copyright 2020 Joel Verhagen; its text is in `LICENSE-NCsvPerf.txt` here, as the license asks of copies.

## Not here yet

- **NPOI:** left out. Its releases from 2.8.0 on carry an "Open Source Maintenance Fee" EULA, and the corpus has POI itself, which NPOI ports.
- **Apple Numbers:** no export of `excel/basic.xlsx` can be made, and Apache POI's test data has no Numbers file. SheetJS's test files, the other source the plan named, are blocked on GitHub ("private_information", since 2024-07-19), so none are taken from there or from a copy.
