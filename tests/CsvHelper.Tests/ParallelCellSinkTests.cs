using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>The switches of a <see cref="FakeChunkSink"/>, which its owner and its workers share.</summary>
internal sealed class FakeChunkOptions
{
	/// <summary>Whether the sink takes parallel writes (<see cref="CsvCellSink.SupportsParallel"/>).</summary>
	public bool Supports { get; init; } = true;

	/// <summary>
	/// The records the owner must have ended (the header included) before it is ready for workers
	/// (<see cref="CsvCellSink.ReadyForWorkers"/>); <see cref="long.MaxValue"/> for never, as a sheet with shared strings.
	/// </summary>
	public long ReadyAfter { get; init; }

	/// <summary>
	/// Flags the last chunk of every this many, in the order the workers begin them, so that the owner refuses it; 0 for
	/// none.
	/// </summary>
	public int RedoEvery { get; init; }

	/// <summary>The length each chunk reports, by which the parallel write sizes its batches; <see langword="null"/> for its bytes.</summary>
	public int? Length { get; init; }

	/// <summary>The text of the field at which a worker's sink throws <see cref="WorkerError"/>; <see langword="null"/> for none.</summary>
	public string? WorkerFailsOn { get; init; }

	/// <summary>What a worker's sink throws at <see cref="WorkerFailsOn"/>.</summary>
	public Exception? WorkerError { get; init; }

	/// <summary>Whether a worker's failure flags its chunk, as a sink's own error does, so that the owner refuses it.</summary>
	public bool FlagsWorkerFailure { get; init; }

	/// <summary>The text of the field at which the owner's sink throws <see cref="OwnerError"/>; <see langword="null"/> for none.</summary>
	public string? OwnerFailsOn { get; init; }

	/// <summary>What the owner's sink throws at <see cref="OwnerFailsOn"/>.</summary>
	public Exception? OwnerError { get; init; }

	/// <summary>
	/// The call of <see cref="CsvCellSink.WriteChunk"/> or <see cref="CsvCellSink.WriteChunkAsync"/> (1 for the first)
	/// that throws <see cref="WriteChunkError"/>, having taken the chunk; 0 for none.
	/// </summary>
	public int FailsWriteChunk { get; init; }

	/// <summary>What the call <see cref="FailsWriteChunk"/> throws.</summary>
	public Exception? WriteChunkError { get; init; }

	/// <summary>
	/// Called with the number of each call of <see cref="CsvCellSink.WriteChunk"/> or <see cref="CsvCellSink.WriteChunkAsync"/>
	/// (1 for the first), before it appends anything.
	/// </summary>
	public Action<int>? OnWriteChunk { get; init; }

	/// <summary>
	/// Every this many records it ends, a sink asks to be flushed; 0 for never. The owner is flushed; a worker's ask must be
	/// ignored.
	/// </summary>
	public int FlushEvery { get; init; }

	/// <summary>
	/// Whether each chunk must start at the record the output has reached when it is appended: so it does when every item
	/// writes one record (<c>ParallelWriteRecords</c>), which checks the record number core begins chunks with.
	/// </summary>
	public bool OneRecordPerItem { get; init; }
}

/// <summary>A chunk of a <see cref="FakeChunkSink"/>'s worker: the bytes of its records, as the owner writes records.</summary>
internal sealed class FakeChunk
{
	/// <summary>Creates an empty chunk.</summary>
	/// <param name="id">The number of the chunk, in the order the workers began them, from 0.</param>
	/// <param name="firstRecord">The record number the parallel write began the chunk with (<see cref="CsvCellSink.BeginChunk"/>).</param>
	public FakeChunk(int id, long firstRecord)
	{
		Id = id;
		FirstRecord = firstRecord;
	}

	/// <summary>The number of the chunk, in the order the workers began them, from 0.</summary>
	public int Id { get; }

	/// <summary>The record number the parallel write began the chunk with.</summary>
	public long FirstRecord { get; }

	/// <summary>The bytes of the chunk's records.</summary>
	public MemoryStream Bytes { get; } = new();

	/// <summary>The number of records in the chunk.</summary>
	public int Records { get; set; }

	/// <summary>Whether the worker flagged the chunk, so that the owner refuses it.</summary>
	public bool Redo { get; set; }
}

/// <summary>A call that a <see cref="FakeChunkSink"/> received, other than the fields and records.</summary>
/// <param name="Name">The member: <c>BeginParallel</c>, <c>BeginWorkers</c>, <c>CreateWorker</c>, <c>WriteChunk</c>, ...</param>
/// <param name="Value">For <c>BeginParallel</c>, the degree; for <c>WriteChunk</c> and its async form, the call's number; else 0.</param>
/// <param name="Thread">The managed thread the call ran on.</param>
internal readonly record struct ChunkSinkCall(string Name, long Value, int Thread);

/// <summary>
/// A cell sink that takes parallel writes, as the owner and as the workers' sinks it makes. Each record is written as
/// bytes: for each field its type's name, <c>=</c>, its text and 0x1F, and then 0x1E. A worker writes its records into its
/// chunk, which the owner appends to its output as it is, so that the outputs of a parallel write and a serial one compare
/// byte for byte. It counts and checks the calls of the parallel members (on which sink, on which thread, in what order,
/// how often), and keeps what it finds wrong in <see cref="Problems"/> rather than throwing, which would change the write.
/// </summary>
internal sealed class FakeChunkSink : CsvCellSink
{
	// The byte after each field's text, and after each record's last field.
	private const byte FieldEnd = 0x1F;
	private const byte RecordEnd = 0x1E;

	private readonly FakeChunkSink? owner;   // null on the owner
	private readonly FakeChunkOptions options;
	private readonly MemoryStream pending = new();   // the record being written
	private long ended;   // the records this sink ended, for FlushEvery

	// The owner's, shared by its workers.
	private readonly object gate = new();
	private readonly List<ChunkSinkCall> calls = [];
	private readonly ConcurrentQueue<string> problems = new();
	private readonly ConcurrentDictionary<int, FakeChunk> taken = new();
	private readonly ConcurrentDictionary<int, string> handled = new();
	private readonly ConcurrentQueue<FakeChunkSink> workers = new();
	private readonly ConcurrentDictionary<int, byte> readyThreads = new();
	private readonly MemoryStream output = new();
	private readonly List<FakeChunk> refused = [];
	private int chunks;
	private int creating;
	private int releasedWorkers;
	private int readyReads;
	private int writeChunkCalls;
	private volatile bool inParallel;
	private volatile bool beganWorkers;
	private long ownerRecords;
	private long lastFirstRecord;

	// A worker's.
	private FakeChunk? chunk;
	private int busy;
	private int released;

	/// <summary>Creates the owner: the sink a writer is created with.</summary>
	/// <param name="options">The switches, or <see langword="null"/> for none.</param>
	public FakeChunkSink(FakeChunkOptions? options = null)
	{
		this.options = options ?? new FakeChunkOptions();
	}

	/// <summary>Creates a worker's sink.</summary>
	/// <param name="owner">The owner, which made it.</param>
	private FakeChunkSink(FakeChunkSink owner)
	{
		this.owner = owner;
		options = owner.options;
	}

	/// <summary>The owner's output: its own records and the chunks it appended, in order.</summary>
	public byte[] Output => output.ToArray();

	/// <summary>The number of records in <see cref="Output"/>.</summary>
	public long Records { get; private set; }

