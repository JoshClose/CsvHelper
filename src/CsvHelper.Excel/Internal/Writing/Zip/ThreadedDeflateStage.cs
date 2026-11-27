using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// A deflate stage whose chunks are deflated on dedicated background threads of its own (<see cref="DeflateStage"/>): the
/// writing thread cuts the entry's data into jobs (<see cref="DeflateJob"/>) and hands them over in order, the stage's
/// threads take them first come first served, each with a worker of its own (its deflater and buffers, made on the thread
/// with its first job and ended as the thread ends), and the writing thread takes their chunks back in the order they were
/// cut. The stages built on it differ in how the data becomes jobs and how a job is deflated: <c>ChunkedDeflateStage</c>
/// on net8.0 and net10.0, and <see cref="SerialDeflateStage"/>. Not generic over the worker, so that code holding a
/// <see cref="DeflateStage"/> reaches the test seams (<see cref="BeforeDeflate"/>, <see cref="StartedThreads"/>) with one
/// cast.
/// </summary>
/// <remarks>
/// <para>
/// A thread starts as a job is handed over while the jobs waiting outnumber the threads free to take them (those waiting
/// for a job, and those started that haven't taken one yet), up to <see cref="Threads"/>; <see cref="RaiseThreads"/> starts
/// more at once when jobs wait. Threads end only as the stage is disposed or faults.
/// </para>
/// <para>
/// Three locks, never one inside another: the work lock, under which the writing thread hands a job over and the threads
/// wait for one; the done lock, under which a thread publishes the chunk it deflated and the writing thread waits for the
/// next chunk in order (<see cref="Take"/> blocks on it for real; <see cref="TakeAsync"/> awaits a task that a thread
/// completes, whose continuation never runs on that thread); and the lock of the chunks to reuse. A thread waits only for
/// a job, and the writing thread only for a chunk a thread has taken or will take (one is started, or busy, whenever jobs
/// wait), so the chain can't stall. A thread publishes its chunk, or its exception, before it takes another job, and wakes
/// every waiter as it ends, so a thread that fails can't leave the writing thread waiting.
/// </para>
/// <para>
/// A thread's exception is held (the first one), every thread then stops taking jobs, and the writing thread's next
/// <see cref="DeflateStage.Append(OwnedSegment)"/>, <see cref="DeflateStage.Append(ReadOnlySpan{byte})"/>,
/// <see cref="DeflateStage.Complete"/>, <see cref="TryTake"/>, <see cref="Take"/> or <see cref="TakeAsync"/> throws it, as
/// each one after does. <see cref="Dispose"/> may run on another thread, while the writing thread is in no call or waits
/// in <see cref="Take"/> or <see cref="TakeAsync"/>, which then return <see langword="null"/>.
/// </para>
/// </remarks>
internal abstract class ThreadedDeflateStage : DeflateStage
{
	/// <summary>The lock the threads wait on for a job, which guards <see cref="waiting"/>, <see cref="started"/>, <see cref="idle"/> and <see cref="starting"/>.</summary>
	private readonly object workLock = new();

	/// <summary>The lock the writing thread waits on for a chunk, which guards <see cref="cut"/>, each job's chunk, <see cref="waitingFor"/> and <see cref="waiter"/>.</summary>
	private readonly object doneLock = new();

	/// <summary>The lock of <see cref="spareChunks"/>.</summary>
	private readonly object spareLock = new();

	/// <summary>The jobs handed over and not yet taken by a thread, in order.</summary>
	private readonly Queue<DeflateJob> waiting = new();

	/// <summary>The threads started, in order.</summary>
	private readonly List<Thread> started = [];

	/// <summary>The jobs cut and their chunks not yet taken, in order: those deflating or waiting for a thread, and those done.</summary>
	private readonly Queue<DeflateJob> cut = new();

	/// <summary>The jobs to reuse, the writing thread's.</summary>
	private readonly Stack<DeflateJob> spareJobs = new();

	/// <summary>The chunks to reuse, emptied.</summary>
	private readonly Stack<DeflatedChunk> spareChunks = new();

	/// <summary>The most threads this kind of stage can have.</summary>
	private readonly int mostThreads;

	/// <summary>The most threads the stage starts (<see cref="Threads"/>), raised by <see cref="RaiseThreads"/>.</summary>
	private int threads;

