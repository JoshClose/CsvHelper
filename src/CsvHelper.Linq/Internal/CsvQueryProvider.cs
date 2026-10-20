using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace CsvHelper.Linq;

/// <summary>The provider of CSV queries: <see cref="Queryable"/> operators on a <see cref="CsvQuery{T}"/> create more of them.</summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class CsvQueryProvider : IQueryProvider
{
	private CsvQueryProvider()
	{
	}

	/// <summary>The provider, which keeps no state of its own: the queries hold their sources.</summary>
	public static CsvQueryProvider Instance { get; } = new();

	/// <summary>Creates the query of an expression whose element type is known only at run time.</summary>
	/// <param name="expression">The query, whose type is an <see cref="IQueryable{T}"/>.</param>
	/// <returns>A <see cref="CsvQuery{T}"/> of its element type.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="expression"/> is not a query of a sequence.</exception>
	public IQueryable CreateQuery(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		var elementType = LinqMethods.SequenceElementOf(expression.Type) ??
			throw new ArgumentException($"The expression is a {expression.Type}, not a query of a sequence.", nameof(expression));

		return (IQueryable)Activator.CreateInstance(typeof(ComposedCsvQuery<>).MakeGenericType(elementType), [expression])!;
	}

	/// <summary>Creates the query of an expression.</summary>
	/// <typeparam name="TElement">The type of the elements.</typeparam>
	/// <param name="expression">The query, whose type is an <see cref="IQueryable{T}"/> of <typeparamref name="TElement"/>.</param>
	/// <returns>The query.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="expression"/> is not a query of <typeparamref name="TElement"/>.</exception>
	public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);
		if (!typeof(IQueryable<TElement>).IsAssignableFrom(expression.Type))
		{
			throw new ArgumentException($"The expression is a {expression.Type}, not a query of {typeof(TElement)}.", nameof(expression));
		}

		return new ComposedCsvQuery<TElement>(expression);
	}

	/// <summary>Runs a query of a single value.</summary>
	/// <param name="expression">The query.</param>
	/// <returns>The value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
	public object? Execute(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		return QueryExecutor.Execute(expression);
	}

	/// <summary>Runs a query of a single value.</summary>
	/// <typeparam name="TResult">The type of the value.</typeparam>
	/// <param name="expression">The query.</param>
	/// <returns>The value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
	public TResult Execute<TResult>(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		return QueryExecutor.Execute<TResult>(expression);
	}
}
