using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>
/// Phase 9's per-target runner: times CsvHelper.Excel's read and write defaults and their stages on the runtime it runs
/// on (.NET 10 or .NET Framework 4.8), in interleaved rounds in one process, since the benchmark project runs on
/// .NET 10 only. Its reads are lane C's rows and inflate probes, its writes the write benchmarks' defaults, and its zip
/// stage lane E's <c>archive</c> probe.
/// </summary>
public static class Program
{
	/// <summary>The affinity mask when <c>BENCH_AFFINITY</c> isn't set: core 4, the CPU <c>BenchConfig</c> pins benchmarks to.</summary>
	private const long DefaultAffinity = 1L << 4;

	/// <summary>The environment variable of the affinity mask.</summary>
	private const string AffinityVariable = "BENCH_AFFINITY";

	/// <summary>Runs a command.</summary>
	/// <param name="args">The command and its arguments; none prints the usage.</param>
	/// <returns>0; 1 for a command line that isn't valid; 2 when the affinity can't be set.</returns>
	public static int Main(string[] args)
	{
		if (args.Length == 0 || args[0] is "help" or "-h" or "--help" or "/?")
		{
			Console.WriteLine(Usage());

			return 0;
		}

		Arguments arguments;
		try
		{
			arguments = Arguments.Parse(args);
		}
		catch (ArgumentException e)
		{
			Console.Error.WriteLine(e.Message);
			Console.Error.WriteLine();
			Console.Error.WriteLine(Usage());

			return 1;
		}

		if (!Pin(out var mask, out var error))
		{
			Console.Error.WriteLine(error);

			return 2;
		}

		Console.WriteLine($"# CsvHelper.Excel.TargetRunner {string.Join(" ", args)}");
		PrintEnvironment(mask);

		IReadOnlyList<Method> methods;
		try
		{
			methods = arguments.Command == "read"
				? new ReadMethods(arguments.Target).Create(arguments.Methods)
				: new WriteMethods(arguments.Target).Create(arguments.Methods);
		}
		catch (ArgumentException e)
		{
			Console.Error.WriteLine(e.Message);

			return 1;
		}

		Rounds.Run(methods, arguments.Rounds, arguments.Warmup);

		return 0;
	}

	/// <summary>Returns the usage text.</summary>
	/// <returns>The text, with the cache folder of this machine.</returns>
	private static string Usage()
	{
		var cache = Path.Combine(Path.GetTempPath(), "csvhelper-bench");

		return $"""
			CsvHelper.Excel.TargetRunner: times CsvHelper.Excel's read and write defaults and their stages on the runtime it
			runs on (net10.0, net8.0 or net48), in interleaved rounds in one process.

			Usage:
			  CsvHelper.Excel.TargetRunner read <workbook.xlsx> [methods] [--rounds N] [--warmup N]
			  CsvHelper.Excel.TargetRunner write <assets|orders> [methods] [--rounds N] [--warmup N]

			read methods, over a workbook of the package assets or of the orders (told apart by its header):
			  {string.Join(",", ReadMethods.Names)} (the default, in that order)
			  inflate      the first sheet's entry inflated to its end in 64 KB reads, nothing kept
			  rows         ExcelReader.Create and Read() to the end, FieldCount summed
			  records      GetRecords<PackageAsset>() or GetRecords<OrderRecord>(), to a list
			  rows-stored  rows over a copy of the workbook with every entry re-zipped at NoCompression, made in memory at start
			  and, only when named:
			  records-parallel  records through ParallelEnumerate at {ReadMethods.ParallelDegree} workers, to a list
			  rows-parallel     rows through ParallelEnumerate at {ReadMethods.ParallelDegree} workers, FieldCount summed

			write methods, of the million package assets (Data\PackageAssets.csv) or the million orders (ExcelOrders.Generate):
			  {string.Join(",", WriteMethods.Names)} (the default, in that order)
			  write        the assets through ExcelWorkbookWriter.AddSheet and WriteField, as ExcelPackageAssetsWrite does; the
			               orders through WriteSheet<OrderRecord> with DateTimeOffsets = Utc, as ExcelOrdersWrite does; defaults
			  write-stored the same at CompressionLevel.NoCompression
			  zip          the writer's sheet XML (captured at start from a stored write) through a ZipArchive entry at Fastest,
			               in 64 KB writes
			  zip-stored   the same at NoCompression
			  and, only when named:
			  write-threads   write with CompressionThreads = {WriteMethods.ParallelDegree}: chunked deflate on .NET 8 and later, one deflate thread on .NET Framework
			  write-parallel  the assets' rows through ParallelWrite, the orders through AddSheet<OrderRecord> and ParallelWriteRecords, at {WriteMethods.ParallelDegree} workers
			Every write goes to a stream that counts its bytes and drops them, and can't seek.

			Methods are separated by commas; the first is the ratios' base. Each round runs them in an order rotated by one
			from the round before, with a full GC before each call, and prints its times; at the end, per method: the median,
			the min, the median of the rounds' ratios to the first method, and the last call's result.
			  --rounds N   timed rounds (default {Arguments.DefaultRounds})
			  --warmup N   untimed rounds first (default {Arguments.DefaultWarmup})

			Environment: {AffinityVariable}, the process's affinity mask, in decimal or in hexadecimal after 0x (default 16, core 4),
			set after start; start the process with a parent affinity that includes it.

			The workbooks are the benchmarks' cache: {cache}\<name>-<key>.xlsx, such as
			packageassets-1000000-shared-<key>.xlsx and orders-1000000-<key>.xlsx, where `dotnet CsvHelper.Benchmarks.dll key`
			prints the key (`dotnet CsvHelper.Benchmarks.dll workbooks` writes those missing).
			""";
	}

