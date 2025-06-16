using System.Collections;

namespace CsvHelper;

/// <summary>
/// Collection of replacements used to replace text in a <see cref="ReplaceTextReader"/>.
/// </summary>
public class Replacements : IEnumerable<Replacement>
{
	private List<Replacement> replacements = new List<Replacement>();

	/// <summary>
	/// The number of replacements in the collection.
	/// </summary>
	public int Count => replacements.Count;

	/// <summary>
	/// Initializes a new instance of the <see cref="Replacements"/> class.
	/// </summary>
	public Replacements() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Replacements"/> class with the specified replacement pairs.
	/// </summary>
	public Replacements(IEnumerable<(string, string)> replacements)
	{
		this.replacements.AddRange(replacements.Select(r => new Replacement(r.Item1, r.Item2)));
	}

	/// <summary>
	/// Adds a replacement to the collection.
	/// </summary>
	/// <param name="from">The replacement from.</param>
	/// <param name="to">The replacement to.</param>
	public void Add(string from, string to)
	{
		replacements.Add(new Replacement(from, to));
	}

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public IEnumerator GetEnumerator()
	{
		return replacements.GetEnumerator();
	}

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	IEnumerator<Replacement> IEnumerable<Replacement>.GetEnumerator()
	{
		return replacements.GetEnumerator();
	}
}
