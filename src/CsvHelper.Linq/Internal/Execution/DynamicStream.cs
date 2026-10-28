using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace CsvHelper.Linq;

/// <summary>
/// The rows of a CSV source read as <see cref="CsvDynamicRecord"/>, with the filters and the projection that a query
/// applies to them run on the reader itself (rewritten by <see cref="DynamicRows"/>): only the rows that pass are made into
/// rows, or, with a projection, none are.
/// </summary>
/// <typeparam name="TOut">The type of the elements: <see cref="CsvDynamicRecord"/>, or what the projection returns.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class DynamicStream<TOut> : StreamBase<TOut>
{
	private readonly ICsvRoot root;
	private readonly long skip;
	private readonly string[] names;
	private readonly Func<CsvReader, int[], bool>[] filters;
	private readonly Func<CsvReader, int[], TOut>? selector;

	/// <summary>Creates the stream.</summary>
	/// <param name="root">The source.</param>
	/// <param name="skip">The rows to skip without reading them.</param>
	/// <param name="names">The names of the columns that the filters and the projection read, by slot.</param>
	/// <param name="filters">The filters, which get the reader and the indexes of the columns by slot.</param>
	/// <param name="selector">The projection, or <see langword="null"/> to return the rows.</param>
	public DynamicStream(ICsvRoot root, long skip, string[] names, Func<CsvReader, int[], bool>[] filters, Delegate? selector)
	{
		this.root = root;
		this.skip = skip;
		this.names = names;
		this.filters = filters;
		this.selector = (Func<CsvReader, int[], TOut>?)selector;
	}

	/// <summary>Whether the source can be read again: it is not a caller's reader.</summary>
	public override bool CanReread => root.Source.CanReopen;

	/// <summary>Reads the rows: on the calling thread, or on several with <see cref="CsvQueryOptions.MaxDegreeOfParallelism"/>.</summary>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	protected override IEnumerator<TOut> Read()
	{
		var reader = root.Source.Open(root.ReaderOptions);
		try
		{
			_ = reader.Headers;
			var columns = Columns(reader);
			var mapper = selector is null ? CsvRecordMapper.Create<CsvDynamicRecord>(reader) : null;
			var degree = root.Options.MaxDegreeOfParallelism;
			if (degree != 1)
			{
				// The workers of ParallelEnumerate filter and project their batches; the results come in the order of the source.
				var last = reader.RowNumber + skip;
				var results = reader.ParallelEnumerate(
					worker => worker.RowNumber > last && Passes(worker, columns) ? (true, Output(worker, columns, mapper)) : (false, default(TOut)!), degree);
				foreach (var (passed, element) in results)
				{
					if (passed)
					{
						yield return element;
					}
				}

				yield break;
			}

			for (var skipped = 0L; skipped < skip; skipped++)
			{
				if (!reader.Read())
				{
					yield break;
				}
			}

			while (reader.Read())
			{
				if (Passes(reader, columns))
				{
					yield return Output(reader, columns, mapper);
				}
			}
		}
		finally
		{
			if (root.Source.OwnsReader)
			{
				reader.Dispose();
			}
		}
	}

	/// <summary>The element of the current record: what the projection makes of it, or the row.</summary>
	private TOut Output(CsvReader reader, int[] columns, CsvRecordMapper<CsvDynamicRecord>? mapper) =>
		selector is not null ? selector(reader, columns) : (TOut)(object)reader.GetRecord(mapper!);

	/// <summary>Reads the rows asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	protected override async IAsyncEnumerator<TOut> ReadAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var reader = root.Source.Open(root.ReaderOptions);
		try
		{
			if (root.Source.OwnsReader)
			{
				await reader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
			}

			var columns = Columns(reader);
			var mapper = selector is null ? CsvRecordMapper.Create<CsvDynamicRecord>(reader) : null;
			for (var skipped = 0L; skipped < skip; skipped++)
			{
				if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					yield break;
				}
			}

			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (Passes(reader, columns))
				{
					yield return Output(reader, columns, mapper);
				}
			}
		}
		finally
		{
			if (root.Source.OwnsReader)
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>The indexes of the columns by slot, from the header; -1 for a name that it lacks.</summary>
	private int[] Columns(CsvReader reader)
	{
		var columns = new int[names.Length];
		for (var i = 0; i < columns.Length; i++)
		{
			columns[i] = reader.TryGetFieldIndex(names[i], out var index) ? index : -1;
		}

		return columns;
	}

	private bool Passes(CsvReader reader, int[] columns)
	{
		foreach (var filter in filters)
		{
			if (!filter(reader, columns))
			{
				return false;
			}
		}

		return true;
	}
}
