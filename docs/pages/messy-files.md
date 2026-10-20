---
title: Messy files
icon: broom
description: What the reader detects and adapts to by default, and the options for malformed input.
---

# Messy files

Real files break the rules: stray quotes, quotes that never close, records with too many or too few fields,
whitespace around values. By default the reader recovers from all of these and keeps reading, so that a file that
opens in a spreadsheet also opens here. The options on this page make it stricter, or tell you what it found.

## What the reader adapts to by default

| Situation | Behavior |
|---|---|
| Separator | Detected from the first lines (`,` `;` tab `\|`), scored by how consistently each appears per line. Excel's `sep=;` first line is honored and skipped. |
| Encoding | UTF-8 (BOM skipped). UTF-16/UTF-32 byte order marks are detected and transcoded. `Encoding` option for other code pages. |
| Line endings | `\n`, `\r\n` and lone `\r`, mixed freely. |
| Quotes | RFC 4180: a quote at the start of a field opens a quoted field (separators/line breaks inside are text, `""` is a quote). |
| Stray quotes (`5'10"`, `ab"c`) | Kept as text; they do not open a quoted region. |
| Text after a closing quote (`"ab"c`) | Appended: `abc`. |
| Unterminated quote | The field runs to the end of the input. |
| Empty lines | Skipped (`SkipEmptyLines`). |
| Ragged records | Allowed; missing fields read as empty (`MissingField`). |
| Header names | Exact match first, then case-insensitive and trimmed. Duplicates allowed (first wins; `GetFieldIndex(name, n)` finds the others). |
| Numbers | Invariant culture, fast UTF-8 paths for integers and floats; `FormatProvider` for other cultures. Booleans accept true/false, yes/no, y/n, t/f, 1/0. |

For example, this file has a stray quote in its second record and text after a closing quote in its third:

```
Name,Height
Anna,5'10"
"Ben"x,6'
Cara,"5'4"
```

```csharp
using var reader = CsvReader.FromFile("heights.csv");
while (reader.Read()) Console.WriteLine($"[{reader.GetString(0)}] [{reader.GetString(1)}]");

// [Anna] [5'10"]     the quotes are text: they are not at the start of the field
// [Benx] [6']        text after the closing quote is appended
// [Cara] [5'4]       a well-formed quoted field
```

## Options for malformed files

