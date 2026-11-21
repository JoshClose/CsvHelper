using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Times several benchmark methods of one class in rounds, in one process: each round calls every method once, in an order
/// rotated each round, so that each method's ratio to the first is taken round by round and drift and load move both
/// sides alike.
/// Usage: interleave &lt;Class&gt; &lt;rounds&gt; &lt;Method,Method,...&gt; [Name=Value ...]
/// </summary>
/// <remarks>
/// The process is pinned by <see cref="BenchAffinity"/> (<c>BENCH_AFFINITY</c>, or logical processor 4) at high priority.
/// Each method is called three times to warm up (a call of a second or more reaches tier-1 code within it), then the
/// rounds run after a pause of 200 ms for the background compiler. A full GC runs before each call, outside its time.
/// Each call's allocation (<see cref="GC.GetTotalAllocatedBytes(bool)"/>, every thread) and the process's CPU time
/// (<see cref="Process.TotalProcessorTime"/>, every thread) are read around it, outside its time too, and printed as
/// means: Windows charges CPU time a clock tick (15.6 ms) at a time, so for calls shorter than a few ticks (about 50 ms)
/// the CPU time reads low, often 0, and only longer calls' is near the truth.
/// </remarks>
public static class InterleaveRunner
{
	/// <summary>Runs the methods in rounds, and prints each round's times and then each method's summary.</summary>
	/// <param name="args">
	/// The class, the number of rounds, the methods (comma-separated, the first the one the others are compared with), and
	/// any <c>Name=Value</c> arguments, which set the class's properties (<see cref="ProfileRunner.SetParameters"/>).
	/// </param>
	/// <exception cref="ArgumentException">
	/// An argument is missing, the rounds are fewer than one, or the class, a method or a property isn't found.
	/// </exception>
	/// <exception cref="FormatException">The rounds or a value isn't a number.</exception>
	public static void Run(string[] args)
	{
		if (args.Length < 3)
		{
			throw new ArgumentException("Usage: interleave <Class> <rounds> <Method,Method,...> [Name=Value ...]", nameof(args));
		}

		var type = ProfileRunner.FindType(args[0]);
		var rounds = int.Parse(args[1], CultureInfo.InvariantCulture);
		if (rounds < 1)
		{
			throw new ArgumentException($"{rounds} rounds: at least one is needed.", nameof(args));
		}

		var methods = args[2].Split(',').Select(name => type.GetMethod(name) ?? throw new ArgumentException($"{type.Name} has no method {name}.", nameof(args))).ToArray();

		BenchAffinity.Apply(highPriority: true);
		BenchWorkbooks.Ensure();

		var instance = Activator.CreateInstance(type)!;
		ProfileRunner.SetParameters(instance, args.Skip(3));
		foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
		{
			setup.Invoke(instance, null);
		}

		foreach (var method in methods)
		{
			for (var i = 0; i < 3; i++)
			{
				Invoke(method, instance);
			}
		}

		Thread.Sleep(200);

		var process = Process.GetCurrentProcess();
		var times = methods.Select(_ => new List<double>()).ToArray();
		var cpu = new double[methods.Length];
		var allocated = new long[methods.Length];
		for (var round = 0; round < rounds; round++)
		{
			for (var k = 0; k < methods.Length; k++)
			{
				var m = (k + round) % methods.Length;
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				// Reading the CPU time allocates, so the allocation is read inside those reads, around the call alone.
				var cpuBefore = process.TotalProcessorTime;
				var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
				var start = Stopwatch.GetTimestamp();
				Invoke(methods[m], instance);
				var elapsed = Stopwatch.GetElapsedTime(start);
				allocated[m] += GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
				cpu[m] += (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
				times[m].Add(elapsed.TotalMilliseconds);
			}

			Console.WriteLine($"round {round + 1,2} {DateTime.Now:HH:mm:ss}: " + string.Join("  ", methods.Select((x, i) => $"{x.Name} {times[i][^1]:F1}")));
		}

		Console.WriteLine($"== {type.Name} {string.Join(" ", args.Skip(3))}, {rounds} rounds ==");
		for (var i = 0; i < methods.Length; i++)
		{
			var sorted = times[i].OrderBy(t => t).ToList();
			var ratios = times[i].Zip(times[0], (a, b) => a / b).OrderBy(r => r).ToList();
			var differences = times[i].Zip(times[0], (a, b) => a - b).OrderBy(d => d).ToList();
			var cores = cpu[i] / times[i].Sum();
			Console.WriteLine($"  {methods[i].Name,-22} median {Median(sorted),9:F1} ms  min {sorted[0],9:F1} ms  ratio median {Median(ratios),6:F3} (q1 {Quartile(ratios, 1),6:F3}, q3 {Quartile(ratios, 3),6:F3})  difference median {Median(differences),8:F1} ms  cpu mean {cpu[i] / rounds,9:F1} ms  cores {cores,5:F2}  alloc {allocated[i] / rounds,14:N0} B");
		}
	}

	/// <summary>Returns the median of sorted values.</summary>
	/// <param name="sorted">The values, sorted.</param>
	/// <returns>The median.</returns>
	private static double Median(List<double> sorted) => sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;

	/// <summary>Returns a quartile of sorted values, the nearest rank's.</summary>
	/// <param name="sorted">The values, sorted.</param>
	/// <param name="quartile">1 or 3.</param>
	/// <returns>The quartile.</returns>
	private static double Quartile(List<double> sorted, int quartile) => sorted[Math.Min(sorted.Count - 1, (int)Math.Round(quartile * (sorted.Count - 1) / 4.0))];

	/// <summary>Calls a benchmark method, and waits for its task.</summary>
	/// <param name="method">The method.</param>
	/// <param name="instance">Its instance.</param>
	private static void Invoke(MethodInfo method, object instance)
	{
		var result = method.Invoke(instance, null);
		if (result is Task task)
		{
			task.GetAwaiter().GetResult();
		}
	}
}
