using System;
using System.Buffers;

namespace CsvHelper;

/// <summary>
/// Gives the text of a <see cref="CsvRecordSource"/>'s fields in their styles: the text a spreadsheet shows in a cell's
/// number format, for CsvHelper.Excel's <c>ExcelText.Formatted</c>. A source sets one through
/// <see cref="CsvCellOptions.Formatter"/> and gives a field a style with
/// <see cref="CsvReader.RecordBatch.CommitField(int, CsvCellKind, int)"/>; the reader then asks it for that field's text
/// whenever it reads text (<see cref="CsvReader.GetString(int)"/>, header names, <c>GetTextUtf8</c>, dynamic records).
/// Typed reads don't: they convert the field's value by its kind, as without a style.
/// </summary>
/// <remarks>
/// A formatter is shared by every reader of its source's records, the parallel workers of
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> among them, so it must be safe to call from
/// several threads at once.
/// </remarks>
internal abstract class CsvCellFormatter
{
	/// <summary>
	/// The largest style a field can have, 2^23 − 1: a dynamic record keeps a field's style with its kind in one
	/// <see cref="int"/>, the kind in the low 8 bits.
	/// </summary>
	public const int MaxStyle = (1 << 23) - 1;

	/// <summary>
	/// The most bytes a text can have for the reader to render it on the stack; longer texts are rendered into a buffer
	/// the reader keeps.
	/// </summary>
	public const int StackLength = 512;

	/// <summary>Writes the text of a field in its style, as UTF-8.</summary>
	/// <param name="value">The field's stored value (UTF-8), as the source wrote it.</param>
	/// <param name="kind">The field's kind.</param>
	/// <param name="style">The field's style, from 0 to <see cref="MaxStyle"/>.</param>
	/// <param name="destination">Receives the text.</param>
	/// <param name="length">
	/// The length of the text written; or -1 when the field's text is what it would be without a style (its
	/// <see cref="CsvCells.Text"/>, else its bytes); or, when <paramref name="destination"/> is too small, the length the
	/// text needs, more than its length.
	/// </param>
	/// <returns>Whether the text was written: <see langword="false"/> when <paramref name="length"/> is -1 or the room needed.</returns>
	public abstract bool TryFormat(ReadOnlySpan<byte> value, CsvCellKind kind, int style, Span<byte> destination, out int length);

	/// <summary>The text of a field in its style, as a string: <see cref="TryFormat"/> on the stack, or in a rented buffer when it is longer.</summary>
	/// <param name="value">The field's stored value (UTF-8).</param>
	/// <param name="kind">The field's kind.</param>
	/// <param name="style">The field's style.</param>
	/// <returns>The text, or <see langword="null"/> when it is what the field's text would be without a style.</returns>
	/// <exception cref="InvalidOperationException">The formatter needed more room than it asked for.</exception>
	public string? Format(ReadOnlySpan<byte> value, CsvCellKind kind, int style)
	{
		Span<byte> room = stackalloc byte[StackLength];
		if (TryFormat(value, kind, style, room, out var length))
		{
			return Utf8String.Create(room.Slice(0, length));
		}

		if (length < 0)
		{
			return null;
		}

		var rented = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			if (!TryFormat(value, kind, style, rented, out length))
			{
				throw MoreRoom();
			}

			return Utf8String.Create(rented.AsSpan(0, length));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	/// <summary>The exception for a formatter that didn't write its text into the room it asked for.</summary>
	/// <returns>The exception to throw.</returns>
	public static InvalidOperationException MoreRoom() => new("The cell formatter needed more room than it asked for.");
}
