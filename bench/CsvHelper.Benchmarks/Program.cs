using BenchmarkDotNet.Running;
using CsvHelper.Benchmarks;

switch (args.FirstOrDefault())
{
	case "quick":
		QuickRunner.Run(args[1..]);

		return;
	case "verify":
		QuickRunner.Verify(args[1..]);

		return;
	case "profile":
		ProfileRunner.Profile(args[1..]);

		return;
	case "summarize":
		ProfileRunner.Summarize(args[1], args.Length > 2 ? int.Parse(args[2]) : 40);

		return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchConfig.Create());
