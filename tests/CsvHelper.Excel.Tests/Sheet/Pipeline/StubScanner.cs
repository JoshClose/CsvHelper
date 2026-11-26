using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>A segment a <see cref="StubScanner"/> was given: its offset, a copy of its bytes, and whether it was final.</summary>
/// <param name="Offset">The part's offset of its first byte.</param>
/// <param name="Bytes">A copy of its bytes.</param>
/// <param name="Final">Whether it was the last the pipeline read.</param>
internal sealed record SegmentCall(long Offset, byte[] Bytes, bool Final);

/// <summary>
/// A segment scanner for the pipeline's tests, in place of lane S's worker. It copies each segment it is given, writes one
/// record into the block whose field is the segment's offset (so that the test can tell the block is that segment's own),
/// and returns what <see cref="Script"/> says: by default <see cref="SegmentEnd.Ended"/> for a segment with the sheet's end,
/// a mark for a final one without it, and <see cref="SegmentEnd.Done"/> otherwise. Its scans can sleep for random times,
/// wait at a gate the test sets, and are counted after the test seals it (<see cref="Sealed"/>).
/// </summary>
/// <param name="seed">The seed of its random delays.</param>
internal sealed class StubScanner(int seed) : ISegmentScanner
{
	/// <summary>The longest a scan waits at <see cref="Gate"/>: past it, the scan throws, and its segment is marked.</summary>
	public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

	private readonly Random random = new(seed);
	private readonly object gate = new();
	private readonly List<SegmentCall> calls = [];
	private byte[]? scanning;
	private int callsAfterSealed;
	private bool sealedCalls;

	/// <summary>The bytes that end the rows: a segment that holds them ends the rows (<see cref="SegmentEnd.Ended"/>).</summary>
	public byte[] EndMarker { get; set; } = Encoding.ASCII.GetBytes("</sheetData>");

	/// <summary>Whether scans sleep, yield or spin for random times.</summary>
	public bool Delays { get; set; }

	/// <summary>Decides a scan's result from its segment and block, in place of the default; it may also write into the block.</summary>
	public Func<SegmentCall, SegmentBlock, SegmentResult>? Script { get; set; }

	/// <summary>When set, a scan waits for it.</summary>
	public ManualResetEventSlim? Gate { get; set; }

	/// <summary>Set when a scan starts, for a test that waits for one to be in progress.</summary>
	public ManualResetEventSlim Entered { get; } = new(false);

	/// <summary>The segments scanned, in the order this scanner scanned them.</summary>
	public IReadOnlyList<SegmentCall> Calls
	{
		get
		{
			lock (gate)
			{
				return [.. calls];
			}
		}
	}

	/// <summary>The number of scans that started after <see cref="Sealed"/> was set.</summary>
	public int CallsAfterSealed
	{
		get
		{
			lock (gate)
			{
				return callsAfterSealed;
			}
		}
	}

	/// <summary>Whether scans from here on are counted in <see cref="CallsAfterSealed"/>.</summary>
	public bool Sealed
	{
		get
		{
			lock (gate)
			{
				return sealedCalls;
			}
		}
		set
		{
			lock (gate)
			{
				sealedCalls = value;
			}
		}
	}

	/// <summary>The default result for a segment.</summary>
	/// <param name="call">The segment.</param>
	/// <param name="endMarker">The bytes that end the rows.</param>
	/// <returns>Ended when the segment holds <paramref name="endMarker"/>; else a mark when it is final; else done.</returns>
	public static SegmentResult Default(SegmentCall call, byte[] endMarker)
	{
		if (call.Bytes.AsSpan().IndexOf(endMarker) >= 0)
		{
			return new SegmentResult { End = SegmentEnd.Ended, FirstRow = -1 };
		}

		return call.Final
			? new SegmentResult { End = SegmentEnd.Marked, Mark = SegmentMark.Unfinished, FirstRow = -1 }
			: new SegmentResult { End = SegmentEnd.Done, FirstRow = -1 };
	}

	/// <summary>Says whether a scan is reading a buffer, for <see cref="CheckingPool.InUse"/>.</summary>
	/// <param name="buffer">The buffer.</param>
	/// <returns>What uses it, or <see langword="null"/>.</returns>
	public string? Using(byte[] buffer)
	{
		lock (gate)
		{
			return ReferenceEquals(scanning, buffer) ? "a scan" : null;
		}
	}

	/// <inheritdoc/>
	public SegmentResult Scan(byte[] bytes, int length, long offset, bool final, SegmentBlock block)
	{
		lock (gate)
		{
			if (sealedCalls)
			{
				callsAfterSealed++;
			}

			scanning = bytes;
		}

		try
		{
			block.Clear();
			Entered.Set();
			if (Gate is not null && !Gate.Wait(Bound))
			{
				throw new TimeoutException("The scan was held too long.");
			}

			if (Delays)
			{
				Delay();
			}

			var call = new SegmentCall(offset, bytes.AsSpan(0, length).ToArray(), final);
			lock (gate)
			{
				calls.Add(call);
			}

			block.AddField(Encoding.ASCII.GetBytes(offset.ToString(CultureInfo.InvariantCulture)), SheetCellKind.Text);
			block.EndRecord(offset);

			return Script is null ? Default(call, EndMarker) : Script(call, block);
		}
		finally
		{
			lock (gate)
			{
				scanning = null;
			}
		}
	}

	/// <summary>Waits a random while: not at all, a yield, a spin, or a sleep of a millisecond.</summary>
	private void Delay()
	{
		switch (random.Next(8))
		{
			case 0:
				Thread.Sleep(1);
				break;
			case 1:
			case 2:
				Thread.Yield();
				break;
			case 3:
			case 4:
				Thread.SpinWait(random.Next(1, 20_000));
				break;
		}
	}
}
