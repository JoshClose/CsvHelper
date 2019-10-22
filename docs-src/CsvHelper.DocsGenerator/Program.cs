using System;

namespace CsvHelper.DocsGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			new Startup()
				.Configure()
				.Run();

			Console.WriteLine();
			Console.WriteLine("Press any key to exit.");
			Console.ReadKey();
		}
	}
}
