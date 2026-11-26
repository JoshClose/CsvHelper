using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace CsvHelper.Excel;

/// <summary>
/// Reads a sheet's <c>sheetData</c> on threads of its own under <c>CsvReader.ParallelEnumerate</c>: an inflating thread reads
/// the part's stream into segments cut at rows' starts (<see cref="RowSplitter"/>), scan threads scan them, each with its
/// own <see cref="ISegmentScanner"/>, and the source takes them in the part's order (<see cref="Next"/>), stitches them into
/// the reader's batches, and gives them back (<see cref="Release"/>). Where a segment can't be stitched, the source stops
/// the pipeline and reads on serially from it (<see cref="FallbackFrom"/>).
/// </summary>
/// <remarks>
/// <para>
/// The segments go round a ring of two slots per scan thread, which bounds the bytes in flight. Three waits move them, each
/// on a lock of its own with one kind of waiter: the inflating thread waits for a free slot, which <see cref="Release"/>
/// frees; the scan threads wait for a filled one, which the inflating thread publishes; and <see cref="Next"/> waits for the
/// next one in order to be scanned, which a scan thread does. No stage waits while it holds what another waits for: the
/// inflating thread publishes a segment before it waits for the next slot, and the bytes after a cut go to a spare buffer,
/// not to the next slot; a scan thread waits only between segments; and the source releases a segment before it asks for
/// one a ring's length later (<see cref="Next"/> throws otherwise). So the chain can't stall. Each stage publishes in
/// <c>finally</c>, so one that fails leaves no other waiting, and <see cref="Stop"/> wakes every wait and joins every thread.
/// </para>
/// <para>
/// The inflating thread reads uncounted (<see cref="LimitedReadStream.ReadUncounted"/>), so that the workbook's guard is only
/// charged on the source's thread: the stitcher charges each segment's bytes as it appends it (<see cref="SegmentSlot.Uncounted"/>),
/// and a released segment's bytes count as charged. The rest are charged by the stream the serial read goes on with
/// (<see cref="SegmentStream"/>), or by <see cref="ChargeUnread"/> and <see cref="Dispose"/>; the bytes the serial window had
/// read before the pipeline began (the carried bytes of <see cref="Start"/>) were counted then, and never are again.
/// </para>
/// <para>
/// The ring, the slots and the stream know nothing of XML: only the splitter and the scanners do.
/// </para>
/// </remarks>
internal sealed class SegmentPipeline : IDisposable
{
	/// <summary>The most bytes the inflating thread reads past a segment's cut target at first, to find the row start after it.</summary>
	private const int ReadAhead = 4096;

	/// <summary>The most the read past the cut target grows to, doubling while no row start is found.</summary>
	private const int MaxReadAhead = 64 * 1024;

	/// <summary>The result of a segment the splitter cut nothing from: never scanned.</summary>
	private static readonly SegmentResult UnsplitResult = new() { End = SegmentEnd.Marked, Mark = SegmentMark.Unsplit, FirstRow = -1 };

	private readonly LimitedReadStream stream;
	private readonly ISegmentScanner[] scanners;
	private readonly RowSplitter splitter;
	private readonly int segmentSize;
	private readonly int minimumCut;
	private readonly int segmentCap;
	private readonly ArrayPool<byte> pool;
	private readonly SegmentSlot[] ring;
	private readonly object freeLock = new();
	private readonly object scanLock = new();
	private readonly object deliverLock = new();
	private readonly List<Thread> threads = [];
	private volatile bool stopping;
	private int cutTarget;

	// The inflating thread's, which the source reads only once the thread is joined (or, before Start, sets). The tail is
	// the bytes read past the last segment published, from tailOffset in the part; the first tailCarried of them are carried
	// bytes, which the serial window counted.
	private byte[] tail = [];
	private int tailLength;
	private long tailOffset;
	private int tailCarried;
	private byte[]? spare;
	private ExceptionDispatchInfo? error;
	private bool partEnded;

