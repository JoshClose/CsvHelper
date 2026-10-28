using System.Linq.Expressions;

namespace CsvHelper.Linq;

/// <summary>
/// A node of a query and its ancestors, up to the top of the query: the path that the planner walks down, which the
/// analysis of the members a query uses walks back up. Immutable, so it can be shared.
/// </summary>
internal sealed class Chain
{
	/// <summary>Creates a link.</summary>
	/// <param name="node">The node.</param>
	/// <param name="parent">The link of its parent, or <see langword="null"/> for the top of the query.</param>
	public Chain(Expression node, Chain? parent)
	{
		Node = node;
		Parent = parent;
	}

	/// <summary>The node.</summary>
	public Expression Node { get; }

	/// <summary>The link of its parent, or <see langword="null"/> for the top of the query.</summary>
	public Chain? Parent { get; }

	/// <summary>The link of a child of <see cref="Node"/>.</summary>
	/// <param name="child">The child.</param>
	/// <returns>The child's link, whose parent is this one.</returns>
	public Chain Push(Expression child) => new(child, this);
}
