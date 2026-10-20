using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Linq;

/// <summary>
/// Rewrites a CSV query for LINQ to Objects to run: each CSV source, with the operators right on it that the reader runs,
/// becomes the stream of its records; each part that another provider can run on its own becomes the results of that
/// provider's query (run once), a set of them for <c>Contains</c>, or the rows with the keys of the CSV records for a join.
/// </summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class QueryRewriter : ExpressionVisitor
{
	private readonly IReadOnlyDictionary<Expression, Shape> shapes;
	private readonly CsvQueryOptions options;
	private readonly Dictionary<IForeignQuery, int> foreignIndexes = new(IdentityComparer.Instance);
	private Chain? chain;

	/// <summary>Creates the rewriter.</summary>
	/// <param name="shapes">The shapes of the query's nodes.</param>
	/// <param name="options">The options of the query: how it joins with other providers.</param>
	public QueryRewriter(IReadOnlyDictionary<Expression, Shape> shapes, CsvQueryOptions options)
	{
		this.shapes = shapes;
		this.options = options;
	}

	/// <summary>The plans of the CSV sources, in the order they were replaced.</summary>
	public List<CsvSourcePlan> Sources { get; } = [];

	/// <summary>The plans of the queries of other providers.</summary>
	public List<CsvForeignQueryPlan> Foreign { get; } = [];

	/// <summary>The streams of the planned query, by the queryable that stands for each in it.</summary>
	public Dictionary<object, object> Streams { get; } = new(IdentityComparer.Instance);

	/// <summary>Visits a node with its link in the chain of ancestors, replacing it when another provider can run all of it.</summary>
	/// <param name="node">The node.</param>
	/// <returns>The node, or what replaces it.</returns>
	public override Expression? Visit(Expression? node)
	{
		if (node is null)
		{
			return null;
		}

		var saved = chain;
		chain = new Chain(node, saved);
		try
		{
			return ReplaceForeign(node) ?? base.Visit(node);
		}
		finally
		{
			chain = saved;
		}
	}

	/// <summary>
	/// Replaces <c>Contains</c> and <c>Any</c> of another provider's query by a set lookup, the operators that the reader runs
	/// with their source by its stream, and the provider's side of a join by the rows with the keys of the other side.
	/// </summary>
	/// <param name="node">The call.</param>
	/// <returns>What replaces the call, or the call with its arguments visited.</returns>
	protected override Expression VisitMethodCall(MethodCallExpression node)
	{
		if (ForeignLookup(node) is { } lookup)
		{
			return lookup;
		}

		if (Fused(node) is { } fused && Replaceable(chain!))
		{
			return Replace(fused.Root, fused.Operators);
		}

		// The reader runs a projection of dynamic rows too, when it reads them by column.
		if (IsProjection(node) && SourceOf(node.Arguments[0]) is { } source && IsDynamic(source.Root) && Replaceable(chain!) &&
			TryReplaceDynamic(source.Root, source.Operators, node, null) is { } projected)
		{
			return projected;
		}

		var visited = base.VisitMethodCall(node);

		return visited is MethodCallExpression call && KeyedJoin(call) is { } joined ? joined : visited;
	}

	/// <summary>Replaces a CSV source by the stream of its records.</summary>
	/// <param name="node">The constant.</param>
	/// <returns>The stream, or the constant.</returns>
	protected override Expression VisitConstant(ConstantExpression node)
	{
		if (node.Value is ICsvRoot { IsRoot: true } root && Replaceable(chain!))
		{
			return Replace(root, []);
		}

		return node;
	}

	/// <summary>
	/// Whether a sequence can be replaced by a stream typed <see cref="IQueryable{T}"/>: at the top of the query, or as an
	/// argument of a LINQ operator, which takes any <see cref="IQueryable{T}"/>.
	/// </summary>
	private static bool Replaceable(Chain at) =>
		at.Parent is null || at.Parent.Node is MethodCallExpression call && LinqMethods.IsLinq(call.Method) && call.Arguments.Contains(at.Node);

	/// <summary>Whether a node is inside a lambda, where it runs for each element of something else.</summary>
	private static bool InLambda(Chain at)
	{
		for (var link = at.Parent; link is not null; link = link.Parent)
		{
			if (link.Node is LambdaExpression)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>The source and the operators right on it that the reader runs, if <paramref name="node"/> is the outermost of them.</summary>
	private (ICsvRoot Root, List<MethodCallExpression> Operators)? Fused(MethodCallExpression node)
	{
		var operators = new List<MethodCallExpression>();
		Expression current = node;
		var filtering = true;
		while (current is MethodCallExpression call)
		{
			if (filtering && IsFilter(call))
			{
				operators.Add(call);
			}
			else if (IsRowSkip(call))
			{
				// Skips come right on the source, before the filters: a Skip after a Where counts records that pass.
				filtering = false;
				operators.Add(call);
			}
			else
			{
				return null;
			}

			current = call.Arguments[0];
		}

		if (current is not ConstantExpression { Value: ICsvRoot { IsRoot: true } root })
		{
			return null;
		}

		operators.Reverse();

		return (root, operators);
	}

	private bool IsFilter(MethodCallExpression call) =>
		LinqMethods.IsQueryable(call.Method, nameof(Queryable.Where)) && LinqMethods.StripQuotes(call.Arguments[1]) is LambdaExpression { Parameters.Count: 1 } &&
		Closed(call.Arguments[1]);

	private static bool IsRowSkip(MethodCallExpression call) =>
		LinqMethods.IsQueryable(call.Method, nameof(Queryable.Skip)) && call.Arguments[1] is ConstantExpression { Value: int };

	private bool IsProjection(MethodCallExpression call) =>
		LinqMethods.IsQueryable(call.Method, nameof(Queryable.Select)) && LinqMethods.StripQuotes(call.Arguments[1]) is LambdaExpression { Parameters.Count: 1 } &&
		Closed(call.Arguments[1]);

	/// <summary>
	/// Whether a lambda uses only its own parameters, so the reader can run it on its own: a filter inside another lambda
	/// that uses that lambda's parameters runs in memory, for each of its elements.
	/// </summary>
	private bool Closed(Expression lambda) => shapes.TryGetValue(lambda, out var shape) && !shape.HasFree;

	/// <summary>The source of a sequence and the operators on it that the reader runs, when the reader runs all of it.</summary>
	private (ICsvRoot Root, List<MethodCallExpression> Operators)? SourceOf(Expression sequence) => sequence switch
	{
		MethodCallExpression call => Fused(call),
		ConstantExpression { Value: ICsvRoot { IsRoot: true } root } => (root, []),
		_ => null,
	};

	/// <summary>Whether a source reads dynamic rows by column: <see cref="CsvDynamicRecord"/> rows of a CSV with a header.</summary>
	private static bool IsDynamic(ICsvRoot root) =>
		root.ElementType == typeof(CsvDynamicRecord) && root.ReaderOptions.HasHeader && root.Options.PruneColumns;

	/// <summary>
	/// The lambda of an operator that the reader runs, with the queries inside it planned: CSV sources and queries of other
	/// providers in it run once, not for each record.
	/// </summary>
	private LambdaExpression PlannedLambda(MethodCallExpression call)
	{
		var saved = chain;
		chain = new Chain(call, saved);
		try
		{
			return (LambdaExpression)LinqMethods.StripQuotes(Visit(call.Arguments[1])!);
		}
		finally
		{
			chain = saved;
		}
	}

	/// <summary>Plans the reader of a source and replaces it, and the operators it runs, by the stream of its records.</summary>
	private Expression Replace(ICsvRoot root, List<MethodCallExpression> operators)
	{
		var dynamicNotes = new List<string>();
		if (IsDynamic(root) && TryReplaceDynamic(root, operators, null, dynamicNotes) is { } dynamic)
		{
			return dynamic;
		}

		// The source's plan comes before those of the sources in its filters, which are planned now.
		var index = Sources.Count;
		Sources.Add(null!);
		var spec = new RecordStreamSpec();
		var pushed = new List<string>();
		var originals = new List<LambdaExpression>();
		foreach (var call in operators)
		{
			if (call.Method.Name == nameof(Queryable.Skip))
			{
				spec.Skip += Math.Max((int)((ConstantExpression)call.Arguments[1]).Value!, 0);
				pushed.Add($"Skip({((ConstantExpression)call.Arguments[1]).Value})");
			}
			else
			{
				originals.Add((LambdaExpression)LinqMethods.StripQuotes(call.Arguments[1]));
				spec.Filters.Add(PlannedLambda(call));
				pushed.Add("Where");
			}
		}

		var notes = dynamicNotes;
		var recordType = root.ElementType;
		var members = Members(root, notes);
		if (members is not null)
		{
			// The filters read their members for every row; the rest of the query, those it uses of the records that pass.
			var filterMembers = new SortedSet<string>(StringComparer.Ordinal);
			foreach (var filter in originals)
			{
				var usage = MemberUsageAnalyzer.OfParameter(new Chain(filter, null), filter, 0, recordType, members);
				if (usage.Selection is null)
				{
					notes.Add($"the filter reads all members: {usage.Escape}");
					filterMembers = null;
					break;
				}

				filterMembers.UnionWith(usage.Members);
			}

			spec.FilterMembers = filterMembers;
			var rest = MemberUsageAnalyzer.OfSequence(chain!, recordType, members);
			spec.Members = rest.Selection;
			if (rest.Escape is not null)
			{
				notes.Add($"reads all members: {rest.Escape}");
			}
		}

		Sources[index] = new CsvSourcePlan(root.Source.Description, recordType, spec.Skip, pushed,
			spec.Filters.Count == 0 ? [] : spec.FilterMembers?.ToList(), spec.Members?.ToList(), notes);

		return Register((IAsyncRecordStream)root.CreateStream(spec), recordType);
	}

	/// <summary>
	/// Puts a stream in the query: as the <see cref="EnumerableQuery{T}"/> that LINQ to Objects runs the rest of the query on.
	/// A stream inside a lambda is read once and kept, so it is not read again for each element of what the lambda runs on.
	/// </summary>
	private Expression Register(IAsyncRecordStream stream, Type elementType)
	{
		stream.Materialize = InLambda(chain!);
		var queryable = ((IEnumerable)stream).AsQueryable();
		Streams.Add(queryable, stream);

		return Expression.Constant(queryable, typeof(IQueryable<>).MakeGenericType(elementType));
	}

	/// <summary>
	/// Plans the reader of a source of dynamic rows that runs the filters on it, and the projection after them, on its
	/// records by column (see <see cref="DynamicRows"/>), and replaces them by its stream; <see langword="null"/> when a
	/// lambda uses the rows otherwise, and the rows are made and given to it.
	/// </summary>
	/// <param name="root">The source.</param>
	/// <param name="operators">The skips and filters right on the source.</param>
	/// <param name="projection">The <c>Select</c> on them, or <see langword="null"/> to return the rows.</param>
	/// <param name="notes">Where to say why the reader cannot run a filter; <see langword="null"/> for a projection that is left to LINQ to Objects.</param>
	/// <returns>The stream, or <see langword="null"/>.</returns>
	private Expression? TryReplaceDynamic(ICsvRoot root, List<MethodCallExpression> operators, MethodCallExpression? projection, List<string>? notes)
	{
		var slots = new List<string>();
		var reader = Expression.Parameter(typeof(CsvReader), "reader");
		var columns = Expression.Parameter(typeof(int[]), "columns");
		// Whether the reader can run the lambdas is decided first, on the lambdas as written, so the queries inside them are
		// planned once, by this method or by the one that makes the rows.
		foreach (var call in operators.Where(c => c.Method.Name != nameof(Queryable.Skip)).Concat(projection is null ? [] : [projection]))
		{
			if (DynamicRows.Rewrite((LambdaExpression)LinqMethods.StripQuotes(call.Arguments[1]), [], reader, columns, [], null, out var reason) is null)
			{
				if (call != projection)
				{
					notes?.Add($"the filter reads whole rows: {reason}");
				}

				return null;
			}
		}

		var index = Sources.Count;
		Sources.Add(null!);
		var skip = 0L;
		var pushed = new List<string>();
		var filterColumns = new SortedSet<string>(StringComparer.Ordinal);
		var filters = new List<Func<CsvReader, int[], bool>>();
		foreach (var call in operators)
		{
			if (call.Method.Name == nameof(Queryable.Skip))
			{
				skip += Math.Max((int)((ConstantExpression)call.Arguments[1]).Value!, 0);
				pushed.Add($"Skip({((ConstantExpression)call.Arguments[1]).Value})");
				continue;
			}

			var filter = DynamicRows.Rewrite(PlannedLambda(call), slots, reader, columns, filterColumns, root.ReaderOptions.NullValues, out _)!;
			filters.Add((Func<CsvReader, int[], bool>)filter.Compile());
			pushed.Add("Where");
		}

		Delegate? selector = null;
		SortedSet<string>? selected = null;
		var elementType = typeof(CsvDynamicRecord);
		if (projection is not null)
		{
			selected = new SortedSet<string>(StringComparer.Ordinal);
			var rewritten = DynamicRows.Rewrite(PlannedLambda(projection), slots, reader, columns, selected, root.ReaderOptions.NullValues, out _)!;
			selector = rewritten.Compile();
			elementType = rewritten.ReturnType;
			pushed.Add("Select");
		}

		Sources[index] = new CsvSourcePlan(root.Source.Description, root.ElementType, skip, pushed, filterColumns.ToList(), selected?.ToList(), notes ?? []);
		var stream = (IAsyncRecordStream)Activator.CreateInstance(
			typeof(DynamicStream<>).MakeGenericType(elementType), root, skip, slots.ToArray(), filters.ToArray(), selector)!;

		return Register(stream, elementType);
	}

	/// <summary>The members that the source's mapper can read on their own, or <see langword="null"/> when it reads all of them.</summary>
	private static IReadOnlyList<string>? Members(ICsvRoot root, List<string> notes)
	{
		if (!root.Options.PruneColumns)
		{
			notes.Add("PruneColumns is off");

			return null;
		}

		if (root.ElementType == typeof(CsvDynamicRecord))
		{
			if (!root.ReaderOptions.HasHeader)
			{
				notes.Add("rows without a header are read whole");
			}

			return null;
		}

		if (root.Members is null)
		{
			notes.Add($"{root.ElementType.Name} has no projections: its assembly does not reference CsvHelper.Linq or have [assembly: CsvRecordProjections]");

			return null;
		}

		return root.Members;
	}

	/// <summary>
	/// Replaces a part of the query that another provider can run on its own (its query's source, or LINQ operators on it
	/// that use nothing from outside it) by the results of the provider's query, run once: a sequence, or a value such as a
	/// count.
	/// </summary>
	private Expression? ReplaceForeign(Expression node)
	{
		if (ForeignRoots.IsRoot(node, out var rootProvider) && rootProvider is null)
		{
			throw new NotSupportedException(
				$"The query uses a query of another provider ({node.Type}) whose provider cannot be found. Read it into a list first (ToList), " +
				"and use the list in the CSV query.");
		}

		if (!shapes.TryGetValue(node, out var shape) || !shape.IsForeign || !IsForeignQuery(node))
		{
			return null;
		}

		var provider = shape.Provider!;
		var element = LinqMethods.ElementOf(node.Type) ?? (node is MethodCallExpression ? null : LinqMethods.SequenceElementOf(node.Type));
		if (element is not null)
		{
			if (!Replaceable(chain!))
			{
				return null;
			}

			var sequence = (IAsyncRecordStream)Activator.CreateInstance(typeof(ForeignSequence<>).MakeGenericType(element), provider, node)!;
			AddForeign((IForeignQuery)sequence, "once");

			return Register(sequence, element);
		}

		if (node is not MethodCallExpression { Method.DeclaringType: { } declaring } || declaring != typeof(Queryable))
		{
			return null;
		}

		var value = (IForeignQuery)Activator.CreateInstance(typeof(ForeignValue<>).MakeGenericType(node.Type), provider, node)!;
		AddForeign(value, "value");

		return Expression.Property(Expression.Constant(value), nameof(ForeignValue<object>.Value));
	}

	/// <summary>
	/// Whether a node is a query that a provider runs: its source, a <see cref="Queryable"/> operator, or another provider's
	/// own operator on it (such as <c>Include</c>) that returns a query.
	/// </summary>
	private bool IsForeignQuery(Expression node) => node switch
	{
		MethodCallExpression call when call.Method.DeclaringType == typeof(Queryable) => true,
		MethodCallExpression call => call.Method.IsStatic && typeof(IQueryable).IsAssignableFrom(call.Type) && call.Arguments.Count > 0 &&
									 shapes.TryGetValue(call.Arguments[0], out var source) && source.IsForeign,
		_ => ForeignRoots.IsRoot(node, out _),
	};

	/// <summary>
	/// Replaces <c>query.Contains(value)</c> and <c>query.Any(row =&gt; row.Key == value &amp;&amp; ...)</c>, where another
	/// provider runs the query on its own, by a lookup in the set of its results (or of its keys, which the provider is asked
	/// for alone, with the conditions on the row alone), run once.
	/// </summary>
	private Expression? ForeignLookup(MethodCallExpression node)
	{
		if (!LinqMethods.IsLinq(node.Method) || node.Arguments.Count != 2 || !shapes.TryGetValue(node.Arguments[0], out var shape) || !shape.IsForeign)
		{
			return null;
		}

		var provider = shape.Provider!;
		var element = node.Method.GetGenericArguments()[0];
		if (node.Method.Name == nameof(Queryable.Contains))
		{
			return Lookup(provider, node.Arguments[0], element, node.Arguments[1]);
		}

		if (node.Method.Name != nameof(Queryable.Any) || LinqMethods.StripQuotes(node.Arguments[1]) is not LambdaExpression { Parameters.Count: 1 } predicate)
		{
			return null;
		}

		// One condition compares a key of the row with a value from outside; the others use the row alone.
		var row = predicate.Parameters[0];
		var conditions = Conjuncts(predicate.Body).ToList();
		BinaryExpression? match = null;
		Expression? key = null;
		Expression? value = null;
		foreach (var condition in conditions)
		{
			if (condition is BinaryExpression { NodeType: ExpressionType.Equal } equal && (equal.Method is null || equal.Method.DeclaringType == typeof(string)) &&
				IsSimple(equal.Left.Type) && match is null)
			{
				var leftOnRow = UsesOnly(equal.Left, row);
				var rightOnRow = UsesOnly(equal.Right, row);
				var leftFree = !Uses(equal.Left, row);
				var rightFree = !Uses(equal.Right, row);
				if (leftOnRow && rightFree)
				{
					(match, key, value) = (equal, equal.Left, equal.Right);
					continue;
				}

				if (rightOnRow && leftFree)
				{
					(match, key, value) = (equal, equal.Right, equal.Left);
					continue;
				}
			}

			if (!UsesOnly(condition, row))
			{
				return null;
			}
		}

		if (match is null)
		{
			return null;
		}

		Expression keys = node.Arguments[0];
		var rest = conditions.Where(c => c != match).ToList();
		if (rest.Count > 0)
		{
			var where = Expression.Lambda(rest.Aggregate(Expression.AndAlso), row);
			keys = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [element], keys, Expression.Quote(where));
		}

		keys = Expression.Call(typeof(Queryable), nameof(Queryable.Select), [element, key!.Type], keys, Expression.Quote(Expression.Lambda(key, row)));

		return Lookup(provider, keys, key.Type, value!);
	}

	/// <summary>The lookup of <paramref name="value"/> in the set of the results of a provider's query.</summary>
	private Expression Lookup(IQueryProvider provider, Expression query, Type element, Expression value)
	{
		var sequence = (IAsyncRecordStream)Activator.CreateInstance(typeof(ForeignSequence<>).MakeGenericType(element), provider, query)!;
		Streams.Add(sequence, sequence);
		AddForeign((IForeignQuery)sequence, "set");
		var set = Activator.CreateInstance(typeof(ForeignSet<>).MakeGenericType(element), sequence)!;

		return Expression.Call(Expression.Constant(set), set.GetType().GetMethod(nameof(ForeignSet<object>.Contains))!, Visit(value)!);
	}

	/// <summary>
	/// Replaces the inner side of a join with another provider's query by the rows that have the keys of the outer side
	/// (<see cref="KeyedJoin{TOuter, TInner, TKey}"/>), for <c>Join</c>, <c>GroupJoin</c> and <c>LeftJoin</c>, where inner rows
	/// without a match never appear in the result.
	/// </summary>
	private Expression? KeyedJoin(MethodCallExpression call)
	{
		if (call.Method.DeclaringType != typeof(Queryable) || call.Method.Name is not ("Join" or "GroupJoin" or "LeftJoin") || call.Arguments.Count != 5 ||
			options.MaxForeignKeys == 0 || call.Arguments[1] is not ConstantExpression { Value: { } innerValue } ||
			!Streams.TryGetValue(innerValue, out var stream) || stream is not IForeignQuery foreign)
		{
			return null;
		}

		var types = call.Method.GetGenericArguments();
		var outerExpression = call.Arguments[0];
		var outer = outerExpression is ConstantExpression { Value: IQueryable queryable } ? queryable : QueryExecutor.InMemory(outerExpression);
		var join = Activator.CreateInstance(typeof(KeyedJoin<,,>).MakeGenericType(types[0], types[1], types[2]), outer, CanReread(outerExpression),
			LinqMethods.StripQuotes(call.Arguments[2]), foreign, LinqMethods.StripQuotes(call.Arguments[3]), options)!;
		var outerSide = ((IEnumerable)join.GetType().GetProperty("Outer")!.GetValue(join)!).AsQueryable();
		var innerSide = ((IEnumerable)join.GetType().GetProperty("Inner")!.GetValue(join)!).AsQueryable();
		// The provider's query is not read whole any more, so asynchronous queries must not buffer it.
		Streams.Remove(innerValue);
		Foreign[foreignIndexes[foreign]] = new CsvForeignQueryPlan(foreign.Expression.ToString(), foreign.Provider.GetType(), "keys");

		return call.Update(null, [
			Expression.Constant(outerSide, typeof(IQueryable<>).MakeGenericType(types[0])),
			Expression.Constant(innerSide, typeof(IQueryable<>).MakeGenericType(types[1])),
			call.Arguments[2],
			call.Arguments[3],
			call.Arguments[4],
		]);
	}

	/// <summary>Whether the streams in a planned sequence can all be read again (a source that is not a caller's reader, or a provider's results).</summary>
	private bool CanReread(Expression sequence)
	{
		var finder = new StreamFinder(Streams);
		finder.Visit(sequence);

		return finder.Found.All(s => s.CanReread);
	}

	private void AddForeign(IForeignQuery query, string strategy)
	{
		foreignIndexes[query] = Foreign.Count;
		Foreign.Add(new CsvForeignQueryPlan(query.Expression.ToString(), query.Provider.GetType(), strategy));
	}

	private static IEnumerable<Expression> Conjuncts(Expression condition) =>
		condition is BinaryExpression { NodeType: ExpressionType.AndAlso } and ? Conjuncts(and.Left).Concat(Conjuncts(and.Right)) : [condition];

	/// <summary>Whether a type compares the same way with <c>==</c> as in a hash set: numbers, strings, dates, Booleans, enums and their nullables.</summary>
	private static bool IsSimple(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;

		return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
			   type == typeof(Guid) || type == typeof(TimeSpan);
	}

	/// <summary>Whether an expression uses no parameters but <paramref name="parameter"/> (and those of lambdas inside it).</summary>
	private static bool UsesOnly(Expression expression, ParameterExpression parameter)
	{
		var uses = new ParameterUses();
		uses.Visit(expression);

		return uses.Free.All(p => p == parameter);
	}

	/// <summary>Whether an expression uses <paramref name="parameter"/>.</summary>
	private static bool Uses(Expression expression, ParameterExpression parameter)
	{
		var uses = new ParameterUses();
		uses.Visit(expression);

		return uses.Free.Contains(parameter);
	}

	/// <summary>Collects the parameters an expression uses that its own lambdas do not declare.</summary>
	private sealed class ParameterUses : ExpressionVisitor
	{
		private readonly HashSet<ParameterExpression> declared = [];

		/// <summary>The parameters used and not declared inside.</summary>
		public HashSet<ParameterExpression> Free { get; } = [];

		/// <summary>Notes a parameter.</summary>
		/// <param name="node">The parameter.</param>
		/// <returns>The parameter.</returns>
		protected override Expression VisitParameter(ParameterExpression node)
		{
			if (!declared.Contains(node))
			{
				Free.Add(node);
			}

			return node;
		}

		/// <summary>Notes the parameters a lambda declares, and visits its body.</summary>
		/// <typeparam name="TDelegate">The type of the lambda.</typeparam>
		/// <param name="node">The lambda.</param>
		/// <returns>The lambda.</returns>
		protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node)
		{
			declared.UnionWith(node.Parameters);
			Visit(node.Body);

			return node;
		}
	}

	/// <summary>Finds the streams that a planned sequence reads.</summary>
	private sealed class StreamFinder : ExpressionVisitor
	{
		private readonly Dictionary<object, object> streams;

		/// <summary>Creates the finder.</summary>
		/// <param name="streams">The streams of the planned query.</param>
		public StreamFinder(Dictionary<object, object> streams) => this.streams = streams;

		/// <summary>The streams found.</summary>
		public List<IAsyncRecordStream> Found { get; } = [];

		/// <summary>Notes a stream.</summary>
		/// <param name="node">A constant.</param>
		/// <returns>The constant.</returns>
		protected override Expression VisitConstant(ConstantExpression node)
		{
			if (node.Value is { } value && streams.TryGetValue(value, out var stream))
			{
				Found.Add((IAsyncRecordStream)stream);
			}

			return node;
		}
	}
}