	// Under freeLock (released is written only by the source's thread).
	private long released;
	private bool inflaterWaiting;

	// Under scanLock (published is written only by the inflating thread).
	private long published;
	private long scanned;
	private bool inflaterDone;

	// Under deliverLock.
	private long waitingFor = -1;
	private bool inflaterEnded;
	private long endCount;

	// The source's.
	private long delivered;
	private bool lastDelivered;
	private bool started;
	private bool stopped;
	private bool handedOff;
	private bool charged;
	private bool disposed;

	/// <summary>Creates the pipeline; <see cref="Start"/> starts its threads.</summary>
	/// <param name="stream">The part's stream, positioned after the bytes the serial window has read.</param>
	/// <param name="scanners">One scanner for each scan thread.</param>
	/// <param name="rowStart">The bytes a row's start tag begins with (<c>SheetScanner.RowStart</c>).</param>
	/// <param name="segmentSize">The cut target: segments are cut at the first row start at or after it (<see cref="SegmentSizes.DefaultSize"/>, or less in tests).</param>
	/// <param name="segmentCap">The most bytes a segment takes (<see cref="SegmentSizes.SegmentCap"/>): one with no row start before it is not cut.</param>
	/// <param name="pool">The pool the segments' buffers come from: <see cref="ArrayPool{T}.Shared"/>, or a checking pool in tests.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="scanners"/> is empty or holds <see langword="null"/>, or <paramref name="rowStart"/> is not a row start.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="segmentSize"/> or <paramref name="segmentCap"/> is 0 or less.</exception>
	public SegmentPipeline(LimitedReadStream stream, ISegmentScanner[] scanners, byte[] rowStart, int segmentSize, int segmentCap, ArrayPool<byte> pool)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(scanners);
		ArgumentNullException.ThrowIfNull(pool);
		if (scanners.Length == 0 || Array.IndexOf(scanners, null) >= 0)
		{
			throw new ArgumentException("The pipeline needs a scanner for each scan thread.", nameof(scanners));
		}

