using BenchmarkDotNet.Running;

namespace CsvHelper.Benchmarks;

internal class BenchmarkMain
{
    static void Main(string[] args)
    {
		_ = BenchmarkSwitcher.FromAssembly(System.Reflection.Assembly.GetExecutingAssembly()).Run(args);
    }
}
