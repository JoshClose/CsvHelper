using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Linq;

/// <summary>
/// What the planner knows about the LINQ operators of <see cref="Queryable"/> and <see cref="Enumerable"/>: it reads
/// their generic definitions to tell which arguments and lambda parameters get the elements of which sequence, so new
/// operators are understood without a table of them.
/// </summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal static class LinqMethods
{
	// The operators that compare the elements themselves (equality or order) when no lambda chooses what to compare,
	// which needs all of their members.
	private static readonly HashSet<string> Comparing = new(StringComparer.Ordinal)
	{
		"Distinct", "Union", "Intersect", "Except", "Contains", "SequenceEqual", "Order", "OrderDescending", "ToHashSet", "Min", "Max",
	};

	/// <summary>Whether a method is an operator of <see cref="Queryable"/> or <see cref="Enumerable"/>.</summary>
	/// <param name="method">The method.</param>
	/// <returns><see langword="true"/> for a LINQ operator.</returns>
	public static bool IsLinq(MethodInfo method) => method.DeclaringType == typeof(Queryable) || method.DeclaringType == typeof(Enumerable);

	/// <summary>Whether a method is an operator of <see cref="Queryable"/>.</summary>
	/// <param name="method">The method.</param>
	/// <param name="name">The name of the operator.</param>
	/// <returns><see langword="true"/> for the <see cref="Queryable"/> operator called <paramref name="name"/>.</returns>
	public static bool IsQueryable(MethodInfo method, string name) => method.DeclaringType == typeof(Queryable) && method.Name == name;

	/// <summary>
	/// Whether an operator compares its elements themselves when it has no lambda to choose what to compare, such as
	/// <c>Distinct</c>, <c>Contains</c>, and <c>Min</c> and <c>Max</c> without a selector.
	/// </summary>
	/// <param name="method">The operator.</param>
	/// <param name="hasLambda">Whether the call passes a lambda.</param>
	/// <returns><see langword="true"/> when the operator compares whole elements.</returns>
	public static bool ComparesElements(MethodInfo method, bool hasLambda) => !hasLambda && Comparing.Contains(method.Name);

	/// <summary>The generic definition of a method, or the method when it is not generic.</summary>
	/// <param name="method">The method.</param>
	/// <returns>Its definition.</returns>
	public static MethodInfo Definition(MethodInfo method) => method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;

	/// <summary>Removes the quotes around a lambda argument of a <see cref="Queryable"/> operator.</summary>
	/// <param name="expression">The argument.</param>
	/// <returns>The expression inside the quotes.</returns>
	public static Expression StripQuotes(Expression expression)
	{
		while (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
		{
			expression = quote.Operand;
		}

		return expression;
	}

	/// <summary>
	/// The element type of <see cref="IQueryable{T}"/>, <see cref="IEnumerable{T}"/>, <see cref="IOrderedQueryable{T}"/> and
	/// <see cref="IOrderedEnumerable{TElement}"/>; <see langword="null"/> for other types.
	/// </summary>
	/// <param name="type">The type, from a generic method definition or a closed one.</param>
	/// <returns>The element type, or <see langword="null"/>.</returns>
	public static Type? ElementOf(Type type)
	{
		if (!type.IsGenericType)
		{
			return null;
		}

		var definition = type.GetGenericTypeDefinition();

		return definition == typeof(IQueryable<>) || definition == typeof(IEnumerable<>) || definition == typeof(IOrderedQueryable<>) ||
			   definition == typeof(IOrderedEnumerable<>)
			? type.GetGenericArguments()[0]
			: null;
	}

	/// <summary>The element type of a sequence type, or of any type that implements <see cref="IEnumerable{T}"/> (but not <see cref="string"/>).</summary>
	/// <param name="type">The type.</param>
	/// <returns>The element type, or <see langword="null"/>.</returns>
	public static Type? SequenceElementOf(Type type)
	{
		if (ElementOf(type) is { } element)
		{
			return element;
		}

		if (type == typeof(string))
		{
			return null;
		}

		foreach (var face in type.GetInterfaces())
		{
			if (face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				return face.GetGenericArguments()[0];
			}
		}

		return null;
	}

	/// <summary>The element type of <see cref="IGrouping{TKey, TElement}"/>; <see langword="null"/> for other types.</summary>
	/// <param name="type">The type, or <see langword="null"/>.</param>
	/// <returns>The element type of the groups, or <see langword="null"/>.</returns>
	public static Type? GroupingElementOf(Type? type) =>
		type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IGrouping<,>) ? type.GetGenericArguments()[1] : null;

	/// <summary>The index of <paramref name="element"/> among the type arguments of a <see cref="ValueTuple"/> type, or -1.</summary>
	/// <param name="type">The type, or <see langword="null"/>.</param>
	/// <param name="element">The type argument to find.</param>
	/// <returns>Its index, which is that of the tuple's field <c>Item{index + 1}</c>, or -1.</returns>
	public static int TupleIndexOf(Type? type, Type element)
	{
		if (type is not { IsGenericType: true, IsValueType: true } ||
			type.GetGenericTypeDefinition().FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) != true)
		{
			return -1;
		}

		var arguments = type.GetGenericArguments();

		// The eighth argument of a long tuple is the tuple of the rest, not an element.
		return arguments.Length <= 7 ? Array.IndexOf(arguments, element) : -1;
	}

	/// <summary>Whether <paramref name="type"/> is or mentions <paramref name="part"/>: as itself, a type argument, or an array element.</summary>
	/// <param name="type">The type.</param>
	/// <param name="part">The type to look for.</param>
	/// <returns><see langword="true"/> when <paramref name="type"/> mentions <paramref name="part"/>.</returns>
	public static bool Mentions(Type type, Type part)
	{
		if (type == part)
		{
			return true;
		}

		if (type.HasElementType)
		{
			return Mentions(type.GetElementType()!, part);
		}

		return type.IsGenericType && type.GetGenericArguments().Any(a => Mentions(a, part));
	}

	/// <summary>
	/// The delegate type of a lambda parameter of an operator: the type itself, or the one inside
	/// <see cref="Expression{TDelegate}"/>; <see langword="null"/> when the parameter does not take a lambda.
	/// </summary>
	/// <param name="parameterType">The type of the operator's parameter.</param>
	/// <returns>The delegate type, or <see langword="null"/>.</returns>
	public static Type? DelegateOf(Type parameterType)
	{
		if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Expression<>))
		{
			parameterType = parameterType.GetGenericArguments()[0];
		}

		return typeof(Delegate).IsAssignableFrom(parameterType) ? parameterType : null;
	}

	/// <summary>The parameters and return type of a delegate type.</summary>
	/// <param name="delegateType">The delegate type, which may mention generic parameters.</param>
	/// <param name="returnType">The type it returns.</param>
	/// <returns>The types of its parameters.</returns>
	public static Type[] SignatureOf(Type delegateType, out Type returnType)
	{
		var invoke = delegateType.GetMethod("Invoke")!;
		returnType = invoke.ReturnType;

		return invoke.GetParameters().Select(p => p.ParameterType).ToArray();
	}

	/// <summary>Whether two members are the same: the same member of the same type, as a property or its getter.</summary>
	/// <param name="a">One member.</param>
	/// <param name="b">The other.</param>
	/// <returns><see langword="true"/> when they are the same member.</returns>
	public static bool SameMember(MemberInfo a, MemberInfo b) =>
		a == b || (MemberName(a) == MemberName(b) && a.DeclaringType == b.DeclaringType);

	/// <summary>The name of a member, with the <c>get_</c> of a property getter removed (anonymous types name their members by getters on some runtimes).</summary>
	/// <param name="member">The member.</param>
	/// <returns>Its name.</returns>
	public static string MemberName(MemberInfo member) =>
		member is MethodInfo { IsSpecialName: true } method && method.Name.StartsWith("get_", StringComparison.Ordinal) ? method.Name.Substring(4) : member.Name;
}