| Option | Values | Use it when |
|---|---|---|
| `MalformedData` | `Recover` (default), `Throw`, `Skip` | Decide what happens to records with bad quoting. `Throw` raises `CsvFormatException` at the bad record (earlier records are still readable). |
| `OnMalformedRecord` | callback | Log every malformed record (record number, reason, byte offset, raw text) whether it was recovered or skipped. |
| `AllowLineBreaksInQuotes` | `true` (default) / `false` | Set `false` for files with unbalanced quotes: every line break then ends the record, so one bad quote cannot swallow the rest of the file. |
| `FieldCountMismatch` | `Allow` (default), `Throw`, `Skip` | Enforce that records have the header's field count. |
| `MissingField` | `ReturnEmpty` (default), `Throw` | Accessing a field past the end of a short record. |
| `TrimWhitespace` | `null` (default: trim in `FixedWidth` mode only), `true`, `false` | Trim spaces/tabs around values; `  "quoted"  ` still unquotes, and escaped whitespace is kept. `TrimInsideQuotes` trims inside the quotes too, and `WhitespaceCharacters` says what is trimmed. |
| `ShouldSkipRecord` | callback | Skip records such as totals or section titles in the middle of the data. |
| `OnRecordError` | callback | Skip records of a [record type](records.html#errors) whose fields do not parse or fail their checks, and go on. |
| `MaxRecordSize` | `null` (default: no limit) | Stop a record that keeps growing, such as one after an unclosed quote, before it fills memory. |
| `TrackLineNumbers` | `false` (default) / `true` | Report the line of each record and of errors (`LineNumber`), counting the lines inside quoted fields. |
| `IncludeValuesInExceptions` | `true` (default) / `false` | Keep field values, which may be personal data, out of exception messages and logs. |
| `Mode` | `Rfc4180` (default), `Escape`, `NoEscape`, `FixedWidth` | `NoEscape` treats quotes as ordinary text (TSV exports, logs); see [Parsing modes](reading.html#parsing-modes). |
| `CommentPrefix` | `null` (default) | Skip lines starting with e.g. `#`. |
| `Separator`, `Quote`, `HasHeader`, `Encoding`, `FormatProvider` | | Override detection and defaults. |
| `PoolStrings` | `false` (default) / `true` | Set `true` when you keep many records in memory: repeated values in a column then share one string instance, which saves memory and GC work (columns of mostly unique values switch this off by themselves). For strings that are used and dropped, the default is faster. |

## Reporting malformed records

`OnMalformedRecord` is called for every malformed record the reader finds, with a `CsvMalformedRecord` that says what
is wrong and where. The reader still recovers (or skips, or throws) as `MalformedData` says:

```csharp
var options = new CsvReaderOptions
{
    OnMalformedRecord = bad => Console.WriteLine(
        $"Record {bad.RecordNumber}: {bad.Reason} at byte {bad.ByteOffset}, skipped: {bad.Skipped}: {bad.RawRecord}"),
};
using var reader = CsvReader.FromFile("heights.csv", options);
while (reader.Read()) { }

// Record 2: StrayQuote at byte 21, skipped: False: Anna,5'10"
// Record 3: TextAfterClosingQuote at byte 28, skipped: False: "Ben"x,6'
```

| `CsvMalformedRecord` | |
|---|---|
| `RecordNumber` | The 1-based number of the record, counting the header and the records returned before it (not skipped records, empty lines or comments). |
| `Reason` | What is wrong: `StrayQuote`, `TextAfterClosingQuote`, `UnterminatedQuote`, `LineBreakInQuotes`, `FieldCountMismatch` or `TrailingEscape` (Escape mode). A record with several problems is reported once, with the first. |
| `Skipped` | Whether the reader skips the record; otherwise it recovers and returns it. |
| `ByteOffset` | The offset, in bytes of UTF-8 input, of the problem (bad quoting) or of the start of the record (field count). |
| `RawRecord` | The record as it appears in the input, without its line break. |
| `LineNumber` | The 1-based line where the record starts, counting every line, with `TrackLineNumbers = true`; 0 otherwise. |

The callback runs on the thread that reads, one record at a time and in input order, also with
[`ParallelEnumerate`](reading.html#reading-on-several-threads). Setting it turns on malformed-quote detection, which
costs a little speed on heavily quoted data; without a callback and with `MalformedData = Recover`, the reader does
not look for these problems at all.

## Throwing or skipping

`MalformedData = Throw` stops at the first record with bad quoting. The records before it are returned normally, so
you can import up to the problem and report it:

```csharp
// Name,Height
// Anna,170
// "Ben"x,180
// Cara,160
var options = new CsvReaderOptions { MalformedData = CsvMalformedDataHandling.Throw };
using var reader = CsvReader.FromFile("heights.csv", options);
try
{
    while (reader.Read()) Console.WriteLine(reader.GetString(0));    // Anna
}
catch (CsvFormatException e)
{
    // Malformed CSV in record 3: text after the closing quote of a quoted field (byte offset 26).
    Console.WriteLine($"{e.Message} (record {e.RecordNumber}, byte {e.ByteOffset})");
}
```

`MalformedData = Skip` passes over records with bad quoting, and reports them to `OnMalformedRecord` with
`Skipped = true`. The same file then reads as Anna and Cara.

## Unbalanced quotes

A quoted field may contain line breaks, so a quote that is opened and never closed makes the rest of the file one
field:

```
Name,Note
Anna,"forgot to close
Ben,ok
Cara,ok
```

```csharp
using var reader = CsvReader.FromFile("notes.csv");
// One record: [Anna] [forgot to close\nBen,ok\nCara,ok\n]
```

If your files never have line breaks inside quotes, set `AllowLineBreaksInQuotes = false`: every line break then ends
the record, and the damage stays on one line. The record with the open quote is reported as `LineBreakInQuotes`:

```csharp
var options = new CsvReaderOptions { AllowLineBreaksInQuotes = false };
using var reader = CsvReader.FromFile("notes.csv", options);
// [Anna] [forgot to close]
// [Ben] [ok]
// [Cara] [ok]
```

## Field counts

Records may have any number of fields by default. `FieldCountMismatch` checks every record against the header (or,
without a header, the first record):

```csharp
// A,B
// 1,2
// 3
// 4,5,6
// 7,8
var options = new CsvReaderOptions
{
    FieldCountMismatch = CsvFieldCountHandling.Skip,
    OnMalformedRecord = bad => Console.WriteLine($"Skipped: {bad.RawRecord}"),
};
using var reader = CsvReader.FromFile("counts.csv", options);
// Records 1,2 and 7,8; "Skipped: 3" and "Skipped: 4,5,6" are logged.
```

With `CsvFieldCountHandling.Throw`, the reader throws `CsvFormatException` (`Record 3 has 1 fields but 2 were
expected.`) at the first record with a different count.

## Missing fields

Without a field count check, a short record reads as if the missing fields were empty. `MissingField = Throw` makes
reading such a field an error instead, while still allowing short records that you don't read past:

```csharp
var options = new CsvReaderOptions { MissingField = CsvMissingFieldHandling.Throw };
using var reader = CsvReader.FromText("A,B,C\n1,2\n", options);
reader.Read();
reader.GetString(1);     // "2"
reader.GetString(2);     // IndexOutOfRangeException: Record 2 has 2 fields; field 2 does not exist.
```

## Whitespace, comments and empty lines

- `TrimWhitespace = true` removes spaces and tabs around values and header names; a quote after leading whitespace
  still opens a quoted field (`  "a, b"  ` reads as `a, b`). `TrimInsideQuotes = true` also turns `" a "` into `a`, and
  `WhitespaceCharacters` trims other padding characters. See [Whitespace](reading.html#whitespace).
- `CommentPrefix = '#'` skips lines that start with `#`, such as notes before the header. See
  [Comments and empty lines](reading.html#comments-and-empty-lines).
- Empty lines are skipped; `SkipEmptyLines = false` returns each as a record with one empty field.
