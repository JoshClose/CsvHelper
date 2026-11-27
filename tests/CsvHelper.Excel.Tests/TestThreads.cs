using System.Globalization;
using System.Runtime.CompilerServices;

namespace CsvHelper.Excel.Tests;

/// <summary>
/// The suite mode that runs the writer suites chunked: <c>CSVHELPER_EXCEL_TEST_THREADS</c> set to a count above 1 raises
/// every workbook writer's compression threads to it (<see cref="ExcelWorkbookWriter.TestThreads"/>) as the tests load,
/// so that every sheet, lists sheet and shared strings part is deflated in chunks on that many threads. The tests that pin
/// a workbook's bytes to those of parts deflated on the writing thread can't hold there on .NET 8 and later, whose chunked
/// parts have other bytes, and skip (<see cref="SerialBytesFactAttribute"/>, <see cref="SerialBytesTheoryAttribute"/>);
/// on .NET Framework the chunked parts' bytes are the serial ones, and they run.
/// </summary>
internal static class TestThreads
{
	/// <summary>The environment variable that sets the mode: the threads, above 1; unset, or 1 or less, for none.</summary>
	public const string Variable = "CSVHELPER_EXCEL_TEST_THREADS";

	/// <summary>The threads the variable asks for, or 0 when it is unset or not a number.</summary>
	public static int Count { get; } = int.TryParse(Environment.GetEnvironmentVariable(Variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;

	/// <summary>Whether the mode is on, so that workbook writers chunk their streamed parts.</summary>
	public static bool Chunked => Count > 1;

	/// <summary>
	/// Whether the mode changes a workbook's bytes from those of its parts deflated on the writing thread: on .NET 8 and
	/// later, where its chunked parts are cut and deflated chunk by chunk (at zlib's level 2 on .NET 10).
	/// </summary>
	public static bool ChangesBytes
	{
		get
		{
#if NET
			return Chunked;
#else
			return false;
#endif
		}
	}

	/// <summary>Why a test that pins a workbook's serial bytes skips in the mode.</summary>
	public static string SkipReason => $"It pins a workbook's bytes to parts deflated on the writing thread, which {Variable}={Count} deflates in chunks, with other bytes on .NET 8 and later.";

	/// <summary>Why a test that measures what a write allocates on the writing thread skips in the mode.</summary>
	public static string AllocationSkipReason => $"It measures what a write allocates on the writing thread, which {Variable}={Count} changes with its chunks, deflated there or on the stage's threads.";

	/// <summary>Sets <see cref="ExcelWorkbookWriter.TestThreads"/> from the variable, as the test assembly loads.</summary>
#pragma warning disable CA2255 // The test assembly sets its writers' threads once, before any test runs.
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Initialize()
	{
		ExcelWorkbookWriter.TestThreads = Count;
	}
}

/// <summary>
/// A fact that pins a workbook's bytes to those of parts deflated on the writing thread: skipped in the chunked suite mode
/// where that changes them (<see cref="TestThreads.ChangesBytes"/>).
/// </summary>
public sealed class SerialBytesFactAttribute : FactAttribute
{
	/// <summary>Creates the attribute, skipping the fact in the chunked suite mode on .NET 8 and later.</summary>
	public SerialBytesFactAttribute()
	{
		if (TestThreads.ChangesBytes)
		{
			Skip = TestThreads.SkipReason;
		}
	}
}

/// <summary>
/// A fact that measures what a write allocates on the writing thread, in an optimized build (as
/// <see cref="OptimizedFactAttribute"/>): skipped in the chunked suite mode too (<see cref="TestThreads.Chunked"/>), where
/// the parts' chunks change it.
/// </summary>
public sealed class SerialAllocationFactAttribute : FactAttribute
{
	/// <summary>Creates the attribute, skipping the fact in an unoptimized build and in the chunked suite mode.</summary>
	public SerialAllocationFactAttribute()
	{
		Skip = new OptimizedFactAttribute().Skip ?? (TestThreads.Chunked ? TestThreads.AllocationSkipReason : null);
	}
}

/// <summary>
/// A theory that pins a workbook's bytes to those of parts deflated on the writing thread: skipped in the chunked suite
/// mode where that changes them (<see cref="TestThreads.ChangesBytes"/>).
/// </summary>
public sealed class SerialBytesTheoryAttribute : TheoryAttribute
{
	/// <summary>Creates the attribute, skipping the theory in the chunked suite mode on .NET 8 and later.</summary>
	public SerialBytesTheoryAttribute()
	{
		if (TestThreads.ChangesBytes)
		{
			Skip = TestThreads.SkipReason;
		}
	}
}
