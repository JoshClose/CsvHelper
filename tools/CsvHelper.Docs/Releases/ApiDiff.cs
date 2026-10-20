using System;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Docs.Api;

namespace CsvHelper.Docs.Releases;

internal enum ChangeKind
{
	Added,
	Changed,
	Deprecated,
	Removed,
}

/// <summary>One API change between two versions.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Breaking">Whether code compiled against the old version can break.</param>
/// <param name="Id">The documentation ID in the newer version (the older one for removals).</param>
/// <param name="Title"><c>CsvReader</c> or <c>CsvReader.GetString(int)</c>.</param>
/// <param name="Before">The old declaration, when it is useful to show.</param>
/// <param name="After">The new declaration, when it is useful to show.</param>
/// <param name="Note">What exactly changed, or the <c>[Obsolete]</c> message.</param>
/// <param name="Frameworks">The target frameworks it applies to; empty for all of them.</param>
internal sealed record ApiChange(ChangeKind Kind, bool Breaking, string Id, string Title, string? Before, string? After, string? Note, IReadOnlyList<string> Frameworks)
{
	/// <summary>The documentation ID of the type the change belongs to (its own ID when the change is to a type).</summary>
	public required string TypeId { get; init; }

	/// <summary>The display name of that type: <c>CsvReader</c>, <c>CsvParser.ProcessedField</c>.</summary>
	public required string TypeName { get; init; }

	/// <summary>The kind of that type: class, struct, interface, enum, delegate and so on.</summary>
	public required string TypeKind { get; init; }

	/// <summary>The namespace of that type; empty for the global namespace.</summary>
	public required string Namespace { get; init; }

	/// <summary>What the change is to: class, struct, enum, method, property, constructor and so on.</summary>
	public required string SymbolKind { get; init; }

	/// <summary>Whether the change is to a type itself rather than to one of its members.</summary>
	public bool IsType => Id == TypeId;
}

/// <summary>The changes between two API snapshots, classified as breaking or not.</summary>
internal sealed class ApiDiff
{
	/// <summary>The target frameworks both snapshots have; framework changes of types and members are only reported for these.</summary>
	private HashSet<string> common = [];

	public List<ApiChange> Changes { get; } = [];

	/// <summary>The target frameworks the newer version adds.</summary>
	public List<string> FrameworksAdded { get; } = [];

	/// <summary>The target frameworks the newer version no longer has.</summary>
	public List<string> FrameworksRemoved { get; } = [];

	public IEnumerable<ApiChange> Breaking => Changes.Where(c => c.Breaking);

	/// <summary>The changes a migration guide lists: the breaking ones, the deprecations, and other changes that have a note.</summary>
	public IEnumerable<ApiChange> Migration => Changes.Where(c => c.Breaking || c.Kind == ChangeKind.Deprecated || c.Kind == ChangeKind.Changed && c.Note is not null);

	/// <summary>Whether a migration guide has something to show: changes to act on, or a target framework that is gone.</summary>
	public bool HasMigration => FrameworksRemoved.Count > 0 || Migration.Any();

	/// <summary>The number of changes of a kind.</summary>
	/// <param name="kind">The kind.</param>
	/// <returns>The number.</returns>
	public int Count(ChangeKind kind) => Changes.Count(c => c.Kind == kind);

