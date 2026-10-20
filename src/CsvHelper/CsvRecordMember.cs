using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CsvHelper;

/// <summary>How a <see cref="CsvRecordMember"/> gets its value.</summary>
public enum CsvRecordMemberKind
{
	/// <summary>From one column, found by <see cref="CsvRecordMember.ColumnNames"/> or <see cref="CsvRecordMember.ColumnIndex"/>.</summary>
	Column = 0,

	/// <summary>
	/// From several columns, into an array, list or dictionary: every header column with one of
	/// <see cref="CsvRecordMember.ColumnNames"/>, or the columns from <see cref="CsvRecordMember.ColumnIndex"/> on.
	/// </summary>
	Collection = 1,

	/// <summary>From the columns of the members of its own record type (a member whose type is a record type).</summary>
	Nested = 2,

	/// <summary>From no column: it always has the value of its <see cref="CsvConstantAttribute"/>.</summary>
	Constant = 3,
}

/// <summary>
/// A member of a <see cref="CsvRecordAttribute"/> type that its generated mapper reads, as
/// <see cref="CsvRecordMapper.TryGetMembers{T}(out IReadOnlyList{CsvRecordMember})"/> describes it. A projection
/// (<see cref="CsvRecordMapper.CreateProjection{T}(CsvReader, IEnumerable{string})"/>) selects members by their
/// <see cref="Name"/>.
/// </summary>
public sealed class CsvRecordMember
{
	/// <summary>Describes a member. Code generated for <see cref="CsvRecordAttribute"/> types creates these.</summary>
	/// <param name="name">
	/// The name of the property or field, or of the constructor parameter when it sets neither.
	/// </param>
	/// <param name="type">The type of the member.</param>
	/// <param name="kind">How the member gets its value.</param>
	/// <param name="columnNames">
	/// The header names it reads: its column's name and alternate names, in the order they are tried, or a collection's
	/// names; empty for a nested record, a constant, and a collection read by index.
	/// </param>
	/// <param name="columnIndex">
	/// The 0-based index of its column (the first column of a collection), or -1 when it is found by name or has none.
	/// </param>
	/// <param name="optional">Whether its columns may be missing from the header.</param>
	/// <param name="isConstructorParameter">Whether a constructor parameter sets it.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="name"/>, <paramref name="type"/> or <paramref name="columnNames"/> is <see langword="null"/>.
	/// </exception>
	public CsvRecordMember(string name, Type type, CsvRecordMemberKind kind, IReadOnlyList<string> columnNames, int columnIndex, bool optional,
		bool isConstructorParameter)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(columnNames);
		var names = new string[columnNames.Count];
		for (var i = 0; i < names.Length; i++)
		{
			names[i] = columnNames[i];
		}

		Name = name;
		Type = type;
		Kind = kind;
		ColumnNames = new ReadOnlyCollection<string>(names);
		ColumnIndex = columnIndex;
		Optional = optional;
		IsConstructorParameter = isConstructorParameter;
	}

	/// <summary>The name of the property or field, or of the constructor parameter when it sets neither.</summary>
	public string Name { get; }

	/// <summary>The type of the member.</summary>
	public Type Type { get; }

	/// <summary>How the member gets its value.</summary>
	public CsvRecordMemberKind Kind { get; }

	/// <summary>
	/// The header names it reads: its column's name and alternate names, in the order they are tried, or a collection's
	/// names; empty for a nested record, a constant, and a collection read by index.
	/// </summary>
	public IReadOnlyList<string> ColumnNames { get; }

	/// <summary>
	/// The 0-based index of its column (the first column of a collection), or -1 when it is found by name or has none.
	/// </summary>
	public int ColumnIndex { get; }

	/// <summary>Whether its columns may be missing from the header.</summary>
	public bool Optional { get; }

	/// <summary>Whether a constructor parameter sets it.</summary>
	public bool IsConstructorParameter { get; }

	/// <summary>The name of the member, its kind and its columns, for debugging.</summary>
	/// <returns>Such as <c>Price (Column 'Unit Price')</c>.</returns>
	public override string ToString()
	{
		var columns = ColumnNames.Count > 0 ? " '" + string.Join("' or '", ColumnNames) + "'" : "";
		var index = ColumnIndex >= 0 ? $" #{ColumnIndex}" : "";

		return $"{Name} ({Kind}{columns}{index})";
	}
}
