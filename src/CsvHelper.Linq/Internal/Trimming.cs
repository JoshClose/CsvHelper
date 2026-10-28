namespace CsvHelper.Linq;

/// <summary>
/// The messages of the <c>[RequiresUnreferencedCode]</c> and <c>[RequiresDynamicCode]</c> attributes of the query
/// provider: trimming and native AOT applications get them as warnings where they call it.
/// </summary>
internal static class Trimming
{
	/// <summary>Why CSV queries need code that trimming may remove.</summary>
	public const string Unreferenced =
		"CSV queries run the LINQ operators that the CSV reader does not run itself with LINQ to Objects, which finds them with " +
		"reflection, so trimming can remove code they need.";

	/// <summary>Why CSV queries generate code at run time.</summary>
	public const string Dynamic =
		"CSV queries compile their lambdas and create generic types and methods at run time, which native AOT cannot do ahead of time.";
}