	/// <summary>The threads waiting for a job.</summary>
	private int idle;

	/// <summary>The threads started that haven't asked for a job yet.</summary>
	private int starting;

	/// <summary>The chunks cut so far: the next one's sequence number.</summary>
	private long sequence;

	/// <summary>The sequence number of the chunk the writing thread waits for, or -1.</summary>
	private long waitingFor = -1;

	/// <summary>What an asynchronous take awaits, which the thread that publishes <see cref="waitingFor"/>'s chunk completes; <see langword="null"/> when none waits.</summary>
	private TaskCompletionSource<bool>? waiter;

	/// <summary>The first exception a thread threw, which the writing thread's next call throws; <see langword="null"/> while none has.</summary>
	private ExceptionDispatchInfo? fault;

	/// <summary>Whether the stage is being disposed: the threads take no more jobs, and the waits end.</summary>
	private volatile bool stopping;

	/// <summary>1 once <see cref="Dispose"/> has begun.</summary>
	private int disposed;

	/// <summary>Creates a stage, which starts no thread until a job is handed over.</summary>
	/// <param name="writer">The writer: its level, which isn't <see cref="CompressionLevel.NoCompression"/>, and its pool.</param>
	/// <param name="threads">The most threads to start: 1 or more (less is 1), at most <paramref name="mostThreads"/>.</param>
	/// <param name="mostThreads">The most threads this kind of stage can have, whatever it is asked.</param>
	/// <exception cref="ArgumentOutOfRangeException">The writer's level is <see cref="CompressionLevel.NoCompression"/>.</exception>
	protected ThreadedDeflateStage(ZipWriter writer, int threads, int mostThreads)
	{
		if (writer.Level == CompressionLevel.NoCompression)
		{
			throw new ArgumentOutOfRangeException(nameof(writer), writer.Level, "A stored entry isn't deflated.");
		}

		Writer = writer;
		this.mostThreads = mostThreads;
		this.threads = Math.Min(Math.Max(threads, 1), mostThreads);
	}

	/// <inheritdoc/>
	public override int Threads => threads;

	/// <inheritdoc/>
	public override bool MustWait => cut.Count >= DeflateSizes.ChunksPerThread * threads;

	/// <summary>
	/// Called on the thread that deflates a chunk, before it does, with the chunk's sequence number: a test's seam, to
	/// hold a chunk in flight or to fail it. <see langword="null"/> by default.
	/// </summary>
	internal Action<long>? BeforeDeflate { get; set; }

	/// <summary>Whether a thread has thrown, for tests.</summary>
	internal bool Faulted => Volatile.Read(ref fault) is not null;

	/// <summary>The threads waiting for a job, for tests.</summary>
	internal int IdleThreads
	{
		get
		{
			lock (workLock)
			{
				return idle;
			}
		}
	}

	/// <summary>The writer, whose pool the stage's buffers come from and whose segments it returns.</summary>
	protected ZipWriter Writer { get; }

	/// <summary>Whether <see cref="Dispose"/> has begun.</summary>
	protected bool IsDisposed => Volatile.Read(ref disposed) != 0;

	/// <summary>Whether a thread has been started.</summary>
	protected bool HasThreads
	{
		get
		{
			lock (workLock)
			{
				return started.Count > 0;
			}
		}
	}

	/// <inheritdoc/>
	public override void RaiseThreads(int threads)
	{
		var raised = Math.Min(threads, mostThreads);
		if (raised <= this.threads || IsDisposed)
		{
			return;
		}

		List<Thread>? more = null;
		lock (workLock)
		{
			this.threads = raised;
			while (NewThreadIfWanted() is { } thread)
			{
				(more ??= []).Add(thread);
			}
		}

		if (more is not null)
		{
			for (var i = 0; i < more.Count; i++)
			{
				try
				{
					Start(more[i]);
				}
				catch
				{
					// The threads after the one that failed never start: they are counted out too, so that Dispose joins
					// only threads that ran.
					lock (workLock)
					{
						for (var j = i + 1; j < more.Count; j++)
						{
							started.Remove(more[j]);
							starting--;
						}
					}

					throw;
				}
			}
		}
	}