	/// <summary>The changes from one API snapshot to the next, sorted by title.</summary>
	/// <param name="before">The older version's API.</param>
	/// <param name="after">The newer version's API.</param>
	/// <returns>The diff.</returns>
	public static ApiDiff Compare(ApiSnapshot before, ApiSnapshot after)
	{
		var diff = new ApiDiff();
		diff.FrameworksAdded.AddRange(after.Frameworks.Except(before.Frameworks));
		diff.FrameworksRemoved.AddRange(before.Frameworks.Except(after.Frameworks));
		diff.common = before.Frameworks.Intersect(after.Frameworks).ToHashSet();

		var oldTypes = before.Types.ToDictionary(t => t.Id);
		var newTypes = after.Types.ToDictionary(t => t.Id);
		foreach (var type in before.Types.Where(t => !newTypes.ContainsKey(t.Id)))
		{
			diff.Changes.Add(TypeChange(type, ChangeKind.Removed, true, First(type.Frameworks).Header, null, null, []));
		}

		// The types whose base type changed: members they no longer declare may still be inherited.
		var rebased = new Dictionary<string, string?>();
		foreach (var type in after.Types)
		{
			if (!oldTypes.TryGetValue(type.Id, out var old))
			{
				diff.Changes.Add(TypeChange(type, ChangeKind.Added, false, null, First(type.Frameworks).Header, null, []));
				continue;
			}

			diff.Frameworks(old.Frameworks.Keys, type.Frameworks.Keys, (kind, breaking, note, frameworks) =>
				TypeChange(type, kind, breaking, kind == ChangeKind.Removed ? First(old.Frameworks).Header : null,
					kind == ChangeKind.Added ? First(type.Frameworks).Header : null, note, frameworks));
			foreach (var group in GroupByFramework(old.Frameworks, type.Frameworks, CompareType, TypeKey))
			{
				var (kind, breaking, note) = group.Change;
				diff.Changes.Add(TypeChange(type, kind, breaking, group.Before.Header, group.After.Header, note, group.Frameworks));
			}

			if (First(old.Frameworks).BaseType != First(type.Frameworks).BaseType)
			{
				rebased[type.Id] = First(type.Frameworks).BaseType;
			}
		}

		// Members of added or removed types are covered by the type's entry.
		var oldMembers = before.Members.Where(m => newTypes.ContainsKey(m.TypeId)).ToDictionary(m => m.Id);
		var newMembers = after.Members.Where(m => oldTypes.ContainsKey(m.TypeId)).ToDictionary(m => m.Id);
		var removed = oldMembers.Values.Where(m => !newMembers.ContainsKey(m.Id)).ToList();
		var added = newMembers.Values.Where(m => !oldMembers.ContainsKey(m.Id)).ToList();
		foreach (var (gone, match) in Pair(removed, added))
		{
			removed.Remove(gone);
			added.Remove(match);
			diff.Changes.Add(MemberChange(newTypes[match.TypeId], match, ChangeKind.Changed, true, First(gone.Frameworks).Signature,
				First(match.Frameworks).Signature, ParametersNote(gone, match), []));
		}

		foreach (var member in removed)
		{
			var type = oldTypes[member.TypeId];
			var note = rebased.TryGetValue(member.TypeId, out var baseType)
				? $"No longer declared by {type.Name}; it may still be inherited from {baseType ?? "object"}."
				: null;
			diff.Changes.Add(MemberChange(type, member, ChangeKind.Removed, true, First(member.Frameworks).Signature, null, note, []));
		}

		foreach (var member in added)
		{
			var type = newTypes[member.TypeId];
			var facets = First(member.Frameworks);
			// Derived classes outside the library don't implement a new abstract member.
			var breaking = facets.IsAbstract && First(type.Frameworks).Inheritable;
			diff.Changes.Add(MemberChange(type, member, ChangeKind.Added, breaking, null, facets.Signature,
				breaking ? "A new abstract member: classes that derive from the type must implement it." : null, []));
		}

		foreach (var member in newMembers.Values.Where(m => oldMembers.ContainsKey(m.Id)))
		{
			var old = oldMembers[member.Id];
			var type = newTypes[member.TypeId];
			var inheritable = First(type.Frameworks).Inheritable;
			diff.Frameworks(old.Frameworks.Keys, member.Frameworks.Keys, (kind, breaking, note, frameworks) =>
				MemberChange(type, member, kind, breaking, kind == ChangeKind.Removed ? First(old.Frameworks).Signature : null,
					kind == ChangeKind.Added ? First(member.Frameworks).Signature : null, note, frameworks));
			var groups = GroupByFramework(old.Frameworks, member.Frameworks, (b, a) => CompareMember(b, a, inheritable),
				f => f.Signature + "|" + f.ConstantValue + "|" + f.Obsolete?.Message);
			foreach (var group in groups)
			{
				var (kind, breaking, note) = group.Change;
				diff.Changes.Add(MemberChange(type, member, kind, breaking, group.Before.Signature, group.After.Signature, note, group.Frameworks));
			}
		}

		diff.Changes.Sort((a, b) => string.CompareOrdinal(a.Title, b.Title));

		return diff;
	}

