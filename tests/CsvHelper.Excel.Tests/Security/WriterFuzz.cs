using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// What the writer's fuzzers share: their settings from the environment, text made of code units (so that no character
/// the tests need depends on how their source is edited), text shown for a failure, and the failures' report.
/// </summary>
internal static class WriterFuzz
{
	/// <summary>The most failures a fuzzer collects before it stops.</summary>
	public const int MaxFailures = 5;

	/// <summary>A setting from the environment, or its default.</summary>
	/// <param name="name">The variable's name, such as <c>CSVHELPER_EXCEL_FUZZ_SEED</c>.</param>
	/// <param name="fallback">The default.</param>
	/// <returns>The setting.</returns>
	public static int Setting(string name, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

	/// <summary>Text of UTF-16 code units, lone surrogates too.</summary>
	/// <param name="units">The code units.</param>
	/// <returns>The text.</returns>
	public static string Units(params int[] units) => new(units.Select(u => (char)u).ToArray());

	/// <summary>Text for a failure: ASCII as it is, and every other code unit as <c>\uXXXX</c>, cut after 120 code units.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text shown.</returns>
	public static string Show(string text)
	{
		var shown = new StringBuilder("\"");
		foreach (var c in text.Length > 120 ? text.Substring(0, 120) : text)
		{
			shown.Append(c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:X4}");
		}

		shown.Append(text.Length > 120 ? $"\"... ({text.Length.ToString("N0", CultureInfo.InvariantCulture)} code units)" : "\"");

		return shown.ToString();
	}

	/// <summary>Bytes for a failure, in hexadecimal, cut after 60.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The bytes shown.</returns>
	public static string Show(byte[] bytes) =>
		string.Join(" ", bytes.Take(60).Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + (bytes.Length > 60 ? $" ... ({bytes.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes)" : "");

	/// <summary>An exception for a failure: its type, its message and the first lines of its stack.</summary>
	/// <param name="e">The exception.</param>
	/// <returns>The description.</returns>
	public static string Describe(Exception e) =>
		$"{e.GetType().FullName}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(10))}";

	/// <summary>Adds one to a key's count.</summary>
	/// <param name="counts">The counts.</param>
	/// <param name="key">The key.</param>
	public static void Count(SortedDictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;

	/// <summary>Fails with the failures, and how to replay one, when there are any.</summary>
	/// <param name="failures">The failures.</param>
	/// <param name="count">The variable that sets the number of inputs.</param>
	public static void AssertNone(List<string> failures, string count) =>
		Assert.True(failures.Count == 0, string.Join("\n\n", failures) + $"\n\nReplay one with CSVHELPER_EXCEL_FUZZ_SEED=<seed> and {count}=1.");
}
