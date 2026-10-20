using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>
/// Runs CSV queries: plans them (<see cref="QueryPlanner"/>), then runs the planned query with LINQ to Objects, whose
/// provider rewrites the <see cref="Queryable"/> operators that are left into <see cref="Enumerable"/> ones over the
/// streams of the CSV sources.
/// </summary>
/// <remarks>
/// LINQ to Objects runs synchronously, so an asynchronous query streams only when what is left after planning is a
/// stream, with <c>Where</c>, <c>Select</c>, <c>Skip</c> and <c>Take</c> on it (and one of the operators that return a
/// single value, for <see cref="ExecuteAsync{TResult}"/>); the executor runs those itself. Otherwise the streams are read
/// into memory asynchronously first.
/// </remarks>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal static class QueryExecutor
{
	// The provider of LINQ to Objects: any EnumerableQuery runs any query whose sources are EnumerableQuery instances.
	private static readonly IQueryProvider Objects = new EnumerableQuery<object>(Array.Empty<object>());

	/// <summary>Runs a query of a sequence.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="expression">The query.</param>
	/// <returns>An enumerator of the results, which reads the sources as it moves.</returns>
	public static IEnumerator<T> Enumerate<T>(Expression expression)
	{
		var planned = Prepare(expression);
		if (planned.Expression is ConstantExpression { Value: { } value } && planned.Streams.TryGetValue(value, out var stream))
		{
			return ((IEnumerable<T>)stream).GetEnumerator();
		}

		return Objects.CreateQuery<T>(planned.Expression).GetEnumerator();
	}

	/// <summary>Runs a query of a single value, such as <c>Count</c> or <c>First</c>.</summary>
	/// <typeparam name="TResult">The type of the value.</typeparam>
	/// <param name="expression">The query.</param>
	/// <returns>The value.</returns>
	public static TResult Execute<TResult>(Expression expression) => Objects.Execute<TResult>(Prepare(expression).Expression);

	/// <summary>A part of a planned query, as a sequence that LINQ to Objects runs when it is enumerated.</summary>
	/// <param name="planned">The planned sequence.</param>
	/// <returns>An <see cref="EnumerableQuery{T}"/> of it.</returns>
	public static IQueryable InMemory(Expression planned) => Objects.CreateQuery(planned);

	/// <summary>Runs a query of a single value, such as <c>Count</c> or <c>First</c>.</summary>
	/// <param name="expression">The query.</param>
	/// <returns>The value.</returns>
	public static object? Execute(Expression expression) => Objects.Execute(Prepare(expression).Expression);

	/// <summary>Runs a query of a sequence, reading the CSV asynchronously.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="expression">The query.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator of the results.</returns>
	public static async IAsyncEnumerator<T> EnumerateAsync<T>(Expression expression, CancellationToken cancellationToken)
	{
		var planned = Prepare(expression);
		if (Tail(planned, planned.Expression) is { } items)
		{
			await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				yield return (T)item!;
			}

			yield break;
		}

		var buffered = await BufferAsync(planned, cancellationToken).ConfigureAwait(false);
		foreach (var item in Objects.CreateQuery<T>(buffered))
		{
			cancellationToken.ThrowIfCancellationRequested();

			yield return item;
		}
	}

	/// <summary>Runs a query of a single value, reading the CSV asynchronously.</summary>
	/// <typeparam name="TResult">The type of the value.</typeparam>
	/// <param name="expression">The query.</param>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The value.</returns>
	public static async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
	{
		var planned = Prepare(expression);
		if (planned.Expression is MethodCallExpression { Method.DeclaringType: { } declaring } terminal && declaring == typeof(Queryable) &&
			Tail(planned, terminal.Arguments[0]) is { } items)
		{
			var (handled, result) = await TerminalAsync<TResult>(terminal, items, cancellationToken).ConfigureAwait(false);
			if (handled)
			{
				return result;
			}
		}

		var buffered = await BufferAsync(planned, cancellationToken).ConfigureAwait(false);

		return Objects.Execute<TResult>(buffered);
	}

	/// <summary>Plans a query and gives its plan to <see cref="CsvQueryOptions.OnExecute"/>.</summary>
	private static PlannedQuery Prepare(Expression expression)
	{
		var planned = QueryPlanner.Plan(expression);
		planned.Options.OnExecute?.Invoke(planned.Plan);

		return planned;
	}

	/// <summary>
	/// The results of a part of a planned query that is a stream with <c>Where</c>, <c>Select</c>, <c>Skip</c> and
	/// <c>Take</c> on it, which the executor runs asynchronously itself; <see langword="null"/> for any other query.
	/// </summary>
	private static IAsyncEnumerable<object?>? Tail(PlannedQuery planned, Expression node)
	{
		var steps = new List<Func<IAsyncEnumerable<object?>, IAsyncEnumerable<object?>>>();
		while (node is MethodCallExpression call && call.Method.DeclaringType == typeof(Queryable))
		{
			var lambda = call.Arguments.Count == 2 ? LinqMethods.StripQuotes(call.Arguments[1]) as LambdaExpression : null;
			switch (call.Method.Name)
			{
				case nameof(Queryable.Where) when lambda is { Parameters.Count: 1 }:
					var predicate = Untyped<bool>(lambda);
					steps.Add(source => WhereAsync(source, predicate));
					break;
				case nameof(Queryable.Select) when lambda is { Parameters.Count: 1 }:
					var selector = Untyped<object?>(lambda);
					steps.Add(source => SelectAsync(source, selector));
					break;
				case nameof(Queryable.Skip) when call.Arguments[1] is ConstantExpression { Value: int skip }:
					steps.Add(source => SkipAsync(source, skip));
					break;
				case nameof(Queryable.Take) when call.Arguments[1] is ConstantExpression { Value: int take }:
					steps.Add(source => TakeAsync(source, take));
					break;
				default:
					return null;
			}

			node = call.Arguments[0];
		}

		if (node is not ConstantExpression { Value: { } value } || !planned.Streams.TryGetValue(value, out var stream))
		{
			return null;
		}

		var items = ((IAsyncRecordStream)stream).ReadObjectsAsync();
		for (var i = steps.Count - 1; i >= 0; i--)
		{
			items = steps[i](items);
		}

		return items;
	}

	/// <summary>
	/// Runs an operator that returns a single value on the results of a <see cref="Tail"/>, asynchronously: <c>Count</c>,
	/// <c>LongCount</c>, <c>Any</c>, <c>First</c>, <c>Single</c>, <c>Last</c> and <c>ElementAt</c> (and their <c>OrDefault</c>
	/// versions), without a predicate (a predicate is a <c>Where</c> before them).
	/// </summary>
	/// <returns>Whether the operator was run, and its value.</returns>
	private static async Task<(bool Handled, TResult Result)> TerminalAsync<TResult>(MethodCallExpression terminal, IAsyncEnumerable<object?> items,
		CancellationToken cancellationToken)
	{
		var name = terminal.Method.Name;
		var index = 0;
		if (name is nameof(Queryable.ElementAt) or nameof(Queryable.ElementAtOrDefault))
		{
			if (terminal.Arguments.Count != 2 || terminal.Arguments[1].Type != typeof(int))
			{
				return (false, default!);
			}

			index = (int)Evaluate(terminal.Arguments[1])!;
		}
		else if (terminal.Arguments.Count != 1)
		{
			return (false, default!);
		}

		var orDefault = name.EndsWith("OrDefault", StringComparison.Ordinal);
		object? result;
		switch (name)
		{
			case nameof(Queryable.Count):
			case nameof(Queryable.LongCount):
				var count = 0L;
				await foreach (var _ in items.WithCancellation(cancellationToken).ConfigureAwait(false))
				{
					count++;
				}

				if (name == nameof(Queryable.Count))
				{
					result = checked((int)count);
				}
				else
				{
					result = count;
				}

				break;
			case nameof(Queryable.Any):
				result = (await FirstAsync(items, 1, cancellationToken).ConfigureAwait(false)).Count > 0;
				break;
			case nameof(Queryable.First):
			case nameof(Queryable.FirstOrDefault):
			case nameof(Queryable.Single):
			case nameof(Queryable.SingleOrDefault):
				var single = name.StartsWith("Single", StringComparison.Ordinal);
				var found = await FirstAsync(items, single ? 2 : 1, cancellationToken).ConfigureAwait(false);
				if (found.Count > 1)
				{
					throw new InvalidOperationException("Sequence contains more than one element");
				}

				if (found.Count == 0)
				{
					return orDefault ? (true, default!) : throw new InvalidOperationException("Sequence contains no elements");
				}

				result = found[0];
				break;
			case nameof(Queryable.Last):
			case nameof(Queryable.LastOrDefault):
				var any = false;
				result = null;
				await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
				{
					any = true;
					result = item;
				}

				if (!any)
				{
					return orDefault ? (true, default!) : throw new InvalidOperationException("Sequence contains no elements");
				}

				break;
			case nameof(Queryable.ElementAt):
			case nameof(Queryable.ElementAtOrDefault):
				var at = index < 0 ? [] : await FirstAsync(SkipAsync(items, index), 1, cancellationToken).ConfigureAwait(false);
				if (at.Count == 0)
				{
					return orDefault ? (true, default!) : throw new ArgumentOutOfRangeException(nameof(index));
				}

				result = at[0];
				break;
			default:
				return (false, default!);
		}

		return (true, (TResult)result!);
	}

	/// <summary>The first <paramref name="count"/> items, or fewer when there are not that many; reading stops there.</summary>
	private static async Task<List<object?>> FirstAsync(IAsyncEnumerable<object?> items, int count, CancellationToken cancellationToken)
	{
		var found = new List<object?>(count);
		await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			found.Add(item);
			if (found.Count == count)
			{
				break;
			}
		}

		return found;
	}

	private static async IAsyncEnumerable<object?> WhereAsync(IAsyncEnumerable<object?> source, Func<object?, bool> predicate,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			if (predicate(item))
			{
				yield return item;
			}
		}
	}

	private static async IAsyncEnumerable<object?> SelectAsync(IAsyncEnumerable<object?> source, Func<object?, object?> selector,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return selector(item);
		}
	}

	private static async IAsyncEnumerable<object?> SkipAsync(IAsyncEnumerable<object?> source, int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var skipped = 0;
		await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			if (skipped < count)
			{
				skipped++;
				continue;
			}

			yield return item;
		}
	}

	private static async IAsyncEnumerable<object?> TakeAsync(IAsyncEnumerable<object?> source, int count,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		if (count <= 0)
		{
			yield break;
		}

		var taken = 0;
		await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return item;
			if (++taken == count)
			{
				yield break;
			}
		}
	}

	/// <summary>Compiles a lambda of one parameter into a delegate that takes an object.</summary>
	private static Func<object?, TResult> Untyped<TResult>(LambdaExpression lambda)
	{
		var item = Expression.Parameter(typeof(object), "item");
		var body = Expression.Invoke(lambda, Expression.Convert(item, lambda.Parameters[0].Type));

		return Expression.Lambda<Func<object?, TResult>>(Expression.Convert(body, typeof(TResult)), item).Compile();
	}

	/// <summary>The value of an expression that does not depend on lambda parameters, such as a captured variable.</summary>
	private static object? Evaluate(Expression expression) =>
		expression is ConstantExpression constant
			? constant.Value
			: Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();

	/// <summary>
	/// Reads each stream of a planned query into memory, asynchronously, so that LINQ to Objects can run the query on
	/// them synchronously.
	/// </summary>
	private static async Task<Expression> BufferAsync(PlannedQuery planned, CancellationToken cancellationToken)
	{
		foreach (var stream in planned.Streams.Values)
		{
			await ((IAsyncRecordStream)stream).BufferAsync(cancellationToken).ConfigureAwait(false);
		}

		return planned.Expression;
	}
}

