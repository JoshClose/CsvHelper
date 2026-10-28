using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>
/// A CSV query that runs asynchronously: <c>await foreach</c> over it, or the asynchronous versions of the LINQ operators
/// that run a query (<see cref="ToListAsync"/>, <see cref="CountAsync(CancellationToken)"/>, <see cref="FirstAsync(CancellationToken)"/>
/// and the others), which read the CSV asynchronously. <see cref="CsvQueryable.AsCsvAsync{T}(IQueryable{T})"/> creates it.
/// </summary>
/// <remarks>
/// <para>
/// Its operators are instance methods, so they are chosen over the extension methods of the same names that Entity
/// Framework Core and .NET 10's <c>AsyncEnumerable</c> have.
/// </para>
/// <para>
/// When the CSV reader runs the whole query (filters, skips and the operators that read the records it returns, such as
/// <c>Select</c> and <c>Take</c>), the records stream; otherwise the records that the reader returns are read into memory
/// first, and the rest of the query runs on them.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the elements.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
public sealed class CsvAsyncQuery<T> : IAsyncEnumerable<T>
{
	private readonly IQueryable<T> query;

	/// <summary>Creates the asynchronous view of a CSV query.</summary>
	/// <param name="query">The query, whose provider is the CSV provider.</param>
	internal CsvAsyncQuery(IQueryable<T> query) => this.query = query;

	/// <summary>Runs the query, reading the CSV asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator of the results; disposing it closes the sources it opened.</returns>
	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
		QueryExecutor.EnumerateAsync<T>(query.Expression, cancellationToken);

	/// <summary>Runs the query and returns its results in a list.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The results.</returns>
	public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
	{
		var results = new List<T>();
		await foreach (var item in this.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			results.Add(item);
		}

		return results;
	}

	/// <summary>Runs the query and returns its results in an array.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The results.</returns>
	public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default) => (await ToListAsync(cancellationToken).ConfigureAwait(false)).ToArray();

	/// <summary>Runs the query and calls <paramref name="action"/> with each result.</summary>
	/// <param name="action">What to do with each result.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>A task that completes after the last result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
	public async Task ForEachAsync(Action<T> action, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(action);
		await foreach (var item in this.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			action(item);
		}
	}

	/// <summary>Counts the results; the reader counts the records without reading their members when it can.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The number of results.</returns>
	public Task<int> CountAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.Count(), cancellationToken);

	/// <summary>Counts the results that match a condition.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The number of results that match.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).CountAsync(cancellationToken);

	/// <summary>Counts the results, as a <see cref="long"/>.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The number of results.</returns>
	public Task<long> LongCountAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.LongCount(), cancellationToken);

	/// <summary>Counts the results that match a condition, as a <see cref="long"/>.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The number of results that match.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	public Task<long> LongCountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).LongCountAsync(cancellationToken);

	/// <summary>Whether the query has any result; reading stops at the first.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns><see langword="true"/> when there is a result.</returns>
	public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.Any(), cancellationToken);

	/// <summary>Whether any result matches a condition; reading stops at the first that does.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns><see langword="true"/> when a result matches.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).AnyAsync(cancellationToken);

	/// <summary>Whether every result matches a condition; reading stops at the first that does not.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns><see langword="true"/> when all results match, or there are none.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	public async Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(predicate);
		var not = Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

		return !await Filtered(not).AnyAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>The first result.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The first result.</returns>
	/// <exception cref="InvalidOperationException">The query has no results.</exception>
	public Task<T> FirstAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.First(), cancellationToken);

	/// <summary>The first result that matches a condition.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The first result that matches.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">No result matches.</exception>
	public Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).FirstAsync(cancellationToken);

	/// <summary>The first result, or the default of <typeparamref name="T"/> when there is none.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The first result, or the default.</returns>
	public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) => ExecuteAsync<T?>(q => q.FirstOrDefault(), cancellationToken);

	/// <summary>The first result that matches a condition, or the default of <typeparamref name="T"/> when none does.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The first result that matches, or the default.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).FirstOrDefaultAsync(cancellationToken);

	/// <summary>The only result.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The only result.</returns>
	/// <exception cref="InvalidOperationException">The query has no results, or more than one.</exception>
	public Task<T> SingleAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.Single(), cancellationToken);

	/// <summary>The only result that matches a condition.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The only result that matches.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">No result matches, or more than one.</exception>
	public Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).SingleAsync(cancellationToken);

	/// <summary>The only result, or the default of <typeparamref name="T"/> when there is none.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The only result, or the default.</returns>
	/// <exception cref="InvalidOperationException">The query has more than one result.</exception>
	public Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) => ExecuteAsync<T?>(q => q.SingleOrDefault(), cancellationToken);

	/// <summary>The only result that matches a condition, or the default of <typeparamref name="T"/> when none does.</summary>
	/// <param name="predicate">The condition.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The only result that matches, or the default.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">More than one result matches.</exception>
	public Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
		Filtered(predicate).SingleOrDefaultAsync(cancellationToken);

	/// <summary>The result at an index; the reader skips the records before it without reading their members when it can.</summary>
	/// <param name="index">The 0-based index.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The result at <paramref name="index"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative, or there are not that many results.</exception>
	public Task<T> ElementAtAsync(int index, CancellationToken cancellationToken = default) => ExecuteAsync(q => q.ElementAt(index), cancellationToken);

	/// <summary>The last result.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The last result.</returns>
	/// <exception cref="InvalidOperationException">The query has no results.</exception>
	public Task<T> LastAsync(CancellationToken cancellationToken = default) => ExecuteAsync(q => q.Last(), cancellationToken);

	/// <summary>The last result, or the default of <typeparamref name="T"/> when there is none.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The last result, or the default.</returns>
	public Task<T?> LastOrDefaultAsync(CancellationToken cancellationToken = default) => ExecuteAsync<T?>(q => q.LastOrDefault(), cancellationToken);

	/// <summary>
	/// Runs any other operator that returns a single value, such as <c>q =&gt; q.Sum(o =&gt; o.Total)</c> or
	/// <c>q =&gt; q.Max(o =&gt; o.Placed)</c>, reading the CSV asynchronously.
	/// </summary>
	/// <typeparam name="TResult">The type of the value.</typeparam>
	/// <param name="operation">The operator, applied to the query.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
	public Task<TResult> ExecuteAsync<TResult>(Expression<Func<IQueryable<T>, TResult>> operation, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(operation);
		var body = new Substitution(operation.Parameters[0], query.Expression).Visit(operation.Body)!;

		return QueryExecutor.ExecuteAsync<TResult>(body, cancellationToken);
	}

	private CsvAsyncQuery<T> Filtered(Expression<Func<T, bool>> predicate)
	{
		ArgumentNullException.ThrowIfNull(predicate);

		return new CsvAsyncQuery<T>(query.Where(predicate));
	}

	/// <summary>Replaces the parameter of an operation by the query.</summary>
	private sealed class Substitution : ExpressionVisitor
	{
		private readonly ParameterExpression parameter;
		private readonly Expression replacement;

		/// <summary>Creates the visitor.</summary>
		/// <param name="parameter">The parameter.</param>
		/// <param name="replacement">The query that replaces it.</param>
		public Substitution(ParameterExpression parameter, Expression replacement)
		{
			this.parameter = parameter;
			this.replacement = replacement;
		}

		/// <summary>Replaces the parameter.</summary>
		/// <param name="node">A parameter.</param>
		/// <returns>The query for the operation's parameter, else the node.</returns>
		protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : node;
	}
}
