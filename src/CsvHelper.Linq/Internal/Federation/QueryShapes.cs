using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Linq;

/// <summary>Finds the queries of other LINQ providers in a CSV query, such as the tables of Entity Framework Core.</summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal static class ForeignRoots
{
	/// <summary>
	/// Whether a node is the source of another provider's query: a constant query whose provider is neither the CSV provider
	/// nor LINQ to Objects, or a node of the provider's own (such as the query roots of Entity Framework Core 7 and later),
	/// which says its provider with a <c>QueryProvider</c> property.
	/// </summary>
	/// <param name="node">The node.</param>
	/// <param name="provider">The provider, or <see langword="null"/> for a node of another provider that does not say which.</param>
	/// <returns><see langword="true"/> for the source of another provider's query.</returns>
	public static bool IsRoot(Expression node, out IQueryProvider? provider)
	{
		switch (node)
		{
			case ConstantExpression { Value: IQueryable query } when query.Provider is not CsvQueryProvider && query is not EnumerableQuery:
				provider = query.Provider;

				return true;
			case { NodeType: ExpressionType.Extension } when node.GetType().GetProperty("QueryProvider", BindingFlags.Public | BindingFlags.Instance) is { } property &&
															   typeof(IQueryProvider).IsAssignableFrom(property.PropertyType):
				provider = property.GetValue(node) as IQueryProvider;

				return true;
			default:
				provider = null;

				return false;
		}
	}
}

/// <summary>What a node of a query holds, as the planner needs it to find the parts that other providers run.</summary>
internal sealed class Shape
{
	/// <summary>Whether the node holds a CSV source.</summary>
	public bool HasCsv { get; set; }

	/// <summary>Whether the node uses a lambda parameter declared outside it, so it cannot run on its own.</summary>
	public bool HasFree { get; set; }

	/// <summary>The provider of the other providers' queries that the node holds, or <see langword="null"/> for none.</summary>
	public IQueryProvider? Provider { get; set; }

	/// <summary>Whether the node holds queries of more than one other provider, or of one that does not say which it is.</summary>
	public bool Mixed { get; set; }

	/// <summary>
	/// Whether the node is a query that one other provider can run on its own: it holds that provider's queries and no CSV
	/// source, and does not use lambda parameters from outside it.
	/// </summary>
	public bool IsForeign => Provider is not null && !Mixed && !HasCsv && !HasFree;
}

/// <summary>Works out the <see cref="Shape"/> of every node of a query, by identity.</summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class ShapeAnalyzer : ExpressionVisitor
{
	private readonly Dictionary<Expression, Shape> shapes = new(IdentityComparer.Instance);
	private readonly Stack<Frame> frames = new();

	/// <summary>Works out the shapes of the nodes of a query.</summary>
	/// <param name="query">The query.</param>
	/// <returns>The shape of each node.</returns>
	public static IReadOnlyDictionary<Expression, Shape> Analyze(Expression query)
	{
		var analyzer = new ShapeAnalyzer();
		analyzer.Visit(query);

		return analyzer.shapes;
	}

	/// <summary>Visits a node and works out its shape from its own and its children's.</summary>
	/// <param name="node">The node.</param>
	/// <returns>The node.</returns>
	public override Expression? Visit(Expression? node)
	{
		if (node is null)
		{
			return null;
		}

		var frame = new Frame();
		frames.Push(frame);
		try
		{
			if (node is ParameterExpression parameter)
			{
				frame.Free.Add(parameter);
			}
			else if (node is ConstantExpression { Value: ICsvRoot })
			{
				frame.HasCsv = true;
			}
			else if (ForeignRoots.IsRoot(node, out var provider))
			{
				// A node of another provider is that provider's to read: its children are not visited.
				frame.Add(provider);
			}
			else
			{
				base.Visit(node);
			}

			if (node is LambdaExpression lambda)
			{
				frame.Free.ExceptWith(lambda.Parameters);
			}

			shapes[node] = new Shape { HasCsv = frame.HasCsv, HasFree = frame.Free.Count > 0, Provider = frame.Provider, Mixed = frame.Mixed };

			return node;
		}
		finally
		{
			frames.Pop();
			if (frames.Count > 0)
			{
				frames.Peek().Merge(frame);
			}
		}
	}

	/// <summary>What a node and its children hold so far.</summary>
	private sealed class Frame
	{
		/// <summary>The parameters used and not declared.</summary>
		public HashSet<ParameterExpression> Free { get; } = [];

		/// <summary>Whether a CSV source is held.</summary>
		public bool HasCsv { get; set; }

		/// <summary>The provider of the other providers' queries held.</summary>
		public IQueryProvider? Provider { get; private set; }

		/// <summary>Whether queries of several other providers (or of an unknown one) are held.</summary>
		public bool Mixed { get; private set; }

		/// <summary>Adds a query of another provider.</summary>
		/// <param name="provider">Its provider, or <see langword="null"/> when it is not known.</param>
		public void Add(IQueryProvider? provider)
		{
			if (provider is null || (Provider is not null && !ReferenceEquals(Provider, provider) && !SameKind(Provider, provider)))
			{
				Mixed = true;
			}

			Provider ??= provider;
		}

		/// <summary>Adds what a child holds.</summary>
		/// <param name="child">The child's frame.</param>
		public void Merge(Frame child)
		{
			Free.UnionWith(child.Free);
			HasCsv |= child.HasCsv;
			if (child.Provider is not null || child.Mixed)
			{
				Add(child.Provider);
				Mixed |= child.Mixed;
			}
		}

		// Two queries of one database context have different provider instances in some providers; providers of the same type
		// that are not the same instance are kept apart, unless they compare equal.
		private static bool SameKind(IQueryProvider a, IQueryProvider b) => a.Equals(b);
	}
}

