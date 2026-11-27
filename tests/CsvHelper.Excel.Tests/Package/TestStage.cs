namespace CsvHelper.Excel.Tests.Package;

/// <summary>How a <see cref="TestStage"/> hands its chunks out.</summary>
internal enum TestStageMode
{
	/// <summary>As they are cut, as the threadless stage does: every chunk ready, and no wait.</summary>
	Ready,

	/// <summary>
	/// Only through a wait for each, never ready: <see cref="TestStage.WaitAt"/> chunks cut and not taken make the entry
	/// wait (<see cref="DeflateStage.MustWait"/>), and a helper thread ends each wait a moment after it starts, so that a
	/// synchronous wait blocks for real and an asynchronous one completes asynchronously.
	/// </summary>
	Waits,

	/// <summary>As <see cref="Waits"/>, but a wait ends only when the test opens the stage (<see cref="TestStage.Open"/>) or the wait is canceled.</summary>
	Held,
}

/// <summary>
/// A deflate stage for the entry's tests (<see cref="ZipEntryStream.StageFactory"/>): the threadless stage's chunks
/// (<see cref="InlineDeflateStage"/>), so with the bytes a chunked entry has, handed out as its mode says
/// (<see cref="TestStageMode"/>); with a fault at the nth call of a kind, out of order, or with their input miscounted. It
/// counts its takes in each mode, its waits and its disposals.
/// </summary>
internal sealed class TestStage : DeflateStage
{
	/// <summary>The threadless stage, which cuts and deflates the chunks.</summary>
	private readonly InlineDeflateStage inner;

	/// <summary>The writer, whose pool the segments return to.</summary>
	private readonly ZipWriter writer;

	/// <summary>The chunks cut and not taken, in order.</summary>
	private readonly Queue<DeflatedChunk> pending = new();

	/// <summary>Ends a wait: released by a helper thread, by <see cref="Open"/>, or never.</summary>
	private readonly SemaphoreSlim gate = new(0);

	/// <summary>The calls of each kind so far, for <see cref="FailAt"/>.</summary>
	private readonly Dictionary<string, int> calls = [];

	/// <summary>The chunks cut so far.</summary>
	private int cut;

	/// <summary>Creates a stage for an entry of a writer.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="threads">The threads the entry asked for.</param>
	public TestStage(ZipWriter writer, int threads)
	{
		this.writer = writer;
		inner = new InlineDeflateStage(writer, threads);
		AskedThreads = threads;
	}

	/// <summary>How the chunks are handed out; <see cref="Open"/> sets it to <see cref="TestStageMode.Ready"/>.</summary>
	public TestStageMode Mode { get; set; }

	/// <summary>
	/// The kind of call that throws <see cref="Failure"/>: <c>append</c> (a copy), <c>append segment</c> (which returns the
	/// segment first, as a stage takes it whatever happens), <c>try take</c>, <c>take</c> (either mode), <c>complete</c>,
	/// <c>raise</c> or <c>dispose</c> (after it has released everything); <see langword="null"/> for none.
	/// </summary>
	public string? FailAt { get; init; }

	/// <summary>The call of <see cref="FailAt"/>'s kind that throws, from 1.</summary>
	public int FailAfter { get; init; } = 1;

	/// <summary>The exception the stage throws, as a stage rethrows its thread's.</summary>
	public InvalidOperationException Failure { get; } = new("The test stage failed.");

	/// <summary>The chunks cut and not taken that make the entry wait, unless the stage is ready: 2 by default.</summary>
	public int WaitAt { get; init; } = 2;

	/// <summary>Whether the first chunk is given the second's place, as a stage that hands chunks out of order would.</summary>
	public bool OutOfOrder { get; init; }

	/// <summary>Whether each chunk reports a byte of input more than it took.</summary>
	public bool Overcounts { get; init; }

	/// <summary>Called on the writing thread as each wait starts.</summary>
	public Action? Waiting { get; set; }

	/// <summary>The threads the entry made the stage with.</summary>
	public int AskedThreads { get; }

	/// <summary>The most threads it was raised to (<see cref="RaiseThreads"/>), or 0.</summary>
	public int RaisedThreads { get; private set; }

	/// <summary>The synchronous takes that had a chunk.</summary>
	public int SyncTakes { get; private set; }

