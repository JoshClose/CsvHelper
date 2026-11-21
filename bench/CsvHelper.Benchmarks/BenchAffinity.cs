using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Pins the runners that time benchmarks in their own process (<c>quick</c>, <c>profile</c> and <c>interleave</c>) to the
/// logical processors of the mask in the environment variable <c>BENCH_AFFINITY</c>, or to logical processor 4 when it
/// isn't set, as <see cref="BenchConfig.BaseJob"/> pins BenchmarkDotNet's processes.
/// </summary>
/// <remarks>
/// The mask is applied from inside the process, after its runtime counted the processors it started with (its parent's):
/// <see cref="Environment.ProcessorCount"/>, and what is sized from it (server GC's heaps, the thread pool, spinning), stays
/// the parent's, as the line printed shows. A BenchmarkDotNet process differs: its job's affinity lands before its runtime
/// counts, so on core 4 it has a count of 1. A runner started with the mask already applied by its parent (the Phase 9
/// window's recipe) counts as BenchmarkDotNet's process does.
/// </remarks>
public static class BenchAffinity
{
	/// <summary>The environment variable that holds the mask: decimal (<c>65535</c>) or hexadecimal (<c>0xFFFF</c>).</summary>
	public const string Variable = "BENCH_AFFINITY";

	/// <summary>The mask used when <see cref="Variable"/> isn't set: logical processor 4.</summary>
	public const long DefaultMask = 1 << 4;

	/// <summary>Returns the mask to pin to: <see cref="Variable"/>'s, or <see cref="DefaultMask"/> when it isn't set.</summary>
	/// <returns>The mask.</returns>
	/// <exception cref="FormatException">The variable is set to something other than a positive decimal or 0x hexadecimal number.</exception>
	public static long Mask()
	{
		var text = Environment.GetEnvironmentVariable(Variable)?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return DefaultMask;
		}

		var parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			? long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var mask)
			: long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out mask);
		if (!parsed || mask == 0)
		{
			throw new FormatException($"{Variable} is '{text}': expected a mask of logical processors, decimal (65535) or hexadecimal (0xFFFF).");
		}

		return mask;
	}

	/// <summary>
	/// Pins the current process to <see cref="Mask"/>'s processors, optionally raises its priority, and prints the mask
	/// and <see cref="Environment.ProcessorCount"/>. A failure to pin is printed, not thrown: the times' spread shows it.
	/// </summary>
	/// <param name="highPriority">Whether to raise the process's priority class to <see cref="ProcessPriorityClass.High"/>.</param>
	/// <exception cref="FormatException">The variable holds no valid mask.</exception>
	public static void Apply(bool highPriority)
	{
		var mask = Mask();
		var source = Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } ? Variable : $"{Variable} not set";
		if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
		{
			Console.WriteLine($"Affinity not set: unsupported on this platform; ProcessorCount {Environment.ProcessorCount}");

			return;
		}

		var process = Process.GetCurrentProcess();
		try
		{
			process.ProcessorAffinity = (IntPtr)mask;
			Console.WriteLine($"Affinity 0x{(long)process.ProcessorAffinity:X} ({source}), ProcessorCount {Environment.ProcessorCount}");
		}
		catch (Win32Exception exception)
		{
			Console.WriteLine($"Affinity 0x{mask:X} ({source}) not set: {exception.Message}; ProcessorCount {Environment.ProcessorCount}");
		}

		if (highPriority)
		{
			try
			{
				process.PriorityClass = ProcessPriorityClass.High;
			}
			catch (Win32Exception exception)
			{
				Console.WriteLine($"Priority not raised: {exception.Message}");
			}
		}
	}
}
