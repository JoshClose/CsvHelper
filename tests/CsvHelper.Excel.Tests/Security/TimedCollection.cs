namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The tests whose time limits measure their own inputs, which run alone: after the tests that run in parallel, and beside
/// none of them. Beside them, on CI's four cores, an input that reads in a tenth of a second waited seven seconds for a
/// thread of the pool, then ran seven more through the other tests' garbage collections, and failed a limit of five. The
/// tests that measure the process's memory (<see cref="LinkMemoryTests"/>) run here too, so that it is their own.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimedCollection
{
	/// <summary>The collection's name, for <see cref="CollectionAttribute"/>.</summary>
	public const string Name = "Timed";
}