	/// <summary>
	/// The namespace of a snapshot type: its documentation ID without the type's own name, which has as many dotted
	/// parts as the display name (<c>T:CsvHelper.CsvParser.ProcessedField</c> and <c>CsvParser.ProcessedField</c> give <c>CsvHelper</c>).
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The namespace; empty for the global namespace.</returns>
	public static string NamespaceOf(SnapshotType type)
	{
		var parts = type.Id[2..].Split('.');
		var depth = 0;
		var nameParts = 1;
		foreach (var c in type.Name)
		{
			if (c == '<')
			{
				depth++;
			}
			else if (c == '>')
			{
				depth--;
			}
			else if (c == '.' && depth == 0)
			{
				nameParts++;
			}
		}

		return string.Join(".", parts.Take(Math.Max(0, parts.Length - nameParts)));
	}

	/// <summary>A change to a type.</summary>
	/// <param name="type">The type.</param>
	/// <param name="kind">What happened.</param>
	/// <param name="breaking">Whether it breaks code written against the older version.</param>
	/// <param name="before">The old declaration, or null.</param>
	/// <param name="after">The new declaration, or null.</param>
	/// <param name="note">What changed, or null.</param>
	/// <param name="frameworks">The target frameworks it applies to; empty for all.</param>
	/// <returns>The change.</returns>
	private static ApiChange TypeChange(SnapshotType type, ChangeKind kind, bool breaking, string? before, string? after, string? note, IReadOnlyList<string> frameworks) =>
		new(kind, breaking, type.Id, type.Name, before, after, note, frameworks)
		{
			TypeId = type.Id,
			TypeName = type.Name,
			TypeKind = type.Kind,
			Namespace = NamespaceOf(type),
			SymbolKind = type.Kind,
		};

	/// <summary>A change to a member of a type.</summary>
	/// <param name="type">The member's type.</param>
	/// <param name="member">The member.</param>
	/// <param name="kind">What happened.</param>
	/// <param name="breaking">Whether it breaks code written against the older version.</param>
	/// <param name="before">The old signature, or null.</param>
	/// <param name="after">The new signature, or null.</param>
	/// <param name="note">What changed, or null.</param>
	/// <param name="frameworks">The target frameworks it applies to; empty for all.</param>
	/// <returns>The change.</returns>
	private static ApiChange MemberChange(SnapshotType type, SnapshotMember member, ChangeKind kind, bool breaking, string? before, string? after, string? note,
		IReadOnlyList<string> frameworks) =>
		new(kind, breaking, member.Id, Title(type, member), before, after, note, frameworks)
		{
			TypeId = type.Id,
			TypeName = type.Name,
			TypeKind = type.Kind,
			Namespace = NamespaceOf(type),
			SymbolKind = member.Kind.ToLowerInvariant(),
		};

	/// <summary>The title of a member's change: <c>CsvReader.GetString(int)</c>.</summary>
	/// <param name="type">The member's type.</param>
	/// <param name="member">The member.</param>
	/// <returns>The title.</returns>
	private static string Title(SnapshotType type, SnapshotMember member) => $"{type.Name}.{member.ShortName}";

	/// <summary>The facets of the first target framework, which stand for all of them where they agree.</summary>
	/// <typeparam name="T">The facets' type.</typeparam>
	/// <param name="frameworks">The facets by framework.</param>
	/// <returns>The first framework's facets.</returns>
	private static T First<T>(Dictionary<string, T> frameworks) => frameworks.Values.First();

	/// <summary>
	/// Records a type or member that was removed from, or added to, some target frameworks that both versions have. A
	/// framework that only one version has is reported once, in <see cref="FrameworksAdded"/> or <see cref="FrameworksRemoved"/>.
	/// </summary>
	/// <param name="before">The frameworks that had it before.</param>
	/// <param name="after">The frameworks that have it now.</param>
	/// <param name="create">Creates the change from its kind, whether it breaks, its note and its frameworks.</param>
	private void Frameworks(IEnumerable<string> before, IEnumerable<string> after, Func<ChangeKind, bool, string, IReadOnlyList<string>, ApiChange> create)
	{
		var gone = before.Except(after).Where(common.Contains).ToList();
		var come = after.Except(before).Where(common.Contains).ToList();
		if (gone.Count > 0)
		{
			Changes.Add(create(ChangeKind.Removed, true, $"Removed from {string.Join(", ", gone)}.", gone));
		}

		if (come.Count > 0)
		{
			Changes.Add(create(ChangeKind.Added, false, $"Added to {string.Join(", ", come)}.", come));
		}
	}

