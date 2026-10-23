using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

public class WriterTests
{
	private static string Write(Action<CsvWriter> write, CsvWriterOptions? options = null)
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, options, leaveOpen: true)) write(writer);

		return Encoding.UTF8.GetString(stream.ToArray());
	}

	/// <summary>Straightforward RFC 4180 formatting used as the expected output.</summary>
	private static string Reference(IEnumerable<string[]> records, char sep = ',', bool quoteAll = false)
	{
		var sb = new StringBuilder();
		foreach (var record in records)
		{
			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0) sb.Append(sep);
				var f = record[i];
				var quote = quoteAll || f.IndexOfAny([sep, '"', '\r', '\n']) >= 0 || (record.Length == 1 && f.Length == 0);
				sb.Append(quote ? "\"" + f.Replace("\"", "\"\"") + "\"" : f);
			}

			sb.Append("\r\n");
		}

		return sb.ToString();
	}

	[Fact]
	public void QuotesOnlyWhenNeeded()
	{
		var csv = Write(w =>
		{
			w.WriteRecord("plain", "with,comma", "with \"quote\"", "line\nbreak", "cr\rhere", " spaces ", "");
			w.WriteRecord("a", "b", "c", "d", "e", "f", "g");
		});
		Assert.Equal("plain,\"with,comma\",\"with \"\"quote\"\"\",\"line\nbreak\",\"cr\rhere\", spaces ,\r\na,b,c,d,e,f,g\r\n", csv);
	}

	[Fact]
	public void LoneEmptyFieldIsQuoted()
	{
		Assert.Equal("\"\"\r\nx\r\n", Write(w =>
		{
			w.WriteField("");
			w.EndRecord();
			w.WriteRecord("x");
		}));
		// The reader must see it as a record with one empty field, not an empty line.
		using var reader = CsvReader.FromText("\"\"\r\nx\r\n", new CsvReaderOptions { HasHeader = false });
		Assert.True(reader.Read());
		Assert.Equal(1, reader.FieldCount);
		Assert.Equal("", reader.GetString(0));
	}

	[Fact]
	public void QuoteAll()
	{
		var csv = Write(w => w.WriteRecord("a", "", "b\"c"), new CsvWriterOptions { Quoting = CsvQuoting.Always });
		Assert.Equal("\"a\",\"\",\"b\"\"c\"\r\n", csv);
	}

	[Fact]
	public void FieldCountIsValidated()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteRecord("a", "b");
			var ex = Assert.Throws<InvalidOperationException>(() => writer.WriteRecord("only one"));
			Assert.Contains("RFC 4180", ex.Message);
			writer.WriteRecord("c", "d");
		}

		Assert.Equal("a,b\r\nc,d\r\n", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Fact]
	public void FieldCountValidationCanBeDisabled()
	{
		var csv = Write(w =>
		{
			w.WriteRecord("a", "b");
			w.WriteRecord("c");
		}, new CsvWriterOptions { ValidateFieldCount = false });
		Assert.Equal("a,b\r\nc\r\n", csv);
	}

	[Fact]
	public void EmptyRecordThrows()
	{
		using var writer = CsvWriter.Create(new MemoryStream());
		Assert.Throws<InvalidOperationException>(() => writer.EndRecord());
	}

	[Fact]
	public void Values()
	{
		var guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
		var csv = Write(w =>
		{
			w.WriteField(42);
			w.WriteField(-7L);
			w.WriteField(0.1);
			w.WriteField(1e21);
			w.WriteField(1.5f);
			w.WriteField(12.50m);
			w.WriteField(true);
			w.WriteField(new DateTime(2024, 3, 1, 10, 20, 30, DateTimeKind.Utc));
#if NET
			w.WriteField(new DateOnly(2024, 3, 1));
#endif
			w.WriteField(guid);
			w.WriteField((int?)null);
			w.WriteField((int?)5);
			w.WriteField(1234.5, "N2");
			w.WriteField((object)3.25);
			w.WriteField("é€😀"u8);
			w.EndRecord();
		});
		Assert.Equal("42,-7,0.1,1E+21,1.5,12.50,True,2024-03-01T10:20:30.0000000Z," +
#if NET
					 "2024-03-01," +
#endif
					 "0f8fad5b-d9cb-469f-a165-70867728950e,,5,\"1,234.50\",3.25,é€😀\r\n", csv);
	}

	/// <summary>An <see cref="IFormattable"/> type that is not UTF-8 formattable.</summary>
	private sealed class Celsius(double value) : IFormattable
	{
		public string ToString(string? format, IFormatProvider? formatProvider) => value.ToString(format, formatProvider) + " °C";
	}

	/// <summary>A type written with its <see cref="object.ToString"/>.</summary>
	private sealed class Tag(string? text)
	{
		public override string? ToString() => text;
	}

	[Fact]
	public void WritesAnyObject()
	{
		var csv = Write(w =>
		{
			w.WriteField((object?)null);
			w.WriteField((object)"a,b");
			w.WriteField((object)true);
			w.WriteField((object)new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc));
			w.WriteField((object)new DateTimeOffset(2024, 3, 1, 10, 20, 30, TimeSpan.FromHours(2)));
			w.WriteField((object)1.5f);
			w.WriteField((object)42);
			w.WriteField((object)12.50m);
			w.WriteField((object)new Celsius(2.5));
			w.WriteField((object)new Tag("x\"y"));
			w.WriteField((object)new Tag(null));
#if NET
			w.WriteField((object)new DateOnly(2024, 3, 1));
#endif
			w.EndRecord();
		});
		Assert.Equal(",\"a,b\",True,2024-03-01T00:00:00.0000000Z,2024-03-01T10:20:30.0000000+02:00,1.5,42,12.50,2.5 °C,\"x\"\"y\"," +
#if NET
					 ",2024-03-01" +
#endif
					 "\r\n", csv);
	}

	[Fact]
	public void WritesObjectsWithTheFormatProviderAndQuoting()
	{
		var german = new CsvWriterOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") };
		Assert.Equal("\"1,5\",\"2,5 °C\",7\r\n", Write(w =>
		{
			w.WriteField((object)1.5m);
			w.WriteField((object)new Celsius(2.5));
			w.WriteField((object)7);
			w.EndRecord();
		}, german));
		Assert.Equal("\"42\",\"x\"\r\n", Write(w =>
		{
			w.WriteField((object)42);
			w.WriteField((object)new Tag("x"));
			w.EndRecord();
		}, new CsvWriterOptions { Quoting = CsvQuoting.Always }));
	}

	[Fact]
	public void WritesDateTimeOffsets()
	{
		var at = new DateTimeOffset(2024, 3, 1, 10, 20, 30, TimeSpan.FromHours(2));
		Assert.Equal("2024-03-01T10:20:30.0000000+02:00,2024-03-01 10:20 +02:00,\"Mar 1, 2024\"\r\n", Write(w =>
		{
			w.WriteField(at);
			w.WriteField(at, "yyyy-MM-dd HH:mm zzz");
			w.WriteField(at, "MMM d, yyyy");
			w.EndRecord();
		}));
		Assert.Throws<FormatException>(() => Write(w => w.WriteField(at, "%")));
	}

	[Fact]
	public void WritesToAFile()
	{
		var path = Path.GetTempFileName();
		try
		{
			using (var writer = CsvWriter.ToFile(path, new CsvWriterOptions { Separator = ';' }))
			{
				Assert.Equal(';', writer.Options.Separator);
				Assert.Equal(0, writer.RecordCount);
				writer.WriteHeader("id", "name");
				Assert.Equal(1, writer.RecordCount);
				writer.WriteField(1);
				Assert.Equal(1, writer.RecordCount);
				writer.WriteField("Anna");
				writer.EndRecord();
				Assert.Equal(2, writer.RecordCount);
				// Other processes can read the file while it is written.
				using var other = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			}

			Assert.Equal("id;name\r\n1;Anna\r\n", File.ReadAllText(path));

			// An existing file is overwritten.
			using (var writer = CsvWriter.ToFile(path))
			{
				Assert.Same(CsvWriterOptions.Default, writer.Options);
				writer.WriteRecord("x");
			}

			Assert.Equal("x\r\n", File.ReadAllText(path));
		}
		finally
		{
			File.Delete(path);
		}

		var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "out.csv");
		Assert.Throws<DirectoryNotFoundException>(() => CsvWriter.ToFile(missing));
	}

	[Fact]
	public void DoublesRoundTrip()
	{
		var rng = new Random(5);
		var values = Enumerable.Range(0, 2000).Select(_ => BitConverter.Int64BitsToDouble(rng.NextInt64())).Where(double.IsFinite).ToArray();
		var csv = Write(w =>
		{
			foreach (var v in values)
			{
				w.WriteField(v);
				w.EndRecord();
			}
		});
		using var reader = CsvReader.FromText(csv, new CsvReaderOptions { HasHeader = false });
		var i = 0;
		while (reader.Read()) Assert.Equal(values[i++], reader.GetDouble(0));
		Assert.Equal(values.Length, i);
	}

