# CsvHelper.Excel's test corpus

Workbooks written by real producers, for the corpus and oracle tests in `tests/CsvHelper.Excel.Tests/Corpus`. The tests only read these files. `tools/CsvHelper.Excel.Corpus` writes them again on demand, on Windows; it never runs in CI. All of them were written on 2026-11-02, except the four from Apache POI's test data under "Downloads".

```
dotnet run --project tools/CsvHelper.Excel.Corpus -c Release -- all
```

`excel`, `packageassets`, `libreoffice`, `python`, `poi` and `dotnet` write one producer's files each, `downloads` fetches the files `downloads.txt` pins, and `check` only checks them.

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
- **Phonetic runs:** the Open XML SDK and ClosedXML write `<rPh>`. Excel can't here: without the Japanese editing language it ignores phonetic readings set through COM. POI has no way to write one.

## Downloads

- **`google/` and `excel-mac/`:** from [apache/poi](https://github.com/apache/poi)'s `test-data/spreadsheet`, commit `81ab52621cb3ecaf9cb96a5a92edcb2e5fd60408` (2026-11-02), unchanged; `tools/CsvHelper.Excel.Corpus/downloads.txt` has each URL and SHA-256. Apache License 2.0, whose text is in the repository's `LICENSE.txt`, and POI's NOTICE: "Apache POI. Copyright 2003-2026 The Apache Software Foundation. This product includes software developed at The Apache Software Foundation (https://www.apache.org/)."
- **`excel/packageassets-1695.xlsx`:** made from `NCsvPerf/TestData/PackageAssets.csv` in [joelverhagen/NCsvPerf](https://github.com/joelverhagen/NCsvPerf), commit `c7bff62e601eb1e2ce97c95a5b54ef94eff4b6ab`: 1,695 records of 25 fields, no header. The repository keeps a copy, which the tool reads, in `bench/CsvHelper.Benchmarks/Data`. MIT License, copyright 2020 Joel Verhagen; its text is in `LICENSE-NCsvPerf.txt` here, as the license asks of copies.

## Not here yet

- **NPOI:** left out. Its releases from 2.8.0 on carry an "Open Source Maintenance Fee" EULA, and the corpus has POI itself, which NPOI ports.
- **Apple Numbers:** no export of `excel/basic.xlsx` can be made, and Apache POI's test data has no Numbers file. SheetJS's test files, the other source the plan named, are blocked on GitHub ("private_information", since 2024-07-19), so none are taken from there or from a copy.
