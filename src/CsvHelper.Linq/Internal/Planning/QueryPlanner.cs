using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace CsvHelper.Linq;

/// <summary>What the reader of a CSV source does for one place in a query that reads it.</summary>
internal sealed class RecordStreamSpec
{
	/// <summary>The rows to skip without reading them, from the <c>Skip</c> operators right on the source.</summary>
	public long Skip { get; set; }

	/// <summary>The filters (<c>Where</c>) right on the source, after the skip, innermost first.</summary>
	public List<LambdaExpression> Filters { get; } = [];

	/// <summary>The members the filters read; <see langword="null"/> for all of them.</summary>
	public IReadOnlyCollection<string>? FilterMembers { get; set; }

	/// <summary>The members that the rest of the query reads of the records that pass; <see langword="null"/> for all of them.</summary>
	public IReadOnlyCollection<string>? Members { get; set; }
}

/// <summary>A query with its CSV sources replaced by the streams that read them, its plan, and the options it runs with.</summary>
/// <param name="Expression">The query that LINQ to Objects runs.</param>
/// <param name="Plan">The plan.</param>
/// <param name="Options">The options of the source that the query starts from.</param>
/// <param name="Streams">
/// The streams of the planned query (each a <see cref="StreamBase{T}"/>: of the records of a CSV source, or of the results
/// of another provider's query), by the <see cref="EnumerableQuery{T}"/> that stands for them in <paramref name="Expression"/>.
/// </param>
internal sealed record PlannedQuery(Expression Expression, CsvQueryPlan Plan, CsvQueryOptions Options, IReadOnlyDictionary<object, object> Streams);

/// <summary>
/// Plans a CSV query: puts in the queries it refers to by variable, finds the parts that other providers can run on their
/// own, and rewrites it (<see cref="QueryRewriter"/>) for LINQ to Objects to run, with the CSV sources read by streams that
/// run what the reader can run itself.
/// </summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal static class QueryPlanner
{
	/// <summary>Plans a query.</summary>
	/// <param name="query">The query, whose innermost source is a CSV source.</param>
	/// <returns>The query to run, and its plan.</returns>
	public static PlannedQuery Plan(Expression query)
	{
		// A source used twice, such as both sides of a self-join, is one node in two places, which are planned apart.
		var separator = new Separator();
		var prepared = separator.Visit(new Inliner().Visit(query))!;
		var options = OptionsOf(prepared);
		var rewriter = new QueryRewriter(ShapeAnalyzer.Analyze(prepared), options, separator.Shared());
		var rewritten = rewriter.Visit(prepared)!;
		var inMemory = new List<string>();
		new OperatorCollector(inMemory).Visit(rewritten);
		var plan = new CsvQueryPlan(rewriter.Sources, rewriter.Foreign, inMemory);

		return new PlannedQuery(rewritten, plan, options, rewriter.Streams);
	}

	/// <summary>The options of the source that a query starts from: the innermost source of its first arguments.</summary>
	private static CsvQueryOptions OptionsOf(Expression query)
	{
		var node = query;
		while (node is MethodCallExpression { Arguments.Count: > 0 } call)
		{
			node = call.Arguments[0];
		}

		return node is ConstantExpression { Value: ICsvRoot { IsRoot: true } root } ? root.Options : CsvQueryOptions.Default;
	}

	/// <summary>
	/// Gives each place where a CSV source is used its own node, so the planner can tell the places apart, and counts the
	/// places of each source.
	/// </summary>
	private sealed class Separator : ExpressionVisitor
	{
		private readonly Dictionary<object, int> places = new(IdentityComparer.Instance);

		/// <summary>
		/// The sources that can be read once (a caller's reader) and are used in more than one place, with the number of
		/// places: they are read once, into memory, for all of them.
		/// </summary>
		/// <returns>The number of places of each, by source.</returns>
		public Dictionary<object, int> Shared()
		{
			var shared = new Dictionary<object, int>(IdentityComparer.Instance);
			foreach (var place in places)
			{
				if (place.Value > 1 && !((ICsvRoot)place.Key).Source.CanReopen)
				{
					shared.Add(place.Key, place.Value);
				}
			}

			return shared;
		}

		/// <summary>Copies the constant of a CSV source, and counts the place.</summary>
		/// <param name="node">The constant.</param>
		/// <returns>A new constant of the source, or the node.</returns>
		protected override Expression VisitConstant(ConstantExpression node)
		{
			if (node.Value is not ICsvRoot { IsRoot: true } root)
			{
				return node;
			}

			places[root] = places.TryGetValue(root, out var count) ? count + 1 : 1;

			return Expression.Constant(node.Value, node.Type);
		}
	}

	/// <summary>Collects the names of the <see cref="Queryable"/> operators left in a planned query, which LINQ to Objects runs.</summary>
	private sealed class OperatorCollector : ExpressionVisitor
	{
		private readonly List<string> names;

		/// <summary>Creates the collector.</summary>
		/// <param name="names">The list to add the names to, innermost first.</param>
		public OperatorCollector(List<string> names) => this.names = names;

		/// <summary>Collects a call after its arguments.</summary>
		/// <param name="node">The call.</param>
		/// <returns>The call, unchanged.</returns>
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			var visited = base.VisitMethodCall(node);
			if (node.Method.DeclaringType == typeof(Queryable))
			{
				names.Add(node.Method.Name);
			}

			return visited;
		}
	}
}
