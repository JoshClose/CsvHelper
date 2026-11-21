using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace CsvHelper.Benchmarks;

/// <summary>
/// A fast Stopwatch based runner for the BenchmarkDotNet classes, for quick iteration while tuning.
/// Usage: dotnet run -c Release -- quick [filter ...]  (filters match class, category or method names; one that starts
/// with ~ is a regular expression over Class.Method; one of the form Name=Value keeps the instances whose parameter Name
/// has that value). The process is pinned by <see cref="BenchAffinity"/> (<c>BENCH_AFFINITY</c>, or logical processor 4).
/// </summary>
public static class QuickRunner
{
	/// <summary>
	/// Times the benchmarks that match every filter, on each instance of their class (<see cref="Instances"/>), and prints
	/// each one's median, minimum and 90th percentile times, the number of calls timed, its throughput, and its GC pause,
	/// process CPU time and allocation per call, with its ratio to its category's baseline.
	/// </summary>
	/// <param name="filters">
	/// The filters of benchmarks (<see cref="Matches"/>), and <c>Name=Value</c> filters, which keep a class's instances whose
	/// parameter <c>Name</c> has one of the values named for it; a class without that parameter keeps all its instances.
	/// </param>
	/// <remarks>
	/// The CPU time is the process's (<see cref="Process.TotalProcessorTime"/>) and the allocation every thread's
	/// (<see cref="GC.GetTotalAllocatedBytes(bool)"/>), both read around each timed call and averaged, so that benchmarks
	/// that run on several threads count their workers. Windows charges CPU time a clock tick (15.6 ms) at a time: for calls
	/// shorter than a few ticks (about 50 ms) the CPU time reads low, often 0, and only longer calls' is near the truth.
	/// </remarks>
	public static void Run(string[] filters)
	{
		BenchAffinity.Apply(highPriority: true);
		BenchWorkbooks.Ensure();

		var (benchmarkFilters, assignments) = Split(filters);
		var types = typeof(QuickRunner).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
			.OrderBy(t => t.Name);
		foreach (var type in types)
		{
			var methods = type.GetMethods()
				.Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null)
				.Select(m => (Method: m, Category: m.GetCustomAttribute<BenchmarkCategoryAttribute>()?.Categories.FirstOrDefault() ?? ""))
				.Where(x => benchmarkFilters.Length == 0 || benchmarkFilters.All(f => Matches(f, type, x.Category, x.Method)))
				.ToList();
			if (methods.Count == 0)
			{
				continue;
			}

			foreach (var (instance, name) in Instances(type, assignments))
			{
				var bytes = instance switch
				{
					ReadBenchmarkBase r => r.DataBytes,
					WriteBenchmarkBase w => w.DataBytes,
					ExcelWriteBenchmarkBase e => e.DataBytes,
					_ => 0,
				};

				Console.WriteLine($"== {name} ({bytes / 1e6:F1} MB) ==");
				foreach (var group in methods.GroupBy(x => x.Category))
				{
					double? baseline = null;
					foreach (var (method, category) in group.OrderByDescending(x => x.Method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline))
					{
						var (times, gcPause, cpuTime, allocated) = Measure(method, instance);
						var median = times[times.Count / 2];
						var min = times[0];
						var p90 = times[(int)Math.Ceiling(times.Count * 0.9) - 1];
						var isBaseline = method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline;
						if (isBaseline)
						{
							baseline = median;
						}

						var ratio = baseline is double b ? $"{median / b,6:F2}x" : "";
						Console.WriteLine($"  {category,-14} {method.Name,-28} median {median,9:F3} ms  min {min,9:F3} ms  p90 {p90,9:F3} ms  n {times.Count,3}  {bytes / 1e6 / (median / 1000),8:F0} MB/s  GC {gcPause,6:F2} ms  cpu {cpuTime,9:F3} ms  alloc {allocated,14:N0} B  {ratio}");
					}
				}
			}
		}
	}

	/// <summary>
	/// Runs every benchmark once and prints its result, to check that all libraries agree: a category whose results differ
	/// is flagged. An Excel write benchmark's result is what it wrote, read back as records, and its size.
	/// </summary>
	/// <param name="filters">
	/// Parts of the classes' names (a class runs when its name contains one, or always when there is none), and
	/// <c>Name=Value</c> filters, which keep the instances as <see cref="Run"/>'s do.
	/// </param>
	public static void Verify(string[] filters)
	{
		BenchWorkbooks.Ensure();

		var (classFilters, assignments) = Split(filters);
		var types = typeof(QuickRunner).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
			.Where(t => classFilters.Length == 0 || classFilters.Any(f => t.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
			.OrderBy(t => t.Name);
		foreach (var type in types)
		{
			foreach (var (instance, name) in Instances(type, assignments))
			{
				if (instance is ExcelWriteBenchmarkBase excelWriter)
				{
					excelWriter.KeepWritten = true;
				}

				Console.WriteLine($"== {name} ==");
				var methods = type.GetMethods().Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null)
					.GroupBy(m => m.GetCustomAttribute<BenchmarkCategoryAttribute>()?.Categories.FirstOrDefault() ?? "");
				foreach (var group in methods)
				{
					var results = new HashSet<string>();
					foreach (var method in group)
					{
						var result = method.Invoke(instance, null);
						if (result is Task task)
						{
							task.GetAwaiter().GetResult();
							result = task.GetType().GetProperty("Result")?.GetValue(task);
						}

						var described = instance switch
						{
							WriteBenchmarkBase writer => $"{result} bytes, content hash {Fnv(writer.Written):X16}",
							ExcelWriteBenchmarkBase excel => Describe(excel.ReadBack()),
							_ => Describe(result),
						};

						var size = instance is ExcelWriteBenchmarkBase ? $" ({result} bytes)" : "";
						Console.WriteLine($"  {group.Key,-14} {method.Name,-28} {described}{size}");
						results.Add(described);
					}

					if (results.Count > 1)
					{
						Console.WriteLine($"  {group.Key,-14} !! the results differ");
					}
				}
			}
		}
	}

	/// <summary>
	/// Whether a benchmark matches a filter: its class's or its method's name contains the filter, or its category is the
	/// filter, ignoring case; or, for a filter that starts with <c>~</c>, the rest of it, a regular expression, matches its
	/// <c>Class.Method</c>, so that a run can name methods exactly.
	/// </summary>
	/// <param name="filter">The filter.</param>
	/// <param name="type">The benchmark's class.</param>
	/// <param name="category">The benchmark's category.</param>
	/// <param name="method">The benchmark's method.</param>
	/// <returns>Whether it matches.</returns>
	private static bool Matches(string filter, Type type, string category, MethodInfo method)
	{
		if (filter.StartsWith('~'))
		{
			return Regex.IsMatch($"{type.Name}.{method.Name}", filter[1..]);
		}

		return type.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			category.Equals(filter, StringComparison.OrdinalIgnoreCase) ||
			method.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Splits a runner's filters into those of benchmarks and the <c>Name=Value</c> filters of parameters: those that hold
	/// <c>=</c> and don't start with <c>~</c> (a regular expression's).
	/// </summary>
	/// <param name="filters">The filters.</param>
	/// <returns>The benchmarks' filters, and the parameters'.</returns>
	private static (string[] Benchmarks, string[] Assignments) Split(string[] filters)
	{
		var assignments = filters.Where(f => f.Contains('=') && !f.StartsWith('~')).ToArray();

		return (filters.Where(f => !assignments.Contains(f)).ToArray(), assignments);
	}

	/// <summary>
	/// Returns the instances to run a class's benchmarks on, set up: one for each combination of the values of its
	/// <see cref="ParamsAttribute"/> properties that the <c>Name=Value</c> filters keep, named with them, as BenchmarkDotNet
	/// runs it.
	/// </summary>
	/// <param name="type">The benchmark class.</param>
	/// <param name="assignments">The <c>Name=Value</c> filters.</param>
	/// <returns>Each instance, with the class's name and its parameters' values.</returns>
	private static IEnumerable<(object Instance, string Name)> Instances(Type type, string[] assignments)
	{
		var parameters = type.GetProperties()
			.Select(p => (Property: p, Values: p.GetCustomAttribute<ParamsAttribute>()?.Values))
			.Where(p => p.Values is not null)
			.Select(p => (p.Property, Values: Keep(p.Property, p.Values!, assignments)))
			.ToList();

		IEnumerable<List<(PropertyInfo Property, object? Value)>> combinations = [[]];
		foreach (var (property, values) in parameters)
		{
			combinations = combinations.SelectMany(c => values.Select(v => new List<(PropertyInfo, object?)>(c) { (property, v) })).ToList();
		}

		foreach (var combination in combinations)
		{
			var instance = Activator.CreateInstance(type)!;
			foreach (var (property, value) in combination)
			{
				property.SetValue(instance, value);
			}

			foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<GlobalSetupAttribute>() is not null))
			{
				setup.Invoke(instance, null);
			}

			var name = combination.Count == 0 ? type.Name : $"{type.Name} ({string.Join(", ", combination.Select(c => $"{c.Property.Name}={c.Value}"))})";

			yield return (instance, name);
		}
	}

	/// <summary>Returns the values of a parameter that the <c>Name=Value</c> filters keep.</summary>
	/// <param name="property">The parameter.</param>
	/// <param name="values">Its values.</param>
	/// <param name="assignments">The filters.</param>
	/// <returns>The values named for it (ignoring case), or all of them when none is.</returns>
	private static object?[] Keep(PropertyInfo property, object?[] values, string[] assignments)
	{
		var named = assignments
			.Where(a => a.StartsWith(property.Name + "=", StringComparison.Ordinal))
			.Select(a => a[(property.Name.Length + 1)..])
			.ToList();
		if (named.Count == 0)
		{
			return values;
		}

		return values.Where(v => named.Contains(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "", StringComparer.OrdinalIgnoreCase)).ToArray();
	}

	/// <summary>Describes a benchmark's result: its records' count and a hash of their content, or the value itself.</summary>
	/// <param name="result">The result.</param>
	/// <returns>The description.</returns>
	private static string Describe(object? result) => result switch
	{
		List<PackageAsset> list => $"{list.Count} records, content hash {ContentHash(list):X16}",
		List<OrderRecord> list => $"{list.Count} orders, content hash {OrderHash(list):X16}",
		CsvHelper.Excel.ExcelImportResult<OrderRecord> import => Describe(import.Records, import.Errors.Select(e => e.Row).Distinct().Count()),
		SkippedOrders skipped => Describe(skipped.Records, skipped.Skipped),
		List<object> list => $"{list.Count} dynamic records, content hash {DynamicHash(list):X16}",
		double d => d.ToString("R"),
		null => "null",
		_ => result.ToString() ?? "",
	};

	/// <summary>
	/// Describes orders read with records at fault left out, as the import's and <c>GetRecords</c>' skipping give them, so
	/// that the two agree when they read the same orders and leave out the same rows.
	/// </summary>
	/// <param name="records">The orders that read.</param>
	/// <param name="rowsAtFault">The rows left out.</param>
	/// <returns>The description.</returns>
	private static string Describe(IReadOnlyList<OrderRecord> records, int rowsAtFault) =>
		$"{records.Count} orders, content hash {OrderHash(records):X16}{(rowsAtFault > 0 ? $", {rowsAtFault} rows at fault" : "")}";

	/// <summary>A hash of the values of dynamic records, in column order, whichever object holds them.</summary>
	/// <param name="records">The records.</param>
	/// <returns>The hash.</returns>
	/// <exception cref="InvalidOperationException">A record is of a type it doesn't know.</exception>
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
				foreach (var c in value?.ToString() ?? "")
				{
					h = (h ^ c) * 1099511628211UL;
				}

				h = (h ^ 0xFFFF) * 1099511628211UL;
			}
		}

		return h;
	}

	/// <summary>An FNV-1a hash of bytes.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The hash.</returns>
	private static ulong Fnv(ReadOnlySpan<byte> bytes)
	{
		var h = 14695981039346656037UL;
		foreach (var b in bytes)
		{
			h = (h ^ b) * 1099511628211UL;
		}

		return h;
	}

	/// <summary>A hash of every field of every package asset, in column order.</summary>
	/// <param name="list">The package assets.</param>
	/// <returns>The hash.</returns>
	private static ulong ContentHash(List<PackageAsset> list)
	{
		var h = 14695981039346656037UL;
		foreach (var asset in list)
		{
			for (var i = 0; i < 25; i++)
			{
				foreach (var c in asset.Get(i))
				{
					h = (h ^ c) * 1099511628211UL;
				}

				h = (h ^ 0x1F) * 1099511628211UL;
			}
		}

		return h;
	}

	/// <summary>
	/// A hash of every member of every order: the numbers' exact values (the total's, whatever its scale), the dates'
	/// ticks (with the offset's), and the strings' characters.
	/// </summary>
	/// <param name="list">The orders.</param>
	/// <returns>The hash.</returns>
	private static ulong OrderHash(IReadOnlyList<OrderRecord> list)
	{
		var h = 14695981039346656037UL;
		foreach (var order in list)
		{
			var values = new object[]
			{
				order.Id, order.CustomerId, order.Customer, order.OrderDate.Ticks, order.ShippedAt.Ticks, order.ShippedAt.Offset.Ticks,
				decimal.ToOACurrency(order.Total), BitConverter.DoubleToInt64Bits(order.Weight), order.Quantity, order.Paid,
				order.Status, order.Sku, order.Notes,
			};

			foreach (var value in values)
			{
				foreach (var c in Convert.ToString(value, CultureInfo.InvariantCulture)!)
				{
					h = (h ^ c) * 1099511628211UL;
				}

				h = (h ^ 0x1F) * 1099511628211UL;
			}
		}

		return h;
	}

	/// <summary>
	/// Times a benchmark: warm-up calls, then at least 9 timed calls (up to 60 within 2.5 s), each after a full GC.
	/// </summary>
	/// <param name="method">The benchmark.</param>
	/// <param name="instance">Its class's instance, set up.</param>
	/// <returns>
	/// The sorted times (ms); and per call, the mean GC pause (ms), the mean process CPU time (ms, every thread) and the
	/// mean bytes allocated (every thread).
	/// </returns>
	private static (List<double> Times, double GcPause, double CpuTime, long Allocated) Measure(MethodInfo method, object instance)
	{
		// Warm up past tiered compilation (30 calls + background Tier-1 JIT), so we measure Tier-1 code. A call of a second
		// or more (the Excel reads) runs its loops long enough to reach Tier-1 code within the call, so three calls do.
		var warmup = Stopwatch.StartNew();
		for (var i = 0; i < 60 && (i < 35 || warmup.ElapsedMilliseconds < 1500) && (i < 3 || warmup.ElapsedMilliseconds < i * 1000L); i++)
		{
			Invoke(method, instance);
		}

		Thread.Sleep(200);
		for (var i = 0; i < 3; i++)
		{
			Invoke(method, instance);
		}

		var process = Process.GetCurrentProcess();
		var times = new List<double>();
		var pause = TimeSpan.Zero;
		var cpu = TimeSpan.Zero;
		var allocated = 0L;
		var total = Stopwatch.StartNew();
		while (times.Count < 9 || (total.ElapsedMilliseconds < 2500 && times.Count < 60))
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			// Reading the CPU time allocates, so the allocation is read inside those reads, around the call alone.
			var cpuBefore = process.TotalProcessorTime;
			var pauseBefore = GC.GetTotalPauseDuration();
			var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
			var start = Stopwatch.GetTimestamp();
			Invoke(method, instance);
			var elapsed = Stopwatch.GetElapsedTime(start);
			allocated += GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
			pause += GC.GetTotalPauseDuration() - pauseBefore;
			cpu += process.TotalProcessorTime - cpuBefore;
			times.Add(elapsed.TotalMilliseconds);
		}

		var count = times.Count;
		times.Sort();

		return (times, pause.TotalMilliseconds / count, cpu.TotalMilliseconds / count, allocated / count);
	}

	/// <summary>Calls a benchmark, and waits for its task.</summary>
	/// <param name="method">The benchmark.</param>
	/// <param name="instance">Its class's instance.</param>
	private static void Invoke(MethodInfo method, object instance)
	{
		var result = method.Invoke(instance, null);
		if (result is Task task)
		{
			task.GetAwaiter().GetResult();
		}
	}
}
