using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Site;

/// <summary>Links to documentation outside the site.</summary>
internal static partial class Links
{
	/// <summary>
	/// The Microsoft Learn page of a .NET symbol, from its documentation ID: <c>T:System.Func`3</c> becomes
	/// <c>https://learn.microsoft.com/dotnet/api/system.func-3</c>, and a member links to the page of its overloads.
	/// Returns null for symbols outside the System and Microsoft namespaces.
	/// </summary>
	public static string? Learn(string id)
	{
		if (id.Length < 3 || id[1] != ':') return null;
		var name = id[2..];
		var parameters = name.IndexOf('(');
		if (parameters >= 0) name = name[..parameters];
		if (!name.StartsWith("System.", System.StringComparison.Ordinal) && !name.StartsWith("Microsoft.", System.StringComparison.Ordinal)) return null;
		name = MethodArity().Replace(name, "");
		name = TypeArity().Replace(name, "-$1");
		name = name.Replace("#ctor", "-ctor").Replace('+', '.');

		return "https://learn.microsoft.com/dotnet/api/" + name.ToLowerInvariant();
	}

	[GeneratedRegex(@"``\d+")]
	private static partial Regex MethodArity();

	[GeneratedRegex(@"`(\d+)")]
	private static partial Regex TypeArity();
}
