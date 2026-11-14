using System;

namespace CsvHelper.Excel;

/// <summary>Maps the type a field was formatted from to the kind of cell it becomes.</summary>
internal static class CellKinds
{
	/// <summary>
	/// The type a field of a date alone is handed to a sheet's sink with, as a <c>DateOnly</c>'s <c>"O"</c> text: <c>DateOnly</c>
	/// itself, and on the builds that have none, a type of the writer's own, for CSV converted to a sheet
	/// (<see cref="ColumnTypes"/>).
	/// </summary>
#if NET
	public static readonly Type Date = typeof(System.DateOnly);
#else
	public static readonly Type Date = typeof(DateOnlyText);
#endif

	/// <summary>
	/// The kind of cell a field of a type becomes. Types are matched by reference; <c>DateOnly</c>, <c>TimeOnly</c>,
	/// <c>Half</c>, <c>Int128</c> and <c>UInt128</c> by name on the builds that can't name them, as core matches them.
	/// </summary>
	/// <param name="type">The type, or a nullable of it.</param>
	/// <returns>The kind.</returns>
	public static CellKind Of(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		if (type == typeof(string))
		{
			return CellKind.Text;
		}

		if (type == typeof(bool))
		{
			return CellKind.Bool;
		}

		if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) ||
			type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
		{
			return CellKind.Integer;
		}

		if (type == typeof(decimal))
		{
			return CellKind.Decimal;
		}

		if (type == typeof(double) || type == typeof(float))
		{
			return CellKind.Float;
		}

		if (type == typeof(System.DateTime))
		{
			return CellKind.DateTime;
		}

		if (type == typeof(System.DateTimeOffset))
		{
			return CellKind.DateTimeOffset;
		}

		if (type == typeof(System.TimeSpan))
		{
			return CellKind.TimeSpan;
		}

		if (type == typeof(ExcelFormula))
		{
			return CellKind.Formula;
		}

		if (type == typeof(System.Uri))
		{
			return CellKind.Uri;
		}

#if NET
		if (type == typeof(System.DateOnly))
		{
			return CellKind.DateOnly;
		}

		if (type == typeof(System.TimeOnly))
		{
			return CellKind.TimeOnly;
		}

		if (type == typeof(Half))
		{
			return CellKind.Float;
		}

		if (type == typeof(Int128) || type == typeof(UInt128))
		{
			return CellKind.Integer;
		}

		return CellKind.Text;
#else
		if (type == typeof(DateOnlyText))
		{
			return CellKind.DateOnly;
		}

		if (type.Assembly != typeof(object).Assembly)
		{
			return CellKind.Text;
		}

		return type.FullName switch
		{
			"System.DateOnly" => CellKind.DateOnly,
			"System.TimeOnly" => CellKind.TimeOnly,
			"System.Half" => CellKind.Float,
			"System.Int128" or "System.UInt128" => CellKind.Integer,
			_ => CellKind.Text,
		};
#endif
	}

#if !NET
	/// <summary>The type of a date alone's text (<see cref="Date"/>) on the builds without <c>DateOnly</c>: never created.</summary>
	private sealed class DateOnlyText
	{
		/// <summary>Never called.</summary>
		private DateOnlyText()
		{
		}
	}
#endif
}
