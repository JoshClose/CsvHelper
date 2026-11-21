using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace CsvHelper.Benchmarks;

/// <summary>The BenchmarkDotNet configuration every benchmark class runs under, and its job.</summary>
public static class BenchConfig
{
	/// <summary>
	/// Gets the job every benchmark runs with unless its class says otherwise: 4 warm-ups, 12 iterations, one launch,
	/// pinned to core 4, with tiering started at once.
	/// </summary>
	/// <remarks>
	/// Pin to one logical core of a performance core: on hybrid CPUs (P + E cores) results otherwise depend on where the
	/// scheduler puts the benchmark process. DOTNET_TC_CallCountingDelayMs=0 starts tiering hot methods at once. By
	/// default call counting waits until no new method has been compiled for 100 ms, and keeps waiting while there is
	/// any; benchmarks that take 50-400 ms per call then ran unoptimized Tier-0 code for seconds, well into the measured
	/// iterations (2-4x slower, any library). It only changes how soon optimized code is reached.
	/// </remarks>
	public static Job BaseJob { get; } = Job.Default
		.WithWarmupCount(4)
		.WithIterationCount(12)
		.WithLaunchCount(1)
		.WithAffinity((IntPtr)(1 << 4))
		.WithEnvironmentVariables(new EnvironmentVariable("DOTNET_TC_CallCountingDelayMs", "0"));

