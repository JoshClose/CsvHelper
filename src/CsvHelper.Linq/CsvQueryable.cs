using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>
/// Creates LINQ queries over CSV records: the records of a file, a stream, text or bytes as <see cref="CsvRecordAttribute"/>
/// types, or as <see cref="CsvDynamicRecord"/> rows for files without a record type.
/// </summary>
/// <remarks>
/// <para>
/// A query reads its source each time it runs, and pushes what it can into the reader: it reads only the members of the
/// records that the query uses (with record types whose assembly references CsvHelper.Linq, which makes the source
/// generator write the projections it needs), skips rows without parsing them, and filters records before it reads the
/// members that only the rest of the query uses. Any LINQ operator can be used; LINQ to Objects runs those that the
/// reader does not. Queries of other LINQ providers in a CSV query, such as tables of Entity Framework Core, run on their
/// provider, and joins with them ask only for the rows with the keys of the CSV records.
/// </para>
/// <para>
/// The query provider compiles lambdas and uses reflection, so it is not supported with native AOT or trimming.
/// </para>
/// </remarks>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
public static class CsvQueryable
{
	/// <summary>Queries and changes the records of a CSV file, which is opened each time a query runs.</summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">How the file is read and written, or <see langword="null"/> for the defaults.</param>
	/// <returns>The table of the file's records.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	public static CsvTable<T> FromFile<T>(string path, CsvQueryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(path);

