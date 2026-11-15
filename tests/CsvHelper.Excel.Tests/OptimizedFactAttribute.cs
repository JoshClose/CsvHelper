using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Excel.Tests;

/// <summary>
/// Skips a fact unless core and CsvHelper.Excel are built optimized, as a Release build and CI build them: a test of what
/// a path allocates, which unoptimized code changes (in a Debug build, core's double parser, which a date's text starts
/// with, allocates 72 bytes a call).
/// </summary>
public sealed class OptimizedFactAttribute : FactAttribute
{
	/// <summary>Creates the attribute, skipping the fact when either library is built unoptimized.</summary>
	public OptimizedFactAttribute()
	{
		if (IsUnoptimized(typeof(CsvReader).Assembly) || IsUnoptimized(typeof(ExcelReader).Assembly))
		{
			Skip = "It measures allocations, which unoptimized code changes: run it in a Release build.";
		}
	}

	/// <summary>Whether an assembly is built with the JIT's optimizations off, as a Debug build is.</summary>
	/// <param name="assembly">The assembly.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsUnoptimized(Assembly assembly)
	{
		return assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled == true;
	}
}
