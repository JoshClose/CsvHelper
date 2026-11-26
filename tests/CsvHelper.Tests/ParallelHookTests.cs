using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>A call that a <see cref="HookRecordingSource"/> received.</summary>
/// <param name="Name"><c>Begin</c>, <c>End</c>, <c>Fill</c> or <c>Dispose</c>.</param>
/// <param name="Value">For <c>Begin</c>, the degree; for <c>End</c> and <c>Fill</c>, the fills running (a fill counts itself); else 0.</param>
/// <param name="Thread">The managed thread the call ran on.</param>
internal readonly record struct HookCall(string Name, int Value, int Thread);

/// <summary>
/// A record source that records the parallel hook's calls (<see cref="CsvRecordSource.BeginParallel"/> and
/// <see cref="CsvRecordSource.EndParallel"/>) among its fills: a header (<c>Id</c>, <c>Text</c>) at line 1, then rows 1
/// to <c>rows</c> at lines 2 on, a few a fill.
/// </summary>
internal sealed class HookRecordingSource : CsvRecordSource
{
	private readonly object gate = new();
	private readonly List<HookCall> calls = [];
	private readonly int rows;
	private readonly int rowsPerFill;
	private int next;
	private int filling;

	/// <summary>Creates the source.</summary>
	/// <param name="rows">The number of rows after the header.</param>
	/// <param name="rowsPerFill">The most rows one fill writes (it also stops when the batch is full).</param>
	public HookRecordingSource(int rows, int rowsPerFill = 7)
	{
		this.rows = rows;
		this.rowsPerFill = rowsPerFill;
	}

	/// <summary>Whether the source writes the header; without it, a source of no rows is empty.</summary>
	public bool Header { get; init; } = true;

	/// <summary>The row whose fill throws an <see cref="InvalidDataException"/>, after the rows before it; 0 for none.</summary>
	public int FailAt { get; init; }

	/// <summary>The row written with a third field; 0 for none.</summary>
	public int WideAt { get; init; }

	/// <summary>The row whose fill sets <see cref="Held"/> and then waits 300 ms before writing it; 0 for none.</summary>
	public int HoldAt { get; init; }

	/// <summary>Set when the fill of <see cref="HoldAt"/> starts to wait.</summary>
	public ManualResetEventSlim Held { get; } = new(false);

	/// <summary>The calls so far, in the order they began.</summary>
	public IReadOnlyList<HookCall> Calls
	{
		get
		{
			lock (gate)
			{
				return calls.ToArray();
			}
		}
	}

	/// <inheritdoc/>
	public override void BeginParallel(int degree) => Add("Begin", degree);

	/// <inheritdoc/>
	public override void EndParallel() => Add("End", Volatile.Read(ref filling));

	/// <inheritdoc/>
	public override bool Fill(CsvReader.RecordBatch batch)
	{
		var running = Interlocked.Increment(ref filling);
		try
		{
			Add("Fill", running);
			if (next == 0)
			{
				next = 1;
				if (Header)
				{
					batch.AddField("Id"u8, CsvCellKind.Text);
					batch.AddField("Text"u8, CsvCellKind.Text);
					batch.EndRecord(1);
				}
			}

			var written = 0;
			while (next <= rows && written < rowsPerFill && !batch.IsFull)
			{
				if (next == FailAt)
				{
					throw new InvalidDataException($"Row {next} is broken.");
				}

				if (next == HoldAt && !Held.IsSet)
				{
					Held.Set();
					Thread.Sleep(300);
				}

				var id = next.ToString(CultureInfo.InvariantCulture);
				batch.AddField(Encoding.UTF8.GetBytes(id), CsvCellKind.Number);
				batch.AddField(Encoding.UTF8.GetBytes("row " + id), CsvCellKind.Text);
				if (next == WideAt)
				{
					batch.AddField("extra"u8, CsvCellKind.Text);
				}

				batch.EndRecord(next + 1);
				next++;
				written++;
			}

			return next <= rows;
		}
		finally
		{
			Interlocked.Decrement(ref filling);
		}
	}

	/// <inheritdoc/>
	public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken) => new(Fill(batch));

	/// <inheritdoc/>
	public override string? Locate(long line, int field) => null;

	/// <inheritdoc/>
	public override void Dispose() => Add("Dispose", 0);

	/// <summary>Records a call on this thread.</summary>
	/// <param name="name">The call.</param>
	/// <param name="value">What it records (see <see cref="HookCall.Value"/>).</param>
	private void Add(string name, int value)
	{
		lock (gate)
		{
			calls.Add(new HookCall(name, value, Environment.CurrentManagedThreadId));
		}
	}
}