	/// <summary>The calls so far, in the order they were made.</summary>
	public IReadOnlyList<ChunkSinkCall> Calls
	{
		get
		{
			lock (gate)
			{
				return calls.ToArray();
			}
		}
	}

	/// <summary>What the sink found wrong: a member called on the wrong sink, out of order, or more than once.</summary>
	public IReadOnlyCollection<string> Problems => problems.ToArray();

	/// <summary>The chunks the workers handed over, by number.</summary>
	public IReadOnlyList<FakeChunk> Chunks => taken.Values.OrderBy(c => c.Id).ToList();

	/// <summary>The chunks the workers handed over.</summary>
	public int ChunksTaken => taken.Count;

	/// <summary>The chunks the owner was handed back: written (or refused) or dropped.</summary>
	public int ChunksHandled => handled.Count;

	/// <summary>The chunks dropped (<see cref="CsvCellSink.DiscardChunk"/>).</summary>
	public int ChunksDiscarded => handled.Values.Count(v => v == nameof(DiscardChunk));

	/// <summary>The chunks the owner refused, in input order.</summary>
	public IReadOnlyList<FakeChunk> Refused => refused;

	/// <summary>The worker sinks made.</summary>
	public int WorkersCreated => workers.Count;

	/// <summary>The worker sinks released.</summary>
	public int WorkersReleased => Volatile.Read(ref releasedWorkers);

	/// <summary>The reads of <see cref="ReadyForWorkers"/>.</summary>
	public int ReadyReads => Volatile.Read(ref readyReads);

	/// <summary>The threads <see cref="ReadyForWorkers"/> was read on.</summary>
	public IReadOnlyCollection<int> ReadyThreads => readyThreads.Keys.ToArray();

	/// <summary>Whether a parallel call began and has not ended.</summary>
	public bool InParallel => inParallel;

	/// <inheritdoc/>
	public override bool SupportsParallel
	{
		get
		{
			CheckOwner(nameof(SupportsParallel));

			return options.Supports;
		}
	}

	/// <inheritdoc/>
	public override bool ReadyForWorkers
	{
		get
		{
			CheckOwner(nameof(ReadyForWorkers));
			Interlocked.Increment(ref readyReads);
			readyThreads[Environment.CurrentManagedThreadId] = 0;
			if (!inParallel || beganWorkers)
			{
				Problem("ReadyForWorkers read outside the serial prefix of a parallel call.");
			}

			return ownerRecords >= options.ReadyAfter;
		}
	}

	/// <summary>The number of calls of a member.</summary>
	/// <param name="name">The member.</param>
	/// <returns>How many times it was called.</returns>
	public int Count(string name) => Calls.Count(c => c.Name == name);

	/// <inheritdoc/>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type)
	{
		var failsOn = owner is null ? options.OwnerFailsOn : options.WorkerFailsOn;
		if (failsOn is not null && Encoding.UTF8.GetString(utf8.ToArray()) == failsOn)
		{
			if (owner is null)
			{
				throw options.OwnerError!;
			}

			if (options.FlagsWorkerFailure && chunk is not null)
			{
				chunk.Redo = true;
			}

			throw options.WorkerError!;
		}

		if (owner is not null && chunk is null)
		{
			Problem("A worker's field outside a chunk.");
		}

		var name = Encoding.UTF8.GetBytes(type.Name);
		pending.Write(name, 0, name.Length);
		pending.WriteByte((byte)'=');
		var bytes = utf8.ToArray();
		pending.Write(bytes, 0, bytes.Length);
		pending.WriteByte(FieldEnd);
	}

	/// <inheritdoc/>
	public override bool EndRecord()
	{
		pending.WriteByte(RecordEnd);
		if (owner is null)
		{
			pending.WriteTo(output);
			ownerRecords++;
			Records++;
		}
		else if (chunk is null)
		{
			Problem("A worker's record outside a chunk.");
		}
		else
		{
			pending.WriteTo(chunk.Bytes);
			chunk.Records++;
		}

		pending.SetLength(0);
		ended++;

		return options.FlushEvery > 0 && ended % options.FlushEvery == 0;
	}

	/// <inheritdoc/>
	public override void DiscardRecord() => pending.SetLength(0);

	/// <inheritdoc/>
	public override void Flush() => Log(nameof(Flush), 0, ownerOnly: true);

	/// <inheritdoc/>
	public override ValueTask FlushAsync(CancellationToken cancellationToken)
	{
		Log(nameof(FlushAsync), 0, ownerOnly: true);

		return default;
	}

	/// <inheritdoc/>
	public override void Complete() => Log(nameof(Complete), 0, ownerOnly: true);

	/// <inheritdoc/>
	public override ValueTask CompleteAsync()
	{
		Log(nameof(CompleteAsync), 0, ownerOnly: true);

		return default;
	}

	/// <inheritdoc/>
	public override void BeginParallel(int degree)
	{
		CheckOwner(nameof(BeginParallel));
		if (inParallel)
		{
			Problem("BeginParallel inside a parallel call.");
		}

		inParallel = true;
		beganWorkers = false;
		lastFirstRecord = long.MinValue;
		Log(nameof(BeginParallel), degree);
	}

	/// <inheritdoc/>
	public override void BeginWorkers()
	{
		CheckOwner(nameof(BeginWorkers));
		if (!inParallel || beganWorkers)
		{
			Problem("BeginWorkers outside a parallel call, or twice in one.");
		}

		if (ownerRecords < options.ReadyAfter)
		{
			Problem($"BeginWorkers after {ownerRecords} records of the owner, before it was ready.");
		}

		beganWorkers = true;
		Log(nameof(BeginWorkers), 0);
	}

	/// <inheritdoc/>
	public override CsvCellSink CreateWorker()
	{
		CheckOwner(nameof(CreateWorker));
		if (Interlocked.Exchange(ref creating, 1) != 0)
		{
			Problem("Two CreateWorker calls at once.");
		}

		if (!inParallel || !beganWorkers)
		{
			Problem("CreateWorker outside BeginWorkers and EndParallel.");
		}

		var worker = new FakeChunkSink(this);
		workers.Enqueue(worker);
		Log(nameof(CreateWorker), 0);
		Volatile.Write(ref creating, 0);

		return worker;
	}

	/// <inheritdoc/>
	public override void BeginChunk(long firstRecord)
	{
		CheckWorker(nameof(BeginChunk));
		if (Interlocked.Exchange(ref busy, 1) != 0)
		{
			Problem("A worker's sink used by two threads at once.");
		}

		if (chunk is not null || pending.Length != 0)
		{
			Problem("A chunk begun before the one before it was taken.");
		}

		chunk = new FakeChunk(Interlocked.Increment(ref owner!.chunks) - 1, firstRecord);
	}

	/// <inheritdoc/>
	public override object TakeChunk(out int length)
	{
		CheckWorker(nameof(TakeChunk));
		var result = chunk;
		if (result is null)
		{
			Problem("TakeChunk without BeginChunk.");
			result = new FakeChunk(-1, 0);
		}

		if (pending.Length != 0)
		{
			Problem("A chunk taken with a record open.");
		}

		chunk = null;
		if (options.RedoEvery > 0 && result.Id % options.RedoEvery == options.RedoEvery - 1)
		{
			result.Redo = true;
		}

		owner!.taken[result.Id] = result;
		length = options.Length ?? (int)result.Bytes.Length;
		Volatile.Write(ref busy, 0);

		return result;
	}