/// <summary>A stream of records that can be read asynchronously without knowing the type of the records.</summary>
internal interface IAsyncRecordStream
{
	/// <summary>Whether the stream reads all of its elements into memory the first time it is enumerated.</summary>
	bool Materialize { get; set; }

	/// <summary>Whether the source can be read again.</summary>
	bool CanReread { get; }

	/// <summary>Reads the records into memory asynchronously, once; enumerating the stream returns them afterwards.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>A task that completes when the records are in memory.</returns>
	ValueTask BufferAsync(CancellationToken cancellationToken);

	/// <summary>Reads the records asynchronously, as objects.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The records.</returns>
	IAsyncEnumerable<object?> ReadObjectsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Compares objects by reference (as <c>ReferenceEqualityComparer</c> does on .NET 5 and later).</summary>
internal sealed class IdentityComparer : IEqualityComparer<object>
{
	/// <summary>The instance.</summary>
	public static IdentityComparer Instance { get; } = new();

	/// <summary>Whether two references are the same object.</summary>
	/// <param name="x">One object.</param>
	/// <param name="y">The other.</param>
	/// <returns><see langword="true"/> for the same object.</returns>
	public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

	/// <summary>The hash code of the object's identity.</summary>
	/// <param name="obj">The object.</param>
	/// <returns>Its identity hash code.</returns>
	public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
