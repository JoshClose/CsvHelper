using System.Text;

namespace CsvHelper.Tests;

/// <summary>Exhaustive length/position checks for the hand-written vector fast paths.</summary>
public class FastPathTests
{
	[Fact]
	public void Utf8ToStringAllLengths()
	{
		for (var len = 0; len <= 70; len++)
		{
			var ascii = new string(Enumerable.Range(0, len).Select(i => (char)('a' + i % 26)).ToArray());
			Check(ascii);
			for (var pos = 0; pos < len; pos++)
			{
				// A non-ASCII character at every position, including the overlapping tail.
				var chars = ascii.ToCharArray();
				chars[pos] = 'é';
				Check(new string(chars));
				chars[pos] = '€';
				Check(new string(chars));
			}
		}

		static void Check(string s)
		{
			var utf8 = Encoding.UTF8.GetBytes(s);
			// Embed in a larger buffer so reads past the span would be caught as wrong results.
			var padded = new byte[utf8.Length + 40];
			padded.AsSpan().Fill((byte)'Z');
			utf8.CopyTo(padded, 20);
			Assert.Equal(s, Utf8String.Create(padded.AsSpan(20, utf8.Length)));
		}
	}

#if NET10_0_OR_GREATER
	[Fact]
	public void Utf8GetCharsAllLengths()
	{
		for (var len = 0; len <= 70; len++)
		{
			var ascii = Encoding.ASCII.GetBytes(new string(Enumerable.Range(0, len).Select(i => (char)('a' + i % 26)).ToArray()));
			Check(ascii);
			for (var pos = 0; pos < len; pos++)
			{
				// A non-ASCII byte at every position, including the overlapping tail: a 2-byte character, and bytes
				// that are not valid UTF-8 (a lone lead byte and a byte that never appears).
				foreach (var b in new byte[] { 0xC3, 0xFF, 0x80 })
				{
					var bytes = (byte[])ascii.Clone();
					bytes[pos] = b;
					Check(bytes);
				}

				var wide = Encoding.UTF8.GetBytes(Encoding.ASCII.GetString(ascii, 0, pos) + "é" + Encoding.ASCII.GetString(ascii, pos, len - pos));
				Check(wide);
			}
		}

		static void Check(byte[] utf8)
		{
			// Embed in larger buffers so that reads or writes past the spans show up as wrong results.
			var padded = new byte[utf8.Length + 40];
			padded.AsSpan().Fill((byte)'Z');
			utf8.CopyTo(padded, 20);
			var chars = new char[utf8.Length + 40];
			chars.AsSpan().Fill('#');
			var n = Utf8String.GetChars(padded.AsSpan(20, utf8.Length), chars.AsSpan(20, utf8.Length));
			Assert.Equal(Encoding.UTF8.GetString(utf8), new string(chars, 20, n));
			Assert.True(chars.AsSpan(0, 20).IndexOfAnyExcept('#') < 0);
			Assert.True(chars.AsSpan(20 + n).IndexOfAnyExcept('#') < 0);
		}
	}

#endif
	[Fact]
	public void StringPoolDistinguishesValuesThatDifferInOneByte()
	{
		// Same length and same first 8 bytes (the fingerprint), differing in one byte at every later
		// position, for every pooled length past 8: exercises each overlapping comparison.
		for (var len = 9; len <= 64; len++)
		{
			var baseValue = new string('a', len);
			var values = new List<string>();
			for (var p = 8; p < len; p++)
			{
				var chars = baseValue.ToCharArray();
				chars[p] = 'b';
				var variant = new string(chars);
				for (var k = 0; k < 3; k++)
				{
					values.Add(variant);
					values.Add(baseValue);
				}
			}

			using var reader = CsvReader.FromText(string.Join("\n", values) + "\n", new CsvReaderOptions { HasHeader = false, PoolStrings = true });
			var read = new List<string>();
			while (reader.Read()) read.Add(reader.GetString(0));
			Assert.Equal(values, read);
			// Repeated values come from the pool: the hit path was exercised.
			Assert.Same(read[1], read[3]);
			Assert.Same(read[0], read[2]);
		}
	}