	/// <inheritdoc/>
	public override bool WriteChunk(object chunk) => Append(chunk, nameof(WriteChunk), default);

	/// <inheritdoc/>
	public override ValueTask<bool> WriteChunkAsync(object chunk, CancellationToken cancellationToken) =>
		new(Append(chunk, nameof(WriteChunkAsync), cancellationToken));

	/// <inheritdoc/>
	public override void DiscardChunk(object chunk)
	{
		CheckOwner(nameof(DiscardChunk));
		Log(nameof(DiscardChunk), 0);
		if (!inParallel)
		{
			Problem("DiscardChunk outside a parallel call.");
		}

		Handle((FakeChunk)chunk, nameof(DiscardChunk));
	}

	/// <inheritdoc/>
	public override void ReleaseWorker(CsvCellSink worker)
	{
		CheckOwner(nameof(ReleaseWorker));
		if (worker is not FakeChunkSink sink || !ReferenceEquals(sink.owner, this))
		{
			Problem("ReleaseWorker with a sink this one did not make.");
		}
		else if (Interlocked.Increment(ref sink.released) != 1)
		{
			Problem("A worker's sink released twice.");
		}

		Interlocked.Increment(ref releasedWorkers);
		Log(nameof(ReleaseWorker), 0);
	}

	/// <inheritdoc/>
	public override void EndParallel()
	{
		CheckOwner(nameof(EndParallel));
		if (!inParallel)
		{
			Problem("EndParallel outside a parallel call.");
		}

		inParallel = false;
		Log(nameof(EndParallel), 0);
	}

	/// <summary>Appends a chunk to the output, or refuses it when it is flagged, as WriteChunk and its async form do.</summary>
	/// <param name="value">The chunk.</param>
	/// <param name="member">The member called.</param>
	/// <param name="cancellationToken">The call's token: a cancellation throws before anything is appended.</param>
	/// <returns>Whether the chunk was appended.</returns>
	private bool Append(object value, string member, CancellationToken cancellationToken)
	{
		CheckOwner(member);
		var call = ++writeChunkCalls;
		Log(member, call);
		if (!inParallel || !beganWorkers)
		{
			Problem($"{member} outside BeginWorkers and EndParallel.");
		}

		var appended = (FakeChunk)value;
		Handle(appended, member);
		if (pending.Length != 0)
		{
			Problem($"{member} inside a record of the owner.");
		}

		if (appended.FirstRecord <= lastFirstRecord)
		{
			Problem($"Chunk {appended.Id} (record {appended.FirstRecord}) after record {lastFirstRecord}: out of order.");
		}

		lastFirstRecord = appended.FirstRecord;
		if (options.OneRecordPerItem && appended.FirstRecord != Records)
		{
			Problem($"Chunk {appended.Id} begins at record {appended.FirstRecord}, after {Records} records.");
		}

		options.OnWriteChunk?.Invoke(call);
		cancellationToken.ThrowIfCancellationRequested();
		if (call == options.FailsWriteChunk)
		{
			throw options.WriteChunkError!;
		}

		if (appended.Redo)
		{
			refused.Add(appended);

			return false;
		}

		appended.Bytes.WriteTo(output);
		Records += appended.Records;

		return true;
	}

	/// <summary>Counts a chunk handed back to the owner, checking that it is one of its workers' and comes back once.</summary>
	/// <param name="value">The chunk.</param>
	/// <param name="member">The member it came back through.</param>
	private void Handle(FakeChunk value, string member)
	{
		if (!taken.ContainsKey(value.Id))
		{
			Problem($"{member} with a chunk no worker handed over.");
		}

		if (!handled.TryAdd(value.Id, member))
		{
			Problem($"Chunk {value.Id} came back twice: through {handled[value.Id]} and {member}.");
		}
	}

	/// <summary>Notes a problem if this is a worker's sink.</summary>
	/// <param name="member">The member called.</param>
	private void CheckOwner(string member)
	{
		if (owner is not null)
		{
			Problem($"{member} called on a worker's sink.");
		}
	}

	/// <summary>Notes a problem if this is the owner.</summary>
	/// <param name="member">The member called.</param>
	private void CheckWorker(string member)
	{
		if (owner is null)
		{
			Problem($"{member} called on the owner.");
		}
	}

	/// <summary>Records a call on this thread, in the owner's list.</summary>
	/// <param name="name">The member called.</param>
	/// <param name="value">What it records (see <see cref="ChunkSinkCall.Value"/>).</param>
	/// <param name="ownerOnly">Whether a call on a worker's sink is a problem.</param>
	private void Log(string name, long value, bool ownerOnly = false)
	{
		if (ownerOnly)
		{
			CheckOwner(name);
		}

		var root = owner ?? this;
		lock (root.gate)
		{
			root.calls.Add(new ChunkSinkCall(name, value, Environment.CurrentManagedThreadId));
		}
	}

	/// <summary>Notes a problem in the owner's list.</summary>
	/// <param name="text">What is wrong.</param>
	private void Problem(string text) => (owner ?? this).problems.Enqueue(text);
}

/// <summary>
/// Core's part of parallel writes to a cell sink (Phase 9 step 10): <see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/>,
/// <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> and their async forms on a writer of
/// a <see cref="FakeChunkSink"/> write what <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> and a loop write
/// through it, byte for byte, with the record count; call the sink's parallel members on the right sink and thread, in
/// order, each chunk handed back once and each worker released once; write a refused chunk's items again; and end where a
/// serial write would when something throws.
/// </summary>
public class ParallelCellSinkTests
{
	/// <summary>The byte a <see cref="FakeChunkSink"/> writes after each record.</summary>
	private const byte RecordEnd = 0x1E;

	/// <summary>The degrees each case runs at: one worker, a few, and one a processor.</summary>
	private static readonly int[] Degrees = [1, 3, -1];

	/// <summary>Parcels whose names are unique (<c>n</c> and the id), so that a sink can fail at one by its text.</summary>
	/// <param name="count">The number of parcels.</param>
	/// <param name="seed">The seed of their other values.</param>
	/// <returns>The parcels.</returns>
	private static Parcel[] Parcels(int count, int seed)
	{
		string[] notes = ["Contoso, Ltd", "say \"hi\"", "two\nlines", "", " padded ", "naïve café", "日本語"];
		var rng = new Random(seed);
		var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var parcels = new Parcel[count];
		for (var i = 0; i < count; i++)
		{
			parcels[i] = new Parcel
			{
				Id = i,
				Name = "n" + i.ToString(CultureInfo.InvariantCulture),
				Weight = Math.Round(rng.NextDouble() * 1000, rng.Next(4)),
				Sent = start.AddSeconds(rng.Next(0, 100_000_000)),
				Note = rng.Next(4) == 0 ? null : notes[rng.Next(notes.Length)],
			};
		}

		return parcels;
	}

