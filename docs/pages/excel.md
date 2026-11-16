---
title: Excel
icon: file-excel
description: Read and write Excel workbooks (.xlsx) with CsvHelper.Excel, where a sheet reads as a CsvReader and writes as a CsvWriter, with typed cells, CsvHelper's record mapping and constant memory, without an Excel library; convert workbooks to CSV and CSV to workbooks; and write import templates, whose uploads import with every cell at fault named.
---

# Excel

The CsvHelper.Excel package reads and writes Excel workbooks (`.xlsx`). A sheet reads as a plain `CsvReader` and writes
as a plain `CsvWriter`, so everything that works for CSV works for a sheet: [`[CsvRecord]`](records.html) types,
dynamic records, `CsvDataReader`, [CsvHelper.Linq](linq.html) and `ParallelEnumerate`.

- **Typed cells.** Numbers, dates, booleans and errors read as what they are, in any culture, and write as Excel's own
  cells: a number is a number Excel can sum, a date is a date, and `00123` stays text.
- **Constant memory.** Rows stream from and into the zip package as they are read or written, so a sheet of a million
  rows takes the memory of a few (with a workbook's shared strings, which a reader holds while it reads).
- **No Excel library.** The package reads and writes the file format itself, with `System.IO.Compression`; Excel doesn't
  need to be installed.
- **Import templates.** A record type's template has a dropdown or a check for each column, and a locked header; the
  import of a filled one names every cell at fault, by its place.

```csharp
using CsvHelper;
using CsvHelper.Excel;

using (var reader = ExcelReader.FromFile("orders.xlsx"))           // the first worksheet
{
    foreach (var order in reader.GetRecords<Order>())
    {
    }
}

using (var workbook = ExcelWorkbookWriter.ToFile("report.xlsx"))
{
    workbook.WriteSheet("Orders", orders);                         // a bold, frozen header, columns as wide as their text
}

ExcelConvert.ToCsv("orders.xlsx", "orders.csv");                  // and back: ExcelConvert.ToExcel("orders.csv", "orders.xlsx")
```

It is not a workbook object model: it reads and writes rows of cells, and doesn't edit an existing workbook, compute
formulas, or write charts, pivot tables or images. A workbook library does those.

The examples on this page use this type:

```csharp
[CsvRecord]
public partial class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public bool Paid { get; set; }
}
```

## Install

<!-- if:released -->
```shell
dotnet add package CsvHelper.Excel
```

The package depends on CsvHelper, whose source generator writes the code that reads and writes `[CsvRecord]` types,
and on the exact version of it that it was built with, since it uses CsvHelper's internals: update the two together.
<!-- endif -->
<!-- if:unreleased -->
CsvHelper.Excel is not on NuGet yet. Build CsvHelper from source and reference the project, with the source generator
as for CsvHelper itself (see [Getting started](getting-started.html)):

```xml
<ItemGroup>
  <ProjectReference Include="..\CsvHelper\src\CsvHelper.Excel\CsvHelper.Excel.csproj" />
  <ProjectReference Include="..\CsvHelper\src\CsvHelper.Generators\CsvHelper.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

CsvHelper.Excel uses CsvHelper's internals, so it needs the CsvHelper it was built with: its package will depend on that
exact version.
<!-- endif -->

It has the same targets as CsvHelper: .NET 10, .NET 8, .NET Standard 2.1 and 2.0, and .NET Framework 4.7.

## Reading

### Opening a sheet

`ExcelReader.FromFile` and `ExcelReader.Create` open a workbook and return a reader of one of its sheets, the first
worksheet unless `ExcelReaderOptions` say otherwise. Disposing the reader closes the workbook.

```csharp
using var reader = ExcelReader.FromFile("orders.xlsx");
using var fromStream = ExcelReader.Create(stream, new ExcelReaderOptions { Sheet = "Orders" }, leaveOpen: true);
using var later = await ExcelReader.FromFileAsync("orders.xlsx");
```

The reader is a `CsvReader`: read [records](records.html), or read the fields of each row yourself as for CSV
([Reading](reading.html)).

```csharp
while (reader.Read())
{
    var id = reader.Parse<int>(0);
    var customer = reader.GetString("Customer");
}
```

- **Rows and columns.** Each row with a value is a record, and field `i` is column `i` of its row, from column `A`, or
  from the first column of the [range, table or defined name](#choosing-the-cells) read. Empty cells are empty fields,
  and rows are padded to the first record's width.
- **The header** is the first row read, as for CSV (`HasHeader`), unless [`HeaderRow`](#finding-the-header) finds it
  further down.
- **Empty rows** are skipped, as `SkipEmptyLines` skips empty lines. With it off, an empty row is a record of one empty
  field.
- **Errors** name their cell, such as `Orders!C14`, in `CsvFormatException.Location`.

The reader's options (`ExcelReaderOptions.Csv`) are those of a CSV reader: the header, empty lines, trimming, null
values, header matching and `OnRecordError` apply as for CSV. `FormatProvider`, `TrueValues` and `FalseValues` apply to
text cells only, since Excel stores numbers and booleans in one form for every culture. CSV's dialect (the separator,
quotes, comments, fixed widths) doesn't apply.

The file can be an `.xlsx`, `.xlsm`, `.xltx`, `.xltm` or `.xlam`. An `.xlsb`, an `.xls` or an encrypted workbook throws
`NotSupportedException`, and a file that isn't a workbook `InvalidDataException`. A stream that can seek is read from
its start; one that can't, such as a request body, is copied to a temporary file first. A file is opened so that Excel
can still save it while it is read.

### Cells and types

Excel stores a number in one form for every culture, and a date as a number in a date format. The reader gives each
cell's value to the target it is read as:

| Target | Number cell | Date cell | Boolean cell | Error cell |
| --- | --- | --- | --- | --- |
| `string` and `GetString` | its stored text: `1.5`, `0.30000000000000004` | ISO 8601: `2024-01-15`, `2024-01-15T13:45:00`, `13:45:00` | `TRUE` or `FALSE` | its text: `#N/A` |
| `double`, `float` | the number | its serial number | 1 or 0 | fails |
| `decimal`, integers | the number rounded to 15 significant digits (for an integer, if whole and in range) | the same | 1 or 0 | fails |
| `bool` | 1 is true, 0 false | fails | its value | fails |
| `DateTime`, `DateOnly`, `TimeOnly` | the serial number's date and time | the same | fails | fails |
| `TimeSpan` | the serial number as days | the same | fails | fails |
| `DateTimeOffset` | the serial number's date and time at offset zero | the same | fails | fails |
| enums, `Guid`, other types | its stored text, parsed as for CSV | | | fails |

Text cells read as CSV fields do, in the reader's culture.

- **Numbers.** Excel often stores 17 significant digits where it shows 15: `=0.1+0.2` is `0.30000000000000004`.
  `decimal` and integer targets round a number to 15 significant digits, half away from zero, as Excel shows it, so
  that it reads as `0.3`; `SignificantDigits` sets the digits (1 to 28), and `null` reads the stored number exactly.
  `double` and `float` targets and text are never rounded.
- **Dates and times.** A number is a date when its format is one: the built-in date formats, and format codes with
  date or time tokens. A duration format (`[h]:mm:ss`) makes it a duration, whose text is `36:00:00`. Excel's 1900 date
  system counts a February 29, 1900 that never was (serial 60), which reads as the text `1900-02-29` and fails for date
  targets; workbooks in the 1904 date system (`ExcelWorkbook.Date1904`) read as Excel shows them. A date's text is ISO
  8601 by what it holds, whatever its format: `2024-01-15`, `2024-01-15T13:45:00`, `2024-01-15T13:45:00.25`.
- **Error cells**, such as `#N/A` and `#DIV/0!`, read as `ErrorCells` says: as their text (`Text`, the default),
  which only text targets take, as empty cells (`Null`), or failing every read (`Error`). A failure inside `GetRecords`
  goes to `OnRecordError` with the cell's location.
- **Formulas** read as the value the file saved with them (`ExcelFormulas.CachedValue`, the default), or as their text,
  `=` and the formula (`FormulaText`). The reader doesn't compute formulas: a formula saved without a value, as some
  libraries write them, is an empty cell.

```csharp
var options = new ExcelReaderOptions
{
    ErrorCells = ExcelErrorCells.Null,       // #N/A reads as an empty cell
    SignificantDigits = null,                // decimals read the stored number exactly
    Formulas = ExcelFormulas.FormulaText,    // =SUM(A1:A3) rather than its value
};
```

### Choosing the sheet

`Sheet` names the sheet to read, matched ignoring case as Excel matches names, and `SheetIndex` gives its place in
tab order (from 0). A sheet that isn't a worksheet, such as a chartsheet, has no cells, and throws
`NotSupportedException`.

To look at the sheets first, or read several, open the workbook with `ExcelWorkbook.Open`: it lists the sheets (their
names, visibility, kinds, tables and used ranges), the tables and the defined names, and opens a reader of any
worksheet. Its readers leave it open; disposing the workbook closes the file.

```csharp
using var workbook = ExcelWorkbook.Open("sales.xlsx");
foreach (var sheet in workbook.Sheets)
{
    Console.WriteLine($"{sheet.Name} {sheet.Visibility} {sheet.Kind} {sheet.Dimension}");
}

using (var orders = workbook.OpenReader("Orders"))
{
    var list = orders.GetRecords<Order>().ToList();
}

using (var totals = workbook.OpenReader(2, new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } }))
{
}
```

Hidden sheets are read as any other. A workbook is not thread-safe: its readers can be used in turn, not at once.

### Choosing the cells

A sheet's data isn't always the whole sheet. These options read a block of cells, whose first column is field 0:

| Option | Reads | Example |
| --- | --- | --- |
| `Range` | a block of cells, of the sheet chosen or of the one it names | `B5:F200`, `B:F`, `5:200`, `Orders!B5:F200`, `'Q1 Orders'!B5:F200` |
| `Table` | an Excel table's data rows, with its columns' names as the header | `Sales` |
| `DefinedName` | the block of cells a defined name refers to | `Data`, `Orders!Local` |

```csharp
using var reader = ExcelReader.FromFile("sales.xlsx", new ExcelReaderOptions { Table = "Sales" });
```

A table and a defined name choose their sheet, and so does a range with a sheet's name; no other sheet can be given
with them. A table's header row is its columns' names even when Excel hides it, and its totals row isn't read.

### Finding the header

Reports often have a title, notes or blank rows above the header. `HeaderRow` finds the header row there, and the
records after it, clipped to its columns:

```csharp
var options = new ExcelReaderOptions
{
    HeaderRow = ExcelHeaderRow.Find<Order>(),                 // the first row with every column Order requires
    // HeaderRow = ExcelHeaderRow.Find(["Id", "Customer"]),  // or with these names
    // HeaderRow = ExcelHeaderRow.At(4),                      // or row 4, as Excel numbers rows
};
```

A search tries the first 20 rows read, or as many as it is given (`Find<Order>(50)`). When none has the columns, opening
the reader throws `KeyNotFoundException`, naming the rows tried and what the closest of them lacks.

### Hidden rows and columns, and merged cells

Hidden rows and columns are read as any other by default, since hidden data is still data and a filter hides rows too.
`SkipHiddenRows` and `SkipHiddenColumns` read only what Excel shows.

A merged range stores its value in its top-left cell, and its other cells are empty. `FillMergedCells` reads each cell
of a merged range as its top-left cell, as Excel shows the range.

### Dynamic records, data readers and LINQ

A sheet's reader is a `CsvReader`, so the rest of CsvHelper works on it:

```csharp
using var reader = ExcelReader.FromFile("orders.xlsx");

foreach (CsvDynamicRecord row in reader.GetRecords<CsvDynamicRecord>())    // values as GetString gives them
{
}

using var data = new CsvDataReader(ExcelReader.FromFile("orders.xlsx"));   // a DataTable, or SqlBulkCopy
var table = new DataTable();
table.Load(data);

using var query = ExcelReader.FromFile("orders.xlsx");                     // CsvHelper.Linq
var big = query.Query<Order>().Where(o => o.Total > 100).ToList();
```

A dynamic record's values are the cells' text, as `GetString` gives it: a date is ISO 8601 text, a boolean `TRUE` or
`FALSE`. Writing dynamic records with a CSV writer (`WriteRecords(reader.GetRecords<dynamic>())`) writes each cell's
stored form instead, a date as its serial number and a boolean as `1` or `0`; [`ExcelConvert.ToCsv`](#workbooks-to-csv)
writes their text.

### Untrusted workbooks

A workbook is a zip package, and a crafted one can inflate a few kilobytes into gigabytes. The reader streams sheets,
and two limits bound everything else:

- `MaxPartSize` (256 MiB by default): the most a part held in memory may inflate to (the workbook, styles and shared
  strings), and the largest single cell of a sheet.
- `MaxUncompressedSize` (4 GiB by default): the most the whole workbook may inflate to, across every reader of it,
  including the copy of a stream that can't seek and the fields that repeated shared strings and empty cells add.

A workbook past a limit, or one whose parts can't be read, throws `InvalidDataException`. The reader's fuzz tests feed
it mutated workbooks, which it reads or rejects with these exceptions.

## Writing

### Writing sheets

`ExcelWorkbookWriter.ToFile` creates a workbook file, and `ExcelWorkbookWriter.Create` writes one into a stream, from
its position; the stream doesn't need to seek, so a response body works. Sheets are written one at a time, in tab
order, and disposing the writer completes the workbook.

```csharp
using (var workbook = ExcelWorkbookWriter.ToFile("report.xlsx"))
{
    workbook.WriteSheet("Orders", orders);            // the header, then a row per record

    using (var sheet = workbook.AddSheet("Notes"))    // a CsvWriter: fields by hand, or records
    {
        sheet.WriteField("Exported");
        sheet.WriteField(DateTime.Now);
        sheet.EndRecord();
    }
}

await using (var workbook = ExcelWorkbookWriter.Create(Response.Body))
{
    await workbook.WriteSheetAsync("Orders", orders);    // or an IAsyncEnumerable<Order>
}
```

- `WriteSheet<T>` writes a sheet whole, as `WriteRecords` writes records. `AddSheet` returns the writer of a sheet,
  which disposing completes: record `n` (from 0) is row `n + 1`, and field `i` is column `i` from `A`.
  `AddSheet<T>` does the same with the formats and Excel attributes of `T`'s members.
- A workbook with no sheet gets one empty sheet, `Sheet1`, since Excel opens no workbook without one.
- **Sheet names** are as Excel takes them: 1 to 31 characters, none of `\ / ? * [ ] :`, no apostrophe first or last,
  and unique ignoring case. Another name throws `ArgumentException`.
- **Errors.** A record that fails (a converter's exception, a cell too long for Excel, a row past the last) is
  discarded, as a CSV writer discards one: the sheet keeps the records before it, and the workbook is still one Excel
  opens.

The writer's options (`ExcelWriterOptions.Csv`) are a CSV writer's, of which `HasHeader`, `ValidateFieldCount`,
`FormatProvider` and `BufferSize` apply. The dialect doesn't, and neither does `Injection`: a text cell is never
evaluated, so `=1+2` is written as the text it is.

### What each value becomes

Each value becomes the cell of its type:

| Value | Cell |
| --- | --- |
| `string`, `char`, `Guid`, enums, other types | text; an empty string is no cell |
| `bool` | a boolean |
| integers, `decimal` | a number; past 15 significant digits, as `LongNumbers` says |
| `double`, `float`, `Half` | a number; `NaN` and the infinities are text |
| `DateTime` | a date, in `yyyy-mm-dd`, `yyyy-mm-dd hh:mm:ss` or `yyyy-mm-dd hh:mm:ss.000` by what it holds |
| `DateOnly`, `TimeOnly` | a date in `yyyy-mm-dd`, a time in `hh:mm:ss` |
| `TimeSpan` | a duration in `[h]:mm:ss` |
| `DateTimeOffset` | as `DateTimeOffsets` says: text by default |
| `Uri` | text, and a link when `Links` says so |
| `ExcelFormula` | a formula |
| `null` | no cell |

- **Text that looks like a number**, such as `00123`, a ZIP code or a phone number, stays text, and Excel doesn't flag
  it with a warning triangle. A cell takes at most 32,767 characters; a longer one throws `InvalidOperationException`
  naming the cell, and so does a field past column `XFD`.
- **Dates** are written to the millisecond, as Excel keeps them, and read back as the same values. A member's
  `[CsvFormat]` gives a date column an Excel format that shows the same text (`dd/MM/yyyy` as `dd/mm/yyyy`); a format
  Excel can't show writes text. Dates before 1900-01-01, Excel's first, and negative durations, which Excel shows as
  `########`, are text, or throw with `OutOfRangeDates = Throw`.
- **Long numbers.** Excel keeps 15 significant digits, so a 16-digit ID or card number would silently change. An integer
  or `decimal` with more is text by default; `LongNumbers = Number` writes it as a number Excel rounds, and `Throw`
  throws. Doubles are numbers whatever their digits, since the cell stores the same double.
- **Offsets.** A cell has no offset, so a `DateTimeOffset` is its ISO 8601 text by default, which keeps both its time
  and its offset. `DateTimeOffsets = Utc` writes it as a date of its time in UTC, and `ClockTime` as a date of its
  clock time.

```csharp
var options = new ExcelWriterOptions
{
    LongNumbers = ExcelLongNumbers.Number,           // 16-digit numbers as numbers, rounded by Excel
    DateTimeOffsets = ExcelDateTimeOffsets.Utc,      // offsets as dates in UTC
    OutOfRangeDates = ExcelOutOfRangeDates.Throw,    // no text dates before 1900
};
```

### Formulas, links and merged cells

A formula is written as a formula cell, for Excel to compute when it opens the workbook: an `ExcelFormula` member or
value, or `WriteFormula` by hand. The cell has no saved value, so a reader that doesn't compute formulas sees it empty.
In CSV, the same field is the formula's text.

```csharp
using (var sheet = workbook.AddSheet("Totals"))
{
    sheet.WriteField("Total");
    sheet.WriteFormula("SUM(Orders!D2:D1001)");    // or sheet.WriteField(new ExcelFormula("=SUM(...)"))
    sheet.EndRecord();
    sheet.MergeCells("A3:B3");                       // a range shown as one cell, its top-left cell's value
}
```

A `Uri` is a link Excel opens when the cell is clicked. By default (`ExcelLinks.Web`) only `http`, `https` and `mailto`
URIs are links, so that data can't plant a link to a file share or another scheme's handler; `All` makes every URI a
link, and `None` none. A sheet holds at most 65,530 links, and the URIs after them are text.

### How sheets look

By default a sheet has a bold header that stays in view as it scrolls, and columns as wide as the text of the header and
the first 100 records, up to 60 characters. `ExcelSheetOptions` change that for a sheet, `ExcelWriterOptions.Sheets`
for every sheet, and `[ExcelSheet]` for a record type's sheets:

```csharp
workbook.WriteSheet("Orders", orders, new ExcelSheetOptions
{
    Title = "Orders, 2024",             // in row 1 above the header, bold, merged across the columns
    AutoFilter = true,                  // filter buttons on the header
    FreezeColumns = 1,                  // column A stays in view
    AutoFit = 1000,                     // widths from the first 1,000 records; 0 for none
    Columns = new Dictionary<string, ExcelColumnOptions>
    {
        ["Total"] = new() { Format = "#,##0.00", Width = 12 },
        ["Customer"] = new() { Wrap = true, Width = 40 },
        ["Paid"] = new() { Hidden = true },
    },
});

workbook.WriteSheet("Sales", sales, new ExcelSheetOptions { Table = new ExcelTableOptions { Name = "Sales" } });

[CsvRecord]
[ExcelSheet(AutoFilter = true, FreezeColumns = 1)]
public partial class Invoice
{
    public int Id { get; set; }

    [ExcelColumn(Format = "#,##0.00", Width = 12)]
    public decimal Amount { get; set; }
}
```

- **Columns** are matched by their header names, and their options beat a member's `[ExcelColumn]`; a name the header
  doesn't have throws `InvalidOperationException` when the header is written. A column's
  `Format` is an Excel format code, for display only: the cells keep their values, so `#,##0.00` shows `1,234.50` for
  a cell that holds 1234.5. (`[CsvFormat]` doesn't format numbers, in a sheet or in CSV.) A code Excel can't open throws
  when the sheet is added.
- **A table** (`ExcelTableOptions`) makes the header and the rows an Excel table, with a name formulas can use
  (`=SUM(Sales[Total])`), one of Excel's 60 built-in styles (`TableStyleMedium2` by default), banded rows and filter
  buttons.
- **Auto-fit** holds the header and the records it measures until their widths are known, since the widths come before
  the rows in the file; a later record's wider cell isn't measured. Dates, times and formatted numbers always get the
  width they need, since Excel shows a number too wide for its column as `########`.

### Large workbooks

A sheet is written in the same memory whatever its size: rows are deflated into the package as they are written.

- **Strings.** Text is written in each cell by default (`ExcelStrings.Inline`), so nothing is kept between rows.
  `Strings = Shared` keeps each distinct string once, as Excel does, which makes a much smaller file when text repeats
  (22.7 MB against 139.2 MB for a million rows of NCsvPerf's package data), at the cost of memory for every distinct
  string.
- **Compression.** `CompressionLevel.Fastest` by default; `Optimal` writes a file 35% to 45% smaller, in about three
  times the time.
- **The last row.** A sheet has at most 1,048,576 rows. Past that, the writer throws by default
  (`ExcelRowLimit.Throw`); `RowLimit = NewSheet` goes on in a new sheet, `Orders (2)`, with the header repeated.
- **Size.** A sheet past 4 GiB is written with Zip64.

Writing is not thread-safe, as `CsvWriter` isn't. Before .NET 10, closing a sheet and the workbook writes to the stream
synchronously, even from `DisposeAsync`; a stream that forbids synchronous writes, such as ASP.NET Core's response body,
then needs a buffer or a file in between.

## Converting

`ExcelConvert` converts a sheet to CSV and CSV to a sheet, from files, from streams, or from a reader into a writer.

### Workbooks to CSV

```csharp
ExcelConvert.ToCsv("orders.xlsx", "orders.csv");                                    // the first worksheet
ExcelConvert.ToCsv(excelStream, csvStream, new ExcelToCsvOptions
{
    Excel = new ExcelReaderOptions { Sheet = "Orders", HeaderRow = ExcelHeaderRow.Find(["Id"]) },
    Csv = new CsvWriterOptions { WriteByteOrderMark = true },
});
var files = ExcelConvert.ToCsvFiles("sales.xlsx", "sales");                         // every worksheet: sales/Orders.csv, ...

using var reader = workbook.OpenReader("Orders");                                  // or a reader into a writer
using var writer = CsvWriter.ToFile("orders.csv");
ExcelConvert.ToCsv(reader, writer);
```

The CSV is the sheet as the reader reads it (`ExcelToCsvOptions.Excel`: the sheet or block, the header, hidden rows,
merged cells, errors and formulas), written as `ExcelToCsvOptions.Csv` says. Each field is the cell's text, copied as
UTF-8 without a string:

- text as it is;
- numbers to 15 significant digits, as Excel shows them, so that `0.30000000000000004` is `0.3`; `SignificantDigits =
  null` writes the stored number;
- dates and times in ISO 8601 (`2024-01-15`, `2024-01-15T10:30:00`), durations as `36:00:00`, booleans as `TRUE` and
  `FALSE`, and errors as `ErrorCells` says.

`ToCsvFiles` writes every worksheet, hidden ones included, to a file named after its sheet, made a file name on every
system: a sheet named `Sales <2024>` is `Sales _2024_.csv`, and no name writes outside the folder. A conversion that
fails deletes the file it was writing; streams are left open.

The CSV differs from Excel's own "Save As" CSV, which writes what each cell shows:

| | ExcelConvert.ToCsv | Excel's "Save As" |
| --- | --- | --- |
| Numbers | 15 significant digits, unformatted: `1234.5` | as shown: `$1,234.50`, General in 11 characters |
| Dates | ISO 8601: `2024-01-15` | as shown: `1/15/2024` |
| Booleans | `TRUE`, `FALSE` | `TRUE`, `FALSE` |
| Columns | from column `A`, each row as wide as the first | the used range, every row as wide as it |
| Byte order mark | none, as CsvHelper writes CSV | "CSV UTF-8" writes one |
| Sheets | one, or every worksheet with `ToCsvFiles` | the active sheet |

Excel reads a CSV file without a byte order mark in the system's code page, so set `WriteByteOrderMark` for a CSV that
Excel will open and that has text beyond ASCII.

The largest doubles are written as Excel writes them, to 15 digits: `double.MaxValue` (`1.7976931348623157E+308`) is
`1.79769313486232E+308`, which is past a double's range. .NET reads it as infinity, and .NET Framework's `double.Parse`
refuses it; `SignificantDigits = null` writes the stored number, which reads back exactly.

### CSV to workbooks

```csharp
ExcelConvert.ToExcel("orders.csv", "orders.xlsx");                                      // a sheet named orders
ExcelConvert.ToExcel("orders.csv", "orders.xlsx", new CsvToExcelOptions { InferTypes = 0 });  // every cell text
ExcelConvert.ToExcel<Order>("orders.csv", "orders.xlsx");                               // typed by Order's members

using (var workbook = ExcelWorkbookWriter.ToFile("all.xlsx"))                          // several CSV files
{
    using var orders = CsvReader.FromFile("orders.csv");
    workbook.WriteSheet("Orders", orders);
    using var customers = CsvReader.FromFile("customers.csv");
    workbook.WriteSheet("Customers", customers);
}
```

`ToExcel` infers each column's type from its first 1,000 records (`InferTypes`), which it holds in memory until they
decide: a column is the narrowest type that every field of it there matches, or text.

| Type | Matches |
| --- | --- |
| Number | a minus or none, digits without a leading zero, then a fraction and an exponent if any (`0`, `-12`, `3.5`, `1E+5`), in the CSV's culture (`1,5` in de-DE), with at most 15 significant digits, or a double's own text |
| Date | `yyyy-MM-dd`, of a day that exists |
| Date and time | a date, `T` or a space, then `HH:mm`, `:ss` and a fraction of up to 7 digits; a column of dates and dates and times is one of dates and times |
| Boolean | `true` or `false`, in any case |
| Text | anything else, a mix of types, or a column of empty fields |

- Each field is then written by its column's type, as `WriteSheet` writes that type; a later field that doesn't match,
  such as `N/A` in a column of numbers, is text. Empty fields, and the CSV reader's `NullValues`, are empty cells.
- Inference keeps what a field shows: `00123`, `+5`, `1,234`, `$5`, `50%` and `1/15/2024` are text, where Excel would
  read them as numbers and dates and drop their zeros or signs. Times alone and durations are text too.
- **Doubles written by .NET**, Python or JavaScript have up to 17 significant digits (`4.505630514819934`), past the 15
  Excel shows. A number with a fraction or an exponent that is its double's own text, the shortest that reads back as
  that double, is a number cell holding that double, whatever `LongNumbers` says. Other numbers of more than 15
  significant digits, such as a 16-digit ID or `0.30000000000000005`, are text, unless
  `Excel = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Number }` makes them numbers that Excel rounds.
- Dates and times with an offset (`2024-01-15T10:30:00+02:00`) follow `DateTimeOffsets`, so they are text by default.

`ToExcel<T>` reads `T`'s records and writes them as `WriteSheet<T>` does, so that every cell is typed by its member.
From a file, the sheet is named after the CSV file as Excel names it (`orders`); from a stream, `Sheet1`, unless
`SheetName` is set. The sheet's look is `Sheet`, else `T`'s `[ExcelSheet]` for `ToExcel<T>`, else the writer's `Sheets`.

## Templates and imports

A template is a workbook for users to fill in: a record type's header without rows, each column checked by Excel as
users type, and the header locked. `ExcelReader.Import` reads the filled workbook back, and gives the records that read
and every cell that doesn't, by its place, so that users can fix them all at once.

```csharp
ExcelTemplate.Create<OrderUpload>("orders-template.xlsx");          // a sheet named OrderUpload

var result = ExcelReader.Import<OrderUpload>("upload.xlsx");
if (result.HasErrors)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"{error.Location}: {error.Message}");
    }
}

// An upload with a bad date and total in row 3, and an unknown status and region in row 4:
// OrderUpload!C3: String '2024-13-01' was not recognized as a valid DateTime.
// OrderUpload!D3: The input string 'abc' was not in a correct format.
// OrderUpload!F4: 'Lost' is not a value of OrderStatus.
// OrderUpload!G4: The region is North, South, East or West.
```

The examples in this section use this type:

```csharp
public enum OrderStatus { Pending, Shipped, Delivered }

[CsvRecord]
public partial class OrderUpload
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public bool Paid { get; set; }
    public OrderStatus Status { get; set; }

    [ExcelColumn(List = ["North", "South", "East", "West"], Prompt = "The region that ships the order.")]
    [CsvValidate(nameof(IsRegion), Message = "The region is North, South, East or West.")]
    public string Region { get; set; } = "";

    private static bool IsRegion(string region) => region is "North" or "South" or "East" or "West";
}
```

### Templates

`ExcelTemplate.Create<T>` writes a workbook of one sheet: `T`'s header and no records,
[validated](#validations-lists-and-prompts) and [protected](#protection), whatever look it takes
(`ExcelTemplateOptions.Sheet`, else `T`'s `[ExcelSheet]`, else the writer's `Sheets`). The sheet is named after the
type, unless `SheetName` is set. A path overload creates or overwrites the file, and deletes it when writing fails; a
stream overload leaves the stream open.

```csharp
await ExcelTemplate.CreateAsync<OrderUpload>(stream, new ExcelTemplateOptions
{
    SheetName = "Orders",
    Sheet = new ExcelSheetOptions { Title = "Orders to import", Password = "s3cret" },
});
```

The columns are as wide as their header's names, and at least Excel's default width; dates' columns as wide as a date.
Validation and protection aren't only for templates: any sheet can have them, such as a sheet of records to edit and
import again.

```csharp
workbook.WriteSheet("Orders", orders, new ExcelSheetOptions { Validate = true, Protect = true });
```

### Validations, lists and prompts

`Validate` gives each column below the header a validation from its member's type, which Excel applies to what users
type, refusing a value with its own message; and the type's format as the column's default, which the cells users add
take:

| Member's type | Excel takes | Column's format |
| --- | --- | --- |
| `bool` | a dropdown of `TRUE` and `FALSE`, or of `[CsvBooleanValues]`' first true and false values | General; Text with `[CsvBooleanValues]` |
| an enum | a dropdown of its names as the writer writes them (`[CsvEnumName]`); anything for `[Flags]` | Text |
| integers | whole numbers in the type's range; for `long` and `ulong`, of at most 15 digits, the digits Excel keeps | General |
| `float`, `double`, `decimal` | numbers in the type's range | General |
| `DateTime`, `DateOnly` | dates from 1900-01-01 to 9999-12-31 | `yyyy-mm-dd hh:mm:ss`, `yyyy-mm-dd` |
| `TimeOnly`, `TimeSpan` | times of day; durations from 0 | `hh:mm:ss`, `[h]:mm:ss` |
| `DateTimeOffset` | as `DateTime` under `DateTimeOffsets = Utc` or `ClockTime`; anything under `Text` | as `DateTime`; Text under `Text` |
| `char` | one character | Text |
| `string`, `Guid`, other types | anything | Text, so that `00123` stays as typed |

- A column's `Format`, or a date member's `[CsvFormat]`, takes the place of the type's format. A `[CsvFormat]` Excel
  can't show makes the column text without a validation, so that users type the member's format. A member with a
  converter gets no validation, and General.
- Excel's dates are its own: `2024-01-15`, `2024-01-15 10:30` and `1/15/2024` are dates, but ISO 8601's
  `2024-01-15T10:30:00` is text, which a date's column refuses.
- Types are known for `WriteSheet<T>`, `AddSheet<T>`, `ToExcel<T>` and templates; another sheet gets only its columns'
  lists and prompts.

A column's `List` (or `[ExcelColumn(List = ...)]`) is a dropdown of its values, in place of its type's validation, and
its `Prompt` is help that Excel shows when a cell of the column is selected, titled with the column's name. Both apply
to any sheet, validated or not:

```csharp
workbook.WriteSheet("Orders", orders, new ExcelSheetOptions
{
    Validate = true,
    Columns = new Dictionary<string, ExcelColumnOptions>
    {
        ["Customer"] = new() { List = customers, Prompt = "A customer from the list." },
    },
});
```

- **Lists.** Each value has 1 to 255 characters. A short list (at most 255 characters joined by commas, and no value
  with a comma or white space at either end) is written in the validation, and Excel matches it in its case; a longer
  one goes in a very hidden sheet, `Lists`, after the workbook's other sheets, and Excel matches it ignoring case. In a
  column of General format, Excel enters a value that looks like a number (`00123`) as a number, which a list of text
  doesn't match: give the column `Format = "@"`.
- **Prompts** have 1 to 255 characters, line breaks and tabs kept; another control character is written as an escape of
  7 characters, which count. A longer prompt throws when the sheet is added, since Excel would drop it.

A validation checks what users type, not what they paste, and a workbook saved by LibreOffice keeps it over the rows it
uses and 1,000 more. So reading the sheet back doesn't trust it: the import reads each cell by its member's type.

### Protection

`Protect` locks the title, the header and every column past the header's, so that users change only the cells below
the header in its columns. They can still resize columns and rows, and sort and filter with `AutoFilter`. `Password`,
of 1 to 255 characters, is asked for to unprotect the sheet; it is stored as Excel stores it, as a SHA-512 hash with a
random salt. `[ExcelSheet]` has no password, since an attribute's text is in the assembly for anyone to read.

- **Protection keeps out slips, not people:** tools remove it, password or not. Check what comes back, as the import
  does.
- A protected sheet can't have a `Table`, since Excel doesn't add rows to a table on a protected sheet, and a `Password`
  needs `Protect`: either throws `ArgumentException`.
- **Large sheets.** `Protect` adds about 10% to the time of writing a large sheet, since every cell below the header
  names its unlocked style. A password's hash takes about 24 ms, once a sheet.

### Importing

`ExcelReader.Import<T>` reads a sheet's records as `GetRecords<T>` does. A record that doesn't read is left out, and
gives an error for each of its cells at fault, so that users see every cell to fix at once:

```csharp
var result = ExcelReader.Import<OrderUpload>(stream, new ExcelReaderOptions { Sheet = "Orders" }, maxErrors: 100);
```

`ExcelImportResult<T>` has the `Records` that read, in the sheet's order; the `Errors`, by row, then column;
`HasErrors`; and `Complete`, which is `false` when reading stopped at `maxErrors` (1,000 by default), since an upload
of the wrong file can have a million cells at fault. Each `ExcelImportError` says where it is and what is wrong:

| Property | For `abc` in the `Total` column of row 3 |
| --- | --- |
| `Location` | `OrderUpload!D3`; a record's row, `OrderUpload!3:3`, when no one cell is at fault, such as when its constructor throws |
| `Row` | 3 |
| `Column`, `Member` | `Total`, `OrderUpload.Total` |
| `Value` | `abc`, the cell's text as users see it |
| `Message`, `Exception` | the cause's message and the cause, here a `FormatException` |

- **What is checked** is what `T`'s mapping reads: types, enum names, converters and
  [`[CsvValidate]`](records.html#checking-values-csvvalidate). A column's list and its validation's bounds aren't
  checked again; `[CsvValidate]` says what a member takes, as `Region`'s does above. The mapping can take more than
  Excel's validation, such as an enum's name in any case.
- **A column the header lacks** is an error at the header's row (`OrderUpload!1:1`), and no record is read. A header row
  that isn't found gives the same errors, at the row `HeaderRow` names or the closest row its search tried.
- **Empty rows** are skipped, and error cells read as `ErrorCells` says, so that a `#N/A` in a number's column is an
  error of its own. `OnRecordError` isn't called, since every error is in the result.
- **A reader.** `Import<T>(reader)` reads from where a reader stands: a sheet of an open `ExcelWorkbook`, or CSV, whose
  errors have no `Location`, and whose `Row` is the record's number. `ImportAsync` reads asynchronously.
- An upload is an [untrusted workbook](#untrusted-workbooks): one that can't be read throws, as `ExcelReader.FromFile`
  throws.

The import takes `GetRecords`' time for records that read, and each cell at fault adds a few microseconds.

## Trimming and native AOT

The `net10.0` and `net8.0` builds are marked `IsAotCompatible` and `IsTrimmable`. Reading and writing work in trimmed
and native AOT applications: `[CsvRecord]` types use their generated code, and `[ExcelSheet]`, `[ExcelColumn]` and
`[CsvFormat]` are read by reflection over the members that the annotations of `WriteSheet<T>` and `AddSheet<T>` keep.
C#'s `dynamic` and CsvHelper.Linq's queries need code generated at run time, as for CSV
([Target frameworks](target-frameworks.html#trimming-and-native-aot)).

On .NET Standard and .NET Framework, `DateOnly` and `TimeOnly` don't exist, and an `ExcelFormula` member reads through
`[CsvConverter(typeof(ExcelFormulaConverter))]`.

## Options

`ExcelReaderOptions`, for `ExcelReader`, `ExcelWorkbook` and their readers:

| Option | Default | |
| --- | --- | --- |
| `Sheet`, `SheetIndex` | the first worksheet | The sheet to read. |
| `Range`, `Table`, `DefinedName` | the whole sheet | A block of cells to read. |
| `HeaderRow` | the first row read | Where the header row is. |
| `SkipHiddenRows`, `SkipHiddenColumns` | `false` | Whether hidden rows and columns are skipped. |
| `FillMergedCells` | `false` | Whether a merged range's cells read as its top-left cell. |
| `ErrorCells` | `Text` | How error cells read. |
| `Formulas` | `CachedValue` | What a formula's cell reads as. |
| `SignificantDigits` | 15 | The digits `decimal` and integer targets round numbers to. |
| `Csv` | `CsvReaderOptions.Default` | The options of the readers. |
| `MaxPartSize`, `MaxUncompressedSize` | 256 MiB, 4 GiB | The limits of what a workbook inflates to. |

`ExcelWriterOptions`, for `ExcelWorkbookWriter`:

| Option | Default | |
| --- | --- | --- |
| `Strings` | `Inline` | Where text cells keep their strings. |
| `CompressionLevel` | `Fastest` | The package's compression. |
| `RowLimit` | `Throw` | What a record past a sheet's last row does. |
| `LongNumbers` | `Text` | What integers and decimals of more than 15 significant digits become. |
| `DateTimeOffsets` | `Text` | What a `DateTimeOffset` becomes. |
| `OutOfRangeDates` | `Text` | What dates before 1900 and negative durations become. |
| `Links` | `Web` | Which URIs are links. |
| `Sheets` | `ExcelSheetOptions.Default` | The look of a sheet added without options of its own. |
| `Csv` | `CsvWriterOptions.Default` | The options of the sheets' writers. |

`ExcelSheetOptions` (and `[ExcelSheet]`), for a sheet's look:

| Option | Default | |
| --- | --- | --- |
| `Title` | none | A title above the header. |
| `BoldHeader`, `FreezeHeader` | `true` | A bold header that stays in view. |
| `FreezeColumns` | 0 | Columns from `A` that stay in view. |
| `AutoFilter` | `false` | Filter buttons on the header. |
| `Table` | none | The header and rows as an Excel table (`ExcelTableOptions`). |
| `AutoFit`, `MaxAutoFitWidth` | 100, 60 | The records auto-fit measures, and the widest it makes a column. |
| `Validate` | `false` | Each column's validation and format from its member's type. |
| `Protect`, `Password` | `false`, none | Whether the sheet is protected, and the password that unprotects it (not on `[ExcelSheet]`). |
| `Columns` | none | Each column's `Width`, `Format`, `Wrap`, `Hidden`, `List` and `Prompt` (`ExcelColumnOptions`, or `[ExcelColumn]`). |

`ExcelToCsvOptions` has the reader's options (`Excel`) and the CSV writer's (`Csv`). `CsvToExcelOptions` has the CSV
reader's options (`Csv`), the workbook writer's (`Excel`), the sheet's name (`SheetName`) and look (`Sheet`), and
`InferTypes`. `ExcelTemplateOptions` has the workbook writer's options (`Excel`), the sheet's name (`SheetName`) and look
(`Sheet`), which a template validates and protects. `Import` takes the reader's options and `maxErrors`, 1,000 by
default.
