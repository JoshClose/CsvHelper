using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace CsvHelper.Linq;

/// <summary>
/// A LINQ query over CSV records: its source (<see cref="CsvQueryable"/>), and the operators applied to it. It runs
/// each time it is enumerated: the CSV reader runs what it can itself (it reads only the members of the records that the
/// query uses, skips rows without parsing them, and filters records before it reads the members that only the rest of
/// the query uses), other LINQ providers run the parts of the query that are theirs, and LINQ to Objects runs the rest.
/// </summary>
/// <remarks>
/// <para>
/// Any LINQ operator can be used. <see cref="CsvQueryable.Explain(IQueryable)"/> tells what a query pushes into the
/// reader.
/// </para>
/// <para>
/// <c>await foreach</c> over a <see cref="CsvQuery{T}"/> reads the CSV asynchronously (<see cref="GetAsyncEnumerator"/>);
/// <see cref="CsvQueryable.AsCsvAsync{T}(IQueryable{T})"/> does the same for the <see cref="IQueryable{T}"/> that LINQ
/// operators return, and has asynchronous versions of <c>ToList</c>, <c>Count</c>, <c>First</c> and the others. The
/// queries that operators return also implement <see cref="IAsyncEnumerable{T}"/>, for code that looks for it, such as
/// the <c>ToListAsync</c> of Entity Framework Core. This class does not, so that the LINQ operators of
/// <see cref="Queryable"/> and of .NET 10's <c>AsyncEnumerable</c> are not both candidates for a query typed as it.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the elements: the records of the source, or what the query makes of them.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
public class CsvQuery<T> : IOrderedQueryable<T>, ICsvRoot, IAsyncSource<T>
{
	private readonly CsvSource? source;
	private readonly CsvQueryOptions? options;
	private readonly CsvReaderOptions? readerOptions;

	/// <summary>Creates a query that applies operators to the sources in <paramref name="expression"/>.</summary>
	/// <param name="expression">The query, whose type is <see cref="IQueryable{T}"/>.</param>
	private protected CsvQuery(Expression expression) => Expression = expression;

	/// <summary>Creates the query of all the records of a source.</summary>
	/// <param name="source">Where the CSV is read from.</param>
	/// <param name="options">How it is read.</param>
	internal CsvQuery(CsvSource source, CsvQueryOptions options)
	{
		this.source = source;
		this.options = options;
		readerOptions = options.ReaderOptions ?? CsvReaderOptions.For<T>();
		Expression = Expression.Constant(this);
	}

	/// <summary>The type of the elements, <typeparamref name="T"/>.</summary>
	public Type ElementType => typeof(T);

	/// <summary>The query: a constant holding the source for the query of all its records, else the operators applied to it.</summary>
	public Expression Expression { get; }

	/// <summary>The provider that creates and runs CSV queries.</summary>
	public IQueryProvider Provider => CsvQueryProvider.Instance;

	/// <summary>The source, when this is the query of all its records; <see langword="null"/> otherwise.</summary>
	internal CsvSource? Source => source;

	/// <summary>The options of the source, when this is the query of all its records; <see langword="null"/> otherwise.</summary>
	internal CsvQueryOptions? Options => options;

	CsvSource ICsvRoot.Source => source ?? throw new InvalidOperationException("The query has no source of its own.");

	CsvQueryOptions ICsvRoot.Options => options ?? CsvQueryOptions.Default;

	CsvReaderOptions ICsvRoot.ReaderOptions => readerOptions ?? CsvReaderOptions.Default;

	bool ICsvRoot.IsRoot => source is not null;

	IReadOnlyList<string>? ICsvRoot.Members => CsvRecordMapper.TryGetMembers<T>(out var members) ? members.Select(m => m.Name).ToList() : null;

	/// <summary>Runs the query and reads its results as they are enumerated.</summary>
	/// <returns>An enumerator of the results; disposing it closes the sources it opened.</returns>
	/// <exception cref="CsvFormatException">A record cannot be read.</exception>
	/// <exception cref="KeyNotFoundException">The header lacks a column that the query reads.</exception>
	public IEnumerator<T> GetEnumerator() => QueryExecutor.Enumerate<T>(Expression);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	/// Runs the query and reads its results as they are enumerated, reading the CSV asynchronously. When the query has
	/// operators that the CSV reader does not run itself, the records it reads are buffered first and the rest of the query
	/// runs on them; otherwise the results stream.
	/// </summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator of the results; disposing it closes the sources it opened.</returns>
	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
		QueryExecutor.EnumerateAsync<T>(Expression, cancellationToken);

	/// <summary>
	/// Tells how the query runs without running it: which members of the records it reads, which operators the CSV reader
	/// runs, and what it asks other providers for.
	/// </summary>
	/// <returns>The plan of the query.</returns>
	public CsvQueryPlan Explain() => QueryPlanner.Plan(Expression).Plan;

	/// <summary>The query, as text.</summary>
	/// <returns>The source and the operators, such as <c>file 'orders.csv'.Where(o =&gt; (o.Total &gt; 100))</c>.</returns>
	public override string ToString() => source is not null ? $"CsvQuery<{typeof(T).Name}>({source})" : Expression.ToString();

	IEnumerable ICsvRoot.CreateStream(RecordStreamSpec spec) => new RecordStream<T>(this, spec);
}

/// <summary>
/// A query that LINQ operators returned (typed <see cref="IQueryable{T}"/>), which is also an
/// <see cref="IAsyncEnumerable{T}"/> for code that looks for one, such as the <c>ToListAsync</c> of Entity Framework Core.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class ComposedCsvQuery<T> : CsvQuery<T>, IAsyncEnumerable<T>
{
	/// <summary>Creates a query that applies operators to the sources in <paramref name="expression"/>.</summary>
	/// <param name="expression">The query, whose type is <see cref="IQueryable{T}"/>.</param>
	public ComposedCsvQuery(Expression expression)
		: base(expression)
	{
	}
}

/// <summary>
/// The query of all the records of a source, as the query planner sees it: a non-generic view of
/// <see cref="CsvQuery{T}"/>, which creates the stream of its records for a plan.
/// </summary>
internal interface ICsvRoot
{
	/// <summary>The type of the records.</summary>
	Type ElementType { get; }

	/// <summary>Whether the query is the query of all the records of a source; the other members need one.</summary>
	bool IsRoot { get; }

	/// <summary>Where the CSV is read from.</summary>
	CsvSource Source { get; }

	/// <summary>How it is read.</summary>
	CsvQueryOptions Options { get; }

	/// <summary>The options of the reader: the query's, or those of the record type.</summary>
	CsvReaderOptions ReaderOptions { get; }

	/// <summary>
	/// The names of the members that the record type's generated mapper can read on their own (with projections), or
	/// <see langword="null"/> when it cannot: every member is read.
	/// </summary>
	IReadOnlyList<string>? Members { get; }

	/// <summary>Creates the stream of the records that a plan reads, which LINQ to Objects runs the rest of the query on.</summary>
	/// <param name="spec">What the reader does: the rows it skips, its filters, and the members it reads.</param>
	/// <returns>
	/// The stream: an <see cref="IEnumerable{T}"/> and <see cref="IAsyncEnumerable{T}"/> of the records, which reads the CSV
	/// each time it is enumerated.
	/// </returns>
	IEnumerable CreateStream(RecordStreamSpec spec);
}
