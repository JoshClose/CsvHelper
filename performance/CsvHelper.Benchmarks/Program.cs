using BenchmarkDotNet.Running;
namespace CsvHelper.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
			var summary = BenchmarkRunner.Run<CsvWriterBenches>();
		}
    }
}
