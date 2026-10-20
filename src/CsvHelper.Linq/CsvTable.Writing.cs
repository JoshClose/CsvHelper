using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

// Adding records to the file, and removing and changing the records that match a condition.
public sealed partial class CsvTable<T>
{
	/// <summary>
	/// Adds records to the end of the file, creating it (with a header) when it does not exist or is empty. They are
	/// written with the file's separator and line break, after a line break when the file does not end with one.
	/// </summary>
	/// <param name="records">The records, enumerated once.</param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The file's header is not the one that the records are written with.</exception>
	public long AppendRecords(IEnumerable<T> records)
	{
		ArgumentNullException.ThrowIfNull(records);

		return CsvEnumerable.AppendCsv(records, Path, QueryOptions.WriterOptions, ((ICsvRoot)this).ReaderOptions);
	}

	/// <summary>Adds records to the end of the file asynchronously; see <see cref="AppendRecords"/>.</summary>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The file's header is not the one that the records are written with.</exception>
	public ValueTask<long> AppendRecordsAsync(IEnumerable<T> records, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);

		return CsvEnumerable.AppendCsvAsync(records, null, Path, QueryOptions.WriterOptions, ((ICsvRoot)this).ReaderOptions, cancellationToken);
	}

	/// <summary>Adds an asynchronous sequence of records to the end of the file asynchronously; see <see cref="AppendRecords"/>.</summary>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing.</param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The file's header is not the one that the records are written with.</exception>
	[OverloadResolutionPriority(1)]
	public ValueTask<long> AppendRecordsAsync(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);

		return CsvEnumerable.AppendCsvAsync(null, records, Path, QueryOptions.WriterOptions, ((ICsvRoot)this).ReaderOptions, cancellationToken);
	}

	/// <summary>
	/// Removes the records that match a condition from the file, with their line breaks. Everything else is kept byte for
	/// byte. The condition reads only the members it uses; a record that cannot be read, and that
	/// <see cref="CsvReaderOptions.OnRecordError"/> skips, is kept. The file is rewritten next to itself and replaced when the
	/// new one is complete, so a failure leaves it as it was.
	/// </summary>
	/// <param name="predicate">The condition of the records to remove.</param>
	/// <returns>The number of records removed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8.</exception>
	public long Delete(Expression<Func<T, bool>> predicate) => DeleteAsync(predicate, false, default).GetAwaiter().GetResult();

	/// <summary>Removes the records that match a condition from the file, asynchronously; see <see cref="Delete"/>.</summary>
	/// <param name="predicate">The condition of the records to remove.</param>
	/// <param name="cancellationToken">Cancels the rewrite, which leaves the file as it was.</param>
	/// <returns>The number of records removed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8.</exception>
	public ValueTask<long> DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		DeleteAsync(predicate, true, cancellationToken);

	/// <summary>
	/// Changes the records that match a condition: each is replaced by what <paramref name="update"/> makes of it, in which
	/// only the fields whose values changed are written again; the others, the columns that <typeparamref name="T"/> does
	/// not map, and the rest of the file are kept byte for byte. The file is rewritten next to itself and replaced when the
	/// new one is complete, so a failure leaves it as it was.
	/// </summary>
	/// <param name="predicate">The condition of the records to change.</param>
	/// <param name="update">
	/// Makes the new record of a record, such as <c>o =&gt; o with { Status = "Open" }</c>; it may also change the record
	/// it gets and return it.
	/// </param>
	/// <returns>The number of records changed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="update"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The update changes a member that the file has no column for.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8, is fixed-width, or quotes with another character than <c>"</c>.</exception>
	public long Update(Expression<Func<T, bool>> predicate, Func<T, T> update) => UpdateAsync(predicate, update, false, default).GetAwaiter().GetResult();

	/// <summary>Changes the records that match a condition, asynchronously; see <see cref="Update"/>.</summary>
	/// <param name="predicate">The condition of the records to change.</param>
	/// <param name="update">Makes the new record of a record; it may also change the record it gets and return it.</param>
	/// <param name="cancellationToken">Cancels the rewrite, which leaves the file as it was.</param>
	/// <returns>The number of records changed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> or <paramref name="update"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The update changes a member that the file has no column for.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8, is fixed-width, or quotes with another character than <c>"</c>.</exception>
	public ValueTask<long> UpdateAsync(Expression<Func<T, bool>> predicate, Func<T, T> update, CancellationToken cancellationToken = default) =>
		UpdateAsync(predicate, update, true, cancellationToken);

	/// <summary>Changes the dynamic rows that match a condition; see <see cref="CsvQueryable.Update(CsvTable{CsvDynamicRecord}, Expression{Func{CsvDynamicRecord, bool}}, Action{IDictionary{string, string}})"/>.</summary>
	internal ValueTask<long> UpdateRowsAsync(Expression<Func<T, bool>> predicate, Action<IDictionary<string, string?>> update, bool useAsync,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentNullException.ThrowIfNull(update);
		var change = new UpdateDynamicRows((Func<CsvDynamicRecord, bool>)(Delegate)predicate.Compile(), update, WriterOptions());

		return FileRewriter.RewriteAsync(Path, ((ICsvRoot)this).ReaderOptions, change, useAsync, cancellationToken);
	}

	private ValueTask<long> DeleteAsync(Expression<Func<T, bool>> predicate, bool useAsync, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		IReadOnlyCollection<string>? members = null;
		if (QueryOptions.PruneColumns && ((ICsvRoot)this).Members is { } mapped)
		{
			members = MemberUsageAnalyzer.OfParameter(new Chain(predicate, null), predicate, 0, typeof(T), mapped).Selection;
		}

		return FileRewriter.RewriteAsync(Path, ((ICsvRoot)this).ReaderOptions, new DeleteRows<T>(predicate.Compile(), members), useAsync, cancellationToken);
	}

	private ValueTask<long> UpdateAsync(Expression<Func<T, bool>> predicate, Func<T, T> update, bool useAsync, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		ArgumentNullException.ThrowIfNull(update);

		return FileRewriter.RewriteAsync(Path, ((ICsvRoot)this).ReaderOptions, new UpdateRows<T>(predicate.Compile(), update, WriterOptions()), useAsync,
			cancellationToken);
	}

	/// <summary>The options of the writer: the table's, else those of <typeparamref name="T"/>.</summary>
	private CsvWriterOptions WriterOptions() => QueryOptions.WriterOptions ?? CsvWriterOptions.For<T>();
}
