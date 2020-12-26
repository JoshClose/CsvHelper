using BenchmarkDotNet.Running;
using System;

namespace CsvHelper.Benchmarks
{
	class Program
	{
		static void Main(string[] args)
		{
			BenchmarkRunner.Run<ObjectCreatorBenchmarks>();
		}
	}
}