	[Fact]
	public void StringPoolDistinguishesLongValuesThatDifferInOneMiddleByte()
	{
		// Past 32 bytes the head and tail vectors do not cover the value: the middle is compared separately.
		for (var len = 30; len <= CsvStringPool.MaxPooledLength; len++)
		{
			var baseValue = string.Concat(Enumerable.Range(0, len).Select(i => (char)('a' + i % 26)));
			var values = new List<string>();
			for (var p = 0; p < len; p++)
			{
				var chars = baseValue.ToCharArray();
				chars[p] = 'Z';
				values.Add(new string(chars));
				values.Add(baseValue);
				values.Add(new string(chars));
			}

			using var reader = CsvReader.FromText(string.Join("\n", values) + "\n", new CsvReaderOptions { HasHeader = false, PoolStrings = true });
			var read = new List<string>();
			while (reader.Read()) read.Add(reader.GetString(0));
			Assert.Equal(values, read);
			Assert.Same(read[1], read[4]);
			Assert.Same(read[0], read[2]);
		}
	}

	[Fact]
	public void StringPoolSkipsValuesLongerThanTheLimit()
	{
		var value = new string('v', CsvStringPool.MaxPooledLength + 1);
		using var reader = CsvReader.FromText($"{value}\n{value}\n", new CsvReaderOptions { HasHeader = false, PoolStrings = true });
		Assert.True(reader.Read());
		var a = reader.GetString(0);
		Assert.True(reader.Read());
		var b = reader.GetString(0);
		Assert.Equal(value, a);
		Assert.Equal(value, b);
		Assert.NotSame(a, b);
	}

	[Fact]
	public void StringPoolSwitchesOffColumnsOfUniqueValues()
	{
		// The id column fills its table and keeps missing, so it stops pooling; values stay right throughout,
		// and the repeating column keeps pooling.
		var sb = new StringBuilder();
		for (var i = 0; i < 20_000; i++) sb.Append("id-").Append(i).Append(",kind ").Append(i % 7).Append('\n');
		using var reader = CsvReader.FromText(sb.ToString(), new CsvReaderOptions { HasHeader = false, PoolStrings = true });
		var kinds = new List<string>();
		for (var i = 0; i < 20_000; i++)
		{
			Assert.True(reader.Read());
			Assert.Equal($"id-{i}", reader.GetString(0));
			kinds.Add(reader.GetString(1));
		}

		Assert.Equal(7, kinds.Distinct(ReferenceEqualityComparer.Instance).Count());
	}

	[Fact]
	public void StringPoolKeepsColumnsOfManyRepeatingValues()
	{
		// 2000 values in random order: most of the first records bring a new value, but not nearly all, so the column
		// keeps pooling and every value ends up with one instance.
		var rng = new Random(3);
		var sb = new StringBuilder();
		for (var i = 0; i < 20_000; i++) sb.Append("value-").Append(rng.Next(2000)).Append('\n');
		using var reader = CsvReader.FromText(sb.ToString(), new CsvReaderOptions { HasHeader = false, PoolStrings = true });
		var values = new List<string>();
		while (reader.Read()) values.Add(reader.GetString(0));
		Assert.Equal(values.Distinct().Count(), values.Distinct(ReferenceEqualityComparer.Instance).Count());
	}