	/// <summary>The asynchronous takes that had a chunk.</summary>
	public int AsyncTakes { get; private set; }

	/// <summary>The waits started.</summary>
	public int Waits { get; private set; }

	/// <summary>The times it was disposed.</summary>
	public int Disposals { get; private set; }

	/// <inheritdoc/>
	public override int Threads => inner.Threads;

	/// <summary>Never when ready; else <see cref="WaitAt"/> chunks cut and not taken.</summary>
	public override bool MustWait => Mode != TestStageMode.Ready && pending.Count >= WaitAt;

	/// <summary>Ends the wait under way, if any, and from then on hands every chunk out as it is cut.</summary>
	public void Open()
	{
		Mode = TestStageMode.Ready;
		gate.Release();
	}

	/// <inheritdoc/>
	public override void RaiseThreads(int threads)
	{
		Check("raise");
		RaisedThreads = Math.Max(RaisedThreads, threads);
		inner.RaiseThreads(threads);
	}

	/// <inheritdoc/>
	public override void Append(OwnedSegment segment)
	{
		if (Fails("append segment"))
		{
			writer.Return(segment);

			throw Failure;
		}

		inner.Append(segment);
		Collect();
	}

	/// <inheritdoc/>
	public override void Append(ReadOnlySpan<byte> bytes)
	{
		Check("append");
		inner.Append(bytes);
		Collect();
	}

	/// <inheritdoc/>
	public override void Complete()
	{
		Check("complete");
		inner.Complete();
		Collect();
	}

	/// <inheritdoc/>
	public override bool TryTake(out DeflatedChunk chunk)
	{
		Check("try take");
		if (Mode == TestStageMode.Ready && pending.Count > 0)
		{
			chunk = pending.Dequeue();

			return true;
		}

		chunk = null!;

		return false;
	}

	/// <inheritdoc/>
	public override DeflatedChunk? Take()
	{
		Check("take");
		if (pending.Count == 0)
		{
			return null;
		}

		SyncTakes++;
		if (Mode != TestStageMode.Ready)
		{
			StartWait();
			gate.Wait();
		}

		return pending.Dequeue();
	}

	/// <inheritdoc/>
	public override async ValueTask<DeflatedChunk?> TakeAsync(CancellationToken cancellationToken)
	{
		Check("take");
		if (pending.Count == 0)
		{
			return null;
		}

		AsyncTakes++;
		if (Mode != TestStageMode.Ready)
		{
			StartWait();
			await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		return pending.Dequeue();
	}

	/// <inheritdoc/>
	public override void Return(DeflatedChunk chunk) => inner.Return(chunk);

	/// <summary>Releases what the stage holds, every time it is called (counted), and then throws if it is to.</summary>
	public override void Dispose()
	{
		Disposals++;
		inner.Dispose();
		while (pending.Count > 0)
		{
			pending.Dequeue().Clear();
		}

		Check("dispose");
	}

	/// <summary>Moves the chunks the threadless stage has cut to <see cref="pending"/>, miscounted or reordered as set.</summary>
	private void Collect()
	{
		while (inner.TryTake(out var chunk))
		{
			if (OutOfOrder && cut == 0)
			{
				chunk.Sequence = 1;
			}

			if (Overcounts)
			{
				chunk.InputLength++;
			}

			cut++;
			pending.Enqueue(chunk);
		}
	}

	/// <summary>Starts a wait: tells the test, and in <see cref="TestStageMode.Waits"/> starts a thread that ends it.</summary>
	private void StartWait()
	{
		Waits++;
		Waiting?.Invoke();
		if (Mode == TestStageMode.Waits)
		{
			new Thread(() => gate.Release()) { IsBackground = true }.Start();
		}
	}

	/// <summary>Throws <see cref="Failure"/> if this call is the one to fail.</summary>
	/// <param name="kind">The call's kind.</param>
	private void Check(string kind)
	{
		if (Fails(kind))
		{
			throw Failure;
		}
	}

	/// <summary>Counts a call, and says whether it is the one to fail.</summary>
	/// <param name="kind">The call's kind.</param>
	/// <returns>Whether it fails.</returns>
	private bool Fails(string kind)
	{
		if (FailAt != kind)
		{
			return false;
		}

		calls[kind] = calls.TryGetValue(kind, out var count) ? count + 1 : 1;

		return calls[kind] == FailAfter;
	}
}
