using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>A query of another provider that a CSV query runs: its provider, and its expression.</summary>
internal interface IForeignQuery
{
	/// <summary>The provider that runs the query.</summary>
	IQueryProvider Provider { get; }

	/// <summary>The query.</summary>
	Expression Expression { get; }
}

/// <summary>
/// The results of a query of another provider, which runs once, on that provider, the first time the stream is enumerated
/// (asynchronously when it is buffered for an asynchronous query and the provider's query is an
/// <see cref="IAsyncEnumerable{T}"/>), and is kept for the rest of the run of the CSV query, so that a query inside a lambda
/// is not run again for each record.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class ForeignSequence<T> : StreamBase<T>, IForeignQuery
{
	/// <summary>Creates the stream.</summary>
	/// <param name="provider">The provider that runs the query.</param>
	/// <param name="expression">The query.</param>
	public ForeignSequence(IQueryProvider provider, Expression expression)
	{
		Provider = provider;
		Expression = expression;
	}

	/// <summary>The provider that runs the query.</summary>
	public IQueryProvider Provider { get; }

	/// <summary>The query.</summary>
	public Expression Expression { get; }

	/// <summary>Runs the query and keeps its results.</summary>
	/// <returns>An enumerator of the results.</returns>
	protected override IEnumerator<T> Read()
	{
		var results = Provider.CreateQuery<T>(Expression).ToList();
		Buffer = results;

		return results.GetEnumerator();
	}

	/// <summary>Runs the query asynchronously when the provider can, and keeps its results.</summary>
	/// <param name="cancellationToken">Cancels the query.</param>
	/// <returns>An enumerator of the results.</returns>
	protected override async IAsyncEnumerator<T> ReadAsync(CancellationToken cancellationToken)
	{
		var query = Provider.CreateQuery<T>(Expression);
		var results = new List<T>();
		if (query is IAsyncEnumerable<T> asynchronous)
		{
			await foreach (var item in asynchronous.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				results.Add(item);
			}
		}
		else
		{
			results.AddRange(query);
		}

		Buffer = results;
		foreach (var item in results)
		{
			yield return item;
		}
	}
}

/// <summary>The value of a query of another provider (such as a count or an average), which runs once, when it is first needed.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class ForeignValue<T> : IForeignQuery
{
	private readonly Lazy<T> value;

	/// <summary>Creates the value.</summary>
	/// <param name="provider">The provider that runs the query.</param>
	/// <param name="expression">The query.</param>
	public ForeignValue(IQueryProvider provider, Expression expression)
	{
		Provider = provider;
		Expression = expression;
		value = new Lazy<T>(() => provider.Execute<T>(expression));
	}

	/// <summary>The provider that runs the query.</summary>
	public IQueryProvider Provider { get; }

	/// <summary>The query.</summary>
	public Expression Expression { get; }

	/// <summary>The value, from the query the first time it is read.</summary>
	public T Value => value.Value;
}

/// <summary>
/// The results of a query of another provider as a set, for <c>Contains</c>: the query runs once, when the set is first
/// used, and each lookup is a hash lookup.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class ForeignSet<T>
{
	private readonly ForeignSequence<T> sequence;
	private HashSet<T>? set;

	/// <summary>Creates the set.</summary>
	/// <param name="sequence">The query of its elements.</param>
	public ForeignSet(ForeignSequence<T> sequence) => this.sequence = sequence;

	/// <summary>Whether the query returns <paramref name="value"/>.</summary>
	/// <param name="value">The value to look for.</param>
	/// <returns><see langword="true"/> when the query returns it.</returns>
	public bool Contains(T value) => (set ??= [.. sequence]).Contains(value);
}

