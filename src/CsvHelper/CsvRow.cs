using System.Diagnostics;

namespace CsvHelper;

[DebuggerDisplay("Start = {Start}, Length = {Length}, FieldCount = {FieldCount}")]
internal record struct CsvRow
(
	int Start,
	int Length,
	int FieldCount
);
