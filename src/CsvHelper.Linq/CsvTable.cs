using System.Diagnostics.CodeAnalysis;

namespace CsvHelper.Linq;

/// <summary>
/// The records of a CSV file, to query (as a <see cref="CsvQuery{T}"/>) and to change: records can be added to the
/// file, and records that match a condition removed or changed, which rewrites the file.
/// </summary>
/// <typeparam name="T">The type of the records: a <see cref="CsvRecordAttribute"/> type, or <see cref="CsvDynamicRecord"/>.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
public sealed partial class CsvTable<T> : CsvQuery<T>
{
	/// <summary>Creates the table of a file.</summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">How the file is read and written.</param>
	internal CsvTable(string path, CsvQueryOptions options)
		: base(new CsvSource.File(path), options)
	{
		Path = path;
		QueryOptions = options;
	}

	/// <summary>The path of the file.</summary>
	public string Path { get; }

	/// <summary>How the file is read and written.</summary>
	public CsvQueryOptions QueryOptions { get; }
}
