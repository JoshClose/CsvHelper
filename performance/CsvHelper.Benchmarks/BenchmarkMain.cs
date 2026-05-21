using BenchmarkDotNet.Running;

namespace CsvHelper.Benchmarks;

internal class BenchmarkMain
{
	static void Main(string[] args)
	{
		_ = BenchmarkSwitcher.FromAssembly(typeof(BenchmarkMain).Assembly).Run(args);
	}
}
