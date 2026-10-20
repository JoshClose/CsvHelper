using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Linq;

/// <summary>The kind of a <see cref="Step"/>.</summary>
internal enum StepKind
{
	/// <summary>Into each element of a sequence.</summary>
	Sequence,

	/// <summary>Into each element of a group (<see cref="IGrouping{TKey, TElement}"/>), whose key is something else.</summary>
	Grouping,

	/// <summary>Into a member, such as the <c>o</c> of the anonymous type <c>new { o, c }</c> that query syntax makes of a join.</summary>
	Member,
}

/// <summary>One step from a value towards the records it carries.</summary>
/// <param name="Kind">The kind of the step.</param>
/// <param name="Member">The member of a <see cref="StepKind.Member"/> step; <see langword="null"/> otherwise.</param>
internal readonly record struct Step(StepKind Kind, MemberInfo? Member)
{
	/// <summary>The step into each element of a sequence.</summary>
	public static Step Sequence { get; } = new(StepKind.Sequence, null);

	/// <summary>The step into each element of a group.</summary>
	public static Step Grouping { get; } = new(StepKind.Grouping, null);
}

/// <summary>The members of a record type that a query uses, or why it needs all of them.</summary>
internal sealed class MemberUsage
{
	/// <summary>The names of the members used, matched against the mapper's members.</summary>
	public SortedSet<string> Members { get; } = new(StringComparer.Ordinal);

	/// <summary>Why the query needs every member, such as a record returned whole; <see langword="null"/> when <see cref="Members"/> are all it uses.</summary>
	public string? Escape { get; set; }

	/// <summary>The members to read: <see langword="null"/> for all of them.</summary>
	public IReadOnlyCollection<string>? Selection => Escape is null ? Members : null;
}

