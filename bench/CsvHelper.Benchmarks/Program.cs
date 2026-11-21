using System.Globalization;
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
	case "interleave":
		InterleaveRunner.Run(args[1..]);

		return;
	case "summarize":
		ProfileRunner.Summarize(args[1], args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 40, args.Length > 3 ? args[3] : null);

		return;
	case "key":
		Console.WriteLine(BenchWorkbooks.Key);

		return;
	case "workbooks":
		BenchWorkbooks.Ensure();
		Console.WriteLine($"{ExcelDataGenerator.CacheFolder} {BenchWorkbooks.Key}");

		return;
	case "workbooks-clean":
		Console.WriteLine($"{BenchWorkbooks.Clean()} files deleted");

		return;
}

BenchWorkbooks.Ensure();
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchConfig.Create());
