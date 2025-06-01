using BenchmarkDotNet.Running;

namespace CsvHelper.Benchmarks;

internal class BenchmarkMain
{
	static void Main(string[] args)
	{
		_ = BenchmarkRunner.Run<BenchmarkEnumerateRecords>();
	}
}