	/// <summary>Sets the process's affinity from <c>BENCH_AFFINITY</c>, or to core 4.</summary>
	/// <param name="mask">The mask set, read back.</param>
	/// <param name="error">Why it couldn't be set, when it couldn't.</param>
	/// <returns>Whether it was set.</returns>
	private static bool Pin(out long mask, out string error)
	{
		var text = Environment.GetEnvironmentVariable(AffinityVariable);
		mask = DefaultAffinity;
		error = "";
		if (!string.IsNullOrWhiteSpace(text))
		{
			var value = text!.Trim();
			var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
			var parsed = hex
				? long.TryParse(value.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out mask)
				: long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out mask);
			if (!parsed || mask == 0)
			{
				error = $"{AffinityVariable}={text} isn't a mask: a nonzero number in decimal, or in hexadecimal after 0x.";

				return false;
			}
		}

#if NET
		if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
		{
			error = "A process's affinity can be set only on Windows and Linux.";

			return false;
		}
#endif

		using var process = Process.GetCurrentProcess();
		try
		{
			process.ProcessorAffinity = new IntPtr(mask);
		}
		catch (Win32Exception e)
		{
			error = $"The affinity 0x{mask:X} can't be set ({e.Message}): it must name CPUs of the parent's affinity.";

			return false;
		}

		process.Refresh();
		mask = process.ProcessorAffinity.ToInt64();

		return true;
	}

	/// <summary>Prints the runtime, the process's affinity and CPUs, its GC, and the library build it runs.</summary>
	/// <param name="mask">The affinity mask set.</param>
	private static void PrintEnvironment(long mask)
	{
		var variable = Environment.GetEnvironmentVariable(AffinityVariable);
		var source = string.IsNullOrWhiteSpace(variable) ? $"{AffinityVariable} unset: core 4" : $"{AffinityVariable}={variable}";
		var bits = RuntimeInformation.ProcessArchitecture + (Environment.Is64BitProcess ? ", 64-bit process" : ", 32-bit process");
		Console.WriteLine($"# {RuntimeInformation.FrameworkDescription} (CLR {Environment.Version}, {bits}); ProcessorCount {Environment.ProcessorCount}; affinity 0x{mask:X} ({source})");

		var gc = GCSettings.IsServerGC ? "server" : "workstation";
		Console.WriteLine($"# GC {gc}, latency {GCSettings.LatencyMode}; CsvHelper.Excel built for {BuiltFor(typeof(ExcelWorkbookWriter))}, CsvHelper for {BuiltFor(typeof(CsvWriter))}");
	}

	/// <summary>Returns the framework an assembly was built for.</summary>
	/// <param name="type">A type of the assembly.</param>
	/// <returns>Its <see cref="TargetFrameworkAttribute.FrameworkName"/>, or "unknown".</returns>
	private static string BuiltFor(Type type)
	{
		return type.Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "unknown";
	}
}

