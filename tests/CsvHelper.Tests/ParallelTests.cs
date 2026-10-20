using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// ParallelEnumerate must return exactly what Read returns, in order, for every source and option, with
/// inputs large enough for many batches.
/// </summary>
public class ParallelTests
{
	public static TheoryData<int> Seeds()
	{
		var data = new TheoryData<int>();
		for (var i = 1; i <= 6; i++) data.Add(i * 104729);

		return data;
	}

	[Theory]
	[MemberData(nameof(Seeds))]
	public void MatchesReferenceForEverySource(int seed)
	{
		var rng = new Random(seed);
		var o = FuzzTests.RandomOptions(rng);
		// Many generated documents back to back: about 1.5 MB, so several batches of every size.
		var sb = new StringBuilder();
		while (sb.Length < 1_500_000) sb.Append(FuzzTests.StructuredText(rng, o)).Append('\n');
		var text = sb.ToString();
		var expected = ReferenceCsv.Parse(text, o);
		var utf8 = Encoding.UTF8.GetBytes(text);
		var bufferSize = 256 << rng.Next(8);
		var lines = rng.Next(2) == 0;
		var options = o.ToReaderOptions(bufferSize: bufferSize) with { TrackLineNumbers = lines };
		var ctx = $"seed {seed}, {o}, buffer {bufferSize}, lines {lines}";

		Check(CsvReader.Create(utf8, options), "byte[]");
		Check(CsvReader.Create(new MemoryStream(utf8), options), "MemoryStream");
		Check(CsvReader.Create(new ChunkedStream(utf8, rng.Next(), rng.Next(1, 100_000)), options), "chunked stream");
		Check(CsvReader.Create(new ChunkedTextReader(text, rng.Next(), rng.Next(1, 100_000)), options), "chunked text reader");
		Check(CsvReader.FromText(text, options), "string");

		void Check(CsvReader reader, string source)
		{
			using (reader)
			{
				var degree = rng.Next(3) switch { 0 => 1, 1 => 3, _ => -1 };
				var results = reader.ParallelEnumerate(r => (r.RowNumber, Record: TestUtil.Record(r),
					Position: (r.ByteOffset, lines ? r.LineNumber : 0, r.RawRecord.ToArray())), degree).ToList();
				TestUtil.AssertSame(expected, results.Select(x => x.Record).ToList(), $"{source}, degree {degree}, {ctx}");
				Assert.Equal(Enumerable.Range(1, results.Count).Select(i => (long)i), results.Select(x => x.RowNumber));
				TestUtil.CheckPositions(utf8, results.Select(x => x.Position), lines, $"{source}, degree {degree}, {ctx}");
				Assert.False(reader.Read());
			}
		}
	}