	/// <inheritdoc/>
	public override bool TryTake(out DeflatedChunk chunk)
	{
		ThrowIfFaulted();
		lock (doneLock)
		{
			if (!stopping && cut.Count > 0 && cut.Peek().Done)
			{
				chunk = TakeHead();

				return true;
			}
		}

		chunk = null!;

		return false;
	}

	/// <inheritdoc/>
	public override DeflatedChunk? Take()
	{
		ThrowIfFaulted();
		lock (doneLock)
		{
			if (stopping || cut.Count == 0)
			{
				return null;
			}

			var head = cut.Peek();
			while (!head.Done && !stopping && Volatile.Read(ref fault) is null)
			{
				waitingFor = head.Sequence;
				Monitor.Wait(doneLock);
			}

			waitingFor = -1;
			ThrowIfFaulted();

			return head.Done && !stopping ? TakeHead() : null;
		}
	}

	/// <inheritdoc/>
	public override ValueTask<DeflatedChunk?> TakeAsync(CancellationToken cancellationToken)
	{
		ThrowIfFaulted();
		cancellationToken.ThrowIfCancellationRequested();
		DeflateJob head;
		lock (doneLock)
		{
			if (stopping || cut.Count == 0)
			{
				return new((DeflatedChunk?)null);
			}

			head = cut.Peek();
			if (head.Done)
			{
				return new(TakeHead());
			}
		}

		return TakeLaterAsync(head, cancellationToken);
	}

	/// <inheritdoc/>
	public override void Return(DeflatedChunk chunk)
	{
		chunk.Clear();
		lock (spareLock)
		{
			spareChunks.Push(chunk);
		}
	}

	/// <summary>
	/// Stops the stage: the threads take no more jobs, a wait for a chunk ends, every thread is joined (one deflating
	/// finishes its chunk first), and then the stage returns what it holds: the chunks deflated and not taken, the pieces of
	/// the jobs no thread took, and the data not yet cut (<see cref="ReleaseInput"/>); each thread returned its own buffers as
	/// it ended. Calling it again does nothing.
	/// </summary>
	public override void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
		{
			return;
		}

		Thread[] joined;
		lock (workLock)
		{
			stopping = true;
			Monitor.PulseAll(workLock);
			joined = [.. started];
		}

		lock (doneLock)
		{
			Monitor.PulseAll(doneLock);
			waiter?.TrySetResult(false);
		}

		foreach (var thread in joined)
		{
			// A stage thread can't join itself: one that disposes the stage (a test's seam) ends as it returns. A thread that
			// never started (its start failed) has nothing to join.
			if (thread != Thread.CurrentThread && (thread.ThreadState & ThreadState.Unstarted) == 0)
			{
				thread.Join();
			}
		}

		lock (doneLock)
		{
			while (cut.Count > 0)
			{
				var job = cut.Dequeue();
				job.Chunk?.Clear();
				job.Chunk = null;
				job.Release(Writer);
			}
		}

		lock (workLock)
		{
			waiting.Clear();
		}

