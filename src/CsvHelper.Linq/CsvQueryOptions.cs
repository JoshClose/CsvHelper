using System;

namespace CsvHelper.Linq;

/// <summary>How a CSV query reads its source, and how much of the work it pushes into the reader and to other providers.</summary>
public sealed record CsvQueryOptions
{
	private readonly int maxDegreeOfParallelism = 1;
	private readonly int foreignKeyBatchSize = 2000;
	private readonly int maxForeignKeys = 100_000;

	/// <summary>The defaults: the record type's own options, column pruning, one thread.</summary>
	public static CsvQueryOptions Default { get; } = new();

	/// <summary>
	/// The options of the reader, or <see langword="null"/> (the default) for those of the record type's
	/// <see cref="CsvOptionsAttribute"/> (<see cref="CsvReaderOptions.For{T}"/>), which are the defaults when it has none.
	/// </summary>
	public CsvReaderOptions? ReaderOptions { get; init; }

	/// <summary>
	/// The options of the writer that adds, changes and removes records of a <see cref="CsvTable{T}"/>, or
	/// <see langword="null"/> (the default) for those of the record type's <see cref="CsvOptionsAttribute"/>
	/// (<see cref="CsvWriterOptions.For{T}"/>), with the separator of the file.
	/// </summary>
	public CsvWriterOptions? WriterOptions { get; init; }

	/// <summary>
	/// Whether a query reads only the members of the records that it uses (<see langword="true"/>, the default), so
	/// that the other columns are neither looked up nor parsed: their values are never checked, they may be missing from
	/// the header, and a record that fails only in them is not reported to <see cref="CsvReaderOptions.OnRecordError"/>.
	/// <see langword="false"/> reads every member of every record that the query reads, as
	/// <see cref="CsvReader.GetRecords{T}"/> does.
	/// </summary>
	public bool PruneColumns { get; init; } = true;

	/// <summary>
	/// The number of threads that read and filter records, with <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>:
	/// 1 (the default) for the calling thread only, -1 for one per processor. The records come out in the order of the
	/// file either way. Only the filters (<c>Where</c>) that the reader runs itself and the mapping of the records run in
	/// parallel, so their lambdas must be safe to call from several threads.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is 0 or less than -1.</exception>
	public int MaxDegreeOfParallelism
	{
		get => maxDegreeOfParallelism;
		init => maxDegreeOfParallelism = value is -1 or > 0
			? value
			: throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism), value, "The degree of parallelism must be -1 or positive.");
	}

	/// <summary>
	/// How many keys a query sends to another provider at once when it joins with one of its queries, such as a table
	/// of Entity Framework Core: it asks for the rows whose keys the CSV has (<c>Where(row =&gt; keys.Contains(row.Key))</c>)
	/// in batches of this many keys, instead of reading the whole table. 2000 by default, which stays below the parameter
	/// limits of databases.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
	public int ForeignKeyBatchSize
	{
		get => foreignKeyBatchSize;
		init => foreignKeyBatchSize = value > 0
			? value
			: throw new ArgumentOutOfRangeException(nameof(ForeignKeyBatchSize), value, "The batch size must be positive.");
	}

	/// <summary>
	/// The most distinct keys that a join with another provider's query sends to it (100,000 by default); with more, the
	/// query reads the other provider's whole query instead, once. 0 never sends keys.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
	public int MaxForeignKeys
	{
		get => maxForeignKeys;
		init => maxForeignKeys = value >= 0
			? value
			: throw new ArgumentOutOfRangeException(nameof(MaxForeignKeys), value, "The number of keys must not be negative.");
	}

	/// <summary>
	/// Called with the plan of each query before it runs: which members of the records it reads, which operators the
	/// reader runs, and what it asks other providers for. <see langword="null"/> (the default) for none.
	/// </summary>
	public Action<CsvQueryPlan>? OnExecute { get; init; }
}
