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

The new `Quote` writer option changes the quote character of RFC 4180 mode, as the reader's `Quote` does, so files
that quote with apostrophes can be written as well as read. 34.0 always quoted with `"`. CsvHelper.Linq's `Update` now
changes files quoted with another character than `"` instead of throwing `NotSupportedException`. See
[Quoting](../../pages/writing.md#quoting).

Listing the column names of a `CsvDynamicRecord`, as its dictionary `Keys` or as the member names that a `dynamic`
binder asks for, no longer reads each value as a string.
