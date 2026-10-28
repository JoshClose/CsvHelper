using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

/// <summary>
/// A fast Stopwatch based runner for the BenchmarkDotNet classes, for quick iteration while tuning.
/// Usage: dotnet run -c Release -- quick [filter ...]  (filters match class, category or method names)
/// </summary>
public static class QuickRunner
{
	public static void Run(string[] filters)
	{
		// Pin to one performance core for steadier numbers on hybrid CPUs.
		try
		{
			var process = Process.GetCurrentProcess();
			if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) process.ProcessorAffinity = 1 << 4;
			process.PriorityClass = ProcessPriorityClass.High;
		}
		catch (Exception) { }

		var types = typeof(QuickRunner).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
			.OrderBy(t => t.Name);
		foreach (var type in types)
		{
			var methods = type.GetMethods()
				.Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null)
				.Select(m => (Method: m, Category: m.GetCustomAttribute<BenchmarkCategoryAttribute>()?.Categories.FirstOrDefault() ?? ""))
				.Where(x => filters.Length == 0 || filters.All(f =>
					type.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
					x.Category.Equals(f, StringComparison.OrdinalIgnoreCase) ||
					x.Method.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
				.ToList();
			if (methods.Count == 0) continue;

			var instance = Activator.CreateInstance(type)!;
			foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
				setup.Invoke(instance, null);
			var bytes = instance switch
			{
				ReadBenchmarkBase r => r.DataBytes,
				WriteBenchmarkBase w => w.DataBytes,
				_ => 0,
			};
			Console.WriteLine($"== {type.Name} ({bytes / 1e6:F1} MB) ==");
			foreach (var group in methods.GroupBy(x => x.Category))
			{
				double? baseline = null;
				foreach (var (method, category) in group.OrderByDescending(x => x.Method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline))
				{
					var (times, gcPause) = Measure(method, instance);
					var median = times[times.Count / 2];
					var min = times[0];
					var isBaseline = method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline;
					if (isBaseline) baseline = median;
					var ratio = baseline is double b ? $"{median / b,6:F2}x" : "";
					Console.WriteLine($"  {category,-14} {method.Name,-28} median {median,9:F3} ms  min {min,9:F3} ms  {bytes / 1e6 / (median / 1000),8:F0} MB/s  GC {gcPause,6:F2} ms  {ratio}");
				}
			}
		}
	}

	/// <summary>Runs every benchmark once and prints its result, to check that all libraries agree.</summary>
	public static void Verify(string[] filters)
	{
		var types = typeof(QuickRunner).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
			.Where(t => filters.Length == 0 || filters.Any(f => t.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
			.OrderBy(t => t.Name);
		foreach (var type in types)
		{
			var instance = Activator.CreateInstance(type)!;
			foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
				setup.Invoke(instance, null);
			Console.WriteLine($"== {type.Name} ==");
			var methods = type.GetMethods().Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null)
				.GroupBy(m => m.GetCustomAttribute<BenchmarkCategoryAttribute>()?.Categories.FirstOrDefault() ?? "");
			foreach (var group in methods)
			{
				foreach (var method in group)
				{
					var result = method.Invoke(instance, null);
					if (result is Task task)
					{
						task.GetAwaiter().GetResult();
						result = task.GetType().GetProperty("Result")?.GetValue(task);
					}

					var described = instance is WriteBenchmarkBase writer
						? $"{result} bytes, content hash {Fnv(writer.Written):X16}"
						: Describe(result);
					Console.WriteLine($"  {group.Key,-14} {method.Name,-28} {described}");
				}
			}
		}
	}

	private static string Describe(object? result) => result switch
	{
		List<PackageAsset> list => $"{list.Count} records, content hash {ContentHash(list):X16}",
		List<object> list => $"{list.Count} dynamic records, content hash {DynamicHash(list):X16}",
		double d => d.ToString("R"),
		null => "null",
		_ => result.ToString() ?? "",
	};

	/// <summary>A hash of the values of dynamic records, in column order, whichever object holds them.</summary>
	private static ulong DynamicHash(List<object> records)
	{
		var h = 14695981039346656037UL;
		foreach (var record in records)
		{
			IEnumerable<object?> values = record switch
			{
				CsvDynamicRecord dynamicRecord => Enumerable.Range(0, dynamicRecord.FieldCount).Select(i => (object?)dynamicRecord[i]),
				IDictionary<string, object?> dictionary => dictionary.Values,
				_ => throw new InvalidOperationException($"Unexpected record type {record.GetType()}."),
			};
			foreach (var value in values)
			{
				foreach (var c in value?.ToString() ?? "") h = (h ^ c) * 1099511628211UL;
				h = (h ^ 0xFFFF) * 1099511628211UL;
			}
		}

		return h;
	}

	private static ulong Fnv(ReadOnlySpan<byte> bytes)
	{
		var h = 14695981039346656037UL;
		foreach (var b in bytes) h = (h ^ b) * 1099511628211UL;

		return h;
	}

	private static ulong ContentHash(List<PackageAsset> list)
	{
		var h = 14695981039346656037UL;
		foreach (var asset in list)
		{
			for (var i = 0; i < 25; i++)
			{
				foreach (var c in asset.Get(i)) h = (h ^ c) * 1099511628211UL;
				h = (h ^ 0x1F) * 1099511628211UL;
			}
		}

		return h;
	}

	/// <summary>Returns the sorted run times (ms) and the mean GC pause time per run (ms).</summary>
	private static (List<double> Times, double GcPause) Measure(MethodInfo method, object instance)
	{
		// Warm up past tiered compilation (30 calls + background Tier-1 JIT), so we measure Tier-1 code.
		var warmup = Stopwatch.StartNew();
		for (var i = 0; i < 60 && (i < 35 || warmup.ElapsedMilliseconds < 1500); i++) Invoke(method, instance);
		Thread.Sleep(200);
		for (var i = 0; i < 3; i++) Invoke(method, instance);
		var times = new List<double>();
		var pause = TimeSpan.Zero;
		var total = Stopwatch.StartNew();
		while (times.Count < 9 || (total.ElapsedMilliseconds < 2500 && times.Count < 60))
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			var pauseBefore = GC.GetTotalPauseDuration();
			var start = Stopwatch.GetTimestamp();
			Invoke(method, instance);
			times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
			pause += GC.GetTotalPauseDuration() - pauseBefore;
		}

		var gcPause = pause.TotalMilliseconds / times.Count;
		times.Sort();

		return (times, gcPause);
	}

	private static void Invoke(MethodInfo method, object instance)
	{
		var result = method.Invoke(instance, null);
		if (result is Task task) task.GetAwaiter().GetResult();
	}
}
