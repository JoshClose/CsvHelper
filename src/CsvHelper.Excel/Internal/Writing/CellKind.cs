using System;

namespace CsvHelper.Excel;

/// <summary>What kind of cell a written field becomes, by the type it was formatted from (<see cref="CellKinds.Of"/>).</summary>
internal enum CellKind : byte
{
	/// <summary>A text cell: strings, and every type not below (<see cref="Guid"/>, enums, <see cref="Uri"/>, ...).</summary>
	Text,

	/// <summary>A boolean cell, <c>t="b"</c>, from <see cref="bool.TrueString"/> or <see cref="bool.FalseString"/>.</summary>
	Bool,

	/// <summary>A number cell from an integer: <see cref="sbyte"/> to <see cref="ulong"/>, <c>Int128</c> and <c>UInt128</c>.</summary>
	Integer,

	/// <summary>A number cell from a <see cref="decimal"/>.</summary>
	Decimal,

	/// <summary>A number cell from a <see cref="double"/>, a <see cref="float"/> or a <c>Half</c>.</summary>
	Float,

	/// <summary>A date cell from a <see cref="System.DateTime"/>'s <c>"O"</c> text.</summary>
	DateTime,

	/// <summary>A date cell from a <c>DateOnly</c>'s <c>"O"</c> text.</summary>
	DateOnly,

	/// <summary>A time cell from a <c>TimeOnly</c>'s <c>"O"</c> text.</summary>
	TimeOnly,

	/// <summary>A duration cell from a <see cref="System.TimeSpan"/>'s <c>"c"</c> text.</summary>
	TimeSpan,

	/// <summary>A cell from a <see cref="System.DateTimeOffset"/>'s <c>"O"</c> text, as <see cref="ExcelWriterOptions.DateTimeOffsets"/> says.</summary>
	DateTimeOffset,
}