#if NET
	[Fact]
	public void FloatsMatchBcl()
	{
		var rng = new Random(11);
		double[] specialDoubles = [0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.Epsilon, -double.Epsilon,
			double.MaxValue, double.MinValue, 0.1, 1e15, 1e16, 1e17, 1e-4, 1e-5, -123.456, 9007199254740993.0, Math.ScaleB(1.0, -25)];
		float[] specialFloats = [0f, -0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.Epsilon, float.MaxValue,
			float.MinValue, 0.1f, 1e7f, 1e8f, 1e9f, 1e-4f, 1e-5f, -123.456f, 16777217f];
		double[] doubles = [.. specialDoubles, .. Enumerable.Range(0, 3000).Select(_ => BitConverter.Int64BitsToDouble(rng.NextInt64()))];
		float[] floats = [.. specialFloats, .. Enumerable.Range(0, 3000).Select(_ => BitConverter.Int32BitsToSingle((int)rng.NextInt64()))];
		var csv = Write(w =>
		{
			for (var i = 0; i < doubles.Length; i++)
			{
				var d = doubles[i];
				var f = floats[i % floats.Length];
				w.WriteField(d);
				w.WriteField(f);
				w.WriteField((object)d);
				w.WriteField((object)f);
				w.WriteField((double?)d);
				w.EndRecord();
			}
		});
		var expected = new StringBuilder();
		for (var i = 0; i < doubles.Length; i++)
		{
			var d = doubles[i].ToString(CultureInfo.InvariantCulture);
			var f = floats[i % floats.Length].ToString(CultureInfo.InvariantCulture);
			expected.Append($"{d},{f},{d},{f},{d}\r\n");
		}

		Assert.Equal(expected.ToString(), csv);
	}
