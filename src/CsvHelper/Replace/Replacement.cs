using System.Diagnostics;

namespace CsvHelper;

/// <summary>
/// Replacement values.
/// </summary>
[DebuggerDisplay("From = {new string(From)}, To = {new string(To)}")]
public readonly record struct Replacement
{
	/// <summary>
	/// The characters to replace.
	/// </summary>
	public char[] From { get; }

	/// <summary>
	/// The characters to replace with.
	/// </summary>
	public char[] To { get; }

	/// <summary>
	/// Initializes a new instance using the specified from and to strings.
	/// </summary>
	/// <param name="from">The text to replace.</param>
	/// <param name="to">The text to replace with.</param>
	public Replacement(string from, string to)
	{
		From = from.ToCharArray();
		To = to.ToCharArray();
	}
}
