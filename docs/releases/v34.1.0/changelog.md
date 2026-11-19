**Behavior change:** a type mapped by several `[CsvRecordMap]` classes now throws. In 34.0 it was read and written with
one of them, chosen by the order of the source files or of the loading of the assemblies, and could get its options from
another. Now `GetRecords<T>()`, `WriteRecords<T>()`, `CsvReaderOptions.For<T>()` and the other methods that would use a
map throw an `InvalidOperationException` that names the maps. A type with one map works as before. See
[Types you cannot change](../../pages/records.md#types-you-cannot-change-csvrecordmap).

The new `SkipLines` reader option skips lines at the start of the input, such as a title or a report date above the
header. The lines are not parsed, and the separator is detected from the lines after them. In 33, `ShouldSkipRecord`
could skip such lines; in 34 it gets data records only. See
[Lines above the header](../../pages/reading.md#lines-above-the-header).

The new `SeparatorCandidates` reader option lists the characters that separator detection chooses from, `,;\t|` by
default, so files separated by spaces or another character can be detected, as `DetectDelimiterValues` allowed in 33.
When none of them appears, the first is used, a comma by default as before. See
[Separator detection](../../pages/reading.md#separator-detection).

The new `newLine` parameter of `CsvSeparatorStream` reads files whose records end with other characters than a line
break, such as `~@^*^@~`, as the reader's `NewLine` did in 33. The stream puts a line break in place of each record end
and drops a line break right after it. A line break elsewhere throws `CsvFormatException`, unless it is text: in a
quoted field, or after an escape character. See
[Records that end with other characters](../../pages/reading.md#records-that-end-with-other-characters).

The new `Quote` writer option changes the quote character of RFC 4180 mode, as the reader's `Quote` does, so files
that quote with apostrophes can be written as well as read. 34.0 always quoted with `"`. CsvHelper.Linq's `Update` now
changes files quoted with another character than `"` instead of throwing `NotSupportedException`. See
[Quoting](../../pages/writing.md#quoting).

Listing the column names of a `CsvDynamicRecord`, as its dictionary `Keys` or as the member names that a `dynamic`
binder asks for, no longer reads each value as a string.

An `override` of a property now has the attributes of the property it overrides, as in 33, so an override that adds
`[CsvDefault]` keeps the base property's `[CsvColumn]` name. In 34.0 only the override's own attributes applied, and
its column was looked up by the property name. An override that declares only a `get` is now read too, with the setter
it inherits. See [Overridden properties](../../pages/records.md#overridden-properties). A `[CsvRecord]` type derived
from another one no longer gets CS0108 warnings for the generated methods that hide the base type's.

**Behavior change:** an override's column is written where the property it overrides is declared. In 34.0 it moved
after the base class's columns, among the derived class's own, so a class that overrode `Age` of a base class with
`Name`, `Age` and `City` was written as `Name,City,Age`. This applies to `[CsvRecord]` types and to the other types
that CsvHelper.Linq's `WriteCsv` writes, where an override of a `[CsvIgnore]` property is now left out too.

When a constructor parameter's type can't be read, CSVH002 no longer suggests `[CsvIgnore]`, which doesn't apply to
parameters. It says which constructor was chosen and suggests marking another one `[CsvConstructor]` or giving the
parameter a `[CsvConverter]`. See
[A constructor for types made from other objects](../../pages/records.md#a-constructor-for-types-made-from-other-objects).

`ParseExact<T>` now parses `TimeSpan`, and on .NET 8 and later `DateOnly` and `TimeOnly`, as well as `DateTime` and
`DateTimeOffset`. `[CsvFormat]` members of these types are read with it, so they no longer create a string for each
field, and with `IncludeValuesInExceptions` off, a `DateOnly` or `TimeOnly` field that doesn't have the format no longer
shows its value in the exception. See [Parse and TryParse](../../pages/reading.md#parse-and-tryparse) and
[Exact formats](../../pages/records.md#exact-formats-csvformat).

CsvHelper.Linq's `AppendRecords` now writes records as the table reads its file: with the mode, quote and escape
character, column widths and comment prefix of its `ReaderOptions`, as `Update` does. In 34.0 they came from the writer
options, RFC 4180 with `"` by default, so the table read some of them back differently: `Bob, B` was appended to a file
quoted with `'` as `"Bob, B"`, which reads as two fields. Appending to a fixed-width file no longer throws.
`AppendCsv` reads the headers it compares with the mode, quote character and comment prefix of its writer options; in
34.0, with another quote than `"` and `CsvQuoting.Always`, a file whose header was not quoted failed the check. See
[Changing files](../../pages/linq.md#changing-files).

CsvHelper.Linq's `Update` no longer turns a record into a comment. A changed value in the file's first column that
starts with the `CommentPrefix` of the table's `ReaderOptions` is now quoted, or escaped in Escape mode. In 34.0 it was
written as it was, so the table skipped the record from then on. The `CommentPrefix` of the writer options, which could
quote a value in another column, no longer applies to `Update`. See
[Changing files](../../pages/linq.md#changing-files).
