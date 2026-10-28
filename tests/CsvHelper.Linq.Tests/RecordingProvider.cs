using System.Collections;

namespace CsvHelper.Linq.Tests;

/// <summary>A row of another provider's table: plain classes, as an ORM would have.</summary>
public sealed class Account
{
	public string Name { get; set; } = "";

	public string Country { get; set; } = "";

	public int Rank { get; set; }

	public Status Status { get; set; }
}

/// <summary>
/// A LINQ provider over a list that records each query it runs, standing in for a database: its queries start from a
/// constant (as most providers' do) or, with <see cref="UseExtensionRoot"/>, from a node of its own that says its provider
/// (as Entity Framework Core 7 and later do). It can refuse queries that use Contains, as a provider that cannot translate
/// them would.
/// </summary>
public sealed class RecordingProvider<T> : IQueryProvider
{
	private readonly List<T> rows;

	public RecordingProvider(IEnumerable<T> rows) => this.rows = rows.ToList();

	/// <summary>The queries run, as text.</summary>
	public List<string> Executed { get; } = [];

	public bool UseExtensionRoot { get; set; }

	public bool RefuseContains { get; set; }

	/// <summary>The table: the query of all the rows.</summary>
	public IQueryable<T> Table => new Query<T>(this, UseExtensionRoot ? new RootExpression(this) : null);

	public IQueryable CreateQuery(Expression expression) =>
		(IQueryable)Activator.CreateInstance(typeof(Query<>).MakeGenericType(LinqElement(expression.Type)), this, expression)!;

	public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new Query<TElement>(this, expression);

	public object? Execute(Expression expression) => Run(expression, e => ((IQueryProvider)rows.AsQueryable()).Execute(e));

	public TResult Execute<TResult>(Expression expression) => Run(expression, e => ((IQueryProvider)rows.AsQueryable()).Execute<TResult>(e));

	private TResult Run<TResult>(Expression expression, Func<Expression, TResult> run)
	{
		var text = expression.ToString();
		if (RefuseContains && text.Contains("Contains"))
		{
			throw new InvalidOperationException("The query could not be translated.");
		}

		Executed.Add(text);

		return run(new ToRows(rows).Visit(expression)!);
	}

	private static Type LinqElement(Type type) => type.GetInterfaces().Concat([type]).First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetGenericArguments()[0];

	/// <summary>A query of the provider.</summary>
	private sealed class Query<TElement> : IOrderedQueryable<TElement>
	{
		private readonly RecordingProvider<T> provider;

		public Query(RecordingProvider<T> provider, Expression? expression)
		{
			this.provider = provider;
			Expression = expression ?? Expression.Constant(this);
		}

		public Type ElementType => typeof(TElement);

		public Expression Expression { get; }

		public IQueryProvider Provider => provider;

		public IEnumerator<TElement> GetEnumerator() => provider.Execute<IEnumerable<TElement>>(Expression).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	/// <summary>The root of the provider's queries as a node of its own, with the provider, like EntityQueryRootExpression.</summary>
	private sealed class RootExpression(RecordingProvider<T> provider) : Expression
	{
		public IQueryProvider QueryProvider { get; } = provider;

		public override ExpressionType NodeType => ExpressionType.Extension;

		public override Type Type => typeof(IQueryable<T>);

		public override string ToString() => $"Table<{typeof(T).Name}>";

		protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
	}

	/// <summary>Replaces the root of a query by the rows, for LINQ to Objects to run it.</summary>
	private sealed class ToRows(List<T> rows) : ExpressionVisitor
	{
		protected override Expression VisitConstant(ConstantExpression node) =>
			node.Value is Query<T> ? Expression.Constant(rows.AsQueryable()) : node;

		protected override Expression VisitExtension(Expression node) =>
			node is RootExpression ? Expression.Constant(rows.AsQueryable()) : base.VisitExtension(node);
	}
}

/// <summary>A node of another provider that does not say which provider it is.</summary>
public sealed class AnonymousRoot : Expression
{
	public IQueryProvider? QueryProvider => null;

	public override ExpressionType NodeType => ExpressionType.Extension;

	public override Type Type => typeof(IQueryable<Account>);

	protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
}
