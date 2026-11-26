using System.Buffers;
using System.Runtime.CompilerServices;

namespace CsvHelper.Excel.Tests.Sheet.Pipeline;

/// <summary>
/// An array pool for the pipeline's tests that checks how its buffers come and go. It records a failure for a buffer
/// returned twice, a buffer it didn't rent, and a buffer returned while something still uses it (<see cref="InUse"/>: a
/// read of a <see cref="GatedStream"/>, a stub's scan); and <see cref="AssertClean"/> fails on any of those, or on a buffer
/// still rented. Each buffer is filled with <c>0xCC</c> when rented and with <c>0xDD</c> when returned, so a thread that
/// used one after its return would read or leave garbage that the tests' byte comparisons see.
/// </summary>
internal sealed class CheckingPool : ArrayPool<byte>
{
	private readonly object gate = new();
	private readonly HashSet<byte[]> rented = new(ReferenceComparer.Instance);
	private readonly HashSet<byte[]> returned = new(ReferenceComparer.Instance);
	private readonly List<string> failures = [];
	private int rents;

	/// <summary>Says what uses a buffer being returned, or <see langword="null"/> when nothing does.</summary>
	public Func<byte[], string?>? InUse { get; set; }

	/// <summary>The number of buffers rented so far.</summary>
	public int Rents
	{
		get
		{
			lock (gate)
			{
				return rents;
			}
		}
	}

	/// <summary>The number of buffers rented and not returned.</summary>
	public int Outstanding
	{
		get
		{
			lock (gate)
			{
				return rented.Count;
			}
		}
	}

	/// <summary>What went wrong, in order.</summary>
	public IReadOnlyList<string> Failures
	{
		get
		{
			lock (gate)
			{
				return [.. failures];
			}
		}
	}

	/// <inheritdoc/>
	public override byte[] Rent(int minimumLength)
	{
		// Rounded up to a power of two, as the shared pool rounds, so that callers see buffers longer than they asked for.
		var length = 16;
		while (length < minimumLength)
		{
			length *= 2;
		}

		var array = new byte[length];
		array.AsSpan().Fill(0xCC);
		lock (gate)
		{
			rented.Add(array);
			rents++;
		}

		return array;
	}

	/// <inheritdoc/>
	public override void Return(byte[] array, bool clearArray = false)
	{
		var user = InUse?.Invoke(array);
		lock (gate)
		{
			if (user is not null)
			{
				failures.Add($"A buffer was returned while {user} used it.");
			}

			if (!rented.Remove(array))
			{
				failures.Add(returned.Contains(array) ? "A buffer was returned twice." : $"A buffer of {array.Length} bytes that the pool didn't rent was returned.");

				return;
			}

			returned.Add(array);
		}

		array.AsSpan().Fill(0xDD);
	}

	/// <summary>Fails unless every buffer came back once, while nothing used it.</summary>
	public void AssertClean()
	{
		Assert.Empty(Failures);
		Assert.Equal(0, Outstanding);
	}

	/// <summary>Compares arrays by reference, which .NET Framework's <c>ReferenceEqualityComparer</c> doesn't have.</summary>
	private sealed class ReferenceComparer : IEqualityComparer<byte[]>
	{
		/// <summary>The comparer.</summary>
		public static readonly ReferenceComparer Instance = new();

		/// <inheritdoc/>
		public bool Equals(byte[]? x, byte[]? y) => ReferenceEquals(x, y);

		/// <inheritdoc/>
		public int GetHashCode(byte[] obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