	private sealed record FrameworkGroup<T>(IReadOnlyList<string> Frameworks, T Before, T After, (ChangeKind Kind, bool Breaking, string? Note) Change);

	/// <summary>Compares the frameworks both versions have, and merges those with the same change into one entry.</summary>
	/// <typeparam name="T">The facets' type.</typeparam>
	/// <param name="before">The older version's facets by framework.</param>
	/// <param name="after">The newer version's facets by framework.</param>
	/// <param name="compare">The change between two facets, or null when there is none.</param>
	/// <param name="key">What makes facets the same, for merging frameworks.</param>
	/// <returns>The changes, each with its frameworks (empty when it is in all of them).</returns>
	private static List<FrameworkGroup<T>> GroupByFramework<T>(Dictionary<string, T> before, Dictionary<string, T> after,
		Func<T, T, (ChangeKind, bool, string?)?> compare, Func<T, string> key)
	{
		var common = before.Keys.Intersect(after.Keys).ToList();
		var groups = new List<FrameworkGroup<T>>();
		foreach (var framework in common)
		{
			if (compare(before[framework], after[framework]) is not { } change)
			{
				continue;
			}

			var existing = groups.FindIndex(g => g.Change == change && key(g.Before) == key(before[framework]) && key(g.After) == key(after[framework]));
			if (existing >= 0)
			{
				groups[existing] = groups[existing] with { Frameworks = [.. groups[existing].Frameworks, framework] };
			}
			else
			{
				groups.Add(new FrameworkGroup<T>([framework], before[framework], after[framework], change));
			}
		}

		// A change in every common framework applies to all of them.
		return groups.Select(g => g.Frameworks.Count == common.Count ? g with { Frameworks = [] } : g).ToList();
	}

	/// <summary>What makes two declarations of a type the same, whatever the order of its interfaces.</summary>
	/// <param name="facets">The type's facets in one framework.</param>
	/// <returns>The key.</returns>
	private static string TypeKey(TypeFacets facets) =>
		$"{facets.Modifiers}|{facets.BaseType}|{string.Join(",", facets.Interfaces.Order(StringComparer.Ordinal))}|{facets.Obsolete?.Message}";

	/// <summary>The declaration with its interfaces in order, so that a different order is not a change.</summary>
	/// <param name="facets">The type's facets in one framework.</param>
	/// <returns>The declaration.</returns>
	private static string CanonicalHeader(TypeFacets facets) =>
		facets.Interfaces.Count < 2
			? facets.Header
			: facets.Header.Replace(string.Join(", ", facets.Interfaces), string.Join(", ", facets.Interfaces.Order(StringComparer.Ordinal)), StringComparison.Ordinal);

	/// <summary>
	/// How a type changed: deprecated, another kind of type, or (all that apply) newly sealed, abstract or static, no longer
	/// so, another base type, interfaces lost or gained.
	/// </summary>
	/// <param name="before">The type in the older version.</param>
	/// <param name="after">The type in the newer version.</param>
	/// <returns>The kind, whether it breaks, and the note; null when nothing changed.</returns>
	private static (ChangeKind, bool, string?)? CompareType(TypeFacets before, TypeFacets after)
	{
		if (after.Obsolete is { } obsolete && before.Obsolete is null)
		{
			return (ChangeKind.Deprecated, obsolete.IsError, obsolete.Message ?? "Marked [Obsolete].");
		}

		if (before.Header == after.Header || CanonicalHeader(before) == CanonicalHeader(after))
		{
			return null;
		}

		var oldWords = before.Modifiers.Split(' ');
		var newWords = after.Modifiers.Split(' ');
		if (oldWords[^1] != newWords[^1] || oldWords.Contains("record") != newWords.Contains("record"))
		{
			return (ChangeKind.Changed, true, $"The type changed from {before.Modifiers} to {after.Modifiers}.");
		}

		var notes = new List<string>();
		var breaking = false;
		var restricted = newWords.Except(oldWords).Except(["readonly"]).ToList();
		if (restricted.Count > 0)
		{
			notes.Add($"The type is now {string.Join(" ", restricted)}.");
			breaking = true;
		}

		var relaxed = oldWords.Except(newWords).Intersect(["sealed", "abstract", "static"]).ToList();
		if (relaxed.Count > 0)
		{
			notes.Add($"The type is no longer {string.Join(" or ", relaxed)}.");
		}

		var rebased = before.BaseType != after.BaseType;
		if (rebased)
		{
			notes.Add($"The base type changed from {before.BaseType ?? "object"} to {after.BaseType ?? "object"}.");
			breaking = true;
		}

		// With a new base type, an interface the type no longer declares may come from the base.
		var lost = before.Interfaces.Except(after.Interfaces).Order(StringComparer.Ordinal).ToList();
		if (lost.Count > 0 && (!rebased || after.BaseType is null))
		{
			notes.Add($"The type no longer implements {string.Join(", ", lost)}.");
			breaking = true;
		}

		var gained = after.Interfaces.Except(before.Interfaces).Order(StringComparer.Ordinal).ToList();
		if (gained.Count > 0)
		{
			notes.Add($"The type now implements {string.Join(", ", gained)}.");
		}

		return (ChangeKind.Changed, breaking, notes.Count > 0 ? string.Join(" ", notes) : "The declaration changed.");
	}

