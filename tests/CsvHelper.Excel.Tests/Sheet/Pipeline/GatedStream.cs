namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// The inner stream of a part for the pipeline's tests. Its reads can be held at a gate that the test opens
/// (<see cref="Hold"/>, <see cref="Open"/>), kept to a chunk, and made to fail at an offset, as a corrupt deflate stream
/// fails. It says which buffer a read is writing into (<see cref="Reading"/>), for <see cref="CheckingPool"/>, and counts
/// the reads made after the test sealed it (<see cref="Sealed"/>): once the pipeline is stopped, there must be none.
/// </summary>
/// <param name="data">The part's bytes.</param>
internal sealed class GatedStream(byte[] data) : Stream
{
	/// <summary>The longest a read waits at the gate: past it, the read goes on, and <see cref="HeldTooLong"/> says so.</summary>
	public static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

	private readonly object gate = new();
	private int position;
	private bool holding;
	private int waiting;
	private byte[]? reading;
	private int reads;
	private int readsAfterSealed;
	private bool sealedReads;
	private bool heldTooLong;

	/// <summary>The most bytes a read returns.</summary>
	public int Chunk { get; set; } = int.MaxValue;

	/// <summary>Makes each read return a random number of bytes, from 1 to <see cref="Chunk"/>.</summary>
	public Random? Chunks { get; set; }

	/// <summary>The offset at which reads throw <see cref="Failure"/>, or -1 for none: the reads before it stop there.</summary>
	public long FailAt { get; set; } = -1;

	/// <summary>The exception a read at <see cref="FailAt"/> throws.</summary>
	public Exception Failure { get; set; } = new InvalidDataException("The archive entry was compressed using an unsupported compression method.");

	/// <summary>The number of bytes read so far.</summary>
	public int Delivered
	{
		get
		{
			lock (gate)
			{
				return position;
			}
		}
	}

	/// <summary>The number of reads so far.</summary>
	public int Reads
	{
		get
		{
			lock (gate)
			{
				return reads;
			}
		}
	}

	/// <summary>The number of reads made after <see cref="Sealed"/> was set.</summary>
	public int ReadsAfterSealed
	{
		get
		{
			lock (gate)
			{
				return readsAfterSealed;
			}
		}
	}

	/// <summary>Whether a read waited at the gate longer than <see cref="Bound"/>.</summary>
	public bool HeldTooLong
	{
		get
		{
			lock (gate)
			{
				return heldTooLong;
			}
		}
	}

	/// <summary>Whether reads from here on are counted in <see cref="ReadsAfterSealed"/>.</summary>
	public bool Sealed
	{
		get
		{
			lock (gate)
			{
				return sealedReads;
			}
		}
		set
		{
			lock (gate)
			{
				sealedReads = value;
			}
		}
	}

	/// <summary>Whether the stream is disposed.</summary>
	public bool Disposed { get; private set; }

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <summary>Holds the reads from here on at the gate, until <see cref="Open"/>.</summary>
	public void Hold()
	{
		lock (gate)
		{
			holding = true;
		}
	}

	/// <summary>Opens the gate: the reads held go on, and the next ones aren't held.</summary>
	public void Open()
	{
		lock (gate)
		{
			holding = false;
			Monitor.PulseAll(gate);
		}
	}

	/// <summary>Waits for a read to be held at the gate.</summary>
	/// <param name="timeout">The longest to wait.</param>
	/// <returns>Whether a read is held.</returns>
	public bool WaitForHeldRead(TimeSpan timeout)
	{
		var until = DateTime.UtcNow + timeout;
		lock (gate)
		{
			while (waiting == 0)
			{
				var left = until - DateTime.UtcNow;
				if (left <= TimeSpan.Zero || !Monitor.Wait(gate, left))
				{
					return waiting > 0;
				}
			}

			return true;
		}
	}

	/// <summary>Says whether a read is writing into a buffer, for <see cref="CheckingPool.InUse"/>.</summary>
	/// <param name="buffer">The buffer.</param>
	/// <returns>What uses it, or <see langword="null"/>.</returns>
	public string? Reading(byte[] buffer)
	{
		lock (gate)
		{
			return ReferenceEquals(reading, buffer) ? "a read of the part's stream" : null;
		}
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		lock (gate)
		{
			reads++;
			if (sealedReads)
			{
				readsAfterSealed++;
			}

			reading = buffer;
			try
			{
				if (holding)
				{
					waiting++;
					Monitor.PulseAll(gate);
					var until = DateTime.UtcNow + Bound;
					while (holding)
					{
						var left = until - DateTime.UtcNow;
						if (left <= TimeSpan.Zero || !Monitor.Wait(gate, left))
						{
							heldTooLong = true;
							holding = false;
						}
					}

					waiting--;
				}

				if (FailAt >= 0 && position >= FailAt)
				{
					throw Failure;
				}

				var bytes = Math.Min(Math.Min(count, Chunk), data.Length - position);
				if (Chunks is not null && bytes > 1)
				{
					bytes = Chunks.Next(1, bytes + 1);
				}

				if (FailAt >= 0)
				{
					bytes = (int)Math.Min(bytes, FailAt - position);
				}

				System.Buffer.BlockCopy(data, position, buffer, offset, bytes);
				position += bytes;

				return bytes;
			}
			finally
			{
				reading = null;
			}
		}
	}

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		Disposed = true;
		base.Dispose(disposing);
	}
}
