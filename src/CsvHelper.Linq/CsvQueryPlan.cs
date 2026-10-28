using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CsvHelper.Linq;

/// <summary>
/// How a CSV query runs: for each CSV source it reads, what the reader does; what it asks other providers for; and the
/// operators that LINQ to Objects runs on the records. <see cref="CsvQueryable.Explain(System.Linq.IQueryable)"/> and
/// <see cref="CsvQueryOptions.OnExecute"/> give it.
/// </summary>
public sealed class CsvQueryPlan
{
	/// <summary>Creates a plan.</summary>
	/// <param name="sources">What the reader of each CSV source does.</param>
	/// <param name="foreignQueries">The queries of other providers that the query runs.</param>
	/// <param name="inMemoryOperators">The operators that LINQ to Objects runs.</param>
	internal CsvQueryPlan(IList<CsvSourcePlan> sources, IList<CsvForeignQueryPlan> foreignQueries, IList<string> inMemoryOperators)
	{
		Sources = new ReadOnlyCollection<CsvSourcePlan>(sources);
		ForeignQueries = new ReadOnlyCollection<CsvForeignQueryPlan>(foreignQueries);
		InMemoryOperators = new ReadOnlyCollection<string>(inMemoryOperators);
	}

	/// <summary>What the reader of each CSV source that the query reads does, in the order the query names them.</summary>
	public IReadOnlyList<CsvSourcePlan> Sources { get; }

	/// <summary>The queries of other LINQ providers that the query runs, such as tables of a database.</summary>
	public IReadOnlyList<CsvForeignQueryPlan> ForeignQueries { get; }

	/// <summary>The names of the LINQ operators that LINQ to Objects runs on the records, innermost first.</summary>
	public IReadOnlyList<string> InMemoryOperators { get; }

	/// <summary>The plan, as lines of text.</summary>
	/// <returns>A line for each source and foreign query, and one for the operators that LINQ to Objects runs.</returns>
	public override string ToString()
	{
		var text = new StringBuilder();
		foreach (var source in Sources)
		{
			text.AppendLine(source.ToString());
		}

		foreach (var query in ForeignQueries)
		{
			text.AppendLine(query.ToString());
		}

		text.Append("In memory: ").Append(InMemoryOperators.Count == 0 ? "nothing" : string.Join(", ", InMemoryOperators));

		return text.ToString();
	}
}

/// <summary>What the reader of one CSV source of a query does.</summary>
public sealed class CsvSourcePlan
{
	/// <summary>Creates the plan of a source.</summary>
	/// <param name="source">The source, such as <c>file 'orders.csv'</c>.</param>
	/// <param name="recordType">The type of its records.</param>
	/// <param name="skippedRows">The rows the reader skips without reading them.</param>
	/// <param name="pushedOperators">The operators the reader runs.</param>
	/// <param name="filterMembers">The members read for every row to filter the records, or <see langword="null"/> for all.</param>
	/// <param name="members">The members read for each record that passes, or <see langword="null"/> for all.</param>
	/// <param name="notes">Why the reader does not do more.</param>
	internal CsvSourcePlan(string source, Type recordType, long skippedRows, IList<string> pushedOperators, IList<string>? filterMembers,
		IList<string>? members, IList<string> notes)
	{
		Source = source;
		RecordType = recordType;
		SkippedRows = skippedRows;
		PushedOperators = new ReadOnlyCollection<string>(pushedOperators);
		FilterMembers = filterMembers is null ? null : new ReadOnlyCollection<string>(filterMembers);
		Members = members is null ? null : new ReadOnlyCollection<string>(members);
		Notes = new ReadOnlyCollection<string>(notes);
	}

	/// <summary>The source, such as <c>file 'orders.csv'</c>.</summary>
	public string Source { get; }

	/// <summary>The type of the records: a <see cref="CsvRecordAttribute"/> type, or <see cref="CsvDynamicRecord"/>.</summary>
	public Type RecordType { get; }

	/// <summary>The number of rows the reader skips without reading them (a <c>Skip</c> at the start of the query).</summary>
	public long SkippedRows { get; }

	/// <summary>
	/// The operators that the reader runs itself, such as <c>Skip(10)</c> and <c>Where</c>, innermost first; the rest of the
	/// query runs on the records it returns.
	/// </summary>
	public IReadOnlyList<string> PushedOperators { get; }

	/// <summary>
	/// The members (for <see cref="CsvDynamicRecord"/> rows, the columns) read for every row to run the filters that the
	/// reader runs (<c>Where</c>); <see langword="null"/> for all of them, empty when there is no filter or it needs none.
	/// </summary>
	public IReadOnlyList<string>? FilterMembers { get; }

	/// <summary>
	/// The members (for <see cref="CsvDynamicRecord"/> rows, the columns) read for each record that the reader returns;
	/// <see langword="null"/> for all of them. The others keep their defaults, and their columns are neither looked up nor
	/// parsed.
	/// </summary>
	public IReadOnlyList<string>? Members { get; }

	/// <summary>Why the reader reads more than the query uses, such as a record passed whole to a method.</summary>
	public IReadOnlyList<string> Notes { get; }

	/// <summary>The plan of the source, as a line of text.</summary>
	/// <returns>Such as <c>file 'orders.csv' (Order): Where; filter reads Total; reads Id, Customer</c>.</returns>
	public override string ToString()
	{
		static string List(IReadOnlyList<string>? names) => names is null ? "all members" : names.Count == 0 ? "no members" : string.Join(", ", names);

		var text = new StringBuilder(Source).Append(" (").Append(RecordType.Name).Append("): ");
		text.Append(PushedOperators.Count == 0 ? "no operators" : string.Join(", ", PushedOperators));
		if (PushedOperators.Contains("Where"))
		{
			text.Append("; filter reads ").Append(List(FilterMembers));
		}

		text.Append("; reads ").Append(List(Members));
		foreach (var note in Notes)
		{
			text.Append("; ").Append(note);
		}

		return text.ToString();
	}
}

/// <summary>A query of another LINQ provider that a CSV query runs, such as a table of Entity Framework Core.</summary>
public sealed class CsvForeignQueryPlan
{
	/// <summary>Creates the plan of a foreign query.</summary>
	/// <param name="query">The query, as text.</param>
	/// <param name="provider">The type of its provider.</param>
	/// <param name="strategy">How the CSV query uses it.</param>
	internal CsvForeignQueryPlan(string query, Type provider, string strategy)
	{
		Query = query;
		Provider = provider;
		Strategy = strategy;
	}

	/// <summary>The query, as text.</summary>
	public string Query { get; }

	/// <summary>The type of its provider.</summary>
	public Type Provider { get; }

	/// <summary>
	/// How the CSV query uses it: <c>keys</c> when it asks only for the rows with the keys of the CSV records (in batches),
	/// <c>once</c> when it reads the query once and keeps the results, or <c>subquery</c> when it runs it for each record.
	/// </summary>
	public string Strategy { get; }

	/// <summary>The plan of the foreign query, as a line of text.</summary>
	/// <returns>Such as <c>foreign (keys): DbSet&lt;Customer&gt;</c>.</returns>
	public override string ToString() => $"foreign ({Strategy}, {Provider.Name}): {Query}";
}