#endif

	[Fact]
	public void NumbersQuotedWhenSeparatorAppearsInThem()
	{
		var csv = Write(w => { w.WriteField(double.PositiveInfinity); w.WriteField(1.5f); w.WriteField(-2.5); w.EndRecord(); },
			new CsvWriterOptions { Separator = 'y' });
		Assert.Equal("\"Infinity\"y1.5y-2.5\r\n", csv);
		csv = Write(w => { w.WriteField(1.5); w.WriteField(2); w.WriteField(0.25f); w.EndRecord(); }, new CsvWriterOptions { Separator = '.' });
		Assert.Equal("\"1.5\".2.\"0.25\"\r\n", csv);
	}

	[Fact]
	public void FloatsUseFormatProvider()
	{
		var comma = new NumberFormatInfo { NumberDecimalSeparator = "," };
		var csv = Write(w => { w.WriteField(1.5); w.WriteField(2.25f); w.WriteField(0.5, "F3"); w.EndRecord(); },
			new CsvWriterOptions { FormatProvider = comma });
		Assert.Equal("\"1,5\",\"2,25\",\"0,500\"\r\n", csv);
	}

	[Fact]
	public void OtherSeparator()
	{
		var csv = Write(w => w.WriteRecord("a,b", "c;d", "e"), new CsvWriterOptions { Separator = ';' });
		Assert.Equal("a,b;\"c;d\";e\r\n", csv);
		var tsv = Write(w => w.WriteRecord("a b", "c\td"), new CsvWriterOptions { Separator = '\t' });
		Assert.Equal("a b\t\"c\td\"\r\n", tsv);
	}

	[Fact]
	public void Unicode()
	{
		var bad = "x" + '\uD800' + "y";   // lone surrogate is replaced
		var csv = Write(w => w.WriteRecord("é", "日本\"語", "😀", bad));
		Assert.Equal("é,\"日本\"\"語\",😀,x�y\r\n", csv);
	}

	[Fact]
	public void TextWriterTarget()
	{
		var sw = new StringWriter();
		using (var writer = CsvWriter.Create(sw, leaveOpen: true))
		{
			writer.WriteHeader("id", "name");
			writer.WriteRecord("1", "Zoë, \"the\" first");
		}

		Assert.Equal("id,name\r\n1,\"Zoë, \"\"the\"\" first\"\r\n", sw.ToString());
	}

	/// <summary>
	/// A <see cref="TextWriter"/> gets the text a stream gets when the output decoded for it grows from flush to flush:
	/// a few bytes first, then multibyte text and a field larger than the buffer.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task TextWriterTargetGrowingFlushes()
	{
		var big = new string('é', 100_000);
		var records = new List<string[]>();
		for (var i = 0; i < 5_000; i++)
		{
			records.Add([i.ToString(CultureInfo.InvariantCulture), i % 1000 == 0 ? big : "日本" + i]);
		}

		var options = new CsvWriterOptions { BufferSize = 1024 };
		var expected = Write(w =>
		{
			w.WriteRecord("first", "x");
			w.Flush();
			foreach (var record in records)
			{
				w.WriteRecord(record);
			}
		}, options);

		var sw = new StringWriter();
		using (var writer = CsvWriter.Create(sw, options, leaveOpen: true))
		{
			writer.WriteRecord("first", "x");
			writer.Flush();
			foreach (var record in records)
			{
				writer.WriteRecord(record);
			}
		}

		var asyncSw = new StringWriter();
		await using (var writer = CsvWriter.Create(asyncSw, options, leaveOpen: true))
		{
			await writer.WriteRecordAsync(["first", "x"]);
			await writer.FlushAsync();
			foreach (var record in records)
			{
				await writer.WriteRecordAsync(record);
			}
		}

		Assert.Equal(expected, sw.ToString());
		Assert.Equal(expected, asyncSw.ToString());
	}

	[Fact]
	public void ByteOrderMark()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { WriteByteOrderMark = true }, leaveOpen: true))
			writer.WriteRecord("a");
		Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 13, 10 }, stream.ToArray());
	}

	[Fact]
	public void LargeFieldsAndManyRecords()
	{
		var big = new string('q', 300_000) + "\"" + new string('z', 1000);
		var records = new List<string[]>();
		for (var i = 0; i < 20_000; i++) records.Add([i.ToString(CultureInfo.InvariantCulture), i % 1000 == 0 ? big : "v" + i, "x,y"]);
		var csv = Write(w =>
		{
			foreach (var r in records) w.WriteRecord(r);
		}, new CsvWriterOptions { BufferSize = 1024 });
		Assert.Equal(Reference(records), csv);
	}

	[Fact]
	public void FlushInTheMiddleOfARecord()
	{
		var stream = new MemoryStream();
		using (var writer = CsvWriter.Create(stream, leaveOpen: true))
		{
			writer.WriteField("abc");
			writer.Flush();
			writer.EndRecord();
			writer.WriteField("");
			writer.Flush();
			writer.EndRecord();
		}

		Assert.Equal("abc\r\n\"\"\r\n", Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Fact]
	public void WritingAfterDisposeThrows()
	{
		var writer = CsvWriter.Create(new MemoryStream());
		writer.WriteRecord("a");
		writer.Dispose();
		Assert.Throws<ObjectDisposedException>(() => writer.WriteField("x"));
		Assert.Throws<ObjectDisposedException>(() => writer.WriteField(1));
		Assert.Throws<ObjectDisposedException>(() => writer.Flush());
	}

	[Fact]
	public void DisposeFinishesOpenRecord()
	{
		Assert.Equal("a,b\r\nc,d\r\n", Write(w =>
		{
			w.WriteRecord("a", "b");
			w.WriteField("c");
			w.WriteField("d");
		}));
	}

	[Fact]
	public async Task AsyncWriting()
	{
		var stream = new MemoryStream();
		var records = Enumerable.Range(0, 5000).Select(i => new[] { i.ToString(CultureInfo.InvariantCulture), "row \"" + i + "\"" }).ToList();
		await using (var writer = CsvWriter.Create(stream, new CsvWriterOptions { BufferSize = 512 }, leaveOpen: true))
		{
			foreach (var r in records) await writer.WriteRecordAsync(r);
			await writer.FlushAsync();
		}

		Assert.Equal(Reference(records), Encoding.UTF8.GetString(stream.ToArray()));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public void RoundTripFuzz(int seed)
	{
		var rng = new Random(seed);
		char[] alphabet = ['a', 'b', ' ', ',', ';', '"', '\r', '\n', '\t', 'é', '€', 'x', 'y', '1'];
		for (var iter = 0; iter < 200; iter++)
		{
			var sep = ",;\t|"[rng.Next(4)];
			var quoteAll = rng.Next(4) == 0;
			var columns = rng.Next(1, 8);
			var records = new List<string[]>();
			var n = rng.Next(0, 40);
			for (var r = 0; r < n; r++)
			{
				var record = new string[columns];
				for (var c = 0; c < columns; c++)
				{
					var len = rng.Next(4) == 0 ? 0 : rng.Next(1, rng.Next(3) == 0 ? 200 : 12);
					record[c] = new string(Enumerable.Range(0, len).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
				}

				records.Add(record);
			}

			var options = new CsvWriterOptions { Separator = sep, Quoting = quoteAll ? CsvQuoting.Always : CsvQuoting.WhenNeeded, BufferSize = 256 << rng.Next(3) };
			var csv = Write(w => { foreach (var r in records) w.WriteRecord(r); }, options);
			Assert.Equal(Reference(records, sep, quoteAll), csv);

			var readOptions = new CsvReaderOptions { HasHeader = false, Separator = sep, SkipEmptyLines = false };
			var back = TestUtil.ReadText(csv, readOptions);
			TestUtil.AssertSame(records, back, $"seed {seed} iter {iter} sep {(int)sep}");
		}
	}
#if !NET
	[Fact]
	public void ParamsArrayOverloads()
	{
		// Callers before C# 13 can only pass arrays; an array argument picks these overloads with any compiler.
		var csv = Write(w =>
		{
			w.WriteHeader(new[] { "a", "b" });
			w.WriteRecord(new string?[] { "1", null });
			w.WriteRecord(new string?[] { "x,y", "z" });
		});
		Assert.Equal("a,b\r\n1,\r\n\"x,y\",z\r\n", csv);
	}

	[Fact]
	public void FormattableValuesWithoutUtf8Formatting()
	{
		// .NET Framework has no IUtf8SpanFormattable: numbers in the invariant culture are written directly, anything
		// else through IFormattable.ToString.
		var csv = Write(w =>
		{
			w.WriteField((short)-3);
			w.WriteField((byte)200);
			w.WriteField(4000000000u);
			w.WriteField(ulong.MaxValue);
			w.WriteField((sbyte)-1);
			w.WriteField((ushort)7);
			w.WriteField(TimeSpan.FromMinutes(90));
			w.WriteField(0.1f);
			w.WriteField((double?)null);
			w.WriteField((double?)2.5);
			w.EndRecord();
		});
		Assert.Equal("-3,200,4000000000,18446744073709551615,-1,7,01:30:00,0.1,,2.5\r\n", csv);
		var dots = Write(w =>
		{
			w.WriteField(1.5);
			w.WriteField(-2);
			w.EndRecord();
		}, new CsvWriterOptions { Separator = '.' });
		Assert.Equal("\"1.5\".-2\r\n", dots);
	}
#endif
}