/// <summary>
/// Core's parallel hook (Phase 9 step 8): <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/> calls a
/// record source's <see cref="CsvRecordSource.BeginParallel"/> once the header is read, before any worker fills, and its
/// <see cref="CsvRecordSource.EndParallel"/> once the workers have stopped, however the enumeration ends; sequential
/// reads call neither.
/// </summary>
public class ParallelHookTests
{
	/// <summary>
	/// Asserts that <paramref name="calls"/> hold <paramref name="count"/> enumerations, one after the other: each a
	/// <c>Begin</c> with <paramref name="degree"/> and then an <c>End</c> with no fill running, both on
	/// <paramref name="caller"/>; every fill between them on a worker, every other fill on <paramref name="caller"/>, never
	/// two fills at once, and the source disposed outside them.
	/// </summary>
	/// <param name="calls">The source's calls.</param>
	/// <param name="count">The number of enumerations.</param>
	/// <param name="degree">The degree each began with.</param>
	/// <param name="caller">The thread that enumerated.</param>
	private static void AssertEnumerations(IReadOnlyList<HookCall> calls, int count, int degree, int caller)
	{
		var inside = false;
		var begins = 0;
		var ends = 0;
		foreach (var call in calls)
		{
			switch (call.Name)
			{
				case "Begin":
					Assert.False(inside);
					Assert.Equal(degree, call.Value);
					Assert.Equal(caller, call.Thread);
					inside = true;
					begins++;
					break;
				case "End":
					Assert.True(inside);
					Assert.Equal(0, call.Value);
					Assert.Equal(caller, call.Thread);
					inside = false;
					ends++;
					break;
				case "Fill":
					Assert.Equal(1, call.Value);
					if (inside)
					{
						Assert.NotEqual(caller, call.Thread);
					}
					else
					{
						Assert.Equal(caller, call.Thread);
					}

					break;
				default:
					Assert.Equal("Dispose", call.Name);
					Assert.False(inside);
					break;
			}
		}

		Assert.False(inside);
		Assert.Equal(count, begins);
		Assert.Equal(count, ends);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(-1)]
	public void BeginsBeforeTheWorkersFillAndEndsAfterTheLast(int degree)
	{
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(2000);
		using (var reader = new CsvReader(source, null))
		{
			Assert.Equal(Enumerable.Range(1, 2000), reader.ParallelEnumerate(r => r.Parse<int>(0), degree).ToList());
		}

		var calls = source.Calls;
		// -1 is one worker a processor.
		AssertEnumerations(calls, 1, degree == -1 ? Environment.ProcessorCount : degree, caller);
		// The header is read on this thread, before Begin; the rows on the workers.
		Assert.Equal(["Fill", "Begin", "Fill"], calls.Take(3).Select(c => c.Name));
		Assert.Equal("Dispose", calls[calls.Count - 1].Name);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(1500)]
	public void EndsAtABreakAndTheReaderReadsOnWithoutEither(int taken)
	{
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(3000);
		using var reader = new CsvReader(source, null);
		var ids = new List<int>();
		foreach (var id in reader.ParallelEnumerate(r => r.Parse<int>(0), 4))
		{
			ids.Add(id);
			if (ids.Count == taken)
			{
				break;
			}
		}

		Assert.Equal(Enumerable.Range(1, taken), ids);
		var atBreak = source.Calls.Count;
		AssertEnumerations(source.Calls, 1, 4, caller);

		// The records the workers read ahead are gone; Read goes on from a later row, on this thread.
		var rest = new List<int>();
		while (reader.Read())
		{
			rest.Add(reader.Parse<int>(0));
		}

		Assert.NotEmpty(rest);
		Assert.True(rest[0] > taken);
		Assert.Equal(Enumerable.Range(rest[0], 3000 - rest[0] + 1), rest);
		Assert.All(source.Calls.Skip(atBreak), c => Assert.Equal(new HookCall("Fill", 1, caller), c));
	}

	[Fact]
	public void EndsOnlyOnceTheFillsHaveStopped()
	{
		// A break while a worker's fill is running: End waits for it (no fill runs during End, and none after it).
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(3000) { HoldAt = 50 };
		using (var reader = new CsvReader(source, null))
		{
			foreach (var id in reader.ParallelEnumerate(r => r.Parse<int>(0), 4))
			{
				Assert.Equal(1, id);
				Assert.True(source.Held.Wait(TimeSpan.FromSeconds(30)));

				break;
			}
		}

		AssertEnumerations(source.Calls, 1, 4, caller);
	}

	[Fact]
	public void BeginsAndEndsEachEnumeration()
	{
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(3000);
		using (var reader = new CsvReader(source, null))
		{
			Assert.Equal(Enumerable.Range(1, 5), reader.ParallelEnumerate(r => r.Parse<int>(0), 3).Take(5));
			var rest = reader.ParallelEnumerate(r => r.Parse<int>(0), 3).ToList();

			Assert.True(rest[0] > 5);
			Assert.Equal(Enumerable.Range(rest[0], 3000 - rest[0] + 1), rest);
		}

		AssertEnumerations(source.Calls, 2, 3, caller);
	}