/// <summary>
/// Puts the queries that a CSV query refers to by variable into it: a query held in a variable (a field of a closure) that
/// is passed to a LINQ operator is replaced by its expression, so that the planner sees its sources, CSV or not.
/// </summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class Inliner : ExpressionVisitor
{
	/// <summary>Replaces a constant CSV query that is not a source by its expression.</summary>
	/// <param name="node">The constant.</param>
	/// <returns>The expression of the query, or the node.</returns>
	protected override Expression VisitConstant(ConstantExpression node) =>
		node.Value is ICsvRoot { IsRoot: false } and IQueryable query ? Visit(query.Expression)! : node;

	/// <summary>Replaces the queries held in variables that are passed to a LINQ operator by their expressions.</summary>
	/// <param name="node">The call.</param>
	/// <returns>The call with the queries put in.</returns>
	protected override Expression VisitMethodCall(MethodCallExpression node)
	{
		if (!LinqMethods.IsLinq(node.Method))
		{
			return base.VisitMethodCall(node);
		}

		var parameters = node.Method.GetParameters();
		var arguments = new Expression[node.Arguments.Count];
		var changed = false;
		for (var i = 0; i < arguments.Length; i++)
		{
			var argument = node.Arguments[i];
			arguments[i] = Inline(argument, parameters[i].ParameterType) is { } inlined ? Visit(inlined)! : Visit(argument)!;
			changed |= arguments[i] != argument;
		}

		return changed ? node.Update(node.Object, arguments) : node;
	}

	/// <summary>The expression of the query that a variable holds, when it can take the variable's place as an argument.</summary>
	private static Expression? Inline(Expression argument, Type parameterType)
	{
		if (argument is not MemberExpression || !typeof(IQueryable).IsAssignableFrom(argument.Type) || !TryEvaluate(argument, out var value) ||
			value is not IQueryable query)
		{
			return null;
		}

		var expression = query.Expression;

		return parameterType.IsAssignableFrom(expression.Type) ? expression : null;
	}

	/// <summary>Reads a chain of fields and properties that starts at a constant (a closure) or a static member.</summary>
	private static bool TryEvaluate(Expression expression, out object? value)
	{
		switch (expression)
		{
			case ConstantExpression constant:
				value = constant.Value;

				return true;
			case MemberExpression member:
				object? target = null;
				if (member.Expression is not null && !TryEvaluate(member.Expression, out target))
				{
					value = null;

					return false;
				}

				if (member.Expression is not null && target is null)
				{
					value = null;

					return false;
				}

				value = member.Member switch
				{
					FieldInfo field => field.GetValue(target),
					PropertyInfo property => property.GetValue(target),
					_ => null,
				};

				return member.Member is FieldInfo or PropertyInfo;
			default:
				value = null;

				return false;
		}
	}
}