/// <summary>A command line: the command, its target, the methods and the rounds.</summary>
internal sealed class Arguments
{
	/// <summary>The timed rounds when <c>--rounds</c> isn't given.</summary>
	public const int DefaultRounds = 5;

	/// <summary>The untimed rounds when <c>--warmup</c> isn't given.</summary>
	public const int DefaultWarmup = 1;

	/// <summary>Creates a command line.</summary>
	/// <param name="command">The command: <c>read</c> or <c>write</c>.</param>
	/// <param name="target">The workbook's path, or the data written.</param>
	/// <param name="methods">The methods, in the order given.</param>
	/// <param name="rounds">The timed rounds.</param>
	/// <param name="warmup">The untimed rounds.</param>
	private Arguments(string command, string target, IReadOnlyList<string> methods, int rounds, int warmup)
	{
		Command = command;
		Target = target;
		Methods = methods;
		Rounds = rounds;
		Warmup = warmup;
	}

	/// <summary>Gets the command: <c>read</c> or <c>write</c>.</summary>
	public string Command { get; }

	/// <summary>Gets the workbook's path (<c>read</c>), or the data written (<c>write</c>).</summary>
	public string Target { get; }

	/// <summary>Gets the methods, in the order given.</summary>
	public IReadOnlyList<string> Methods { get; }

	/// <summary>Gets the number of timed rounds.</summary>
	public int Rounds { get; }

	/// <summary>Gets the number of untimed rounds before them.</summary>
	public int Warmup { get; }

	/// <summary>Parses a command line.</summary>
	/// <param name="args">The arguments.</param>
	/// <returns>The command line.</returns>
	/// <exception cref="ArgumentException">The command, a method or an option isn't valid.</exception>
	public static Arguments Parse(string[] args)
	{
		var command = args[0];
		var names = command switch
		{
			"read" => ReadMethods.Names,
			"write" => WriteMethods.Names,
			_ => throw new ArgumentException($"Unknown command '{command}'."),
		};

		var positional = new List<string>();
		var rounds = DefaultRounds;
		var warmup = DefaultWarmup;
		for (var i = 1; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--rounds":
					rounds = Count(args, ++i, "--rounds", 1);

					break;
				case "--warmup":
					warmup = Count(args, ++i, "--warmup", 0);

					break;
				default:
					positional.Add(args[i]);

					break;
			}
		}

		if (positional.Count is 0 or > 2)
		{
			throw new ArgumentException($"'{command}' takes {(command == "read" ? "a workbook's path" : "assets or orders")} and, optionally, the methods.");
		}

		var methods = positional.Count == 2 ? positional[1].Split(',').Select(m => m.Trim()).ToArray() : names;
		string[] valid = command == "read" ? [.. names, .. ReadMethods.Optional] : [.. names, .. WriteMethods.Optional];
		var unknown = methods.Where(m => !valid.Contains(m)).ToList();
		if (unknown.Count > 0)
		{
			throw new ArgumentException($"Unknown {command} method '{string.Join("', '", unknown)}': the methods are {string.Join(", ", valid)}.");
		}

		if (methods.Distinct().Count() != methods.Length)
		{
			throw new ArgumentException("A method is named twice.");
		}

		return new Arguments(command, positional[0], methods, rounds, warmup);
	}

	/// <summary>Parses an option's count.</summary>
	/// <param name="args">The arguments.</param>
	/// <param name="index">The count's index.</param>
	/// <param name="option">The option, for the message.</param>
	/// <param name="minimum">The least count.</param>
	/// <returns>The count.</returns>
	/// <exception cref="ArgumentException">The count is missing, isn't a number, or is less than <paramref name="minimum"/>.</exception>
	private static int Count(string[] args, int index, string option, int minimum)
	{
		if (index >= args.Length || !int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < minimum)
		{
			throw new ArgumentException($"{option} takes a number from {minimum}.");
		}

		return count;
	}
}