	[Fact]
	public void EndsAtASelectorsException()
	{
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(3000);
		using (var reader = new CsvReader(source, null))
		{
			var ids = new List<int>();
			var e = Assert.Throws<InvalidOperationException>(() =>
			{
				foreach (var id in reader.ParallelEnumerate(r => r.Parse<int>(0) == 500 ? throw new InvalidOperationException("Record 500.") : r.Parse<int>(0), 4))
				{
					ids.Add(id);
				}
			});

			Assert.Equal("Record 500.", e.Message);
			Assert.Equal(Enumerable.Range(1, 499), ids);
		}

		AssertEnumerations(source.Calls, 1, 4, caller);
	}

	[Fact]
	public void EndsAtAReadersException()
	{
		var caller = Environment.CurrentManagedThreadId;

		// The source's fill throws.
		var failing = new HookRecordingSource(3000) { FailAt = 700 };
		using (var reader = new CsvReader(failing, null))
		{
			var ids = new List<int>();
			var e = Assert.Throws<InvalidDataException>(() =>
			{
				foreach (var id in reader.ParallelEnumerate(r => r.Parse<int>(0), 4))
				{
					ids.Add(id);
				}
			});

			Assert.Equal("Row 700 is broken.", e.Message);
			Assert.Equal(Enumerable.Range(1, ids.Count), ids);
			Assert.True(ids.Count < 700);
		}

		AssertEnumerations(failing.Calls, 1, 4, caller);

		// The reader's own check of the records.
		var wide = new HookRecordingSource(3000) { WideAt = 900 };
		using (var reader = new CsvReader(wide, new CsvReaderOptions { FieldCountMismatch = CsvFieldCountHandling.Throw }))
		{
			Assert.Throws<CsvFormatException>(() => reader.ParallelEnumerate(r => r.Parse<int>(0), 4).ToList());
		}

		AssertEnumerations(wide.Calls, 1, 4, caller);
	}

	[Fact]
	public void EndsWhenTheEnumeratorIsDisposed()
	{
		var caller = Environment.CurrentManagedThreadId;
		var source = new HookRecordingSource(3000);
		using var reader = new CsvReader(source, null);

		// Disposed before it starts: no header read, and neither call.
		reader.ParallelEnumerate(r => r.Parse<int>(0), 4).GetEnumerator().Dispose();
		Assert.Empty(source.Calls);

		using (var e = reader.ParallelEnumerate(r => r.Parse<int>(0), 4).GetEnumerator())
		{
			Assert.True(e.MoveNext());
			Assert.Equal(1, e.Current);
			Assert.DoesNotContain(source.Calls, c => c.Name == "End");
		}

		AssertEnumerations(source.Calls, 1, 4, caller);
	}

	[Fact]
	public void CallsNeitherForAnInputThatEndsWhereItsHeaderWouldBe()
	{
		var caller = Environment.CurrentManagedThreadId;

		// No header record: the enumeration ends before it begins.
		var empty = new HookRecordingSource(0) { Header = false };
		using (var reader = new CsvReader(empty, null))
		{
			Assert.Empty(reader.ParallelEnumerate(r => r.Parse<int>(0), 4));
		}

		Assert.Equal([new HookCall("Fill", 1, caller), new HookCall("Dispose", 0, caller)], empty.Calls);

		// A header and no rows: the header check passes, so both are called, with no fill between them (the header's fill
		// found the end).
		var headerOnly = new HookRecordingSource(0);
		using (var reader = new CsvReader(headerOnly, null))
		{
			Assert.Empty(reader.ParallelEnumerate(r => r.Parse<int>(0), 4));
		}

		Assert.Equal(["Fill", "Begin", "End", "Dispose"], headerOnly.Calls.Select(c => c.Name));
		AssertEnumerations(headerOnly.Calls, 1, 4, caller);
	}

	[Fact]
	public async Task CallsNeitherInSequentialReads()
	{
		var caller = Environment.CurrentManagedThreadId;
		var read = new HookRecordingSource(500);
		using (var reader = new CsvReader(read, null))
		{
			var count = 0;
			while (reader.Read())
			{
				count++;
			}

			Assert.Equal(500, count);
		}

		AssertEnumerations(read.Calls, 0, 0, caller);

		var records = new HookRecordingSource(500);
		using (var reader = new CsvReader(records, null))
		{
			Assert.Equal(500, reader.GetRecords<CsvDynamicRecord>().Count());
		}

		AssertEnumerations(records.Calls, 0, 0, caller);

		var async = new HookRecordingSource(500);
		using (var reader = new CsvReader(async, null))
		{
			var count = 0;
			while (await reader.ReadAsync())
			{
				count++;
			}

			Assert.Equal(500, count);
		}

		Assert.DoesNotContain(async.Calls, c => c.Name is "Begin" or "End");
	}
}