		if (segmentSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(segmentSize), segmentSize, "The size must be more than 0.");
		}

		if (segmentCap <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(segmentCap), segmentCap, "The cap must be more than 0.");
		}

		this.stream = stream;
		this.scanners = scanners;
		splitter = new RowSplitter(rowStart);
		this.segmentSize = segmentSize;
		minimumCut = Math.Min(SegmentSizes.MinimumCut, segmentSize);
		this.segmentCap = segmentCap;
		this.pool = pool;
		cutTarget = segmentSize;
		ring = new SegmentSlot[2 * scanners.Length];
		for (var i = 0; i < ring.Length; i++)
		{
			ring[i] = new SegmentSlot();
		}
	}

	/// <summary>The number of slots in the ring: two for each scan thread.</summary>
	public int RingSize => ring.Length;

	/// <summary>
	/// Starts the threads: the inflating thread reads the part from <paramref name="carriedOffset"/>, starting with the bytes
	/// the serial window had read past its position, and the scan threads scan what it cuts.
	/// </summary>
	/// <param name="carried">The serial window's unread bytes, from the scanner's position to the window's end: counted already, and copied here.</param>
	/// <param name="carriedOffset">The part's offset of the first carried byte: the scanner's position.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="carriedOffset"/> is negative.</exception>
	/// <exception cref="InvalidOperationException">The pipeline has started already, or has stopped.</exception>
	/// <exception cref="ObjectDisposedException">The pipeline is disposed.</exception>
	public void Start(ReadOnlySpan<byte> carried, long carriedOffset)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (carriedOffset < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(carriedOffset), carriedOffset, "The offset can't be negative.");
		}

		if (started || stopping)
		{
			throw new InvalidOperationException("The pipeline can start only once, before it is stopped.");
		}

		started = true;
		try
		{
			tail = pool.Rent(Math.Max(carried.Length, FirstSize(segmentSize)));
			carried.CopyTo(tail);
			tailLength = carried.Length;
			tailCarried = carried.Length;
			tailOffset = carriedOffset;
			var name = stream.PartName;
			Begin(new Thread(Inflate) { IsBackground = true, Name = $"CsvHelper.Excel inflate {name}" }, null);
			for (var i = 0; i < scanners.Length; i++)
			{
				Begin(new Thread(Scan) { IsBackground = true, Name = $"CsvHelper.Excel scan {i + 1} {name}" }, scanners[i]);
			}
		}
		catch
		{
			Stop();

			throw;
		}
	}

	/// <summary>
	/// Waits for the next segment in the part's order to be scanned, and returns it: the source stitches it, then gives it
	/// back with <see cref="Release"/>.
	/// </summary>
	/// <returns>The segment; <see langword="null"/> after the last (the final one or an unsplit one), or once the pipeline is stopped.</returns>
	/// <exception cref="InvalidOperationException">
	/// The pipeline hasn't started, or the source holds as many segments as the ring has slots: the inflating thread would
	/// wait for one of them.
	/// </exception>
	public SegmentSlot? Next()
	{
		if (!started)
		{
			throw new InvalidOperationException("The pipeline hasn't started.");
		}

		if (lastDelivered || stopping)
		{
			return null;
		}

		var sequence = delivered;
		if (sequence - released >= ring.Length)
		{
			throw new InvalidOperationException("A segment must be released before the source takes more than the ring holds.");
		}

		var slot = ring[sequence % ring.Length];
		lock (deliverLock)
		{
			while (!slot.Ready || slot.Sequence != sequence)
			{
				if (stopping || (inflaterEnded && sequence >= endCount))
				{
					waitingFor = -1;
					lastDelivered = true;

					return null;
				}

				waitingFor = sequence;
				Monitor.Wait(deliverLock);
			}

			waitingFor = -1;
		}

		delivered++;
		if (slot.Final || slot.Result.Mark == SegmentMark.Unsplit)
		{
			lastDelivered = true;
		}

		return slot;
	}

	/// <summary>
	/// Gives a segment back once the stitcher has appended it, and has charged its bytes: its slot can take the segment a
	/// ring's length later. Segments are released in the order <see cref="Next"/> gave them.
	/// </summary>
	/// <param name="slot">The oldest segment <see cref="Next"/> gave and not yet released.</param>
	/// <exception cref="ArgumentNullException"><paramref name="slot"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><paramref name="slot"/> is not the oldest segment held, or was handed to a fallback stream.</exception>
	public void Release(SegmentSlot slot)
	{
		ArgumentNullException.ThrowIfNull(slot);
		if (handedOff || released >= delivered || slot.Sequence != released || ring[released % ring.Length] != slot)
		{
			throw new InvalidOperationException("Segments are released in the order Next gave them, before a fallback.");
		}

		lock (deliverLock)
		{
			slot.Ready = false;
		}

		slot.Clear();
		lock (freeLock)
		{
			released++;
			if (inflaterWaiting)
			{
				Monitor.Pulse(freeLock);
			}
		}
	}

	/// <summary>
	/// Stops the threads and joins them: once it returns, no thread of the pipeline touches a slot, a buffer or the stream.
	/// It waits for a read of the inflating thread to return, and for scans in progress to end. It does nothing the second
	/// time, and only marks the pipeline stopped before <see cref="Start"/>.
	/// </summary>
	public void Stop()
	{
		if (stopped)
		{
			return;
		}

		stopping = true;
		lock (freeLock)
		{
			Monitor.PulseAll(freeLock);
		}

		lock (scanLock)
		{
			Monitor.PulseAll(scanLock);
		}

		lock (deliverLock)
		{
			Monitor.PulseAll(deliverLock);
		}

		foreach (var thread in threads)
		{
			thread.Join();
		}

		stopped = true;
	}

	/// <summary>
	/// After <see cref="Stop"/>, the stream the serial read goes on with from a segment the stitcher couldn't append: its
	/// bytes, every later segment's, the bytes the inflating thread read past its last cut, then the part's stream (or the
	/// exception of the read that failed). The stream owns them and the part's stream from here; the pipeline keeps none.
	/// </summary>
	/// <param name="slot">The segment: the oldest one <see cref="Next"/> gave and not yet released.</param>
	/// <returns>The stream, from <paramref name="slot"/>'s offset.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="slot"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The pipeline isn't stopped, <paramref name="slot"/> is not the oldest segment held, or the bytes went to a fallback or
	/// were charged already (<see cref="ChargeUnread"/>).
	/// </exception>
	public SegmentStream FallbackFrom(SegmentSlot slot)
	{
		ArgumentNullException.ThrowIfNull(slot);
		CheckHeld();
		if (released >= delivered || slot.Sequence != released || ring[released % ring.Length] != slot)
		{
			throw new InvalidOperationException("A fallback starts from the oldest segment held.");
		}

		return HandOff(released);
	}

	/// <summary>
	/// After <see cref="Stop"/>, the stream the serial read goes on with when the enumeration ended early: from the first
	/// segment not released, delivered or not, as <see cref="FallbackFrom"/>; else from the bytes the inflating thread read past
	/// its last cut, then the part's stream (or the exception of the read that failed).
	/// </summary>
	/// <returns>The stream; <see langword="null"/> when every segment was released and the part was read to its end: nothing is left.</returns>
	/// <exception cref="InvalidOperationException">The pipeline isn't stopped, or the bytes went to a fallback or were charged already.</exception>
	public SegmentStream? FallbackAfterDelivered()
	{
		CheckHeld();
		if (released == published && tailLength == 0 && error is null && partEnded)
		{
			return null;
		}

		return HandOff(released);
	}

	/// <summary>
	/// After <see cref="Stop"/>, when the rows have ended and nothing reads on: charges the inflated bytes that neither the
	/// stitcher (the released segments) nor a fallback stream charged, without throwing past the limit
	/// (<see cref="LimitedReadStream.TryCharge"/>). It does nothing the second time, or after a fallback.
	/// </summary>
	/// <exception cref="InvalidOperationException">The pipeline isn't stopped.</exception>
	public void ChargeUnread()
	{
		if (!stopped)
		{
			throw new InvalidOperationException("The pipeline must be stopped first.");
		}

		if (charged || handedOff)
		{
			return;
		}

		charged = true;
		var bytes = (long)tailLength - tailCarried;
		for (var sequence = released; sequence < published; sequence++)
		{
			bytes += ring[sequence % ring.Length].Uncounted;
		}

		if (bytes > 0)
		{
			stream.TryCharge(bytes);
		}
	}

	/// <summary>
	/// Stops the pipeline, charges the bytes it holds that no one charged (as <see cref="ChargeUnread"/>), and returns every
	/// buffer it still holds to the pool: those a fallback stream took are the stream's. It does nothing the second time.
	/// </summary>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		Stop();
		ChargeUnread();
		disposed = true;
		foreach (var slot in ring)
		{
			ReturnBuffer(slot.TakeBuffer());
		}

		ReturnBuffer(tail);
		tail = [];
		tailLength = 0;
		ReturnBuffer(spare);
		spare = null;
	}

	/// <summary>Starts one of the pipeline's threads.</summary>
	/// <param name="thread">The thread.</param>
	/// <param name="state">What it is started with: its scanner, for a scan thread.</param>
	private void Begin(Thread thread, object? state)
	{
		thread.Start(state);
		threads.Add(thread);
	}

	/// <summary>The size of a segment's buffer at first: the cut target and the read past it, within the cap.</summary>
	/// <param name="target">The cut target.</param>
	/// <returns>The size.</returns>
	private int FirstSize(int target) => (int)Math.Min(segmentCap, (long)target + ReadAhead + splitter.RowStart.Length + 1);

	/// <summary>The inflating thread: fills the ring's slots with segments, in order, until the part ends, a segment isn't cut, a read fails or the pipeline stops.</summary>
	/// <param name="state">Not used.</param>
	private void Inflate(object? state)
	{
		var sequence = 0L;
		SegmentSlot? held = null;
		try
		{
			for (; ; sequence++)
			{
				if (!WaitForSlot(sequence))
				{
					return;
				}

				held = ring[sequence % ring.Length];
				// The slot is free: its buffer takes the bytes after this segment's cut.
				var free = held.TakeBuffer();
				if (spare is null)
				{
					spare = free.Length > 0 ? free : null;
				}
				else
				{
					ReturnBuffer(free);
				}

				if (!FillSegment(held, sequence))
				{
					return;
				}

				held = null;
			}
		}
		catch (Exception e)
		{
			// Not a read (FillSegment captures a read's exception where it happens): a buffer that couldn't be had. The bytes
			// read so far end the part as a read's error would, and the stream the serial read goes on with throws it there.
			error ??= ExceptionDispatchInfo.Capture(e);
			if (held is not null)
			{
				PublishTail(held, sequence, true, false);
			}
		}
		finally
		{
			lock (scanLock)
			{
				inflaterDone = true;
				Monitor.PulseAll(scanLock);
			}

			lock (deliverLock)
			{
				inflaterEnded = true;
				endCount = published;
				Monitor.PulseAll(deliverLock);
			}
		}
	}

	/// <summary>Waits until the slot of a sequence number is free: its segment a ring's length earlier released.</summary>
	/// <param name="sequence">The sequence number of the segment the inflating thread fills next.</param>
	/// <returns>Whether the slot is free; <see langword="false"/> when the pipeline is stopping.</returns>
	private bool WaitForSlot(long sequence)
	{
		lock (freeLock)
		{
			while (sequence - released >= ring.Length && !stopping)
			{
				inflaterWaiting = true;
				Monitor.Wait(freeLock);
				inflaterWaiting = false;
			}
		}

		return !stopping;
	}

	/// <summary>
	/// Reads the part into the tail until it holds a row start at or after the cut target, and publishes the segment before
	/// it; or, at the part's end, a read's error or the cap, publishes the tail whole.
	/// </summary>
	/// <param name="slot">The free slot the segment goes in.</param>
	/// <param name="sequence">The segment's sequence number.</param>
	/// <returns>Whether the inflating thread goes on to the next segment: <see langword="false"/> after the last, or when the pipeline is stopping.</returns>
	private bool FillSegment(SegmentSlot slot, long sequence)
	{
		var target = Volatile.Read(ref cutTarget);
		var rowLength = splitter.RowStart.Length;
		var searchFrom = target;
		var readAhead = ReadAhead;
		while (true)
		{
			// Only from the cut target on, and only below the cap: a row start's bytes and the byte after them must be there.
			var limit = Math.Min(tailLength, segmentCap);
			if (limit > searchFrom + rowLength)
			{
				var cut = splitter.Find(tail.AsSpan(0, limit), searchFrom, out searchFrom);
				if (cut >= 0)
				{
					PublishCut(slot, sequence, cut, target);

					return true;
				}

				if (tailLength > target)
				{
					readAhead = Math.Min(readAhead * 2, MaxReadAhead);
				}
			}

			if (tailLength >= segmentCap)
			{
				// No row start before the cap: the rest of the part reads serially, from this segment's start.
				PublishTail(slot, sequence, false, true);

				return false;
			}

			if (stopping)
			{
				return false;
			}

			if (tailLength == tail.Length)
			{
				GrowTail();
			}

			var count = (int)Math.Min(Math.Min(tail.Length, segmentCap) - tailLength, Math.Max((long)target - tailLength, 0) + readAhead);
			int read;
			try
			{
				read = stream.ReadUncounted(tail, tailLength, count);
			}
			catch (Exception e)
			{
				error = ExceptionDispatchInfo.Capture(e);
				PublishTail(slot, sequence, true, false);

				return false;
			}

			if (read == 0)
			{
				partEnded = true;
				PublishTail(slot, sequence, true, false);

				return false;
			}

			tailLength += read;
		}
	}

	/// <summary>Doubles the tail's buffer, up to the cap, keeping its bytes.</summary>
	private void GrowTail()
	{
		var bigger = pool.Rent((int)Math.Min(segmentCap, Math.Max((long)tail.Length * 2, ReadAhead)));
		System.Buffer.BlockCopy(tail, 0, bigger, 0, tailLength);
		var old = tail;
		tail = bigger;
		ReturnBuffer(old);
	}

	/// <summary>
	/// Publishes the tail's bytes before a cut as a segment, and keeps those after it as the next segment's start, copied
	/// once, to the spare buffer.
	/// </summary>
	/// <param name="slot">The segment's slot.</param>
	/// <param name="sequence">The segment's sequence number.</param>
	/// <param name="cut">The position of the row start the segment ends before.</param>
	/// <param name="target">The cut target, to size a new buffer.</param>
	private void PublishCut(SegmentSlot slot, long sequence, int cut, int target)
	{
		var after = tailLength - cut;
		var needed = Math.Max(after, FirstSize(target));
		if (spare is null || spare.Length < needed)
		{
			var old = spare;
			spare = pool.Rent(needed);
			ReturnBuffer(old);
		}

		// The copy before the publication: once the segment is published, a scan thread reads its buffer.
		System.Buffer.BlockCopy(tail, cut, spare, 0, after);
		var next = spare;
		spare = null;
		Publish(slot, sequence, tail, cut, false, false);
		tail = next;
		tailLength = after;
		tailOffset += cut;
	}

	/// <summary>Publishes the tail's bytes whole as a segment: the last the inflating thread reads.</summary>
	/// <param name="slot">The segment's slot.</param>
	/// <param name="sequence">The segment's sequence number.</param>
	/// <param name="final">Whether the part ends after them, or the read after them failed.</param>
	/// <param name="unsplit">Whether they hold no row start before the cap: the segment is never scanned.</param>
	private void PublishTail(SegmentSlot slot, long sequence, bool final, bool unsplit)
	{
		var length = tailLength;
		Publish(slot, sequence, tail, length, final, unsplit);
		tail = [];
		tailLength = 0;
		tailOffset += length;
	}

	/// <summary>Fills a slot with a segment from the tail's start, and hands it to the scan threads.</summary>
	/// <param name="slot">The slot.</param>
	/// <param name="sequence">The segment's sequence number.</param>
	/// <param name="buffer">The segment's buffer: the tail's.</param>
	/// <param name="length">The number of bytes of the segment.</param>
	/// <param name="final">Whether they are the last bytes the pipeline reads.</param>
	/// <param name="unsplit">Whether the splitter cut nothing from them.</param>
	private void Publish(SegmentSlot slot, long sequence, byte[] buffer, int length, bool final, bool unsplit)
	{
		var carried = Math.Min(length, tailCarried);
		slot.Fill(sequence, tailOffset, buffer, length, length - carried, final, unsplit ? UnsplitResult : default);
		tailCarried -= carried;
		lock (scanLock)
		{
			published++;
			Monitor.Pulse(scanLock);
		}
	}

	/// <summary>A scan thread: scans the segments it takes, in the order they were published, until there are no more or the pipeline stops.</summary>
	/// <param name="state">The thread's scanner.</param>
	private void Scan(object? state)
	{
		var scanner = (ISegmentScanner)state!;
		while (TakeToScan() is { } slot)
		{
			var result = new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Error, FirstRow = -1 };
			try
			{
				result = slot.Result.Mark == SegmentMark.Unsplit ? slot.Result : scanner.Scan(slot.Buffer, slot.Length, slot.Offset, slot.Final, slot.Block);
			}
			catch (Exception e)
			{
				// A scan doesn't throw; this one did, and its segment is read serially.
				result = new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Error, FirstRow = -1, Error = e };
			}
			finally
			{
				Scanned(slot, result);
			}
		}
	}

	/// <summary>Waits for a published segment no scan thread has taken, and takes it.</summary>
	/// <returns>The segment's slot; <see langword="null"/> when the pipeline is stopping, or the inflating thread has ended and every segment is taken.</returns>
	private SegmentSlot? TakeToScan()
	{
		lock (scanLock)
		{
			while (true)
			{
				if (stopping)
				{
					return null;
				}

				if (scanned < published)
				{
					var slot = ring[scanned % ring.Length];
					scanned++;

					return slot;
				}

				if (inflaterDone)
				{
					return null;
				}

				Monitor.Wait(scanLock);
			}
		}
	}

	/// <summary>Gives a scanned segment its result, adjusts the cut target by its block, and wakes the source if it waits for it.</summary>
	/// <param name="slot">The segment's slot.</param>
	/// <param name="result">How its scan ended.</param>
	private void Scanned(SegmentSlot slot, SegmentResult result)
	{
		if (result.Mark != SegmentMark.Unsplit)
		{
			AdjustTarget(slot.Block.Size);
		}

		slot.Result = result;
		lock (deliverLock)
		{
			// Nothing of the slot is touched after this: the source may release it, and the inflating thread fill it again.
			slot.Ready = true;
			if (waitingFor == slot.Sequence)
			{
				Monitor.Pulse(deliverLock);
			}
		}
	}

	/// <summary>
	/// Halves the cut target after a block larger than <see cref="SegmentSizes.LargeBlock"/>, down to the minimum, and
	/// doubles it after a smaller one, up to the configured size: so that a sheet whose cells expand (long repeated shared
	/// strings, gaps) keeps its blocks under their cap.
	/// </summary>
	/// <param name="blockSize">The block's size (<see cref="SegmentBlock.Size"/>).</param>
	private void AdjustTarget(long blockSize)
	{
		var target = Volatile.Read(ref cutTarget);
		var next = blockSize > SegmentSizes.LargeBlock ? Math.Max(target / 2, minimumCut) : (int)Math.Min((long)target * 2, segmentSize);
		if (next != target)
		{
			Interlocked.CompareExchange(ref cutTarget, next, target);
		}
	}

	/// <summary>Hands the bytes from a segment on to a fallback stream, with the part's stream.</summary>
	/// <param name="from">The sequence number of the first segment the stream reads: <see cref="published"/> for the tail alone.</param>
	/// <returns>The stream.</returns>
	private SegmentStream HandOff(long from)
	{
		handedOff = true;
		var pieces = new List<SegmentStream.Piece>();
		var offset = from < published ? ring[from % ring.Length].Offset : tailOffset;
		for (var sequence = from; sequence < published; sequence++)
		{
			var slot = ring[sequence % ring.Length];
			pieces.Add(new SegmentStream.Piece(slot.TakeBuffer(), slot.Length, (int)(slot.Length - slot.Uncounted)));
		}

		if (tailLength > 0)
		{
			pieces.Add(new SegmentStream.Piece(tail, tailLength, tailCarried));
			tail = [];
			tailLength = 0;
		}

		return new SegmentStream(stream, pool, [.. pieces], offset, error, partEnded);
	}

	/// <summary>Throws unless the pipeline is stopped and still holds its bytes.</summary>
	/// <exception cref="InvalidOperationException">The pipeline isn't stopped, or its bytes went to a fallback or were charged.</exception>
	private void CheckHeld()
	{
		if (!stopped)
		{
			throw new InvalidOperationException("The pipeline must be stopped first.");
		}

		if (handedOff || charged)
		{
			throw new InvalidOperationException("The pipeline's bytes went to a fallback, or were charged, already.");
		}
	}

	/// <summary>Returns a buffer to the pool, unless it is empty (none was rented).</summary>
	/// <param name="buffer">The buffer, or <see langword="null"/>.</param>
	private void ReturnBuffer(byte[]? buffer)
	{
		if (buffer is { Length: > 0 })
		{
			pool.Return(buffer);
		}
	}
}