	/// <summary>How a member with the same documentation ID changed (the same name and parameter types).</summary>
	/// <param name="before">The member in the older version.</param>
	/// <param name="after">The member in the newer version.</param>
	/// <param name="inheritable">Whether code outside the library can derive from the member's type now.</param>
	/// <returns>The kind, whether it breaks, and the note; null when nothing changed.</returns>
	private static (ChangeKind, bool, string?)? CompareMember(MemberFacets before, MemberFacets after, bool inheritable)
	{
		if (after.Obsolete is { } obsolete && before.Obsolete is null)
		{
			return (ChangeKind.Deprecated, obsolete.IsError, obsolete.Message ?? "Marked [Obsolete].");
		}

		if (before.Signature == after.Signature && before.ConstantValue == after.ConstantValue)
		{
			return null;
		}

		if (before.ConstantValue != after.ConstantValue)
		{
			return (ChangeKind.Changed, true, $"The value changed from {before.ConstantValue} to {after.ConstantValue}.");
		}

		var lost = before.Accessors.Except(after.Accessors).ToList();
		if (lost.Count > 0)
		{
			var note = lost.Contains("set") && after.Accessors.Contains("init")
				? "The property can only be set in an object initializer now (set became init)."
				: $"The {string.Join(" and ", lost)} accessor was removed.";

			return (ChangeKind.Changed, true, note);
		}

		if (before.SignatureWithoutNullability == after.SignatureWithoutNullability)
		{
			return (ChangeKind.Changed, false, "Only nullable annotations changed.");
		}

		if (before.SignatureWithoutDefaults == after.SignatureWithoutDefaults)
		{
			return (ChangeKind.Changed, false, "A default parameter value changed: callers that omit the argument get the new value once they recompile.");
		}

		var gained = after.Accessors.Except(before.Accessors).ToList();
		if (gained.Count > 0 && before.Accessors.All(after.Accessors.Contains))
		{
			return (ChangeKind.Changed, false, $"The property now has a {string.Join(" and ", gained)} accessor.");
		}

		if (MemberShape.TryParse(before.SignatureWithoutNullability) is { } old && MemberShape.TryParse(after.SignatureWithoutNullability) is { } now
			&& Describe(old, now, inheritable) is { } change)
		{
			return change;
		}

		return (ChangeKind.Changed, true, "The signature changed.");
	}