	[Fact]
	public void UnescapeAllLengthsAndPositions()
	{
		// Escaped quotes at every position of fields up to 70 bytes (across the 16-byte steps of the vector
		// loop and the overlapping-load quote check), runs of them, text after the closing quote, and a
		// field that is never closed.
		var sb = new StringBuilder();
		for (var len = 0; len <= 70; len++)
		{
			var body = new string(Enumerable.Range(0, len).Select(i => (char)('a' + i % 26)).ToArray());
			for (var pos = 0; pos <= len; pos++)
			{
				var escaped = body.Insert(pos, "\"\"");
				sb.Append('"').Append(escaped).Append("\",x\n");
				sb.Append('"').Append(body.Insert(pos, "\"\"\"\"")).Append("\",x\n");
				sb.Append('"').Append(escaped).Append("\"tail,x\n");
				sb.Append('"').Append(escaped.Replace("a", "\"\"a")).Append("\",x\n");
				sb.Append('"').Append(escaped).Append("\"\"\"t\"ail\",x\n");
			}
		}

		sb.Append("\"ab\"\"cd,ef\nnever closed \"\" here");
		var text = sb.ToString();
		var options = new ReferenceOptions();
		var expected = ReferenceCsv.Parse(text, options);
		using var reader = CsvReader.FromText(text, options.ToReaderOptions());
		TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), "unescape");
	}

	[Fact]
	public void QuotedFieldEndingAtEveryBlockPosition()
	{
		// The scanner marks quoted fields that need full unescaping; a closing quote in the last byte of a block
		// is decided by the next block. Put the closing quote of a quoted field on every position around two
		// block boundaries, followed by each kind of continuation, with and without more records after it.
		string[] tails = [",x", "\nx", "\r\nx", "\"\"y\",x", "z,x", "\"\"", "\"\"\"", "\"z", ""];
		var filler = string.Concat(Enumerable.Repeat("a,\"b\",c\n", 30));
		var optionSets = new[]
		{
			new ReferenceOptions(),
			new ReferenceOptions { LineBreaksInQuotes = false },
			new ReferenceOptions { Trim = true },
		};
		foreach (var options in optionSets)
		foreach (var contentLength in new[] { 0, 3, 70 })
		foreach (var tail in tails)
		foreach (var more in new[] { false, true })
		{
			for (var pad = 0; pad < 140; pad++)
			{
				var content = new string('q', contentLength);
				if (!options.LineBreaksInQuotes && contentLength > 0) content = "q\n" + content;
				var text = new string('p', pad) + ",\"" + content + "\"" + tail + (more ? "\n" + filler : "");
				var expected = ReferenceCsv.Parse(text, options);
				var utf8 = Encoding.UTF8.GetBytes(text);
				foreach (var width in TestUtil.Widths)
				{
					using var reader = CsvReader.Create(utf8, options.ToReaderOptions());
					reader.vectorWidth = width;
					TestUtil.AssertSame(expected, TestUtil.ReadAll(reader), $"options {options}, length {contentLength}, tail {tail}, more {more}, pad {pad}, width {width}");
				}
			}
		}
	}

	[Fact]
	public void PrefixXorFallbackMatches()
	{
		var rng = new Random(11);
		for (var i = 0; i < 200_000; i++)
		{
			var x = (ulong)rng.NextInt64() ^ ((ulong)rng.Next() << 63);
			// Reference: bit k is the parity of bits 0..k.
			var expected = 0UL;
			var parity = 0;
			for (var k = 0; k < 64; k++)
			{
				parity ^= (int)((x >> k) & 1);
				expected |= (ulong)parity << k;
			}

			Assert.Equal(expected, CsvReader.PrefixXorPortable(x));
			Assert.Equal(expected, CsvReader.PrefixXorForTest(x));
		}
	}

	[Fact]
	public void WriterAsciiFastPathAllLengths()
	{
		char[] specials = [',', '"', '\r', '\n'];
		for (var len = 0; len <= 40; len++)
		{
			var plain = new string(Enumerable.Range(0, len).Select(i => (char)('A' + i % 26)).ToArray());
			Check(plain);
			for (var pos = 0; pos < len; pos++)
			{
				foreach (var special in specials)
				{
					var chars = plain.ToCharArray();
					chars[pos] = special;
					Check(new string(chars));
				}

				var wide = plain.ToCharArray();
				wide[pos] = 'ü';
				Check(new string(wide));
			}
		}

		static void Check(string field)
		{
			var stream = new MemoryStream();
			using (var writer = CsvWriter.Create(stream, leaveOpen: true))
			{
				writer.WriteField("x");
				writer.WriteField(field);
				writer.EndRecord();
			}

			var quote = field.IndexOfAny([',', '"', '\r', '\n']) >= 0;
			var expected = "x," + (quote ? "\"" + field.Replace("\"", "\"\"") + "\"" : field) + "\r\n";
			Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()));
		}
	}
}