	/// <summary>Creates the configuration every benchmark runs under.</summary>
	/// <returns>
	/// The configuration: <see cref="BaseJob"/> as the default job, <see cref="ThroughputColumn"/>, and
	/// <see cref="AffinityValidator"/>.
	/// </returns>
	public static IConfig Create()
	{
		var job = BaseJob;

		// No JoinSummary: classes reuse category names ("Rows", "Write"), and a joined summary compares each
		// category with a single baseline from whichever class comes first.
		return DefaultConfig.Instance
			.AddJob(job.AsDefault())
			.AddColumn(new ThroughputColumn())
			.AddValidator(AffinityValidator.Default)
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

/// <summary>
/// Runs a class of benchmarks that use several threads on CCD0 (logical processors 0 to 15) of the benchmark machine,
/// with workstation GC, and shows lock contentions, work items and process CPU time per operation.
/// </summary>
/// <remarks>
/// <para>
/// The affinity is a mutator, as the default job's core 4 would otherwise win over a job of the class: the class runs
/// <see cref="BenchConfig.BaseJob"/> on CCD0. The CPU time is read by <see cref="CpuTimeDiagnoser"/>.
/// </para>
/// <para>
/// BenchmarkDotNet sets a job's affinity right after it starts the benchmark process, in practice before the runtime
/// counts its processors: a CCD0 process has <see cref="Environment.ProcessorCount"/> 16, a core-4 one 1 (measured
/// 2026-11-20). It first masks the affinity with the host's own count, so the host must see all 16 of CCD0's processors
/// (<see cref="AffinityValidator"/> refuses a run otherwise): hosts start with affinity 0xFFFF or none.
/// </para>
/// </remarks>
public sealed class Ccd0Config : ManualConfig
{
	/// <summary>The mask of CCD0's logical processors.</summary>
	public const long Mask = 0xFFFF;

	/// <summary>Initializes the configuration.</summary>
	public Ccd0Config()
	{
		AddJob(Job.Default.WithAffinity((IntPtr)Mask).WithId("Ccd0").AsMutator());
		AddDiagnoser(BenchmarkDotNet.Diagnosers.ThreadingDiagnoser.Default);
		AddDiagnoser(CpuTimeDiagnoser.Default);
	}
}

/// <summary>
/// Runs a class of benchmarks that use several threads on CCD0, as <see cref="Ccd0Config"/> does, once with workstation
/// GC and once with server GC.
/// </summary>
/// <remarks>
/// The two jobs replace the default job, so a class runs exactly these two, each <see cref="BenchConfig.BaseJob"/> with the
/// mutator's affinity.
/// </remarks>
public sealed class Ccd0GcConfig : ManualConfig
{
	/// <summary>Initializes the configuration.</summary>
	public Ccd0GcConfig()
	{
		AddJob(BenchConfig.BaseJob.WithGcServer(false).WithId("Workstation"));
		AddJob(BenchConfig.BaseJob.WithGcServer(true).WithId("Server"));
		AddJob(Job.Default.WithAffinity((IntPtr)Ccd0Config.Mask).WithId("Ccd0").AsMutator());
		AddDiagnoser(BenchmarkDotNet.Diagnosers.ThreadingDiagnoser.Default);
		AddDiagnoser(CpuTimeDiagnoser.Default);
	}
}

/// <summary>
/// Refuses a run in which a job's affinity names a logical processor the host doesn't count. BenchmarkDotNet masks a job's
/// affinity with <c>(1 &lt;&lt; Environment.ProcessorCount) - 1</c> of the host before it applies it: a host started on 8
/// processors ran <see cref="Ccd0Config"/>'s 0xFFFF on 0xFF while the summary said 0xFFFF, and turned a mask of 0xFF0000
/// into 0, which Windows refuses ("The parameter is incorrect"), so that each process kept the host's processors.
/// </summary>
public sealed class AffinityValidator : IValidator
{
	/// <summary>Gets the instance a configuration adds.</summary>
	public static AffinityValidator Default { get; } = new();

	/// <inheritdoc/>
	public bool TreatsWarningsAsErrors => true;

	/// <inheritdoc/>
	public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
	{
		var counted = Environment.ProcessorCount >= 64 ? -1L : (1L << Environment.ProcessorCount) - 1;
		var jobs = validationParameters.Benchmarks
			.Where(b => b.Job.Environment.HasValue(EnvironmentMode.AffinityCharacteristic))
			.DistinctBy(b => b.Job.DisplayInfo);
		foreach (var benchmark in jobs)
		{
			var mask = (long)benchmark.Job.Environment.Affinity;
			if ((mask & ~counted) != 0)
			{
				yield return new ValidationError(
					true,
					$"Job {benchmark.Job.DisplayInfo} asks for affinity 0x{mask:X}, but this host counts {Environment.ProcessorCount} processors, so BenchmarkDotNet would apply 0x{mask & counted:X}. Start the host with affinity 0xFFFF or none.",
					benchmark);
			}
		}
	}
}

/// <summary>Shows input megabytes processed per second, based on the mean time.</summary>
public sealed class ThroughputColumn : IColumn
{
	/// <inheritdoc/>
	public string Id => nameof(ThroughputColumn);

	/// <inheritdoc/>
	public string ColumnName => "MB/s";

	/// <inheritdoc/>
	public bool AlwaysShow => true;

	/// <inheritdoc/>
	public ColumnCategory Category => ColumnCategory.Custom;

	/// <inheritdoc/>
	public int PriorityInCategory => 0;

	/// <inheritdoc/>
	public bool IsNumeric => true;

	/// <inheritdoc/>
	public UnitType UnitType => UnitType.Dimensionless;

	/// <inheritdoc/>
	public string Legend => "Megabytes of CSV processed per second (mean)";

	/// <inheritdoc/>
	public bool IsAvailable(Summary summary) => true;

	/// <inheritdoc/>
	public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
	{
		var report = summary[benchmarkCase];
		if (report?.ResultStatistics is null)
		{
			return "-";
		}

		var bytes = DataSize.Of(benchmarkCase);
		if (bytes == 0)
		{
			return "-";
		}

		var seconds = report.ResultStatistics.Mean / 1e9;

		return (bytes / 1e6 / seconds).ToString("N0");
	}
}

/// <summary>Computes (and caches) the size of the CSV data used by a benchmark class.</summary>
public static class DataSize
{
	/// <summary>Each class's size, once computed, by class and parameter values.</summary>
	private static readonly Dictionary<(Type Type, string Parameters), long> Cache = [];

	/// <summary>
	/// Returns the bytes of CSV a benchmark processes, from an instance of its class set up in this process the first time,
	/// with the benchmark's parameter values.
	/// </summary>
	/// <param name="benchmarkCase">The benchmark.</param>
	/// <returns>The bytes, or 0 for a class that isn't a read or write benchmark, which is never set up here.</returns>
	public static long Of(BenchmarkCase benchmarkCase)
	{
		var type = benchmarkCase.Descriptor.Type;
		if (!typeof(ReadBenchmarkBase).IsAssignableFrom(type) && !typeof(WriteBenchmarkBase).IsAssignableFrom(type) && !typeof(ExcelWriteBenchmarkBase).IsAssignableFrom(type))
		{
			return 0;
		}

		var key = (type, benchmarkCase.Parameters.ValueInfo);
		lock (Cache)
		{
			if (Cache.TryGetValue(key, out var bytes))
			{
				return bytes;
			}

			bytes = BenchCases.SetUp(benchmarkCase, null) switch
			{
				ReadBenchmarkBase r => r.DataBytes,
				WriteBenchmarkBase w => w.DataBytes,
				ExcelWriteBenchmarkBase e => e.DataBytes,
				_ => 0,
			};

			Cache[key] = bytes;

			return bytes;
		}
	}
}

/// <summary>Makes instances of benchmark classes in the host process, for the columns that measure what a benchmark does.</summary>
public static class BenchCases
{
	/// <summary>
	/// Creates an instance of a benchmark's class with the benchmark's parameter values, and runs its global setups: those
	/// without targets, and those that target the method, when one is given.
	/// </summary>
	/// <param name="benchmarkCase">The benchmark.</param>
	/// <param name="method">The method whose targeted setups run too, or null for the untargeted setups only.</param>
	/// <returns>The set-up instance.</returns>
	public static object SetUp(BenchmarkCase benchmarkCase, string? method)
	{
		var type = benchmarkCase.Descriptor.Type;
		var instance = Activator.CreateInstance(type)!;
		foreach (var parameter in benchmarkCase.Parameters.Items)
		{
			if (type.GetProperty(parameter.Name) is { } property)
			{
				property.SetValue(instance, parameter.Value);
			}
			else
			{
				type.GetField(parameter.Name)?.SetValue(instance, parameter.Value);
			}
		}

		foreach (var setup in type.GetMethods())
		{
			var attribute = (BenchmarkDotNet.Attributes.GlobalSetupAttribute?)Attribute.GetCustomAttribute(setup, typeof(BenchmarkDotNet.Attributes.GlobalSetupAttribute), true);
			if (attribute is null)
			{
				continue;
			}

			var targets = attribute.Targets ?? [];
			if (targets.Length > 0 && (method is null || !targets.Contains(method)))
			{
				continue;
			}

			setup.Invoke(instance, null);
		}

		return instance;
	}
}

/// <summary>Adds <see cref="WrittenSizeColumn"/> to a class of Excel write benchmarks.</summary>
public sealed class WrittenSizeConfig : ManualConfig
{
	/// <summary>Initializes a new instance of the <see cref="WrittenSizeConfig"/> class, with the column.</summary>
	public WrittenSizeConfig()
	{
		AddColumn(new WrittenSizeColumn());
	}
}

/// <summary>
/// Shows the size of what an Excel write benchmark writes: a workbook's, or the CSV's. To measure it, each benchmark runs
/// once more in the host process, on an instance of its class set up once.
/// </summary>
public sealed class WrittenSizeColumn : IColumn
{
	/// <summary>The set-up instance of each class, by class, parameter values and the method whose targeted setups ran.</summary>
	private static readonly Dictionary<(Type Type, string Parameters, string? Method), ExcelWriteBenchmarkBase> Instances = [];

	/// <summary>Each benchmark's size, by class, parameter values and method name.</summary>
	private static readonly Dictionary<(Type Type, string Parameters, string Method), long> Sizes = [];

	/// <inheritdoc/>
	public string Id => nameof(WrittenSizeColumn);

	/// <inheritdoc/>
	public string ColumnName => "Written";

	/// <inheritdoc/>
	public bool AlwaysShow => true;

	/// <inheritdoc/>
	public ColumnCategory Category => ColumnCategory.Custom;

	/// <inheritdoc/>
	public int PriorityInCategory => 1;

	/// <inheritdoc/>
	public bool IsNumeric => false;

	/// <inheritdoc/>
	public UnitType UnitType => UnitType.Dimensionless;

	/// <inheritdoc/>
	public string Legend => "Megabytes the benchmark writes (a workbook's size, or the CSV's)";

	/// <inheritdoc/>
	public bool IsAvailable(Summary summary) => true;

	/// <inheritdoc/>
	public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

	/// <inheritdoc/>
	public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
	{
		var type = benchmarkCase.Descriptor.Type;
		if (!typeof(ExcelWriteBenchmarkBase).IsAssignableFrom(type))
		{
			return "-";
		}

		return $"{Of(benchmarkCase) / 1e6:F1} MB";
	}

	/// <summary>
	/// Runs a benchmark once in this process, the first time, on an instance of its class with its parameter values, and
	/// returns the bytes it wrote.
	/// </summary>
	/// <param name="benchmarkCase">The benchmark, whose method returns the bytes it wrote.</param>
	/// <returns>The bytes.</returns>
	private static long Of(BenchmarkCase benchmarkCase)
	{
		var type = benchmarkCase.Descriptor.Type;
		var method = benchmarkCase.Descriptor.WorkloadMethod;
		var parameters = benchmarkCase.Parameters.ValueInfo;
		lock (Sizes)
		{
			if (Sizes.TryGetValue((type, parameters, method.Name), out var size))
			{
				return size;
			}

			// Instances are shared between a class's methods but those with targeted setups, which get one of their own.
			var targeted = type.GetMethods().Any(m => Attribute.GetCustomAttribute(m, typeof(BenchmarkDotNet.Attributes.GlobalSetupAttribute), true) is BenchmarkDotNet.Attributes.GlobalSetupAttribute { Targets.Length: > 0 } a && a.Targets.Contains(method.Name));
			var key = (type, parameters, targeted ? method.Name : null);
			if (!Instances.TryGetValue(key, out var instance))
			{
				instance = (ExcelWriteBenchmarkBase)BenchCases.SetUp(benchmarkCase, method.Name);
				Instances[key] = instance;
			}

			var result = method.Invoke(instance, null);
			size = result is Task<long> task ? task.GetAwaiter().GetResult() : (long)result!;
			Sizes[(type, parameters, method.Name)] = size;

			return size;
		}
	}
}
