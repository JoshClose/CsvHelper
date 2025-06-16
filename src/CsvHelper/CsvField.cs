using System.Diagnostics;

namespace CsvHelper;

[DebuggerDisplay("Start = {Start}, Length = {Length}, EscapeMask = {EscapeMask}")]
internal record struct CsvField
(
	int Start,
	int Length,
	int EscapeMask,
	char[] EscapeBuffer,
	int EscapeLength,
	bool IsInvalid
);
