using System;

namespace CsvHelper;

// Records read as dynamic (CsvDynamicRecord): the bytes of a record are copied into blocks shared by many records, or
// referenced in place when the input is a byte array in memory.
public sealed partial class CsvReader
{
	// Blocks larger records do not share: a record of more bytes, or more fields, gets arrays of its own.
	private const int DynamicBlockBytes = 64 * 1024;
	private const int DynamicBlockOffsets = 8 * 1024;

	private byte[]? dynamicBytes;
	private int dynamicBytesPos;
	private int[]? dynamicOffsets;
	private int dynamicOffsetsPos;

	/// <summary>The schema of the dynamic records of this reader, for its current header (read now if it has not been).</summary>
	internal CsvDynamicSchema DynamicSchema()
	{
		if (!headerDone)
		{
			EnsureHeader();
		}

		return CsvDynamicSchema.Get(headers, headerKeys, options, nullValues, booleans);
	}

	/// <summary>
	/// The current record as a <see cref="CsvDynamicRecord"/>: its field positions, with the reader's flag on the fields
	/// that need decoding, and its bytes as they are in the input (referenced in place when the input is a byte array).
	/// A flagged field is a quoted field that the record unquotes when it is read; in the modes and with the trimming
	/// whose decoding is more than that, the fields are decoded now instead.
	/// </summary>
	internal CsvDynamicRecord ReadDynamicRecord(CsvDynamicSchema schema)
	{
		var count = fieldCount;
		var ends = this.ends;
		var b = fieldBase;
		var offsets = TakeOffsets(count + 1, out var first);
		var start = ends[b] & int.MaxValue;
		if (!quickUnquote && !NoFieldFlagged(ends, b, count))
		{
			return CopyDecoded(schema, offsets, first, start);
		}

		// The entry of field 0 carries the flag of the field before the record.
		offsets[first] = start;
		if (sourceKind == SourceKind.Memory && !parallelWorker)
		{
			// The caller's array, which does not change while records are read: no copy.
			Array.Copy(ends, b + 1, offsets, first + 1, count);

			return new CsvDynamicRecord(schema, buffer, offsets, first, count);
		}

		// The fields and the separators between them, in one copy. Adding the shift keeps the flags (the high bits).
		var length = (ends[b + count] & int.MaxValue) - 1 - start;
		var bytes = TakeBytes(length, out var pos);
		Array.Copy(buffer, start, bytes, pos, length);
		var shift = pos - start;
		offsets[first] = pos;
		for (var k = 1; k <= count; k++)
		{
			offsets[first + k] = ends[b + k] + shift;
		}

		return new CsvDynamicRecord(schema, bytes, offsets, first, count);
	}

	/// <summary>Whether no field of the record needs decoding: the flag of field k is on the entry after it.</summary>
	private static bool NoFieldFlagged(int[] ends, int b, int count)
	{
		var flags = 0;
		for (var k = 1; k <= count; k++)
		{
			flags |= ends[b + k];
		}

		return flags >= 0;
	}

	/// <summary>
	/// Copies the decoded fields of the current record one byte apart, so that each field ends one byte before the next
	/// starts, as in the input.
	/// </summary>
	private CsvDynamicRecord CopyDecoded(CsvDynamicSchema schema, int[] offsets, int first, int start)
	{
		var count = fieldCount;
		// Decoding never makes a field longer, and the record's bytes cover the gaps between its fields.
		var bound = (ends[fieldBase + count] & int.MaxValue) - start + count + 1;
		var bytes = TakeBytes(bound, out var pos);
		var p = pos;
		for (var k = 0; k < count; k++)
		{
			var field = GetSpan(k);
			field.CopyTo(new Span<byte>(bytes, p, field.Length));
			offsets[first + k] = p;
			p += field.Length + 1;
		}

		offsets[first + count] = p;
		if (bytes == dynamicBytes)
		{
			// Give back what the record did not use.
			dynamicBytesPos = p;
		}

		return new CsvDynamicRecord(schema, bytes, offsets, first, count);
	}

	/// <summary>Room for <paramref name="length"/> bytes, at <paramref name="pos"/> of the array it returns.</summary>
	private byte[] TakeBytes(int length, out int pos)
	{
		if (length > DynamicBlockBytes / 4)
		{
			pos = 0;

			return new byte[length];
		}

		if (dynamicBytes is null || dynamicBytes.Length - dynamicBytesPos < length)
		{
			dynamicBytes = new byte[DynamicBlockBytes];
			dynamicBytesPos = 0;
		}

		pos = dynamicBytesPos;
		dynamicBytesPos += length;

		return dynamicBytes;
	}

	/// <summary>Room for <paramref name="length"/> field offsets, at <paramref name="pos"/> of the array it returns.</summary>
	private int[] TakeOffsets(int length, out int pos)
	{
		if (length > DynamicBlockOffsets / 4)
		{
			pos = 0;

			return new int[length];
		}

		if (dynamicOffsets is null || dynamicOffsets.Length - dynamicOffsetsPos < length)
		{
			dynamicOffsets = new int[DynamicBlockOffsets];
			dynamicOffsetsPos = 0;
		}

		pos = dynamicOffsetsPos;
		dynamicOffsetsPos += length;

		return dynamicOffsets;
	}
}