		return new CsvTable<T>(path, options ?? CsvQueryOptions.Default);
	}

	/// <summary>Queries and changes the rows of a CSV file as dynamic records, which is opened each time a query runs.</summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">How the file is read and written, or <see langword="null"/> for the defaults.</param>
	/// <returns>The table of the file's rows.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	public static CsvTable<CsvDynamicRecord> FromFile(string path, CsvQueryOptions? options = null) => FromFile<CsvDynamicRecord>(path, options);

	/// <summary>Queries the records of CSV text.</summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="text">The CSV.</param>
	/// <param name="options">How the text is read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the records.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	public static CsvQuery<T> FromText<T>(string text, CsvQueryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(text);

		return new CsvQuery<T>(new CsvSource.Text(text), options ?? CsvQueryOptions.Default);
	}

	/// <summary>Queries the rows of CSV text as dynamic records.</summary>
	/// <param name="text">The CSV.</param>
	/// <param name="options">How the text is read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the rows.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	public static CsvQuery<CsvDynamicRecord> FromText(string text, CsvQueryOptions? options = null) => FromText<CsvDynamicRecord>(text, options);

	/// <summary>Queries the records of CSV in memory, as UTF-8 bytes, which are read in place.</summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="utf8">The bytes of the CSV.</param>
	/// <param name="options">How the bytes are read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the records.</returns>
	public static CsvQuery<T> FromBytes<T>(ReadOnlyMemory<byte> utf8, CsvQueryOptions? options = null) =>
		new(new CsvSource.Bytes(utf8), options ?? CsvQueryOptions.Default);

	/// <summary>Queries the rows of CSV in memory, as UTF-8 bytes, as dynamic records.</summary>
	/// <param name="utf8">The bytes of the CSV.</param>
	/// <param name="options">How the bytes are read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the rows.</returns>
	public static CsvQuery<CsvDynamicRecord> FromBytes(ReadOnlyMemory<byte> utf8, CsvQueryOptions? options = null) => FromBytes<CsvDynamicRecord>(utf8, options);

	/// <summary>Queries the records of CSV streams: <paramref name="open"/> opens one each time a query runs, which is disposed after it.</summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="open">Opens the stream, positioned at the start of the CSV.</param>
	/// <param name="options">How the streams are read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the records.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="open"/> is <see langword="null"/>.</exception>
	public static CsvQuery<T> FromStream<T>(Func<Stream> open, CsvQueryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(open);

		return new CsvQuery<T>(new CsvSource.StreamFactory(open), options ?? CsvQueryOptions.Default);
	}

	/// <summary>Queries the rows of CSV streams as dynamic records: <paramref name="open"/> opens one each time a query runs.</summary>
	/// <param name="open">Opens the stream, positioned at the start of the CSV.</param>
	/// <param name="options">How the streams are read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the rows.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="open"/> is <see langword="null"/>.</exception>
	public static CsvQuery<CsvDynamicRecord> FromStream(Func<Stream> open, CsvQueryOptions? options = null) => FromStream<CsvDynamicRecord>(open, options);

	/// <summary>Queries the records of CSV text readers: <paramref name="open"/> opens one each time a query runs, which is disposed after it.</summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="open">Opens the text reader, positioned at the start of the CSV.</param>
	/// <param name="options">How the text is read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the records.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="open"/> is <see langword="null"/>.</exception>
	public static CsvQuery<T> FromTextReader<T>(Func<TextReader> open, CsvQueryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(open);

		return new CsvQuery<T>(new CsvSource.TextReaderFactory(open), options ?? CsvQueryOptions.Default);
	}

	/// <summary>Queries the rows of CSV text readers as dynamic records: <paramref name="open"/> opens one each time a query runs.</summary>
	/// <param name="open">Opens the text reader, positioned at the start of the CSV.</param>
	/// <param name="options">How the text is read, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of all the rows.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="open"/> is <see langword="null"/>.</exception>
	public static CsvQuery<CsvDynamicRecord> FromTextReader(Func<TextReader> open, CsvQueryOptions? options = null) =>
		FromTextReader<CsvDynamicRecord>(open, options);

	/// <summary>
	/// Queries the remaining records of a reader, from where it is. The query can run once, and leaves the reader open; its
	/// options are the reader's (<see cref="CsvQueryOptions.ReaderOptions"/> is not used).
	/// </summary>
	/// <typeparam name="T">The <see cref="CsvRecordAttribute"/> type of the records.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="options">How much the query pushes into the reader, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of the remaining records.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	public static CsvQuery<T> Query<T>(this CsvReader reader, CsvQueryOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(reader);

		return new CsvQuery<T>(new CsvSource.Reader(reader), (options ?? CsvQueryOptions.Default) with { ReaderOptions = reader.Options });
	}

	/// <summary>Queries the remaining rows of a reader as dynamic records, from where it is. The query can run once.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="options">How much the query pushes into the reader, or <see langword="null"/> for the defaults.</param>
	/// <returns>The query of the remaining rows.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	public static CsvQuery<CsvDynamicRecord> Query(this CsvReader reader, CsvQueryOptions? options = null) => Query<CsvDynamicRecord>(reader, options);

	/// <summary>
	/// Tells how a CSV query runs without running it: which members of the records it reads, which operators the CSV reader
	/// runs, and what it asks other providers for.
	/// </summary>
	/// <param name="query">A query over a CSV source.</param>
	/// <returns>The plan of the query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="query"/> is not a CSV query.</exception>
	public static CsvQueryPlan Explain(IQueryable query)
	{
		ArgumentNullException.ThrowIfNull(query);
		if (query.Provider is not CsvQueryProvider)
		{
			throw new ArgumentException($"The query is not a CSV query: its provider is {query.Provider.GetType()}.", nameof(query));
		}

		return QueryPlanner.Plan(query.Expression).Plan;
	}

	/// <summary>
	/// Changes the dynamic rows of a file that match a condition: <paramref name="update"/> changes the values of each, by
	/// column name, and only the fields whose values changed are written again; the rest of the file is kept byte for byte.
	/// The file is rewritten next to itself and replaced when the new one is complete, so a failure leaves it as it was.
	/// </summary>
	/// <param name="table">The table of the file.</param>
	/// <param name="predicate">The condition of the rows to change.</param>
	/// <param name="update">Changes the values of a row, such as <c>row =&gt; row["Status"] = "Open"</c>.</param>
	/// <returns>The number of rows changed.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The update sets a column that the file does not have.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8, is fixed-width, or quotes with another character than <c>"</c>.</exception>
	public static long Update(this CsvTable<CsvDynamicRecord> table, Expression<Func<CsvDynamicRecord, bool>> predicate, Action<IDictionary<string, string?>> update)
	{
		ArgumentNullException.ThrowIfNull(table);

		return table.UpdateRowsAsync(predicate, update, false, default).GetAwaiter().GetResult();
	}

	/// <summary>Changes the dynamic rows of a file that match a condition, asynchronously; see <see cref="Update(CsvTable{CsvDynamicRecord}, Expression{Func{CsvDynamicRecord, bool}}, Action{IDictionary{string, string}})"/>.</summary>
	/// <param name="table">The table of the file.</param>
	/// <param name="predicate">The condition of the rows to change.</param>
	/// <param name="update">Changes the values of a row.</param>
	/// <param name="cancellationToken">Cancels the rewrite, which leaves the file as it was.</param>
	/// <returns>The number of rows changed.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">The update sets a column that the file does not have.</exception>
	/// <exception cref="NotSupportedException">The file is not UTF-8, is fixed-width, or quotes with another character than <c>"</c>.</exception>
	public static ValueTask<long> UpdateAsync(this CsvTable<CsvDynamicRecord> table, Expression<Func<CsvDynamicRecord, bool>> predicate,
		Action<IDictionary<string, string?>> update, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(table);

		return table.UpdateRowsAsync(predicate, update, true, cancellationToken);
	}

	/// <summary>
	/// Runs a CSV query asynchronously: <c>await foreach</c> over the result, or call its asynchronous operators, such as
	/// <see cref="CsvAsyncQuery{T}.ToListAsync"/> and <see cref="CsvAsyncQuery{T}.CountAsync(System.Threading.CancellationToken)"/>.
	/// </summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="source">A query over a CSV source, such as one that LINQ operators returned.</param>
	/// <returns>The asynchronous view of the query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="source"/> is not a CSV query.</exception>
	public static CsvAsyncQuery<T> AsCsvAsync<T>(this IQueryable<T> source)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (source.Provider is not CsvQueryProvider)
		{
			throw new ArgumentException($"The query is not a CSV query: its provider is {source.Provider.GetType()}.", nameof(source));
		}

		return new CsvAsyncQuery<T>(source);
	}
}