	/// <summary>
	/// The change between two declarations of the same member when it is in what this can describe: the return type, the
	/// constraints, the inheritance modifiers and the parameter names. Null when anything else differs (other modifiers,
	/// parameter types or modifiers, type parameters, accessors).
	/// </summary>
	/// <param name="before">The older declaration.</param>
	/// <param name="after">The newer declaration.</param>
	/// <param name="inheritable">Whether code outside the library can derive from the member's type now.</param>
	/// <returns>The kind, whether it breaks, and the note.</returns>
	private static (ChangeKind, bool, string?)? Describe(MemberShape before, MemberShape after, bool inheritable)
	{
		var sameModifiers = before.CoreModifiers.Order().SequenceEqual(after.CoreModifiers.Order());
		var sameParameters = before.Parameters.Count == after.Parameters.Count
			&& before.Parameters.Zip(after.Parameters).All(p => p.First with { Name = "" } == p.Second with { Name = "" });
		if (!sameModifiers || !sameParameters || before.TypeParameters != after.TypeParameters || before.Accessors != after.Accessors)
		{
			return null;
		}

		var notes = new List<string>();
		var breaking = false;
		if (before.ReturnType != after.ReturnType)
		{
			var what = after.Kind is "method" or "operator" ? "return type" : "type";
			notes.Add($"The {what} changed from {before.ReturnType} to {after.ReturnType}.");
			breaking = true;
		}

		if (before.Constraints != after.Constraints)
		{
			notes.Add(before.Constraints.Length == 0 ? $"New type parameter constraints: {after.Constraints}."
				: after.Constraints.Length == 0 ? $"The type parameter constraints were removed ({before.Constraints})."
				: $"The type parameter constraints changed from {before.Constraints} to {after.Constraints}.");
			breaking |= after.Constraints.Length > 0;
		}

		if (before.Overridable && !after.Overridable)
		{
			if (inheritable)
			{
				notes.Add("No longer virtual: derived classes can't override it.");
				breaking = true;
			}
			else
			{
				notes.Add("No longer virtual (the type can't be derived from).");
			}
		}

		if (!before.Has("abstract") && after.Has("abstract") && inheritable)
		{
			notes.Add("Now abstract: derived classes must implement it.");
			breaking = true;
		}
		else if (before.Has("abstract") && !after.Has("abstract") && after.Overridable)
		{
			notes.Add("No longer abstract: derived classes may still override it.");
		}

		if (!before.Has("override") && after.Has("override"))
		{
			notes.Add("Now overrides the base class's member.");
		}
		else if (!before.Overridable && after.Overridable)
		{
			notes.Add("Now virtual.");
		}

		var renamed = before.Parameters.Zip(after.Parameters).Where(p => p.First.Name != p.Second.Name).ToList();
		if (renamed.Count == 1)
		{
			notes.Add($"Parameter {renamed[0].First.Name} is now named {renamed[0].Second.Name} (this only affects calls that name the argument).");
		}
		else if (renamed.Count > 1)
		{
			notes.Add($"Parameters {string.Join(", ", renamed.Select(p => p.First.Name))} are now named {string.Join(", ", renamed.Select(p => p.Second.Name))} " +
				"(this only affects calls that name the arguments).");
		}

		return (ChangeKind.Changed, breaking, notes.Count > 0 ? string.Join(" ", notes) : "Only modifiers changed.");
	}

	/// <summary>The name, generic arity, accessibility and parameter types of a member, for pairing overloads.</summary>
	private sealed record Overload(int Arity, string Access, IReadOnlyList<string> Parameters, IReadOnlyList<bool> Optional);

	/// <summary>
	/// Pairs removed and added members of the same type, name and kind that are the same member with other parameters:
	/// optional parameters added or removed at the end, a parameter of another type, or the only overload either way. A
	/// pair is a changed member; anything left over is removed or added.
	/// </summary>
	/// <param name="removed">The members only the older version has.</param>
	/// <param name="added">The members only the newer version has.</param>
	/// <returns>The pairs, each member in at most one.</returns>
	private static List<(SnapshotMember Removed, SnapshotMember Added)> Pair(List<SnapshotMember> removed, List<SnapshotMember> added)
	{
		var pairs = new List<(SnapshotMember, SnapshotMember)>();
		foreach (var group in removed.GroupBy(m => (m.TypeId, m.Name, m.Kind)))
		{
			var gone = group.ToList();
			var candidates = added.Where(m => (m.TypeId, m.Name, m.Kind) == group.Key).ToList();
			if (candidates.Count == 0)
			{
				continue;
			}

			var only = gone.Count == 1 && candidates.Count == 1;
			var scored = new List<(int Score, int Common, SnapshotMember Removed, SnapshotMember Added)>();
			foreach (var old in gone)
			{
				foreach (var now in candidates)
				{
					var (oldOverload, newOverload) = (Parse(old), Parse(now));
					if (Score(oldOverload, newOverload, only) is { } score)
					{
						scored.Add((score, oldOverload.Parameters.Zip(newOverload.Parameters).TakeWhile(p => p.First == p.Second).Count(), old, now));
					}
				}
			}

			var used = new HashSet<SnapshotMember>();
			foreach (var (_, _, old, now) in scored.OrderBy(s => s.Score).ThenByDescending(s => s.Common))
			{
				if (used.Contains(old) || used.Contains(now))
				{
					continue;
				}

				used.Add(old);
				used.Add(now);
				pairs.Add((old, now));
			}
		}

		return pairs;
	}