		ReleaseInput();
	}

	/// <summary>The threads started so far, for tests that check that none is left alive.</summary>
	/// <returns>A copy of the list.</returns>
	internal Thread[] StartedThreads()
	{
		lock (workLock)
		{
			return [.. started];
		}
	}

	/// <summary>Throws the exception a thread threw, if one has.</summary>
	protected void ThrowIfFaulted() => Volatile.Read(ref fault)?.Throw();

	/// <summary>A job to fill, reused or new; the writing thread's.</summary>
	/// <returns>The job, empty.</returns>
	protected DeflateJob NewJob() => spareJobs.Count > 0 ? spareJobs.Pop() : new DeflateJob();

	/// <summary>
	/// Hands a job over, next in order: a thread free to take it is woken, or, when none is and fewer than
	/// <see cref="Threads"/> are started, one is started. It never waits.
	/// </summary>
	/// <param name="job">The job, filled: its pieces are the threads' from here.</param>
	protected void Hand(DeflateJob job)
	{
		job.Sequence = sequence++;
		lock (doneLock)
		{
			cut.Enqueue(job);
		}

		Thread? thread;
		lock (workLock)
		{
			waiting.Enqueue(job);
			if (idle > 0)
			{
				Monitor.Pulse(workLock);
			}

			thread = NewThreadIfWanted();
		}

		if (thread is not null)
		{
			Start(thread);
		}
	}

	/// <summary>
	/// Deflates a job on the writing thread, next in order, with a worker made for it: an entry's only chunk, which starts
	/// no thread. Its chunk is then taken as any other. A failure is thrown here, not held.
	/// </summary>
	/// <param name="job">The job, filled; its pieces are released here.</param>
	protected void DeflateHere(DeflateJob job)
	{
		job.Sequence = sequence++;
		var chunk = RentChunk();
		chunk.Sequence = job.Sequence;
		var worker = CreateWorker();
		try
		{
			BeforeDeflate?.Invoke(job.Sequence);
			Deflate(worker, job, chunk);
		}
		catch
		{
			Return(chunk);

			throw;
		}
		finally
		{
			job.Release(Writer);
			EndWorker(worker);
		}

		lock (doneLock)
		{
			job.Chunk = chunk;
			job.Done = true;
			cut.Enqueue(job);
		}
	}

	/// <summary>Makes a thread's worker, on the thread, with its first job (or on the writing thread, for <see cref="DeflateHere"/>).</summary>
	/// <returns>The worker.</returns>
	protected abstract object CreateWorker();

	/// <summary>
	/// Deflates a job into a chunk, on the thread whose worker it is: the chunk's output, CRC and input length. The job's
	/// pieces may be released once gathered (<see cref="DeflateJob.Release"/>); those left are released after it returns
	/// or throws.
	/// </summary>
	/// <param name="worker">The thread's worker.</param>
	/// <param name="job">The job.</param>
	/// <param name="chunk">The chunk, empty, its sequence number set.</param>
	protected abstract void Deflate(object worker, DeflateJob job, DeflatedChunk chunk);

	/// <summary>Ends a worker as its thread ends (or after <see cref="DeflateHere"/>): it returns its buffers and drops its deflater. It doesn't throw.</summary>
	/// <param name="worker">The worker.</param>
	protected abstract void EndWorker(object worker);

	/// <summary>Returns the data appended and not yet cut into a job, once every thread is joined (<see cref="Dispose"/>).</summary>
	protected abstract void ReleaseInput();

	/// <summary>Takes the first chunk cut, which is done, and reuses its job. Under the done lock.</summary>
	/// <returns>The chunk.</returns>
	private DeflatedChunk TakeHead()
	{
		var job = cut.Dequeue();
		var chunk = job.Chunk!;
		job.Reset();
		spareJobs.Push(job);

		return chunk;
	}

	/// <summary>
	/// Waits asynchronously for a chunk that isn't done yet, the first cut, and takes it: the thread that publishes it
	/// completes the wait, and so does a fault, disposal or <paramref name="cancellationToken"/>, which throws and leaves the
	/// chunk for the next call.
	/// </summary>
	/// <param name="head">The first job cut.</param>
	/// <param name="cancellationToken">Cancels the wait.</param>
	/// <returns>A task whose result is the chunk; <see langword="null"/> when the stage was disposed meanwhile.</returns>
	private async ValueTask<DeflatedChunk?> TakeLaterAsync(DeflateJob head, CancellationToken cancellationToken)
	{
		while (true)
		{
			TaskCompletionSource<bool> wait;
			lock (doneLock)
			{
				if (head.Done || stopping || Volatile.Read(ref fault) is not null)
				{
					waitingFor = -1;
					waiter = null;

					break;
				}

				// Its continuation never runs on the thread that completes it: a stage thread writes nothing to the stream.
				wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				waiter = wait;
				waitingFor = head.Sequence;
			}

			using (cancellationToken.Register(static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false), wait))
			{
				await wait.Task.ConfigureAwait(false);
			}

			if (cancellationToken.IsCancellationRequested)
			{
				lock (doneLock)
				{
					if (waiter == wait)
					{
						waiter = null;
						waitingFor = -1;
					}
				}

				cancellationToken.ThrowIfCancellationRequested();
			}
		}

		lock (doneLock)
		{
			ThrowIfFaulted();

			return head.Done && !stopping ? TakeHead() : null;
		}
	}

	/// <summary>A chunk to deflate into, reused or new.</summary>
	/// <returns>The chunk, empty.</returns>
	private DeflatedChunk RentChunk()
	{
		lock (spareLock)
		{
			if (spareChunks.Count > 0)
			{
				return spareChunks.Pop();
			}
		}

		return new DeflatedChunk(Writer);
	}

	/// <summary>
	/// A thread to start when the jobs waiting outnumber the threads free to take them and fewer than <see cref="Threads"/>
	/// are started, counted as started. Under the work lock.
	/// </summary>
	/// <returns>The thread, not yet started; <see langword="null"/> when none is wanted.</returns>
	private Thread? NewThreadIfWanted()
	{
		if (stopping || waiting.Count <= idle + starting || started.Count >= threads)
		{
			return null;
		}

		var thread = new Thread(Run) { IsBackground = true, Name = $"CsvHelper.Excel deflate {started.Count + 1}" };
		started.Add(thread);
		starting++;

		return thread;
	}

	/// <summary>Starts a thread counted as started; one that can't start is counted out again, and its exception thrown.</summary>
	/// <param name="thread">The thread.</param>
	private void Start(Thread thread)
	{
		try
		{
			thread.Start();
		}
		catch
		{
			lock (workLock)
			{
				started.Remove(thread);
				starting--;
			}

			throw;
		}
	}

	/// <summary>A stage thread: deflates the jobs it takes until the stage is disposed or a thread has thrown.</summary>
	private void Run()
	{
		var first = true;
		object? worker = null;
		try
		{
			while (TakeJob(ref first) is { } job)
			{
				RunJob(ref worker, job);
			}
		}
		catch (Exception e)
		{
			Fail(e);
		}
		finally
		{
			if (worker is not null)
			{
				EndWorker(worker);
			}

			// Whatever ended the thread, a wait for a chunk it would have deflated ends too.
			lock (doneLock)
			{
				Monitor.PulseAll(doneLock);
				waiter?.TrySetResult(false);
			}
		}
	}

	/// <summary>Waits for a job and takes it, the first handed over.</summary>
	/// <param name="first">Whether it is the thread's first ask, which counts it out of the threads starting; then <see langword="false"/>.</param>
	/// <returns>The job; <see langword="null"/> once the stage is disposed or a thread has thrown.</returns>
	private DeflateJob? TakeJob(ref bool first)
	{
		lock (workLock)
		{
			if (first)
			{
				first = false;
				starting--;
			}

			while (true)
			{
				if (stopping || Volatile.Read(ref fault) is not null)
				{
					return null;
				}

				if (waiting.Count > 0)
				{
					return waiting.Dequeue();
				}

				idle++;
				try
				{
					Monitor.Wait(workLock);
				}
				finally
				{
					idle--;
				}
			}
		}
	}

	/// <summary>Deflates a job on a stage thread and publishes its chunk; the job's pieces are released whatever happens.</summary>
	/// <param name="worker">The thread's worker, made with its first job: <see langword="null"/> before.</param>
	/// <param name="job">The job.</param>
	private void RunJob(ref object? worker, DeflateJob job)
	{
		DeflatedChunk? chunk = null;
		try
		{
			chunk = RentChunk();
			chunk.Sequence = job.Sequence;
			BeforeDeflate?.Invoke(job.Sequence);
			worker ??= CreateWorker();
			Deflate(worker, job, chunk);
		}
		catch
		{
			if (chunk is not null)
			{
				Return(chunk);
			}

			throw;
		}
		finally
		{
			job.Release(Writer);
		}

		lock (doneLock)
		{
			// Nothing of the job is touched after this: the writing thread takes its chunk, and reuses it.
			job.Chunk = chunk;
			job.Done = true;
			if (waitingFor == job.Sequence)
			{
				Monitor.Pulse(doneLock);
				waiter?.TrySetResult(true);
			}
		}
	}

	/// <summary>Holds a thread's exception, the first one, stops the threads taking jobs, and wakes the writing thread.</summary>
	/// <param name="exception">The exception.</param>
	private void Fail(Exception exception)
	{
		Interlocked.CompareExchange(ref fault, ExceptionDispatchInfo.Capture(exception), null);
		lock (workLock)
		{
			Monitor.PulseAll(workLock);
		}

		lock (doneLock)
		{
			Monitor.PulseAll(doneLock);
			waiter?.TrySetResult(false);
		}
	}
}
