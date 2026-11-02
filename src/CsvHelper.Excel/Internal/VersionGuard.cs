using System;
using System.Reflection;

namespace CsvHelper.Excel;

/// <summary>
/// Checks that the loaded CsvHelper is the version this assembly was built with. CsvHelper.Excel uses CsvHelper's
/// internals, so its package depends on that exact version. NuGet only warns (NU1608) when an application's own reference
/// to CsvHelper breaks the pin, and a mismatched pair would otherwise fail later, with a
/// <see cref="MissingMethodException"/> from deep inside a read.
/// </summary>
/// <remarks>
/// The versions compared are the <see cref="AssemblyInformationalVersionAttribute"/>s without their build metadata (the
/// part from <c>+</c>, such as a commit id), which the packages of one release share. When either assembly has no such
/// attribute (trimmed away), there is nothing to compare, and the check passes.
/// </remarks>
internal static class VersionGuard
{
	// Whether a check has passed. A failed check is not remembered: it runs, and throws, again on the next call.
	private static volatile bool verified;

	/// <summary>
	/// Checks the loaded CsvHelper against this assembly, for the methods that open a workbook to call first. Only the first
	/// call that passes does the work.
	/// </summary>
	/// <exception cref="InvalidOperationException">The loaded CsvHelper is not the version this assembly was built with.</exception>
	public static void EnsureMatch()
	{
		if (verified)
		{
			return;
		}

		Check(typeof(CsvReader).Assembly, typeof(VersionGuard).Assembly);
		verified = true;
	}

	/// <summary>Checks that <paramref name="csvHelper"/> is the CsvHelper version that <paramref name="excel"/> was built with.</summary>
	/// <param name="csvHelper">The CsvHelper assembly that is loaded.</param>
	/// <param name="excel">The CsvHelper.Excel assembly.</param>
	/// <exception cref="InvalidOperationException">The versions of the two assemblies differ.</exception>
	internal static void Check(Assembly csvHelper, Assembly excel)
	{
		var expected = VersionOf(excel);
		var loaded = VersionOf(csvHelper);
		if (expected is null || loaded is null)
		{
			return;
		}

		if (!string.Equals(expected, loaded, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"CsvHelper.Excel {expected} needs CsvHelper {expected}, but CsvHelper {loaded} is loaded. CsvHelper.Excel uses " +
				$"CsvHelper's internals, so it works only with the CsvHelper version of the same release: reference CsvHelper " +
				$"{expected}, or CsvHelper.Excel {loaded}.");
		}
	}

	/// <summary>The informational version of <paramref name="assembly"/> without its build metadata.</summary>
	/// <param name="assembly">The assembly.</param>
	/// <returns>
	/// The version, such as <c>34.0.0</c> or <c>34.0.0-beta.1</c>, or <see langword="null"/> when the assembly has no
	/// informational version.
	/// </returns>
	internal static string? VersionOf(Assembly assembly)
	{
		var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		if (string.IsNullOrEmpty(version))
		{
			return null;
		}

		var plus = version!.IndexOf('+');

		return plus < 0 ? version : version.Substring(0, plus);
	}
}
