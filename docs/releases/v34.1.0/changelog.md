**Behavior change:** a type mapped by several `[CsvRecordMap]` classes now throws. In 34.0 it was read and written with
one of them, chosen by the order of the source files or of the loading of the assemblies, and could get its options from
another. Now `GetRecords<T>()`, `WriteRecords<T>()`, `CsvReaderOptions.For<T>()` and the other methods that would use a
map throw an `InvalidOperationException` that names the maps. A type with one map works as before. See
[Types you cannot change](../../pages/records.md#types-you-cannot-change-csvrecordmap).

Listing the column names of a `CsvDynamicRecord`, as its dictionary `Keys` or as the member names that a `dynamic`
binder asks for, no longer reads each value as a string.
