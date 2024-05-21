using System.Collections;

namespace CsvHelper;

public class Replacements : IEnumerable<Replacement>
{
	private List<Replacement> replacements = new List<Replacement>();

	public int Count => replacements.Count;

	public void Add(string from, string to)
	{
		replacements.Add(new Replacement(from, to));
	}

	public IEnumerator GetEnumerator()
	{
		return replacements.GetEnumerator();
	}

	IEnumerator<Replacement> IEnumerable<Replacement>.GetEnumerator()
	{
		return replacements.GetEnumerator();
	}
}