/// <summary>
/// A slot of <see cref="SegmentPipeline"/>'s ring: one segment, its bytes in a buffer of the pipeline's pool, and once it is
/// scanned, its records and result. The slot's buffer and block are reused from one segment to the next.
/// </summary>
internal sealed class SegmentSlot
{
	/// <summary>Creates an empty slot.</summary>
	public SegmentSlot()
	{
	}

	/// <summary>The segment's place in the part's order, from 0.</summary>
	public long Sequence { get; private set; }

	/// <summary>The part's offset of the segment's first byte.</summary>
	public long Offset { get; private set; }

	/// <summary>The buffer holding the segment's bytes, from the pipeline's pool: <see cref="Length"/> of them, from its start.</summary>
	public byte[] Buffer { get; private set; } = [];

	/// <summary>The number of bytes of the segment.</summary>
	public int Length { get; private set; }

	/// <summary>
	/// The bytes of the segment that no stream counted toward the workbook's total, which the stitcher charges when it appends
	/// it: <see cref="Length"/>, less the carried bytes in it (those the serial window had read before the pipeline began).
	/// </summary>
	public long Uncounted { get; private set; }

	/// <summary>Whether the segment's bytes are the last the pipeline read: the part ends after them, or the read after them failed.</summary>
	public bool Final { get; private set; }

