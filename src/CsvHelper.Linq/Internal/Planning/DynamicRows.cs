using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CsvHelper.Linq;

/// <summary>
/// Rewrites the lambdas that a query applies to <see cref="CsvDynamicRecord"/> rows into lambdas over the reader, so the
/// reader runs them on its current record without creating the row: <c>row["Name"]</c> becomes
/// <c>reader.GetStringOrNull(column)</c>, <c>row.Parse&lt;decimal&gt;("Total")</c> becomes
/// <c>reader.Parse&lt;decimal&gt;(column)</c>, and so on, with the columns found by name once per run of the query.
/// </summary>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal static class DynamicRows
{
	// Reads a field from the reader as CsvDynamicRecord.Parse<T> parses it; made generic for each type parsed.
	private static readonly MethodInfo Parse = typeof(DynamicAccess).GetMethod(nameof(DynamicAccess.Parse))!;
#if NET
	// The same for enums, which CsvEnumExtensions parses on .NET.
	private static readonly MethodInfo ParseEnum = typeof(DynamicAccess).GetMethod(nameof(DynamicAccess.ParseEnum))!;
#endif
	private static readonly MethodInfo Text = typeof(DynamicAccess).GetMethod(nameof(DynamicAccess.Text))!;
	private static readonly MethodInfo TextEquals = typeof(DynamicAccess).GetMethod(nameof(DynamicAccess.TextEquals))!;
	private static readonly MethodInfo StringOrNull = typeof(CsvReader).GetMethod(nameof(CsvReader.GetStringOrNull), [typeof(int)])!;
	private static readonly PropertyInfo FieldCount = typeof(CsvReader).GetProperty(nameof(CsvReader.FieldCount))!;

	/// <summary>
	/// Rewrites a lambda whose first parameter is a row into a lambda of <paramref name="reader"/> and
	/// <paramref name="columns"/> (the indexes of the columns in <paramref name="slots"/>), when it uses the row only through
	/// its indexers and <c>Parse&lt;T&gt;</c> with column names that do not depend on the row, and its <c>FieldCount</c> and
	/// <c>ContainsKey</c>.
	/// </summary>
	/// <param name="lambda">The lambda.</param>
	/// <param name="slots">The column names of the stream, which the lambda's are added to.</param>
	/// <param name="reader">The reader parameter of the rewritten lambda.</param>
	/// <param name="columns">The parameter of the rewritten lambda that holds the indexes of the columns, by slot.</param>
	/// <param name="used">The column names that the lambda reads.</param>
	/// <param name="nullValues">The reader's <see cref="CsvReaderOptions.NullValues"/>, which a field never equals as text.</param>
	/// <param name="reason">Why the lambda cannot be rewritten; <see langword="null"/> when it is.</param>
	/// <returns>The rewritten lambda, or <see langword="null"/>.</returns>
	public static LambdaExpression? Rewrite(LambdaExpression lambda, List<string> slots, ParameterExpression reader, ParameterExpression columns,
		SortedSet<string> used, IReadOnlyList<string>? nullValues, out string? reason)
	{
		var rewriter = new Rewriter(lambda.Parameters[0], slots, reader, columns, used, nullValues);
		var body = rewriter.Visit(lambda.Body)!;
		reason = rewriter.Failure;

		return reason is null ? Expression.Lambda(body, reader, columns) : null;
	}

	/// <summary>Replaces the uses of a row by reads of the reader, and notes the first use that it cannot replace.</summary>
	[RequiresUnreferencedCode(Trimming.Unreferenced)]
	[RequiresDynamicCode(Trimming.Dynamic)]
	private sealed class Rewriter : ExpressionVisitor
	{
		private readonly ParameterExpression row;
		private readonly List<string> slots;
		private readonly ParameterExpression reader;
		private readonly ParameterExpression columns;
		private readonly SortedSet<string> used;
		private readonly IReadOnlyList<string>? nullValues;

		/// <summary>Creates the rewriter.</summary>
		/// <param name="row">The parameter of the row.</param>
		/// <param name="slots">The column names of the stream.</param>
		/// <param name="reader">The reader parameter.</param>
		/// <param name="columns">The parameter of the column indexes.</param>
		/// <param name="used">The column names read, which the rewriter adds to.</param>
		/// <param name="nullValues">The reader's null values.</param>
		public Rewriter(ParameterExpression row, List<string> slots, ParameterExpression reader, ParameterExpression columns, SortedSet<string> used,
			IReadOnlyList<string>? nullValues)
		{
			this.row = row;
			this.slots = slots;
			this.reader = reader;
			this.columns = columns;
			this.used = used;
			this.nullValues = nullValues;
		}

		/// <summary>Why the lambda cannot be rewritten, or <see langword="null"/>.</summary>
		public string? Failure { get; private set; }

		/// <summary>
		/// Replaces <c>row["Name"] == "text"</c> (and <c>!=</c>) by a comparison of the field's UTF-8 bytes with those of the
		/// text, which makes no string: the same result, as the field's string is those bytes decoded, and a null value is
		/// never equal to text that is not one.
		/// </summary>
		/// <param name="node">The comparison.</param>
		/// <returns>The comparison of bytes, or the node visited.</returns>
		protected override Expression VisitBinary(BinaryExpression node)
		{
			if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual && node.Left.Type == typeof(string) && node.Right.Type == typeof(string))
			{
				var (field, text) = ColumnText(node.Left) is { } left ? (left, Literal(node.Right)) : (ColumnText(node.Right), Literal(node.Left));
				if (field is not null && text is not null && text.IndexOf('\uFFFD') < 0 && (nullValues is null || !nullValues.Contains(text)))
				{
					used.Add(field);
					Expression equal = Expression.Call(TextEquals, reader, Column(field), Expression.Constant(field), Expression.Constant(Encoding.UTF8.GetBytes(text)));

					return node.NodeType == ExpressionType.Equal ? equal : Expression.Not(equal);
				}
			}

			return base.VisitBinary(node);
		}

		/// <summary>Replaces a call of the row's indexer, <c>Parse&lt;T&gt;</c> or <c>ContainsKey</c>.</summary>
		/// <param name="node">The call.</param>
		/// <returns>The read of the reader, or the call with its arguments visited.</returns>
		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
#if NET
			// row.Parse<MyEnum>("Name"), an extension method of the row on .NET.
			if (node.Object is null && node.Method.DeclaringType == typeof(CsvEnumExtensions) && node.Arguments.Count == 2 && node.Arguments[0] == row)
			{
				if (Name(node.Arguments[1]) is not { } column)
				{
					return Fail($"the query calls {node.Method.Name} of the rows", node);
				}

				used.Add(column);

				return Expression.Call(ParseEnum.MakeGenericMethod(node.Method.GetGenericArguments()), reader, Column(column), Expression.Constant(column));
			}

#endif
			if (node.Object != row)
			{
				return base.VisitMethodCall(node);
			}

			var parameters = node.Method.GetParameters();
			if (node.Method.Name == "get_Item" && parameters[0].ParameterType == typeof(int))
			{
				return Expression.Call(reader, StringOrNull, Visit(node.Arguments[0])!);
			}

			if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string) || Name(node.Arguments[0]) is not { } name)
			{
				return Fail($"the query calls {node.Method.Name} of the rows", node);
			}

			if (node.Method.Name == nameof(CsvDynamicRecord.ContainsKey))
			{
				return Expression.GreaterThanOrEqual(Column(name), Expression.Constant(0));
			}

			used.Add(name);
			if (node.Method.Name == "get_Item")
			{
				return Expression.Call(Text, reader, Column(name), Expression.Constant(name));
			}

			return node.Method is { Name: nameof(CsvDynamicRecord.Parse), IsGenericMethod: true }
				? Expression.Call(Parse.MakeGenericMethod(node.Method.GetGenericArguments()), reader, Column(name), Expression.Constant(name))
				: Fail($"the query calls {node.Method.Name} of the rows", node);
		}

		/// <summary>Replaces the row's <c>FieldCount</c>.</summary>
		/// <param name="node">The member access.</param>
		/// <returns>The reader's <c>FieldCount</c>, or the member access visited.</returns>
		protected override Expression VisitMember(MemberExpression node)
		{
			if (node.Expression != row)
			{
				return base.VisitMember(node);
			}

			return node.Member.Name == nameof(CsvDynamicRecord.FieldCount)
				? Expression.Property(reader, FieldCount)
				: Fail($"the query uses {node.Member.Name} of the rows", node);
		}

		/// <summary>Notes a use of the row that is none of those above.</summary>
		/// <param name="node">The parameter.</param>
		/// <returns>The parameter.</returns>
		protected override Expression VisitParameter(ParameterExpression node) =>
			node == row ? Fail("the query uses whole rows", node) : node;

		/// <summary>The constant index of a column, by its name: <c>columns[slot]</c>.</summary>
		private Expression Column(string name)
		{
			var slot = slots.IndexOf(name);
			if (slot < 0)
			{
				slot = slots.Count;
				slots.Add(name);
			}

			return Expression.ArrayIndex(columns, Expression.Constant(slot));
		}

		/// <summary>The column name of <c>row["Name"]</c>, or <see langword="null"/> for any other expression.</summary>
		private string? ColumnText(Expression expression) =>
			expression is MethodCallExpression { Method.Name: "get_Item" } call && call.Object == row && call.Arguments[0].Type == typeof(string)
				? Name(call.Arguments[0])
				: null;

		/// <summary>The text an expression gives, when it is not <see langword="null"/> and does not depend on any lambda parameter.</summary>
		private static string? Literal(Expression expression) => expression.Type == typeof(string) ? Name(expression) : null;

		/// <summary>The column name an argument gives, when it does not depend on the row (or on any lambda parameter).</summary>
		private static string? Name(Expression argument)
		{
			if (argument is ConstantExpression { Value: string constant })
			{
				return constant;
			}

			return new ParameterFinder().Has(argument)
				? null
				: Expression.Lambda<Func<string?>>(argument).Compile()();
		}

		/// <summary>Notes why the lambda cannot be rewritten, and leaves the node as it is.</summary>
		private Expression Fail(string reason, Expression node)
		{
			Failure ??= reason;

			return node;
		}
	}

	/// <summary>Tells whether an expression uses any parameter.</summary>
	private sealed class ParameterFinder : ExpressionVisitor
	{
		private bool found;

		/// <summary>Whether <paramref name="expression"/> uses a parameter.</summary>
		/// <param name="expression">The expression.</param>
		/// <returns><see langword="true"/> when it does.</returns>
		public bool Has(Expression expression)
		{
			Visit(expression);

			return found;
		}

		/// <summary>Notes the parameter.</summary>
		/// <param name="node">The parameter.</param>
		/// <returns>The parameter.</returns>
		protected override Expression VisitParameter(ParameterExpression node)
		{
			found = true;

			return node;
		}
	}
}

