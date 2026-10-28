using System.Text;

namespace CsvHelper.Tests;

/// <summary>A stream that hands out data in small random chunks to stress buffer refills.</summary>
internal sealed class ChunkedStream(byte[] data, int seed, int maxChunk, bool yieldAsync = false) : Stream
{
	private readonly Random rng = new(seed);
	private int pos;

	public override bool CanRead => true;
	public override bool CanSeek => false;
	public override bool CanWrite => false;
	public override long Length => throw new NotSupportedException();
	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

#if NET
	public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

	public override int Read(Span<byte> buffer)
	{
		if (pos >= data.Length || buffer.Length == 0) return 0;
		var n = Math.Min(buffer.Length, Math.Min(rng.Next(1, maxChunk + 1), data.Length - pos));
		data.AsSpan(pos, n).CopyTo(buffer);
		pos += n;

		return n;
	}

	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (yieldAsync && rng.Next(3) == 0) await Task.Yield();

		return Read(buffer.Span);
	}

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
#else
	// .NET Framework streams have no span overloads: the array overloads do the work.
	public override int Read(byte[] buffer, int offset, int count)
	{
		if (pos >= data.Length || count == 0) return 0;
		var n = Math.Min(count, Math.Min(rng.Next(1, maxChunk + 1), data.Length - pos));
		Buffer.BlockCopy(data, pos, buffer, offset, n);
		pos += n;

		return n;
	}

	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (yieldAsync && rng.Next(3) == 0) await Task.Yield();

		return Read(buffer, offset, count);
	}
#endif

	public override void Flush() { }
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>A text reader that hands out chars in small random chunks.</summary>
internal sealed class ChunkedTextReader(string text, int seed, int maxChunk) : TextReader
{
	private readonly Random rng = new(seed);
	private int pos;

#if NET
	public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

	public override int Read(Span<char> buffer)
	{
		if (pos >= text.Length || buffer.Length == 0) return 0;
		var n = Math.Min(buffer.Length, Math.Min(rng.Next(1, maxChunk + 1), text.Length - pos));
		text.AsSpan(pos, n).CopyTo(buffer);
		pos += n;

		return n;
	}
#else
	public override int Read(char[] buffer, int index, int count)
	{
		if (pos >= text.Length || count == 0) return 0;
		var n = Math.Min(count, Math.Min(rng.Next(1, maxChunk + 1), text.Length - pos));
		text.CopyTo(pos, buffer, index, n);
		pos += n;

		return n;
	}
#endif

	public override int Peek() => pos < text.Length ? text[pos] : -1;
	public override int Read() => pos < text.Length ? text[pos++] : -1;
}

internal static class TestUtil
{
	/// <summary>Every scanner width: the SIMD widths (.NET only), the 64-bit integer masks and the scalar state machine.</summary>
	public static readonly int[] Widths =
#if NET
		[512, 256, 128, 64, 0];
#else
		[64, 0];
#endif

	public static List<string[]> ReadAll(CsvReader reader)
	{
		var records = new List<string[]>();
		while (reader.Read()) records.Add(Record(reader));

		return records;
	}

	public static async Task<List<string[]>> ReadAllAsync(CsvReader reader)
	{
		var records = new List<string[]>();
		while (await reader.ReadAsync()) records.Add(Record(reader));

		return records;
	}

	/// <summary>Reads every record and checks its position (see <see cref="CheckPositions"/>); needs TrackLineNumbers.</summary>
	public static List<string[]> ReadCheckingPositions(CsvReader reader, byte[] utf8, string context)
	{
		var records = new List<string[]>();
		var positions = new List<(long Offset, long Line, byte[] Raw)>();
		while (reader.Read())
		{
			records.Add(Record(reader));
			positions.Add((reader.ByteOffset, reader.LineNumber, reader.RawRecord.ToArray()));
		}

		CheckPositions(utf8, positions, lines: true, context);

		return records;
	}

	/// <summary>
	/// Checks that the input at each record's byte offset starts with its raw record, and (when <paramref name="lines"/>
	/// is set) that its line number is 1 plus the line breaks before that offset.
	/// </summary>
	public static void CheckPositions(byte[] utf8, IEnumerable<(long Offset, long Line, byte[] Raw)> positions, bool lines, string context)
	{
		var line = 1L;
		var counted = 0;
		var record = 0;
		foreach (var (offset, actualLine, raw) in positions)
		{
			record++;
			if (offset < counted || offset + raw.Length > utf8.Length || !utf8.AsSpan((int)offset, raw.Length).SequenceEqual(raw))
			{
				Assert.Fail($"record {record}: byte offset {offset} does not hold {Escape(Encoding.UTF8.GetString(raw))}; {context}");
			}

			for (; counted < offset; counted++)
			{
				var b = utf8[counted];
				if (b == '\n' || (b == '\r' && (counted + 1 == utf8.Length || utf8[counted + 1] != '\n'))) line++;
			}

			if (lines && actualLine != line) Assert.Fail($"record {record} at byte offset {offset}: line {actualLine}, expected {line}; {context}");
		}
	}

	public static string[] Record(CsvReader reader)
	{
		var record = new string[reader.FieldCount];
		for (var i = 0; i < record.Length; i++) record[i] = reader.GetString(i);

		return record;
	}

	public static List<string[]> ReadText(string text, CsvReaderOptions? options = null)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), options);

		return ReadAll(reader);
	}

	public static string Show(List<string[]> records)
	{
		var sb = new StringBuilder();
		foreach (var record in records)
		{
			sb.Append('[');
			sb.Append(string.Join(", ", record.Select(Escape)));
			sb.Append("]\n");
		}

		return sb.ToString();
	}

	public static string Escape(string s)
	{
		var sb = new StringBuilder("<");
		foreach (var c in s)
		{
			sb.Append(c switch
			{
				'\r' => "\\r",
				'\n' => "\\n",
				'\t' => "\\t",
				_ => c.ToString(),
			});
		}

		return sb.Append('>').ToString();
	}

	public static void AssertSame(List<string[]> expected, List<string[]> actual, string context)
	{
		if (expected.Count == actual.Count && expected.Zip(actual).All(p => p.First.SequenceEqual(p.Second))) return;
		var firstDiff = 0;
		while (firstDiff < Math.Min(expected.Count, actual.Count) && expected[firstDiff].SequenceEqual(actual[firstDiff])) firstDiff++;
		Assert.Fail($"{context}\nFirst difference at record {firstDiff} (expected {expected.Count} records, got {actual.Count}).\n" +
			$"Expected:\n{Show(expected.Skip(firstDiff).Take(3).ToList())}\nActual:\n{Show(actual.Skip(firstDiff).Take(3).ToList())}");
	}
}