	/// <summary>How alike two overloads are (lower is closer), or null when they are not the same member.</summary>
	/// <param name="before">The removed overload.</param>
	/// <param name="after">The added overload.</param>
	/// <param name="only">Whether they are the only removed and the only added overload of their name.</param>
	/// <returns>0 when one adds optional parameters to the other, 1 when they share a parameter, 2 for the only ones; null otherwise.</returns>
	private static int? Score(Overload before, Overload after, bool only)
	{
		if (before.Arity != after.Arity || before.Access != after.Access)
		{
			return null;
		}

		var (shorter, longer) = before.Parameters.Count <= after.Parameters.Count ? (before, after) : (after, before);
		if (shorter.Parameters.SequenceEqual(longer.Parameters.Take(shorter.Parameters.Count))
			&& longer.Optional.Skip(shorter.Parameters.Count).All(o => o))
		{
			return 0;
		}

		if (before.Parameters.Count == after.Parameters.Count && before.Parameters.Zip(after.Parameters).Any(p => p.First == p.Second))
		{
			return 1;
		}

		return only ? 2 : null;
	}

	/// <summary>The parts of a member that pairing compares, from its short name and signature.</summary>
	/// <param name="member">The member.</param>
	/// <returns>The overload.</returns>
	private static Overload Parse(SnapshotMember member)
	{
		var name = member.ShortName;
		var open = name.IndexOfAny(['(', '[']);
		var head = open < 0 ? name : name[..open];
		var parameters = open < 0 ? [] : SplitTopLevel(name[(open + 1)..^1]);
		var angle = head.IndexOf('<');
		var arity = angle < 0 ? 0 : SplitTopLevel(head[(angle + 1)..^1]).Count;
		var signature = First(member.Frameworks).Signature;
		var access = string.Join(" ", signature.Split(' ').TakeWhile(w => w is "public" or "protected" or "internal" or "private"));
		var shape = MemberShape.TryParse(First(member.Frameworks).SignatureWithoutNullability);
		var optional = shape?.Parameters.Select(p => p.Default is not null || p.Modifiers.Contains("params")).ToList()
			?? parameters.Select(_ => false).ToList();

		return new Overload(arity, access, parameters, optional);
	}

	/// <summary>Splits a comma-separated list at the commas that are not inside brackets: <c>Dictionary&lt;string, int&gt;, int</c>.</summary>
	/// <param name="text">The list.</param>
	/// <returns>The items, trimmed; none for an empty list.</returns>
	private static List<string> SplitTopLevel(string text)
	{
		var parts = new List<string>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return parts;
		}

		var depth = 0;
		var start = 0;
		for (var i = 0; i < text.Length; i++)
		{
			switch (text[i])
			{
				case '<' or '(' or '[':
					depth++;
					break;
				case '>' or ')' or ']':
					depth--;
					break;
				case ',' when depth == 0:
					parts.Add(text[start..i].Trim());
					start = i + 1;
					break;
			}
		}

		parts.Add(text[start..].Trim());

		return parts;
	}

	/// <summary>How the parameters of a changed member differ.</summary>
	/// <param name="before">The removed overload.</param>
	/// <param name="after">The added overload it was paired with.</param>
	/// <returns>The note.</returns>
	private static string ParametersNote(SnapshotMember before, SnapshotMember after)
	{
		var (old, now) = (Parse(before), Parse(after));
		if (now.Parameters.Count > old.Parameters.Count && Score(old, now, false) == 0)
		{
			return $"New optional parameters ({string.Join(", ", now.Parameters.Skip(old.Parameters.Count))}): calls compile unchanged, but code built " +
				"against the old version must be recompiled.";
		}

		return $"The parameters changed from ({string.Join(", ", old.Parameters)}) to ({string.Join(", ", now.Parameters)}).";
	}
}