/// <summary>
/// Reads a column of the reader's current record as <see cref="CsvDynamicRecord"/> reads it, for the rewritten lambdas of
/// queries of dynamic rows: by its index, or throwing <see cref="KeyNotFoundException"/> when the header has no column with
/// the name (-1), as the row would.
/// </summary>
internal static class DynamicAccess
{
	/// <summary>Whether the field is <paramref name="utf8"/>, as <c>row[name] == text</c> for text that is not a null value.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="column">The index of the column, or -1.</param>
	/// <param name="name">The name of the column.</param>
	/// <param name="utf8">The text, as UTF-8.</param>
	/// <returns><see langword="true"/> when the field is the text.</returns>
	public static bool TextEquals(CsvReader reader, int column, string name, byte[] utf8) => reader.GetSpan(Column(column, name)).SequenceEqual(utf8);

	/// <summary>The field as a string, or <see langword="null"/> for a null value, as <c>row[name]</c>.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="column">The index of the column, or -1.</param>
	/// <param name="name">The name of the column.</param>
	/// <returns>The value.</returns>
	public static string? Text(CsvReader reader, int column, string name) => reader.GetStringOrNull(Column(column, name));

	/// <summary>The field parsed as <typeparamref name="T"/>, as <c>row.Parse&lt;T&gt;(name)</c>.</summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="column">The index of the column, or -1.</param>
	/// <param name="name">The name of the column.</param>
	/// <returns>The value.</returns>
	public static T Parse<T>(CsvReader reader, int column, string name)
#if NET
		where T : ISpanParsable<T>
#endif
		=> reader.Parse<T>(Column(column, name));

#if NET
	/// <summary>The field parsed as the enum <typeparamref name="TEnum"/>, as <c>row.Parse&lt;TEnum&gt;(name)</c>.</summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="reader">The reader.</param>
	/// <param name="column">The index of the column, or -1.</param>
	/// <param name="name">The name of the column.</param>
	/// <returns>The value.</returns>
	public static TEnum ParseEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(CsvReader reader, int column, string name)
		where TEnum : struct, Enum
		=> reader.Parse<TEnum>(Column(column, name));
#endif

	private static int Column(int column, string name) =>
		column >= 0 ? column : throw new KeyNotFoundException($"The record has no column named '{name}'.");
}
