using System.Diagnostics;

namespace CsvHelper;

[DebuggerDisplay("From = {new string(From)}, To = {new string(To)}")]
public readonly record struct Replacement
{
	public char[] From { get; }

	public char[] To { get; }

	public Replacement(string from, string to)
	{
		From = from.ToCharArray();
		To = to.ToCharArray();
	}
}
