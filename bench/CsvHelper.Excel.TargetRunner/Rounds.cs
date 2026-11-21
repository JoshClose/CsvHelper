using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>A timed method: its name, what its result counts, and the work.</summary>
internal sealed class Method
{
	/// <summary>Creates a method.</summary>
	/// <param name="name">The method's name, as the command line names it.</param>
	/// <param name="unit">What the result counts, such as "bytes".</param>
	/// <param name="run">The work, which returns its result.</param>
	public Method(string name, string unit, Func<long> run)
	{
		Name = name;
		Unit = unit;
		Run = run;
	}

	/// <summary>Gets the method's name.</summary>
	public string Name { get; }

	/// <summary>Gets what the result counts.</summary>
	public string Unit { get; }

	/// <summary>Gets the work, which returns its result: a count that also keeps the work from being optimized away.</summary>
	public Func<long> Run { get; }
}

/// <summary>
/// Interleaved rounds, as lane C's probes and lane G's <c>interleave</c> ran them: every method once a round, in an order
/// rotated by one each round, with a full GC before each call; the times of the timed rounds, per method.
/// </summary>
internal static class Rounds
{
	/// <summary>Runs the rounds and prints each round's times, then each method's median, min and ratio.</summary>
	/// <param name="methods">The methods; the first is the ratios' base.</param>
	/// <param name="rounds">The timed rounds.</param>
	/// <param name="warmup">The untimed rounds before them.</param>
	public static void Run(IReadOnlyList<Method> methods, int rounds, int warmup)
	{
		var count = methods.Count;
		var width = Math.Max(12, methods.Max(m => m.Name.Length) + 1);
		var times = new double[count][];
		for (var i = 0; i < count; i++)
		{
			times[i] = new double[rounds];
		}

		var results = new long[count];
		for (var round = 0; round < warmup + rounds; round++)
		{
			var elapsed = new double[count];
			var order = new string[count];
			for (var k = 0; k < count; k++)
			{
				var i = (round + k) % count;
				order[k] = methods[i].Name;

				// As BenchmarkDotNet does before each iteration.
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				var watch = Stopwatch.StartNew();
				results[i] = methods[i].Run();
				elapsed[i] = watch.Elapsed.TotalMilliseconds;
			}

			var label = round < warmup ? $"warm-up {round + 1}/{warmup}" : $"round {round - warmup + 1}/{rounds}";
			var cells = string.Join("  ", Enumerable.Range(0, count).Select(i => FormattableString.Invariant($"{methods[i].Name} {elapsed[i]:F1}")));
			Console.WriteLine($"{label,-14} {cells}   (ran {string.Join(", ", order)}) {DateTime.Now:HH:mm:ss}");
			if (round >= warmup)
			{
				for (var i = 0; i < count; i++)
				{
					times[i][round - warmup] = elapsed[i];
				}
			}
		}

		Console.WriteLine();
		Console.WriteLine($"{"method".PadRight(width)} {"median ms",10} {"min ms",10} {"ratio",7}   result");
		for (var i = 0; i < count; i++)
		{
			var ratios = Enumerable.Range(0, rounds).Select(r => times[i][r] / times[0][r]).ToArray();
			Console.WriteLine(FormattableString.Invariant(
				$"{methods[i].Name.PadRight(width)} {Median(times[i]),10:F1} {times[i].Min(),10:F1} {Median(ratios),7:F3}   {results[i]:N0} {methods[i].Unit}"));
		}
	}

	/// <summary>Returns the median of values: the middle one, or the mean of the middle two.</summary>
	/// <param name="values">The values, at least one.</param>
	/// <returns>The median.</returns>
	private static double Median(double[] values)
	{
		var sorted = values.OrderBy(v => v).ToArray();
		var middle = sorted.Length / 2;

		return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
	}
}