	[Theory]
	[InlineData(0L)]
	[InlineData(100L)]
	[InlineData(long.MaxValue)]
	public async Task RecordsMatchWriteRecords(long readyAfter)
	{
		var parcels = Parcels(5_000, 1);
		var expected = Write(w => w.WriteRecords(parcels));
		foreach (var degree in Degrees)
		{
			var options = new FakeChunkOptions { ReadyAfter = readyAfter, OneRecordPerItem = true, FlushEvery = 50 };
			var context = $"ready after {readyAfter}, degree {degree}";
			var results = new List<(string Name, Result Actual)>
			{
				("array", Write(w => w.ParallelWriteRecords(parcels, degree), options)),
				("list", Write(w => w.ParallelWriteRecords(parcels.ToList(), degree), options)),
				("enumerable", Write(w => w.ParallelWriteRecords(parcels.Select(p => p), degree), options)),
				("async", await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, degree), options)),
				("async enumerable", await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options)),
			};
			foreach (var (name, actual) in results)
			{
				AssertWrites(expected, actual, $"{context}, {name}");
				AssertCall(actual, degree, $"{context}, {name}");
				var sink = actual.Sink;
				if (readyAfter == long.MaxValue)
				{
					// Never ready: every record through the owner, reading ReadyForWorkers once an item (and once at the end).
					Assert.Equal(0, sink.Count("BeginWorkers"));
					Assert.Equal(0, sink.WorkersCreated);
					Assert.Equal(0, sink.ChunksTaken);
					Assert.InRange(sink.ReadyReads, parcels.Length, parcels.Length + 1);
				}
				else
				{
					Assert.Equal(1, sink.Count("BeginWorkers"));
					Assert.NotEqual(0, sink.ChunksTaken);
					Assert.Equal(sink.ChunksTaken, sink.Count(name.StartsWith("async", StringComparison.Ordinal) ? "WriteChunkAsync" : "WriteChunk"));
					// The workers begin after the owner wrote what it was not ready without (the header counts).
					Assert.True(sink.Chunks[0].FirstRecord >= readyAfter, context);
				}
			}
		}
	}

	[Fact]
	public async Task RecordWritersOfYourOwnMatchWriteRecords()
	{
		var parcels = Parcels(5_000, 2);
		var recordWriter = new ParcelWeightWriter();
		var expected = Write(w => w.WriteRecords(parcels, recordWriter));
		foreach (var degree in Degrees)
		{
			foreach (var readyAfter in new[] { 0L, 100L })
			{
				var options = new FakeChunkOptions { ReadyAfter = readyAfter, OneRecordPerItem = true };
				var context = $"degree {degree}, ready after {readyAfter}";
				var sync = Write(w => w.ParallelWriteRecords(parcels, recordWriter, degree), options);
				AssertWrites(expected, sync, context);
				AssertCall(sync, degree, context);
				var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, recordWriter, degree), options);
				AssertWrites(expected, asyncResult, context + ", async");
				AssertCall(asyncResult, degree, context + ", async");
				var asyncItems = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), recordWriter, degree), options);
				AssertWrites(expected, asyncItems, context + ", async enumerable");
				AssertCall(asyncItems, degree, context + ", async enumerable");
			}
		}
	}

	[Fact]
	public async Task SmallInputsMatchWriteRecords()
	{
		foreach (var count in new[] { 0, 1, 2, 63, 64, 65, 66, 129, 163, 164, 165, 500 })
		{
			var parcels = Parcels(count, count);
			foreach (var header in new[] { true, false })
			{
				var csv = new CsvWriterOptions { HasHeader = header };
				var expected = Write(w => w.WriteRecords(parcels), csv: csv);
				foreach (var readyAfter in new[] { 0L, 100L })
				{
					var options = new FakeChunkOptions { ReadyAfter = readyAfter, OneRecordPerItem = true };
					var context = $"{count} records, header {header}, ready after {readyAfter}";
					var sync = Write(w => w.ParallelWriteRecords(parcels, 4), options, csv);
					AssertWrites(expected, sync, context);
					AssertCall(sync, 4, context);
					var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, 4), options, csv);
					AssertWrites(expected, asyncResult, context + ", async");
					AssertCall(asyncResult, 4, context + ", async");

					// The items written before the workers may start: the field count's first record without a header, and
					// those the owner is not ready without. The rest go to the workers when they are more than a batch.
					var prefix = Math.Min(count, Math.Max(readyAfter - (header ? 1 : 0), header ? 0 : 1));
					var workers = count - prefix > 64;
					foreach (var sink in new[] { sync.Sink, asyncResult.Sink })
					{
						Assert.True((workers ? 1 : 0) == sink.Count("BeginWorkers"), context);
						Assert.True(workers == (sink.ChunksTaken > 0), context);
					}
				}
			}
		}
	}

	[Fact]
	public void RecordsWrittenBeforeTheCallAreContinued()
	{
		var parcels = Parcels(3_000, 3);
		var expected = Write(Before(w => w.WriteRecords(parcels)));
		var actual = Write(Before(w => w.ParallelWriteRecords(parcels, 3)), new FakeChunkOptions { OneRecordPerItem = true });
		AssertWrites(expected, actual, "after a header and a record");
		AssertCall(actual, 3, "after a header and a record");
		Assert.Equal(3_002, actual.Records);

		static Action<CsvWriter> Before(Action<CsvWriter> then) => w =>
		{
			w.WriteHeader<Parcel>();
			w.WriteRecord(new Parcel { Id = -1, Name = "first" });
			then(w);
		};
	}

	[Fact]
	public async Task CallbacksMatchALoop()
	{
		var parcels = Parcels(6_000, 4);
		var expected = Write(w =>
		{
			w.WriteHeader<Parcel>();
			foreach (var parcel in parcels)
			{
				WriteItem(w, parcel);
			}
		});
		foreach (var degree in Degrees)
		{
			foreach (var readyAfter in new[] { 0L, 100L })
			{
				var options = new FakeChunkOptions { ReadyAfter = readyAfter };
				var context = $"degree {degree}, ready after {readyAfter}";
				var sync = Write(w =>
				{
					w.WriteHeader<Parcel>();
					w.ParallelWrite(parcels, WriteItem, degree);
				}, options);
				AssertWrites(expected, sync, context);
				AssertCall(sync, degree, context);
				var asyncResult = await WriteAsync(w =>
				{
					w.WriteHeader<Parcel>();

					return w.ParallelWriteAsync(parcels, WriteItem, degree);
				}, options);
				AssertWrites(expected, asyncResult, context + ", async");
				AssertCall(asyncResult, degree, context + ", async");
				var asyncItems = await WriteAsync(w =>
				{
					w.WriteHeader<Parcel>();

					return w.ParallelWriteAsync(Yield(parcels), WriteItem, degree);
				}, options);
				AssertWrites(expected, asyncItems, context + ", async enumerable");
				AssertCall(asyncItems, degree, context + ", async enumerable");
			}
		}

		// Some items write no record, some two: a detail and a subtotal.
		static void WriteItem(CsvWriter writer, Parcel parcel)
		{
			if (parcel.Id % 5 == 0)
			{
				return;
			}

			writer.WriteRecord(parcel);
			if (parcel.Id % 7 == 0)
			{
				writer.WriteRecord(parcel.Id.ToString(CultureInfo.InvariantCulture), "Subtotal", (parcel.Weight * 2).ToString(CultureInfo.InvariantCulture), "", "");
			}
		}
	}

	[Fact]
	public async Task DynamicRecordsMatchWriteRecords()
	{
		var rows = new List<Dictionary<string, object?>>();
		for (var i = 0; i < 6_000; i++)
		{
			var row = new Dictionary<string, object?> { ["Id"] = i, ["Name"] = i % 3 == 0 ? "a, b" : "c" };
			if (i % 4 != 0)
			{
				row["Amount"] = i * 1.5m;
			}

			rows.Add(row);
		}

		// The first row has every column.
		rows[0]["Amount"] = 0m;
		var expected = Write(w => w.WriteRecords(rows));
		foreach (var readyAfter in new[] { 0L, 100L })
		{
			var options = new FakeChunkOptions { ReadyAfter = readyAfter, OneRecordPerItem = true };
			var context = $"dictionaries, ready after {readyAfter}";
			var sync = Write(w => w.ParallelWriteRecords(rows, 3), options);
			AssertWrites(expected, sync, context);
			AssertCall(sync, 3, context);
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(rows), 3), options);
			AssertWrites(expected, asyncResult, context + ", async");
			AssertCall(asyncResult, 3, context + ", async");
		}

		var expandos = new List<ExpandoObject>();
		for (var i = 0; i < 5_000; i++)
		{
			dynamic expando = new ExpandoObject();
			expando.Name = "n" + i;
			expando.Total = i / 4.0;
			expandos.Add(expando);
		}

		var expandosActual = Write(w => w.ParallelWriteRecords(expandos, -1), new FakeChunkOptions { OneRecordPerItem = true });
		AssertWrites(Write(w => w.WriteRecords(expandos)), expandosActual, "ExpandoObjects");
		AssertCall(expandosActual, -1, "ExpandoObjects");
	}

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	public async Task RefusedChunksAreWrittenAgainSerially(int every)
	{
		var parcels = Parcels(6_000, 5);
		var expected = Write(w => w.WriteRecords(parcels));
		foreach (var degree in Degrees)
		{
			var options = new FakeChunkOptions { RedoEvery = every, OneRecordPerItem = true, FlushEvery = 7 };
			var context = $"every {every}, degree {degree}";
			var sync = Write(w => w.ParallelWriteRecords(parcels, degree), options);
			AssertWrites(expected, sync, context);
			AssertCall(sync, degree, context);
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options);
			AssertWrites(expected, asyncResult, context + ", async");
			AssertCall(asyncResult, degree, context + ", async");
			foreach (var sink in new[] { sync.Sink, asyncResult.Sink })
			{
				Assert.NotEmpty(sink.Refused);
				Assert.Equal(sink.Chunks.Count(c => c.Redo), sink.Refused.Count);
				if (every == 1)
				{
					Assert.Equal(sink.ChunksTaken, sink.Refused.Count);
				}
			}
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task ARefusedChunksCallbacksRunAgainOnTheOwnersWriter(int every)
	{
		var items = Enumerable.Range(0, 6_000).ToArray();
		var expected = Write(w =>
		{
			w.WriteHeader(["Item", "Text"]);
			foreach (var item in items)
			{
				w.WriteField(item);
				w.WriteField("t" + item.ToString(CultureInfo.InvariantCulture));
				w.EndRecord();
			}
		});
		foreach (var useAsync in new[] { false, true })
		{
			var ownerCalls = new int[items.Length];
			var workerCalls = new int[items.Length];
			CsvWriter? owner = null;
			var options = new FakeChunkOptions { RedoEvery = every };
			var actual = useAsync
				? await WriteAsync(w =>
				{
					owner = w;
					w.WriteHeader(["Item", "Text"]);

					return w.ParallelWriteAsync(items, WriteItem, 3);
				}, options)
				: Write(w =>
				{
					owner = w;
					w.WriteHeader(["Item", "Text"]);
					w.ParallelWrite(items, WriteItem, 3);
				}, options);
			var context = $"every {every}, async {useAsync}";
			AssertWrites(expected, actual, context);

			// Each item ran once on a worker, and once more on the owner's writer when its chunk was refused.
			var refused = new bool[items.Length];
			foreach (var chunk in actual.Sink.Refused)
			{
				// One record an item, after the header: record 1 is item 0.
				for (var r = 0; r < chunk.Records; r++)
				{
					refused[chunk.FirstRecord - 1 + r] = true;
				}
			}

			Assert.All(workerCalls, calls => Assert.Equal(1, calls));
			for (var i = 0; i < items.Length; i++)
			{
				Assert.True((refused[i] ? 1 : 0) == ownerCalls[i], $"{context}: item {i} ran {ownerCalls[i]} times on the owner.");
			}

			Assert.Equal(actual.Sink.Refused.Sum(c => c.Records), ownerCalls.Sum());
			if (every == 1)
			{
				Assert.All(ownerCalls, calls => Assert.Equal(1, calls));
			}

			void WriteItem(CsvWriter writer, int item)
			{
				Interlocked.Increment(ref (ReferenceEquals(writer, owner) ? ownerCalls : workerCalls)[item]);
				writer.WriteField(item);
				writer.WriteField("t" + item.ToString(CultureInfo.InvariantCulture));
				writer.EndRecord();
			}
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public async Task AnExceptionOfAWorkersItemIsThrownAfterTheRecordsBeforeIt(int redoEvery)
	{
		var parcels = Parcels(12_000, 6);
		var error = new InvalidDataException("Parcel 7777 can't be written.");
		var recordWriter = new FailingParcelWriter(7_777, error);
		var expected = Write(w => w.WriteRecords(parcels, recordWriter));
		Assert.Same(error, expected.Error);
		foreach (var degree in Degrees)
		{
			var options = new FakeChunkOptions { RedoEvery = redoEvery, OneRecordPerItem = true };
			var context = $"degree {degree}, redo every {redoEvery}";
			var sync = Write(w => w.ParallelWriteRecords(parcels, recordWriter, degree), options);
			Assert.Same(error, sync.Error);
			AssertWrites(expected, sync, context);
			AssertCall(sync, degree, context);
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), recordWriter, degree), options);
			Assert.Same(error, asyncResult.Error);
			AssertWrites(expected, asyncResult, context + ", async");
			AssertCall(asyncResult, degree, context + ", async");
		}

		// A callback's exception, after the records of the items before it.
		var callbackError = new InvalidOperationException("Item 4444 can't be written.");
		var expectedCallback = Write(w =>
		{
			foreach (var parcel in parcels)
			{
				WriteItem(w, parcel);
			}
		});
		var callback = Write(w => w.ParallelWrite(parcels, WriteItem, 3), new FakeChunkOptions { RedoEvery = redoEvery });
		Assert.Same(callbackError, expectedCallback.Error);
		Assert.Same(callbackError, callback.Error);
		AssertWrites(expectedCallback, callback, "callback");
		AssertCall(callback, 3, "callback");

		void WriteItem(CsvWriter writer, Parcel parcel)
		{
			if (parcel.Id == 4_444)
			{
				throw callbackError;
			}

			writer.WriteRecord(parcel);
		}
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task ASinksOwnErrorInAFlaggedChunkIsTheSerialWritesError(bool flags)
	{
		var parcels = Parcels(12_000, 7);
		var ownerError = new FormatException("The owner can't write n7777.");
		var workerError = new FormatException("A worker can't write n7777.");
		var expected = Write(w => w.WriteRecords(parcels), new FakeChunkOptions { OwnerFailsOn = "n7777", OwnerError = ownerError });
		Assert.Same(ownerError, expected.Error);
		Assert.Equal(7_778, expected.Records);
		foreach (var degree in Degrees)
		{
			foreach (var readyAfter in new[] { 0L, 10_000L })
			{
				var options = new FakeChunkOptions
				{
					ReadyAfter = readyAfter,
					OwnerFailsOn = "n7777",
					OwnerError = ownerError,
					WorkerFailsOn = "n7777",
					WorkerError = workerError,
					FlagsWorkerFailure = flags,
					OneRecordPerItem = true,
				};
				var context = $"flags {flags}, degree {degree}, ready after {readyAfter}";
				var sync = Write(w => w.ParallelWriteRecords(parcels, degree), options);
				var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, degree), options);
				foreach (var actual in new[] { sync, asyncResult })
				{
					// A flagged chunk is written again by the owner, which throws its own error; an unflagged one is appended
					// up to the record that failed, and the worker's error is thrown. Before the workers, the owner's.
					Assert.Same(flags || readyAfter > 7_778 ? ownerError : workerError, actual.Error);
					AssertWrites(expected, actual, context, anyMessage: true);
					AssertCall(actual, degree, context);
					Assert.Equal(flags && readyAfter == 0, actual.Sink.Refused.Count > 0);
				}
			}
		}
	}

	[Fact]
	public async Task AWorkersErrorInARefusedChunkIsDroppedWhenTheOwnerWritesIt()
	{
		var parcels = Parcels(12_000, 14);
		var expected = Write(w => w.WriteRecords(parcels));
		foreach (var degree in Degrees)
		{
			// Only the workers fail on the record: the owner writes it, and the call goes on to the end.
			var options = new FakeChunkOptions
			{
				WorkerFailsOn = "n7777",
				WorkerError = new FormatException("A worker can't write n7777."),
				FlagsWorkerFailure = true,
				OneRecordPerItem = true,
			};
			var context = $"degree {degree}";
			var sync = Write(w => w.ParallelWriteRecords(parcels, degree), options);
			AssertWrites(expected, sync, context);
			AssertCall(sync, degree, context);
			Assert.Single(sync.Sink.Refused);
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options);
			AssertWrites(expected, asyncResult, context + ", async");
			AssertCall(asyncResult, degree, context + ", async");
			Assert.Single(asyncResult.Sink.Refused);
		}
	}

	[Fact]
	public async Task AFailingWriteChunkEndsTheCall()
	{
		var parcels = Parcels(12_000, 8);
		var full = Write(w => w.WriteRecords(parcels)).Output;
		foreach (var degree in Degrees)
		{
			var error = new IOException("The disk is full.");
			var options = new FakeChunkOptions { FailsWriteChunk = 3, WriteChunkError = error, OneRecordPerItem = true };
			var context = $"degree {degree}";
			var sync = Write(w => w.ParallelWriteRecords(parcels, degree), options);
			Assert.Same(error, sync.Error);
			AssertStopped(full, sync, context);
			AssertCall(sync, degree, context);
			Assert.Equal(3, sync.Sink.Count("WriteChunk"));
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(parcels), degree), options);
			Assert.Same(error, asyncResult.Error);
			AssertStopped(full, asyncResult, context + ", async");
			AssertCall(asyncResult, degree, context + ", async");
			Assert.Equal(3, asyncResult.Sink.Count("WriteChunkAsync"));
		}
	}

	[Fact]
	public async Task CanBeCanceled()
	{
		var parcels = Parcels(12_000, 9);
		var full = Write(w => w.WriteRecords(parcels)).Output;
		foreach (var degree in Degrees)
		{
			foreach (var asyncItems in new[] { false, true })
			{
				using var cancel = new CancellationTokenSource();
				var options = new FakeChunkOptions
				{
					OnWriteChunk = n =>
					{
						if (n == 3)
						{
							cancel.Cancel();
						}
					},
					OneRecordPerItem = true,
				};
				var context = $"degree {degree}, async enumerable {asyncItems}";
				var actual = await WriteAsync(w => asyncItems
					? w.ParallelWriteRecordsAsync(Yield(parcels), degree, cancel.Token)
					: w.ParallelWriteRecordsAsync(parcels, degree, cancel.Token), options);
				Assert.IsAssignableFrom<OperationCanceledException>(actual.Error);
				AssertStopped(full, actual, context);
				AssertCall(actual, degree, context);
				Assert.Equal(3, actual.Sink.Count("WriteChunkAsync"));
			}
		}

		// Canceled before the call.
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		var early = await WriteAsync(w => w.ParallelWriteRecordsAsync(parcels, 3, canceled.Token), new FakeChunkOptions());
		Assert.IsAssignableFrom<OperationCanceledException>(early.Error);
		AssertStopped(full, early, "canceled before");
		AssertCall(early, 3, "canceled before");
	}

	[Fact]
	public void TheLengthAChunkReportsSizesTheBatches()
	{
		var parcels = Parcels(20_000, 10);
		var expected = Write(w => w.WriteRecords(parcels));

		// Huge: one item a batch, once the first batches are written.
		var huge = Write(w => w.ParallelWriteRecords(parcels, 3), new FakeChunkOptions { Length = int.MaxValue, OneRecordPerItem = true });
		AssertWrites(expected, huge, "huge");
		AssertCall(huge, 3, "huge");
		Assert.Contains(huge.Sink.Chunks, c => c.Records == 1);
		Assert.Equal(64, huge.Sink.Chunks.Max(c => c.Records));

		// 1 byte a batch: the largest batches.
		var tiny = Write(w => w.ParallelWriteRecords(parcels, 3), new FakeChunkOptions { Length = 1, OneRecordPerItem = true });
		AssertWrites(expected, tiny, "tiny");
		AssertCall(tiny, 3, "tiny");
		Assert.Equal(4_096, tiny.Sink.Chunks.Max(c => c.Records));
	}

	[Fact]
	public async Task EveryParallelCallBeginsAndEndsOnce()
	{
		var parcels = Parcels(3_000, 11);
		var recordWriter = new ParcelWeightWriter();
		var calls = new (string Name, bool Async, Func<CsvWriter, IEnumerable<Parcel>, ValueTask> Write)[]
		{
			("records", false, (w, items) => Sync(() => w.ParallelWriteRecords(items, 3))),
			("record writer", false, (w, items) => Sync(() => w.ParallelWriteRecords(items, recordWriter, 3))),
			("callback", false, (w, items) => Sync(() => w.ParallelWrite(items, (x, p) => x.WriteRecord(p), 3))),
			("records, async", true, (w, items) => w.ParallelWriteRecordsAsync(items, 3)),
			("records, async enumerable", true, (w, items) => w.ParallelWriteRecordsAsync(Yield(items), 3)),
			("record writer, async", true, (w, items) => w.ParallelWriteRecordsAsync(items, recordWriter, 3)),
			("record writer, async enumerable", true, (w, items) => w.ParallelWriteRecordsAsync(Yield(items), recordWriter, 3)),
			("callback, async", true, (w, items) => w.ParallelWriteAsync(items, (x, p) => x.WriteRecord(p), 3)),
			("callback, async enumerable", true, (w, items) => w.ParallelWriteAsync(Yield(items), (x, p) => x.WriteRecord(p), 3)),
		};
		foreach (var (name, async, write) in calls)
		{
			var full = await Run(async, w => write(w, parcels));
			Assert.Null(full.Error);
			AssertCall(full, 3, name);
			Assert.Equal(1, full.Sink.Count("BeginWorkers"));

			var empty = await Run(async, w => write(w, []));
			Assert.Null(empty.Error);
			AssertCall(empty, 3, name + ", empty");
			Assert.Equal(0, empty.Sink.Count("BeginWorkers"));

			var failing = await Run(async, w => write(w, Failing(parcels, 2_000)));
			Assert.IsType<InvalidDataException>(failing.Error);
			AssertCall(failing, 3, name + ", failing source");

			// Two calls on one writer: a pair each.
			var twice = await WriteAsync(async w =>
			{
				await write(w, parcels);
				await write(w, parcels);
			});
			Assert.Null(twice.Error);
			AssertAccounted(twice.Sink, name + ", twice");
			Assert.Equal(["BeginParallel", "EndParallel", "BeginParallel", "EndParallel"], twice.Sink.Calls.Select(c => c.Name).Where(n => n.EndsWith("Parallel", StringComparison.Ordinal)));
		}

		// Dynamic records, whose first record is written before the workers.
		var rows = Enumerable.Range(0, 3_000).Select(i => new Dictionary<string, object?> { ["Id"] = i }).ToList();
		var dynamic = Write(w => w.ParallelWriteRecords(rows, 3));
		AssertCall(dynamic, 3, "dynamic");
		var dynamicAsync = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(rows), 3));
		AssertCall(dynamicAsync, 3, "dynamic, async");
		var dynamicEmpty = Write(w => w.ParallelWriteRecords(new List<Dictionary<string, object?>>(), 3));
		AssertCall(dynamicEmpty, 3, "dynamic, empty");
		Assert.Empty(dynamicEmpty.Output);

		static ValueTask Sync(Action write)
		{
			write();

			return default;
		}

		// A sync entry runs through Write, on this thread, so that its threads are checked.
		static async Task<Result> Run(bool async, Func<CsvWriter, ValueTask> write) =>
			async ? await WriteAsync(write) : Write(w => write(w).GetAwaiter().GetResult());
	}

	[Fact]
	public async Task AnExceptionFromTheItemsIsThrownAfterTheRecordsBeforeIt()
	{
		var parcels = Parcels(12_000, 12);
		var expected = Write(w => w.WriteRecords(Failing(parcels, 9_000)));
		Assert.IsType<InvalidDataException>(expected.Error);
		foreach (var degree in Degrees)
		{
			var options = new FakeChunkOptions { OneRecordPerItem = true };
			var sync = Write(w => w.ParallelWriteRecords(Failing(parcels, 9_000), degree), options);
			AssertWrites(expected, sync, $"degree {degree}");
			AssertCall(sync, degree, $"degree {degree}");
			var asyncResult = await WriteAsync(w => w.ParallelWriteRecordsAsync(Yield(Failing(parcels, 9_000)), degree), options);
			AssertWrites(expected, asyncResult, $"degree {degree}, async");
			AssertCall(asyncResult, degree, $"degree {degree}, async");
		}
	}

	[Fact]
	public async Task ASinkThatTakesNoParallelWritesThrows()
	{
		var sink = new FakeChunkSink(new FakeChunkOptions { Supports = false });
		using (var writer = new CsvWriter(sink, null))
		{
			Assert.Throws<NotSupportedException>(() => writer.ParallelWriteRecords(Parcels(1_000, 13)));
			Assert.Throws<NotSupportedException>(() => writer.ParallelWrite(Parcels(1_000, 13), (w, p) => w.WriteRecord(p)));
			await Assert.ThrowsAsync<NotSupportedException>(async () => await writer.ParallelWriteRecordsAsync(Yield(Parcels(1_000, 13))));
			await Assert.ThrowsAsync<NotSupportedException>(async () => await writer.ParallelWriteAsync(Parcels(1_000, 13), (w, p) => w.WriteRecord(p)));
		}

		Assert.Empty(sink.Problems);
		// Disposing the writer flushes the sink and completes it.
		Assert.Equal(["Flush", "Complete"], sink.Calls.Select(c => c.Name));
		Assert.Empty(sink.Output);
	}

	/// <summary>What a write through a <see cref="FakeChunkSink"/> gave.</summary>
	/// <param name="Output">The sink's output.</param>
	/// <param name="Records">The writer's record count after the write.</param>
	/// <param name="Error">What the write threw, or <see langword="null"/>.</param>
	/// <param name="Sink">The sink.</param>
	/// <param name="Thread">The thread a sync write ran on; <see langword="null"/> for an async one.</param>
	private sealed record Result(byte[] Output, long Records, Exception? Error, FakeChunkSink Sink, int? Thread);

	/// <summary>Writes with a writer of a new <see cref="FakeChunkSink"/>, catching an exception, and disposes the writer.</summary>
	/// <param name="write">The write.</param>
	/// <param name="options">The sink's switches, or <see langword="null"/> for none.</param>
	/// <param name="csv">The writer's options, or <see langword="null"/> for the defaults.</param>
	/// <returns>What the write gave.</returns>
	private static Result Write(Action<CsvWriter> write, FakeChunkOptions? options = null, CsvWriterOptions? csv = null)
	{
		var thread = Environment.CurrentManagedThreadId;
		var sink = new FakeChunkSink(options);
		Exception? error = null;
		long records;
		using (var writer = new CsvWriter(sink, csv))
		{
			try
			{
				write(writer);
			}
			catch (Exception ex)
			{
				error = ex;
			}

			records = writer.RecordCount;
		}

		return new Result(sink.Output, records, error, sink, thread);
	}

	/// <summary>Writes as <see cref="Write"/> does, asynchronously.</summary>
	/// <param name="write">The write.</param>
	/// <param name="options">The sink's switches, or <see langword="null"/> for none.</param>
	/// <param name="csv">The writer's options, or <see langword="null"/> for the defaults.</param>
	/// <returns>What the write gave.</returns>
	private static async Task<Result> WriteAsync(Func<CsvWriter, ValueTask> write, FakeChunkOptions? options = null, CsvWriterOptions? csv = null)
	{
		var sink = new FakeChunkSink(options);
		Exception? error = null;
		long records;
		await using (var writer = new CsvWriter(sink, csv))
		{
			try
			{
				await write(writer);
			}
			catch (Exception ex)
			{
				error = ex;
			}

			records = writer.RecordCount;
		}

		return new Result(sink.Output, records, error, sink, null);
	}

	/// <summary>
	/// Asserts that <paramref name="actual"/> wrote what <paramref name="expected"/> did, byte for byte, with the same record
	/// count and an exception of the same type (and message, unless <paramref name="anyMessage"/>), and that its sink is
	/// accounted for.
	/// </summary>
	/// <param name="expected">The serial write.</param>
	/// <param name="actual">The parallel write.</param>
	/// <param name="context">What is written, for the messages.</param>
	/// <param name="anyMessage">Whether the exception's message may differ: the caller checks which exception it is.</param>
	private static void AssertWrites(Result expected, Result actual, string context, bool anyMessage = false)
	{
		AssertSame(expected.Output, actual.Output, context);
		Assert.True(expected.Records == actual.Records, $"{context}: {actual.Records} records, not {expected.Records}.");
		Assert.True(actual.Records == actual.Sink.Records, $"{context}: the writer counted {actual.Records} records, the sink {actual.Sink.Records}.");
		Assert.True(expected.Error?.GetType() == actual.Error?.GetType(), $"{context}: threw {actual.Error}, not {expected.Error}.");
		if (!anyMessage)
		{
			Assert.Equal(expected.Error?.Message, actual.Error?.Message);
		}

		AssertAccounted(actual.Sink, context);
	}

	/// <summary>
	/// Asserts that a write that stopped (a failing WriteChunk, a cancellation) wrote whole records of the serial output's
	/// first ones, counted them, and accounted for its sink.
	/// </summary>
	/// <param name="full">The serial output of every record.</param>
	/// <param name="actual">The write.</param>
	/// <param name="context">What is written, for the messages.</param>
	private static void AssertStopped(byte[] full, Result actual, string context)
	{
		var output = actual.Output;
		Assert.True(output.Length < full.Length, $"{context}: the whole output was written.");
		AssertSame(full.Take(output.Length).ToArray(), output, context);
		Assert.True(output.Length == 0 || output[output.Length - 1] == RecordEnd, $"{context}: the output ends inside a record.");
		Assert.Equal(output.Count(b => b == RecordEnd), actual.Records);
		Assert.Equal(actual.Sink.Records, actual.Records);
		AssertAccounted(actual.Sink, context);
	}

	/// <summary>
	/// Asserts that the sink found nothing wrong, every chunk the workers handed over came back once (the sink checks
	/// that none came back twice), every parallel call ended, and every worker's sink was released once: maybe after the
	/// call returned, so this waits for that.
	/// </summary>
	/// <param name="sink">The sink.</param>
	/// <param name="context">What was written, for the messages.</param>
	private static void AssertAccounted(FakeChunkSink sink, string context)
	{
		// A worker that leaves after the call returned releases its sink within moments; 10 s allows for a loaded machine.
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (sink.WorkersReleased < sink.WorkersCreated && DateTime.UtcNow < deadline)
		{
			Thread.Sleep(1);
		}

		Assert.True(sink.Problems.Count == 0, $"{context}: {string.Join(" ", sink.Problems)}");
		Assert.True(sink.ChunksTaken == sink.ChunksHandled, $"{context}: {sink.ChunksTaken} chunks taken, {sink.ChunksHandled} came back.");
		Assert.True(sink.WorkersCreated == sink.WorkersReleased, $"{context}: {sink.WorkersCreated} workers made, {sink.WorkersReleased} released.");
		Assert.False(sink.InParallel, context);
		Assert.Equal(sink.Count("BeginParallel"), sink.Count("EndParallel"));
	}

	/// <summary>
	/// Asserts the order and threads of one parallel call's members: <c>BeginParallel</c> first, with the degree (-1 is a
	/// worker a processor), and <c>EndParallel</c> last, but for workers released later; <c>BeginWorkers</c> at most once,
	/// before any worker is made and any chunk comes back; and, for a sync call, the owner's calls on the calling thread
	/// and the workers made on others.
	/// </summary>
	/// <param name="actual">The write, which made one parallel call.</param>
	/// <param name="degree">The degree the call was made with.</param>
	/// <param name="context">What was written, for the messages.</param>
	private static void AssertCall(Result actual, int degree, string context)
	{
		var sink = actual.Sink;
		AssertAccounted(sink, context);
		var all = sink.Calls;
		var calls = all.Where(c => c.Name is not ("Flush" or "FlushAsync" or "Complete" or "CompleteAsync" or "ReleaseWorker")).ToList();
		Assert.True(calls.Count >= 2, context);
		Assert.Equal("BeginParallel", calls[0].Name);
		Assert.Equal(degree == -1 ? Environment.ProcessorCount : degree, calls[0].Value);
		Assert.Equal("EndParallel", calls[calls.Count - 1].Name);
		Assert.Equal(1, calls.Count(c => c.Name == "BeginParallel"));
		Assert.Equal(1, calls.Count(c => c.Name == "EndParallel"));
		var workersBegin = calls.FindIndex(c => c.Name == "BeginWorkers");
		Assert.True(calls.Count(c => c.Name == "BeginWorkers") <= 1, context);
		for (var i = 0; i < calls.Count; i++)
		{
			if (calls[i].Name is "CreateWorker" or "WriteChunk" or "WriteChunkAsync" or "DiscardChunk")
			{
				Assert.True(workersBegin >= 0 && workersBegin < i, $"{context}: {calls[i].Name} before BeginWorkers.");
			}
		}

		if (actual.Thread is not { } thread)
		{
			return;
		}

		foreach (var call in all)
		{
			if (call.Name == "CreateWorker")
			{
				Assert.True(call.Thread != thread, $"{context}: a worker made on the calling thread.");
			}
			else if (call.Name != "ReleaseWorker")
			{
				Assert.True(call.Thread == thread, $"{context}: {call.Name} off the calling thread.");
			}
		}

		Assert.All(sink.ReadyThreads, t => Assert.Equal(thread, t));
	}

	/// <summary>Asserts that two outputs are the same bytes, naming the first that differs.</summary>
	/// <param name="expected">The expected output.</param>
	/// <param name="actual">The output.</param>
	/// <param name="context">What was written, for the message.</param>
	private static void AssertSame(byte[] expected, byte[] actual, string context)
	{
		var length = Math.Min(expected.Length, actual.Length);
		var at = 0;
		while (at < length && expected[at] == actual[at])
		{
			at++;
		}

		Assert.True(at == length && expected.Length == actual.Length,
			$"{context}: the output differs at byte {at} of {expected.Length} (got {actual.Length} bytes).");
	}

	/// <summary>The items before <paramref name="at"/>, then an <see cref="InvalidDataException"/>: a source that fails.</summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The items.</param>
	/// <param name="at">The index at which the enumeration throws.</param>
	/// <returns>The items, enumerated lazily.</returns>
	private static IEnumerable<T> Failing<T>(IEnumerable<T> items, int at)
	{
		var i = 0;
		foreach (var item in items)
		{
			if (i++ == at)
			{
				throw new InvalidDataException("The source failed.");
			}

			yield return item;
		}
	}

	/// <summary>The items as an asynchronous sequence that yields every 1,000 items and honours its token.</summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="items">The items.</param>
	/// <param name="cancellationToken">Cancels the enumeration.</param>
	/// <returns>The items.</returns>
	private static async IAsyncEnumerable<T> Yield<T>(IEnumerable<T> items, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var i = 0;
		foreach (var item in items)
		{
			if (i++ % 1000 == 0)
			{
				await Task.Yield();
			}

			cancellationToken.ThrowIfCancellationRequested();

			yield return item;
		}
	}

	/// <summary>A record writer of three of a parcel's fields: its id, name and weight.</summary>
	private sealed class ParcelWeightWriter : CsvRecordWriter<Parcel>
	{
		/// <inheritdoc/>
		public override int FieldCount => 3;

		/// <inheritdoc/>
		public override void WriteHeader(CsvWriter writer)
		{
			WriteColumnName(writer, "Id");
			WriteColumnName(writer, "Name");
			WriteColumnName(writer, "Weight");
		}

		/// <inheritdoc/>
		public override void Write(CsvWriter writer, Parcel record)
		{
			writer.WriteField(record.Id);
			writer.WriteField(record.Name);
			writer.WriteField(record.Weight);
		}
	}

	/// <summary>A record writer as <see cref="ParcelWeightWriter"/> that throws a given exception after the first field of one parcel.</summary>
	/// <param name="failAt">The id of the parcel that fails.</param>
	/// <param name="error">What it throws.</param>
	private sealed class FailingParcelWriter(int failAt, Exception error) : CsvRecordWriter<Parcel>
	{
		/// <inheritdoc/>
		public override int FieldCount => 3;

		/// <inheritdoc/>
		public override void WriteHeader(CsvWriter writer)
		{
			WriteColumnName(writer, "Id");
			WriteColumnName(writer, "Name");
			WriteColumnName(writer, "Weight");
		}

		/// <inheritdoc/>
		public override void Write(CsvWriter writer, Parcel record)
		{
			writer.WriteField(record.Id);
			if (record.Id == failAt)
			{
				throw error;
			}

			writer.WriteField(record.Name);
			writer.WriteField(record.Weight);
		}
	}
}
