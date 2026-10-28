using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace CsvHelper.Linq;

/// <summary>
/// The records of a CSV source that one place of a query reads, as its plan says: the reader skips rows without
/// parsing them, filters the records on the members that the filters read, and reads the members that the rest of the
/// query uses only for the records that pass. It opens the source each time it is enumerated.
/// </summary>
/// <typeparam name="T">The type of the records.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class RecordStream<T> : StreamBase<T>
{
	private readonly ICsvRoot root;
	private readonly RecordStreamSpec spec;
	private readonly Func<T, bool>[] filters;

	/// <summary>Creates the stream.</summary>
	/// <param name="root">The source.</param>
	/// <param name="spec">What the reader does.</param>
	public RecordStream(ICsvRoot root, RecordStreamSpec spec)
	{
		this.root = root;
		this.spec = spec;
		filters = spec.Filters.Select(f => (Func<T, bool>)f.Compile()).ToArray();
	}

	/// <summary>Whether the source can be read again: it is not a caller's reader.</summary>
	public override bool CanReread => root.Source.CanReopen;

	/// <summary>Reads the records: on the calling thread, or on several with <see cref="CsvQueryOptions.MaxDegreeOfParallelism"/>.</summary>
	/// <returns>An enumerator that reads the next record each time it moves; disposing it closes the source.</returns>
	protected override IEnumerator<T> Read() => root.Options.MaxDegreeOfParallelism == 1 ? ReadSequentially() : ReadInParallel(root.Options.MaxDegreeOfParallelism);

	private IEnumerator<T> ReadSequentially()
	{
		var reader = root.Source.Open(root.ReaderOptions);
		try
		{
			var mapping = new Mapping(this, reader);
			for (var skipped = 0L; skipped < spec.Skip; skipped++)
			{
				if (!reader.Read())
				{
					yield break;
				}
			}

			while (reader.Read())
			{
				if (mapping.TryRead(reader, out var record))
				{
					yield return record;
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

	/// <summary>
	/// Reads the records on several threads with <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>,
	/// which returns them in the order of the source: each worker skips, filters and maps the records of its batches.
	/// </summary>
	private IEnumerator<T> ReadInParallel(int degree)
	{
		var reader = root.Source.Open(root.ReaderOptions);
		try
		{
			var mapping = new Mapping(this, reader);
			// The rows the skip leaves out, counted from where the reader is (a caller's reader may be past some).
			var last = reader.RowNumber + spec.Skip;
			var results = reader.ParallelEnumerate(
				worker => worker.RowNumber > last && mapping.TryRead(worker, out var record) ? (true, record) : (false, default(T)!), degree);
			foreach (var (passed, record) in results)
			{
				if (passed)
				{
					yield return record;
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

	/// <summary>Reads the records asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator that reads the next record each time it moves; disposing it closes the source.</returns>
	protected override async IAsyncEnumerator<T> ReadAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var reader = root.Source.Open(root.ReaderOptions);
		try
		{
			// The mappers find their columns in the header: read it asynchronously first. A reader of the caller's is
			// past it already, or reads it when the mappers ask.
			if (root.Source.OwnsReader)
			{
				await reader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
			}

			var mapping = new Mapping(this, reader);
			for (var skipped = 0L; skipped < spec.Skip; skipped++)
			{
				if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					yield break;
				}
			}

			// ReadAsync completes synchronously while records are buffered, without looking at the token.
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (mapping.TryRead(reader, out var record))
				{
					yield return record;
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

	/// <summary>
	/// The mappers of one enumeration: one that reads what the filters read, one that reads what the rest of the query
	/// reads, and the full mapper, for when a projection fails where reading every member does not. The workers of a
	/// parallel enumeration share it: mappers can read the records of any reader with the same columns, and the switch to
	/// the full mapper only ever replaces one mapper that works by another.
	/// </summary>
	private sealed class Mapping
	{
		private readonly RecordStream<T> stream;
		private volatile CsvRecordMapper<T>? filterMapper;
		private volatile CsvRecordMapper<T>? restMapper;
		private volatile bool projected;

		/// <summary>Creates the mappers of the members that the plan reads, reading the header if it has not been read.</summary>
		/// <param name="stream">The stream.</param>
		/// <param name="reader">The reader.</param>
		public Mapping(RecordStream<T> stream, CsvReader reader)
		{
			this.stream = stream;
			var spec = stream.spec;
			if (stream.filters.Length > 0)
			{
				filterMapper = Mapper(reader, spec.FilterMembers);
			}

			// The records that pass the filters already have what the rest reads when it reads no other members.
			var reuse = stream.filters.Length > 0 && (spec.FilterMembers is null || (spec.Members is not null && spec.Members.All(spec.FilterMembers.Contains)));
			if (!reuse && (spec.Members is null || spec.Members.Count > 0 || stream.filters.Length > 0))
			{
				restMapper = Mapper(reader, spec.Members);
			}

			projected = spec.FilterMembers is not null || spec.Members is not null;
		}

		/// <summary>Reads the current record, if it passes the filters.</summary>
		/// <param name="reader">The reader, on the record: the stream's, or a worker's of a parallel enumeration.</param>
		/// <param name="record">
		/// The record, with the members that the rest of the query reads; the default of <typeparamref name="T"/> when the
		/// rest reads none (it does not look at the records, only counts them).
		/// </param>
		/// <returns>
		/// Whether the record passed: <see langword="false"/> when a filter rejects it or
		/// <see cref="CsvReaderOptions.OnRecordError"/> skips it.
		/// </returns>
		public bool TryRead(CsvReader reader, out T record)
		{
			var filtering = filterMapper;
			var rest = restMapper;
			if (filtering is not null)
			{
				if (!TryMap(reader, filtering, out var candidate))
				{
					record = default!;

					return false;
				}

				foreach (var filter in stream.filters)
				{
					if (!filter(candidate))
					{
						record = default!;

						return false;
					}
				}

				if (rest is null)
				{
					record = candidate;

					return true;
				}
			}

			if (rest is null)
			{
				record = default!;

				return true;
			}

			return TryMap(reader, rest, out record);
		}

		/// <summary>Creates the mapper of some members: a projection, or the full mapper for all of them.</summary>
		private static CsvRecordMapper<T> Mapper(CsvReader reader, IReadOnlyCollection<string>? members) =>
			members is null ? CsvRecordMapper.Create<T>(reader) : CsvRecordMapper.CreateProjection<T>(reader, members);

		/// <summary>
		/// Maps the current record. A projection that fails is tried again with the full mapper: if that reads the record,
		/// the projection failed on the defaults it gives the members it leaves out (a constructor that checks its
		/// arguments), so the rest of the source is read with the full mapper.
		/// </summary>
		private bool TryMap(CsvReader reader, CsvRecordMapper<T> mapper, out T record)
		{
			if (!projected)
			{
				return reader.TryGetRecord(mapper, out record);
			}

			// When the header lacks columns that the query does not read, there is no full mapper: the failure stands.
			CsvRecordMapper<T>? full = null;
			try
			{
				record = reader.GetRecord(mapper);

				return true;
			}
			catch (CsvFormatException) when ((full = FullMapper(reader)) is not null)
			{
				if (!reader.TryGetRecord(full, out record))
				{
					return false;
				}

				filterMapper = filterMapper is null ? null : full;
				restMapper = restMapper is null ? null : full;
				projected = false;

				return true;
			}
		}

		/// <summary>The full mapper, or <see langword="null"/> when the header lacks columns that it needs.</summary>
		private static CsvRecordMapper<T>? FullMapper(CsvReader reader)
		{
			try
			{
				return CsvRecordMapper.Create<T>(reader);
			}
			catch (KeyNotFoundException)
			{
				return null;
			}
		}
	}
}