/// <summary>
/// A join of records with the results of another provider's query, which asks the provider only for the rows whose keys
/// the records have: the outer records are read first for their keys (a second time for the join, when their sources can
/// be read again, else kept in memory), and their distinct keys are sent to the provider in batches, as
/// <c>Where(row =&gt; keys.Contains(key(row)))</c>, which a database runs as an <c>IN</c> list. With more keys than
/// <see cref="CsvQueryOptions.MaxForeignKeys"/>, or when the provider cannot run that query, the provider's query is read
/// whole, once, instead.
/// </summary>
/// <typeparam name="TOuter">The type of the outer records.</typeparam>
/// <typeparam name="TInner">The type of the provider's rows.</typeparam>
/// <typeparam name="TKey">The type of the keys.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class KeyedJoin<TOuter, TInner, TKey>
{
	private static readonly MethodInfo Where = new Func<IQueryable<TInner>, Expression<Func<TInner, bool>>, IQueryable<TInner>>(Queryable.Where).Method;

	private readonly IEnumerable<TOuter> outer;
	private readonly bool reread;
	private readonly Func<TOuter, TKey> outerKey;
	private readonly IForeignQuery inner;
	private readonly LambdaExpression innerKey;
	private readonly Func<TInner, TKey> innerKeyOf;
	private readonly CsvQueryOptions options;
	private List<TOuter>? buffered;

	/// <summary>Creates the join.</summary>
	/// <param name="outer">The outer records: the query of the CSV side.</param>
	/// <param name="reread">Whether the outer records can be read twice, rather than kept in memory.</param>
	/// <param name="outerKey">The key of an outer record.</param>
	/// <param name="inner">The provider's query.</param>
	/// <param name="innerKey">The key of a row of the provider's query, which the provider evaluates.</param>
	/// <param name="options">The batch size and the most keys to send.</param>
	public KeyedJoin(IEnumerable<TOuter> outer, bool reread, LambdaExpression outerKey, IForeignQuery inner, LambdaExpression innerKey, CsvQueryOptions options)
	{
		this.outer = outer;
		this.reread = reread;
		this.outerKey = (Func<TOuter, TKey>)outerKey.Compile();
		this.inner = inner;
		this.innerKey = innerKey;
		innerKeyOf = (Func<TInner, TKey>)innerKey.Compile();
		this.options = options;
	}

	/// <summary>The outer records, for the join.</summary>
	public IEnumerable<TOuter> Outer => reread ? outer : Buffered();

	/// <summary>The rows of the provider's query that have keys of the outer records, for the join.</summary>
	public IEnumerable<TInner> Inner => Fetch();

	private IEnumerable<TOuter> Buffered()
	{
		foreach (var record in buffered ??= outer.ToList())
		{
			yield return record;
		}
	}

	private IEnumerable<TInner> Fetch()
	{
		var keys = new HashSet<TKey>();
		foreach (var record in reread ? outer : buffered ??= outer.ToList())
		{
			// Null keys never match in a join.
			if (outerKey(record) is { } key)
			{
				keys.Add(key);
			}
		}

		var rows = keys.Count == 0
			? new List<TInner>()
			: keys.Count > options.MaxForeignKeys ? All() : Batches(keys.ToList());
		foreach (var row in rows)
		{
			yield return row;
		}
	}

	/// <summary>The whole query of the provider.</summary>
	private List<TInner> All() => inner.Provider.CreateQuery<TInner>(inner.Expression).ToList();

	/// <summary>
	/// The rows of each batch of keys. Each batch keeps only the rows whose key is one of its own, so a provider that matches
	/// more loosely (such as a database that ignores case, or the per-member lists of composite keys) adds no row twice.
	/// </summary>
	private List<TInner> Batches(List<TKey> keys)
	{
		var rows = new List<TInner>();
		for (var start = 0; start < keys.Count; start += options.ForeignKeyBatchSize)
		{
			var batch = keys.GetRange(start, Math.Min(options.ForeignKeyBatchSize, keys.Count - start));
			var exact = new HashSet<TKey>(batch);
			List<TInner> fetched;
			try
			{
				fetched = inner.Provider.CreateQuery<TInner>(Filtered(batch)).ToList();
			}
			catch (Exception e) when (start == 0 && e is InvalidOperationException or NotSupportedException)
			{
				// The provider cannot run the filter, such as a key that it cannot translate: read the query whole.
				return All();
			}

			rows.AddRange(fetched.Where(row => innerKeyOf(row) is { } key && exact.Contains(key)));
		}

		return rows;
	}

	/// <summary>
	/// The provider's query with the filter of a batch of keys: <c>keys.Contains(key(row))</c>, or for a composite key (an
	/// anonymous type), each of its members in the list of the batch's values of that member.
	/// </summary>
	private Expression Filtered(List<TKey> batch)
	{
		var row = innerKey.Parameters[0];
		Expression condition;
		if (innerKey.Body is NewExpression { Members: { } members } created && created.Arguments.Count > 0)
		{
			condition = null!;
			for (var i = 0; i < created.Arguments.Count; i++)
			{
				var member = members[i];
				var memberType = created.Arguments[i].Type;
				var values = batch.Select(k => ValueOf(member, k)).Where(v => v is not null).Distinct().ToList();
				var contains = ContainsOf(memberType, values, created.Arguments[i]);
				condition = i == 0 ? contains : Expression.AndAlso(condition, contains);
			}
		}
		else
		{
			condition = ContainsOf(typeof(TKey), batch.Cast<object?>().ToList(), innerKey.Body);
		}

		var predicate = Expression.Lambda<Func<TInner, bool>>(condition, row);

		return Expression.Call(Where, inner.Expression, Expression.Quote(predicate));
	}

	private static object? ValueOf(MemberInfo member, TKey key) => member switch
	{
		PropertyInfo property => property.GetValue(key),
		MethodInfo getter => getter.Invoke(key, null),
		FieldInfo field => field.GetValue(key),
		_ => null,
	};

	/// <summary>
	/// <c>values.Contains(key)</c>, with the values in a field of a holder object, which providers such as Entity Framework
	/// Core send as a parameter rather than as constants in the query text.
	/// </summary>
	private static Expression ContainsOf(Type type, List<object?> values, Expression key)
	{
		var array = Array.CreateInstance(type, values.Count);
		for (var i = 0; i < values.Count; i++)
		{
			array.SetValue(values[i], i);
		}

		var holder = Activator.CreateInstance(typeof(KeyHolder<>).MakeGenericType(type), array)!;
		var keys = Expression.Field(Expression.Constant(holder), nameof(KeyHolder<object>.Keys));
		var contains = typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2).MakeGenericMethod(type);

		return Expression.Call(contains, keys, key);
	}
}

/// <summary>Holds the keys of a batch in a field, as a variable would.</summary>
/// <typeparam name="T">The type of the keys.</typeparam>
internal sealed class KeyHolder<T>
{
	/// <summary>Creates the holder.</summary>
	/// <param name="keys">The keys.</param>
	public KeyHolder(T[] keys) => Keys = keys;

	/// <summary>The keys, which providers read as a parameter.</summary>
#pragma warning disable CA1051 // A field, not a property: a captured variable is a field, which providers know to send as a parameter.
	public readonly T[] Keys;
#pragma warning restore CA1051
}
