using System.Diagnostics;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Shows the CPU time a benchmark's process spends per operation, on all its threads, and that time over the wall time:
/// the average number of busy cores. For benchmarks that run on several threads, where the mean is only the wall time.
/// </summary>
/// <remarks>
/// <para>
/// The host reads the benchmark process's <see cref="Process.TotalProcessorTime"/> when the process signals the start
/// and the end of its actual iterations (<see cref="HostSignal.BeforeActualRun"/>, <see cref="HostSignal.AfterActualRun"/>),
/// while the process waits for the host's answer, and times the same interval. The CPU time is divided by the actual
/// iterations' operations, and by the interval's wall time.
/// </para>
/// <para>
/// The interval holds the actual iterations and what BenchmarkDotNet does between them (a full GC before each, and the
/// iteration setups), and every thread of the process: the runtime's (tiering, the GC's) as well as the benchmark's.
/// Windows charges CPU time a clock tick (15.6 ms) at a time, so the figure is exact only over a run of many ticks, as
/// BenchmarkDotNet's actual iterations are.
/// </para>
/// </remarks>
public sealed class CpuTimeDiagnoser : IDiagnoser
{
	/// <summary>Gets the instance a configuration adds.</summary>
	public static CpuTimeDiagnoser Default { get; } = new();

	/// <summary>The readings of each benchmark, one a launch, in launch order.</summary>
	private readonly Dictionary<BenchmarkCase, List<Reading>> readings = [];

	/// <summary>The reading begun at the last <see cref="HostSignal.BeforeActualRun"/>, by benchmark.</summary>
	private readonly Dictionary<BenchmarkCase, (TimeSpan Cpu, long Timestamp)> started = [];

	/// <inheritdoc/>
	public IEnumerable<string> Ids => ["CpuTime"];

	/// <inheritdoc/>
	public IEnumerable<IExporter> Exporters => [];

	/// <inheritdoc/>
	public IEnumerable<IAnalyser> Analysers => [];

	/// <inheritdoc/>
	public RunMode GetRunMode(BenchmarkCase benchmarkCase) => RunMode.NoOverhead;

	/// <inheritdoc/>
	public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
	{
		if (signal is not (HostSignal.BeforeActualRun or HostSignal.AfterActualRun))
		{
			return;
		}

		var cpu = parameters.Process.TotalProcessorTime;
		var timestamp = Stopwatch.GetTimestamp();
		lock (readings)
		{
			if (signal == HostSignal.BeforeActualRun)
			{
				started[parameters.BenchmarkCase] = (cpu, timestamp);

				return;
			}

			if (!started.Remove(parameters.BenchmarkCase, out var start))
			{
				return;
			}

			if (!readings.TryGetValue(parameters.BenchmarkCase, out var list))
			{
				list = [];
				readings[parameters.BenchmarkCase] = list;
			}

			list.Add(new Reading(cpu - start.Cpu, Stopwatch.GetElapsedTime(start.Timestamp, timestamp)));
		}
	}

	/// <inheritdoc/>
	public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
	{
		List<Reading>? list;
		lock (readings)
		{
			readings.TryGetValue(results.BenchmarkCase, out list);
		}

		if (list is null)
		{
			return [];
		}

		// The actual iterations' operations of each launch whose reading was taken: launches count from 1, and a reading
		// is taken a launch.
		var cpu = TimeSpan.Zero;
		var wall = TimeSpan.Zero;
		var operations = 0L;
		var launches = results.Measurements
			.Where(m => m.IterationMode == IterationMode.Workload && m.IterationStage == IterationStage.Actual)
			.GroupBy(m => m.LaunchIndex);
		foreach (var launch in launches)
		{
			if (launch.Key < 1 || launch.Key > list.Count)
			{
				continue;
			}

			cpu += list[launch.Key - 1].Cpu;
			wall += list[launch.Key - 1].Wall;
			operations += launch.Sum(m => m.Operations);
		}

		if (operations == 0 || wall <= TimeSpan.Zero)
		{
			return [];
		}

		return
		[
			new Metric(CpuTimeDescriptor.Instance, cpu.TotalNanoseconds / operations),
			new Metric(BusyCoresDescriptor.Instance, cpu / wall),
		];
	}

	/// <inheritdoc/>
	public void DisplayResults(ILogger logger)
	{
	}

	/// <inheritdoc/>
	public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => [];

	/// <summary>A launch's CPU time and wall time over its actual iterations.</summary>
	/// <param name="Cpu">The process's CPU time, all threads.</param>
	/// <param name="Wall">The wall time.</param>
	private readonly record struct Reading(TimeSpan Cpu, TimeSpan Wall);

	/// <summary>The column of CPU time per operation.</summary>
	private sealed class CpuTimeDescriptor : IMetricDescriptor
	{
		/// <summary>Gets the descriptor.</summary>
		public static CpuTimeDescriptor Instance { get; } = new();

		/// <inheritdoc/>
		public string Id => "CpuTimePerOperation";

		/// <inheritdoc/>
		public string DisplayName => "CPU/op";

		/// <inheritdoc/>
		public string Legend => "Process CPU time per operation, all threads, over the actual iterations";

		/// <inheritdoc/>
		public string NumberFormat => "N2";

		/// <inheritdoc/>
		public UnitType UnitType => UnitType.Time;

		/// <inheritdoc/>
		public string Unit => "ns";

		/// <inheritdoc/>
		public bool TheGreaterTheBetter => false;

		/// <inheritdoc/>
		public int PriorityInCategory => 10;

		/// <inheritdoc/>
		public bool GetIsAvailable(Metric metric) => true;
	}

	/// <summary>The column of CPU time over wall time.</summary>
	private sealed class BusyCoresDescriptor : IMetricDescriptor
	{
		/// <summary>Gets the descriptor.</summary>
		public static BusyCoresDescriptor Instance { get; } = new();

		/// <inheritdoc/>
		public string Id => "BusyCores";

		/// <inheritdoc/>
		public string DisplayName => "CPU/wall";

		/// <inheritdoc/>
		public string Legend => "Process CPU time over wall time during the actual iterations: the average number of busy cores";

		/// <inheritdoc/>
		public string NumberFormat => "0.00";

		/// <inheritdoc/>
		public UnitType UnitType => UnitType.Dimensionless;

		/// <inheritdoc/>
		public string Unit => "cores";

		/// <inheritdoc/>
		public bool TheGreaterTheBetter => false;

		/// <inheritdoc/>
		public int PriorityInCategory => 11;

		/// <inheritdoc/>
		public bool GetIsAvailable(Metric metric) => true;
	}
}
