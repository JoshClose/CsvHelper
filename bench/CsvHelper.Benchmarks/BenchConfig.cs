using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace CsvHelper.Benchmarks;

public static class BenchConfig
{
	public static IConfig Create()
	{
		// Pin to one logical core of a performance core: on hybrid CPUs (P + E cores) results otherwise
		// depend on where the scheduler puts the benchmark process.
		// DOTNET_TC_CallCountingDelayMs=0 starts tiering hot methods at once. By default call counting waits
		// until no new method has been compiled for 100 ms, and keeps waiting while there is any; benchmarks
		// that take 50-400 ms per call then ran unoptimized Tier-0 code for seconds, well into the measured
		// iterations (2-4x slower, any library). It only changes how soon optimized code is reached.
		var job = Job.Default
			.WithWarmupCount(4)
			.WithIterationCount(12)
			.WithLaunchCount(1)
			.WithAffinity((IntPtr)(1 << 4))
			.WithEnvironmentVariables(new EnvironmentVariable("DOTNET_TC_CallCountingDelayMs", "0"));

		// No JoinSummary: classes reuse category names ("Rows", "Write"), and a joined summary compares each
		// category with a single baseline from whichever class comes first.
		return DefaultConfig.Instance
			.AddJob(job.AsDefault())
			.AddColumn(new ThroughputColumn())
			.WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
	}
}

/// <summary>
/// Runs the job of <see cref="BenchConfig"/> on every core, for benchmarks that run on several threads. A mutator, as the
/// default job's affinity would otherwise win over the affinity of a job of the class.
/// </summary>
public sealed class AllCoresConfig : ManualConfig
{
	public AllCoresConfig()
	{
		var allCores = Environment.ProcessorCount >= 64 ? -1L : (1L << Environment.ProcessorCount) - 1;
		AddJob(Job.Default.WithAffinity((IntPtr)allCores).WithId("AllCores").AsMutator());
	}
}

/// <summary>Shows input megabytes processed per second, based on the mean time.</summary>
public sealed class ThroughputColumn : IColumn
{
	public string Id => nameof(ThroughputColumn);
	public string ColumnName => "MB/s";
	public bool AlwaysShow => true;
	public ColumnCategory Category => ColumnCategory.Custom;
	public int PriorityInCategory => 0;
	public bool IsNumeric => true;
	public UnitType UnitType => UnitType.Dimensionless;
	public string Legend => "Megabytes of CSV processed per second (mean)";
	public bool IsAvailable(Summary summary) => true;
	public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

	public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

	public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
	{
		var report = summary[benchmarkCase];
		if (report?.ResultStatistics is null) return "-";
		var bytes = DataSize.Of(benchmarkCase.Descriptor.Type);
		if (bytes == 0) return "-";
		var seconds = report.ResultStatistics.Mean / 1e9;

		return (bytes / 1e6 / seconds).ToString("N0");
	}
}

/// <summary>Computes (and caches) the size of the CSV data used by a benchmark class.</summary>
public static class DataSize
{
	private static readonly Dictionary<Type, long> Cache = [];

	public static long Of(Type type)
	{
		lock (Cache)
		{
			if (Cache.TryGetValue(type, out var bytes)) return bytes;
			var instance = Activator.CreateInstance(type)!;
			foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(BenchmarkDotNet.Attributes.GlobalSetupAttribute), true).Length > 0))
				setup.Invoke(instance, null);
			bytes = instance switch
			{
				ReadBenchmarkBase r => r.DataBytes,
				WriteBenchmarkBase w => w.DataBytes,
				_ => 0,
			};
			Cache[type] = bytes;

			return bytes;
		}
	}
}