	[Fact]
	public void ReportsMalformedRecordsInOrderOneAtATime()
	{
		var text = BuildText(50_000, i => i % 997 == 0 ? $"{i},a\"b,c\n" : $"{i},x,y\n");
		var reported = new List<long>();
		var inCallback = 0;
		var options = new CsvReaderOptions
		{
			HasHeader = false,
			MalformedData = CsvMalformedDataHandling.Skip,
			OnMalformedRecord = m =>
			{
				Assert.Equal(1, Interlocked.Increment(ref inCallback));
				reported.Add(m.RecordNumber);
				Thread.SpinWait(1000);
				Interlocked.Decrement(ref inCallback);
			},
		};
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(text)), options);
		var ids = reader.ParallelEnumerate(r => r.GetInt32(0)).ToList();
		Assert.Equal(Enumerable.Range(0, 50_000).Where(i => i % 997 != 0), ids);
		Assert.Equal(Enumerable.Range(0, 50_000).Where(i => i % 997 == 0).Count(), reported.Count);
		Assert.True(reported.SequenceEqual(reported.Order()));
	}

	[Fact]
	public void SelectorExceptionIsThrownAfterThePrecedingResults()
	{
		var text = BuildText(200_000, i => $"{i},v\n");
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), new CsvReaderOptions { HasHeader = false });
		var seen = new List<int>();
		var ex = Assert.Throws<InvalidDataException>(() =>
		{
			foreach (var id in reader.ParallelEnumerate(r =>
			{
				var id = r.GetInt32(0);

				return id == 123_456 ? throw new InvalidDataException("boom") : id;
			}))
			{
				seen.Add(id);
			}
		});
		Assert.Equal("boom", ex.Message);
		Assert.Equal(Enumerable.Range(0, 123_456), seen);
	}

	[Fact]
	public void ReaderExceptionIsThrownAfterThePrecedingResults()
	{
		var text = BuildText(100_000, i => i == 77_777 ? "77777,a\"b\n" : $"{i},v\n");
		var options = new CsvReaderOptions { HasHeader = false, MalformedData = CsvMalformedDataHandling.Throw };
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(text)), options);
		var seen = new List<int>();
		Assert.Throws<CsvFormatException>(() =>
		{
			foreach (var id in reader.ParallelEnumerate(r => r.GetInt32(0))) seen.Add(id);
		});
		Assert.Equal(Enumerable.Range(0, 77_777), seen);
	}

	[Fact]
	public void StoppingEarlyEndsTheEnumeration()
	{
		var text = BuildText(300_000, i => $"{i},value {i % 10}\n");
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(text)), new CsvReaderOptions { HasHeader = false });
		Assert.Equal(Enumerable.Range(0, 10), reader.ParallelEnumerate(r => r.GetInt32(0)).Take(10));
	}

	[Fact]
	public void SelectorCannotAdvanceTheReader()
	{
		using var reader = CsvReader.Create("a\nb\nc\n"u8.ToArray(), new CsvReaderOptions { HasHeader = false });
		Assert.Throws<InvalidOperationException>(() => reader.ParallelEnumerate(r => r.Read()).ToList());
	}

	[Fact]
	public void ContinuesAfterRecordsReadWithRead()
	{
		var text = BuildText(100_000, i => $"{i}\n");
		using var reader = CsvReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(text)), new CsvReaderOptions { HasHeader = false });
		for (var i = 0; i < 3; i++)
		{
			Assert.True(reader.Read());
			Assert.Equal(i, reader.GetInt32(0));
		}

		Assert.Equal(Enumerable.Range(3, 100_000 - 3), reader.ParallelEnumerate(r => r.GetInt32(0)));
	}

	[Fact]
	public void HeaderNamesAreAvailableToTheSelector()
	{
		var text = "id,name\n" + BuildText(50_000, i => $"{i},n{i}\n");
		using var reader = CsvReader.FromText(text);
		var names = reader.ParallelEnumerate(r => r.GetString("name")).ToList();
		Assert.Equal(Enumerable.Range(0, 50_000).Select(i => $"n{i}"), names);
		Assert.Equal(["id", "name"], reader.Headers);
	}

	[Fact]
	public void RecordsLargerThanABatch()
	{
		// One quoted field of 1 MB with line breaks and escaped quotes inside.
		var huge = new string('x', 700_000) + "\n\"\"quoted\"\"\n" + new string('y', 300_000);
		var sb = new StringBuilder();
		for (var i = 0; i < 20_000; i++) sb.Append(i).Append(",small\n");
		sb.Append("big,\"").Append(huge).Append("\"\n");
		for (var i = 0; i < 20_000; i++) sb.Append(i).Append(",after\n");
		var text = sb.ToString();
		var expected = ReferenceCsv.Parse(text, new ReferenceOptions());
		using var reader = CsvReader.Create(new ChunkedStream(Encoding.UTF8.GetBytes(text), 1, 50_000), new CsvReaderOptions { HasHeader = false, BufferSize = 4096 });
		var actual = reader.ParallelEnumerate(TestUtil.Record).ToList();
		TestUtil.AssertSame(expected, actual, "huge record");
		Assert.Contains(actual, r => r.Length == 2 && r[1].Length == huge.Length - 2);
	}

	[Fact]
	public void WorkersShareThePooledStrings()
	{
		var text = BuildText(400_000, i => $"{i},category {i % 50},{(i % 3 == 0 ? "a value that is longer than thirty-two bytes" : "short")}\n");
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), new CsvReaderOptions { HasHeader = false, PoolStrings = true });
		var rows = reader.ParallelEnumerate(r => (Category: r.GetString(1), Text: r.GetString(2))).ToList();
		Assert.Equal(50, rows.Select(r => r.Category).Distinct(ReferenceEqualityComparer.Instance).Count());
		Assert.Equal(2, rows.Select(r => r.Text).Distinct(ReferenceEqualityComparer.Instance).Count());
	}

	[Fact]
	public void ColumnsSwitchOffPoolingWhileWorkersReadThem()
	{
		// Unique values fill the shared tables and switch them off while other workers are looking values up.
		for (var round = 0; round < 5; round++)
		{
			var text = BuildText(150_000, i => $"id-{i},\"quoted {i}\",{i % 13}\n");
			using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(text), new CsvReaderOptions { HasHeader = false, PoolStrings = true });
			var rows = reader.ParallelEnumerate(r => (r.GetString(0), r.GetString(1), r.GetString(2))).ToList();
			for (var i = 0; i < rows.Count; i++)
				Assert.Equal(($"id-{i}", $"quoted {i}", (i % 13).ToString()), rows[i]);
		}
	}

	[Fact]
	public void EmptyInputAndHeaderOnly()
	{
		using (var reader = CsvReader.Create(Array.Empty<byte>()))
			Assert.Empty(reader.ParallelEnumerate(r => r.GetString(0)));
		using (var reader = CsvReader.FromText("a,b\n"))
			Assert.Empty(reader.ParallelEnumerate(r => r.GetString(0)));
		using (var reader = CsvReader.FromText("a,b", new CsvReaderOptions { HasHeader = false }))
			Assert.Equal(["b"], reader.ParallelEnumerate(r => r.GetString(1)));
	}

	private static string BuildText(int count, Func<int, string> line)
	{
		var sb = new StringBuilder();
		for (var i = 0; i < count; i++) sb.Append(line(i));

		return sb.ToString();
	}
}