/// <summary>
/// Finds the members of the records of a CSV source that a query uses, so the reader reads only those: it follows the
/// records from the source up through the operators of the query, into the lambdas that get them, and through the
/// anonymous types, groups, joins and tuples that carry them, and collects the members that the lambdas read. A record
/// used in any other way (returned, passed to a method, compared, stored) needs all of its members.
/// </summary>
/// <remarks>
/// What a value holds is described by the steps from it to the records (a flow): none for a record itself;
/// <c>[Sequence]</c> for a sequence of records, such as the source; <c>[Sequence, Grouping]</c> for the result of
/// <c>GroupBy</c>; <c>[Sequence, Member o]</c> for the result of a join that query syntax writes as <c>new { o, c }</c>.
/// The operators of <see cref="Queryable"/> and <see cref="Enumerable"/> are understood from their generic definitions:
/// the lambda parameters and results typed by the generic parameter that the records come in as get them.
/// </remarks>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal sealed class MemberUsageAnalyzer
{
	// Deep enough for any real query; deeper flows give up and read everything.
	private const int MaxDepth = 256;

	private static readonly Step[] Record = [];
	private static readonly Step[] Records = [Step.Sequence];

	private readonly Type recordType;
	private readonly HashSet<string> mapped;
	private readonly MemberUsage usage = new();
	private int depth;

	private MemberUsageAnalyzer(Type recordType, IEnumerable<string> mapped)
	{
		this.recordType = recordType;
		this.mapped = new HashSet<string>(mapped, StringComparer.Ordinal);
	}

	/// <summary>Finds the members used of the records of a sequence: those of a CSV source, after the operators the reader runs.</summary>
	/// <param name="at">The sequence, with its ancestors in the query.</param>
	/// <param name="recordType">The type of the records.</param>
	/// <param name="mapped">The names of the members that the mapper reads.</param>
	/// <returns>The members used, or why all of them are needed.</returns>
	public static MemberUsage OfSequence(Chain at, Type recordType, IEnumerable<string> mapped)
	{
		var analyzer = new MemberUsageAnalyzer(recordType, mapped);
		analyzer.Follow(at, Records);

		return analyzer.usage;
	}

	/// <summary>Finds the members used of the record that a lambda gets as its parameter, such as the predicate of a <c>Where</c>.</summary>
	/// <param name="atLambda">The lambda, with its ancestors in the query.</param>
	/// <param name="lambda">The lambda.</param>
	/// <param name="parameter">The index of the parameter that gets the record.</param>
	/// <param name="recordType">The type of the records.</param>
	/// <param name="mapped">The names of the members that the mapper reads.</param>
	/// <returns>The members used, or why all of them are needed.</returns>
	public static MemberUsage OfParameter(Chain atLambda, LambdaExpression lambda, int parameter, Type recordType, IEnumerable<string> mapped)
	{
		var analyzer = new MemberUsageAnalyzer(recordType, mapped);
		analyzer.FollowParameter(atLambda, lambda, parameter, Record);

		return analyzer.usage;
	}

	/// <summary>Follows a value that carries records to where its parent uses it.</summary>
	/// <param name="at">The value, with its ancestors.</param>
	/// <param name="flow">The steps from the value to the records.</param>
	private void Follow(Chain at, Step[] flow)
	{
		if (usage.Escape is not null)
		{
			return;
		}

		if (++depth > MaxDepth)
		{
			Escape("the query is too deeply nested to follow");

			return;
		}

		try
		{
			FollowToParent(at, flow);
		}
		finally
		{
			depth--;
		}
	}

	private void FollowToParent(Chain at, Step[] flow)
	{
		var node = at.Node;
		if (at.Parent is not { } parent)
		{
			Escape(flow.All(s => s.Kind != StepKind.Member) ? "the query returns whole records" : "the query returns records inside its results");

			return;
		}

		switch (parent.Node)
		{
			case MemberExpression member when member.Expression == node:
				FollowMember(parent, member, flow);
				break;
			case MethodCallExpression call:
				FollowCall(parent, call, node, flow);
				break;
			case NewExpression created when created.Members is not null && created.Arguments.IndexOf(node) is var index and >= 0:
				// A member of an anonymous type (or of a type that names its constructor's members) carries what the argument does.
				Follow(parent, [new Step(StepKind.Member, created.Members[index]), .. flow]);
				break;
			case LambdaExpression lambda when lambda.Body == node:
				FollowLambdaResult(parent, lambda, flow);
				break;
			case ConditionalExpression conditional when conditional.Test != node:
				Follow(parent, flow);
				break;
			case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } comparison when IsNull(comparison.Left) || IsNull(comparison.Right):
				// A record compared with null (such as the missing side of a left join): no member is read.
				break;
			default:
				Escape($"the query uses records in {Describe(parent.Node)}");
				break;
		}
	}

	/// <summary>Follows a member access on a value that carries records: a member of a record, or a step towards them.</summary>
	private void FollowMember(Chain at, MemberExpression member, Step[] flow)
	{
		if (flow.Length == 0)
		{
			UseMember(member);

			return;
		}

		var step = flow[0];
		if (step.Kind == StepKind.Member)
		{
			// The other members of a carrier do not hold the records.
			if (LinqMethods.SameMember(member.Member, step.Member!))
			{
				Follow(at, flow.Skip(1).ToArray());
			}

			return;
		}

		if (step.Kind == StepKind.Grouping && member.Member.Name == "Key")
		{
			return;
		}

		Escape($"the query uses {member.Member.Name} of a sequence of records");
	}

	/// <summary>Uses a member of a record: it must be one that the mapper reads.</summary>
	private void UseMember(MemberExpression member)
	{
		var declaring = member.Member.DeclaringType;
		if (declaring is not null && declaring.IsAssignableFrom(recordType) && mapped.Contains(member.Member.Name))
		{
			usage.Members.Add(member.Member.Name);

			return;
		}

		Escape($"the query uses {member.Member.Name}, which is not read from a column");
	}

	/// <summary>Follows a value that carries records into a method call: a LINQ operator, or anything else, which needs all members.</summary>
	private void FollowCall(Chain at, MethodCallExpression call, Expression node, Step[] flow)
	{
		var index = call.Arguments.IndexOf(node);
		if (call.Object == node || index < 0 || !LinqMethods.IsLinq(call.Method))
		{
			Escape($"the query passes records to {call.Method.Name}");

			return;
		}

		// The same node as two arguments (a lambda parameter joined with itself) cannot be told apart: give up.
		if (call.Arguments.Count(a => a == node) > 1)
		{
			Escape($"the query passes the same records to {call.Method.Name} twice");

			return;
		}

		var definition = LinqMethods.Definition(call.Method);
		var parameters = definition.GetParameters();
		var parameterType = parameters[index].ParameterType;
		// The generic parameter of the operator that the incoming values are elements of, and what each element holds.
		Type? element;
		Step[] elementFlow;
		if (parameterType.IsGenericParameter)
		{
			element = parameterType;
			elementFlow = flow;
		}
		else if (LinqMethods.ElementOf(parameterType) is { IsGenericParameter: true } sequenceElement && flow.Length > 0 &&
				 flow[0].Kind is StepKind.Sequence or StepKind.Grouping)
		{
			element = sequenceElement;
			elementFlow = flow.Skip(1).ToArray();
		}
		else
		{
			Escape($"{call.Method.Name} gets the records in a {parameterType.Name}");

			return;
		}

		var lambdas = Enumerable.Range(0, call.Arguments.Count).Where(j => LinqMethods.StripQuotes(call.Arguments[j]) is LambdaExpression).ToList();
		if (LinqMethods.ComparesElements(call.Method, lambdas.Count > 0))
		{
			Escape($"{call.Method.Name} compares the records");

			return;
		}

		foreach (var j in lambdas)
		{
			FollowLambdaParameters(at, call, j, parameters[j].ParameterType, element, elementFlow);
		}

		FollowResult(at, call, definition.ReturnType, element, elementFlow, accumulates: false);
	}

	/// <summary>
	/// Follows what an operator returns, when its values (or its elements) are values of <paramref name="element"/>, one of
	/// its generic parameters, which hold the records as <paramref name="elementFlow"/> says.
	/// </summary>
	private void FollowResult(Chain at, MethodCallExpression call, Type returnType, Type element, Step[] elementFlow, bool accumulates)
	{
		var resultElement = LinqMethods.ElementOf(returnType);
		if (returnType == element && !accumulates)
		{
			Follow(at, elementFlow);
		}
		else if (resultElement == element)
		{
			Follow(at, [Step.Sequence, .. elementFlow]);
		}
		else if (LinqMethods.GroupingElementOf(resultElement) == element)
		{
			Follow(at, [Step.Sequence, Step.Grouping, .. elementFlow]);
		}
		else if (LinqMethods.TupleIndexOf(resultElement, element) is var item and >= 0)
		{
			// Zip and Index pair the elements with other values, in the tuple's Item1, Item2 and so on.
			var tuple = LinqMethods.ElementOf(call.Method.ReturnType)!;
			var field = tuple.GetField("Item" + (item + 1).ToString(CultureInfo.InvariantCulture))!;
			Follow(at, [Step.Sequence, new Step(StepKind.Member, field), .. elementFlow]);
		}
		else if (LinqMethods.Mentions(returnType, element))
		{
			Escape($"{call.Method.Name} returns the records in a {call.Method.ReturnType.Name}");
		}
	}

	/// <summary>
	/// Follows the parameters of the lambda argument <paramref name="argument"/> of an operator that get values of
	/// <paramref name="element"/>, one of its generic parameters: a parameter of that type gets one value, one of
	/// <see cref="IEnumerable{T}"/> a sequence of them.
	/// </summary>
	private void FollowLambdaParameters(Chain atCall, MethodCallExpression call, int argument, Type parameterType, Type element, Step[] elementFlow)
	{
		if (LinqMethods.DelegateOf(parameterType) is not { } delegateType)
		{
			return;
		}

		var lambda = (LambdaExpression)LinqMethods.StripQuotes(call.Arguments[argument]);
		var parameterTypes = LinqMethods.SignatureOf(delegateType, out _);
		var atLambda = AtLambda(atCall, call.Arguments[argument], lambda);
		for (var k = 0; k < parameterTypes.Length && k < lambda.Parameters.Count; k++)
		{
			if (parameterTypes[k] == element)
			{
				FollowParameter(atLambda, lambda, k, elementFlow);
			}
			else if (LinqMethods.ElementOf(parameterTypes[k]) == element)
			{
				FollowParameter(atLambda, lambda, k, [Step.Sequence, .. elementFlow]);
			}
			else if (LinqMethods.Mentions(parameterTypes[k], element))
			{
				Escape($"{call.Method.Name} passes the records to a lambda in a {parameterTypes[k].Name}");
			}
		}
	}

	/// <summary>
	/// Follows the records that a lambda returns, to whatever the operator that calls it does with what it returns: other
	/// lambdas of the operator may get it, and its result may hold it. What is returned as a key (<c>TKey</c>), compared or
	/// accumulated needs all members.
	/// </summary>
	private void FollowLambdaResult(Chain atLambda, LambdaExpression lambda, Step[] flow)
	{
		var up = atLambda.Parent;
		Expression argumentNode = lambda;
		if (up?.Node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
		{
			argumentNode = quote;
			up = up.Parent;
		}

		if (up is null || up.Node is not MethodCallExpression call || !LinqMethods.IsLinq(call.Method) ||
			call.Arguments.IndexOf(argumentNode) is not (var j and >= 0))
		{
			Escape("the query returns records from a lambda");

			return;
		}

		var definition = LinqMethods.Definition(call.Method);
		var parameters = definition.GetParameters();
		if (LinqMethods.DelegateOf(parameters[j].ParameterType) is not { } delegateType)
		{
			Escape($"{call.Method.Name} gets records from a lambda");

			return;
		}

		var ownParameters = LinqMethods.SignatureOf(delegateType, out var returned);
		// What the lambda produces: values of a generic parameter, or (from a sequence) the elements of one.
		Type? produced;
		Step[] producedFlow;
		if (returned.IsGenericParameter)
		{
			produced = returned;
			producedFlow = flow;
		}
		else if (LinqMethods.ElementOf(returned) is { IsGenericParameter: true } sequenceElement && flow.Length > 0 &&
				 flow[0].Kind is StepKind.Sequence or StepKind.Grouping)
		{
			produced = sequenceElement;
			producedFlow = flow.Skip(1).ToArray();
		}
		else
		{
			Escape($"{call.Method.Name} gets records from a lambda as a {returned.Name}");

			return;
		}

		// Keys are compared, and a lambda that gets what it returns accumulates it.
		if (produced.Name == "TKey" || ownParameters.Any(p => LinqMethods.Mentions(p, produced)))
		{
			Escape($"{call.Method.Name} compares or accumulates the records that a lambda returns");

			return;
		}

		// Other lambdas that get what this one returns, such as the result selector of SelectMany.
		for (var other = 0; other < call.Arguments.Count; other++)
		{
			if (other != j && LinqMethods.StripQuotes(call.Arguments[other]) is LambdaExpression)
			{
				FollowLambdaParameters(up, call, other, parameters[other].ParameterType, produced, producedFlow);
			}
		}

		FollowResult(up, call, definition.ReturnType, produced, producedFlow, accumulates: true);
	}

	/// <summary>Follows each use of a parameter of a lambda, which gets values that hold records as <paramref name="flow"/> says.</summary>
	private void FollowParameter(Chain atLambda, LambdaExpression lambda, int index, Step[] flow) =>
		new Uses(lambda.Parameters[index], atLambda, use => Follow(use, flow)).Visit(lambda.Body);

	/// <summary>The link of a lambda argument of an operator, under its quote when it has one.</summary>
	private static Chain AtLambda(Chain atCall, Expression argument, LambdaExpression lambda)
	{
		var at = atCall.Push(argument);

		return argument == lambda ? at : at.Push(lambda);
	}

	private void Escape(string reason) => usage.Escape ??= reason;

	/// <summary>Whether an expression is the constant <see langword="null"/>, possibly converted.</summary>
	private static bool IsNull(Expression expression)
	{
		while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } conversion)
		{
			expression = conversion.Operand;
		}

		return expression is ConstantExpression { Value: null };
	}

	private static string Describe(Expression node) => node switch
	{
		MethodCallExpression call => $"a call of {call.Method.Name}",
		BinaryExpression binary => $"a {binary.NodeType} expression",
		UnaryExpression unary => $"a {unary.NodeType} expression",
		_ => $"a {node.NodeType} expression",
	};

	/// <summary>Finds the uses of a parameter in a lambda's body, with their ancestors, leaving out its declarations.</summary>
	private sealed class Uses : ExpressionVisitor
	{
		private readonly ParameterExpression parameter;
		private readonly Action<Chain> found;
		private Chain chain;

		/// <summary>Creates the visitor.</summary>
		/// <param name="parameter">The parameter.</param>
		/// <param name="atLambda">The lambda, with its ancestors.</param>
		/// <param name="found">Called with each use.</param>
		public Uses(ParameterExpression parameter, Chain atLambda, Action<Chain> found)
		{
			this.parameter = parameter;
			this.found = found;
			chain = atLambda;
		}

		/// <summary>Visits a node with its link in the chain.</summary>
		/// <param name="node">The node.</param>
		/// <returns>The node, unchanged.</returns>
		public override Expression? Visit(Expression? node)
		{
			if (node is null)
			{
				return null;
			}

			var saved = chain;
			chain = chain.Push(node);
			try
			{
				if (node == parameter)
				{
					found(chain);

					return node;
				}

				return base.Visit(node);
			}
			finally
			{
				chain = saved;
			}
		}

		/// <summary>Visits the body of a nested lambda, not its parameter declarations.</summary>
		/// <typeparam name="TDelegate">The type of the lambda.</typeparam>
		/// <param name="node">The lambda.</param>
		/// <returns>The lambda, unchanged.</returns>
		protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node)
		{
			Visit(node.Body);

			return node;
		}
	}
}