	/// <summary>The segment's records, once it is scanned.</summary>
	public SegmentBlock Block { get; } = new();

	/// <summary>How its scan ended; <see cref="SegmentMark.Unsplit"/> when the splitter cut nothing from it, and it was never scanned.</summary>
	public SegmentResult Result { get; internal set; }

	/// <summary>Whether the segment is scanned, for <see cref="SegmentPipeline.Next"/>: set and cleared under the pipeline's lock.</summary>
	internal bool Ready { get; set; }

	/// <summary>Fills the free slot with a segment.</summary>
	/// <param name="sequence">The segment's sequence number.</param>
	/// <param name="offset">The part's offset of its first byte.</param>
	/// <param name="buffer">The buffer of its bytes.</param>
	/// <param name="length">The number of bytes.</param>
	/// <param name="uncounted">The bytes of it no stream counted.</param>
	/// <param name="final">Whether they are the last the pipeline read.</param>
	/// <param name="result">The result it starts with: <see cref="SegmentMark.Unsplit"/>'s, or none.</param>
	internal void Fill(long sequence, long offset, byte[] buffer, int length, long uncounted, bool final, SegmentResult result)
	{
		Sequence = sequence;
		Offset = offset;
		Buffer = buffer;
		Length = length;
		Uncounted = uncounted;
		Final = final;
		Result = result;
	}

	/// <summary>Takes the slot's buffer, leaving it none.</summary>
	/// <returns>The buffer: empty when the slot had none.</returns>
	internal byte[] TakeBuffer()
	{
		var buffer = Buffer;
		Buffer = [];

		return buffer;
	}

	/// <summary>Clears the segment once it is released, keeping the buffer and the block's arrays.</summary>
	internal void Clear()
	{
		Length = 0;
		Uncounted = 0;
		Final = false;
		Result = default;
		Block.Clear();
	}
}
