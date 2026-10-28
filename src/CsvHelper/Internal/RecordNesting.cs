using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvHelper;

/// <summary>
/// Tracks the record types whose mappers or record writers are being created on the current thread, so that nested
/// mappers that lead back to their own type throw instead of overflowing the stack. The generator rejects such cycles
/// among generated mappers (CSVH004), but cannot see what a hand-written mapper creates.
/// </summary>
internal static class RecordNesting
{
	/// <summary>
	/// How deep records may nest. A hand-written mapper can still nest its own type a few times, with a prefix that ends
	/// the nesting.
	/// </summary>
	public const int MaxDepth = 64;

	// The types whose mapper or record writer is being created on this thread, outermost first.
	[ThreadStatic]
	private static List<Type>? creating;

	/// <summary>
	/// Records that the mapper or record writer of <paramref name="type"/> is being created. Every call that returns must
	/// be followed by <see cref="Exit"/>.
	/// </summary>
	/// <param name="type">The type of the records.</param>
	/// <param name="kind">What is being created, for the message: <c>"mapper"</c> or <c>"record writer"</c>.</param>
	/// <exception cref="InvalidOperationException">
	/// The records already nest <see cref="MaxDepth"/> deep: the nested mappers lead back to their own type without end.
	/// </exception>
	public static void Enter(Type type, string kind)
	{
		creating ??= [];
		if (creating.Count == MaxDepth)
		{
			throw new InvalidOperationException(Message([.. creating, type], kind));
		}

		creating.Add(type);
	}

	/// <summary>Records that the mapper or record writer passed to the last <see cref="Enter"/> has been created, or failed.</summary>
	public static void Exit() => creating!.RemoveAt(creating.Count - 1);

	/// <summary>The message of the exception that <see cref="Enter"/> throws, naming the first type that leads back to itself.</summary>
	/// <param name="types">The types being created, outermost first, with the one that went too deep.</param>
	/// <param name="kind">What is being created: <c>"mapper"</c> or <c>"record writer"</c>.</param>
	/// <returns>The message.</returns>
	private static string Message(List<Type> types, string kind)
	{
		var depth = MaxDepth.ToString(CultureInfo.InvariantCulture);
		var message = $"Creating the {kind} of {types[0]} nests records more than {depth} deep";
		for (var i = 0; i < types.Count; i++)
		{
			var again = types.IndexOf(types[i], i + 1);
			if (again > 0)
			{
				var loop = string.Join(" -> ", types.Skip(i).Take(again - i + 1).Select(t => t.Name));

				return $"{message}: {loop} repeats without end. A hand-written {kind} creates a nested {kind} that leads back to its own type.";
			}
		}

		return $"{message}.";
	}
}
