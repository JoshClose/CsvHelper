using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>
/// What the streams of a planned query have in common: they read their source each time they are enumerated,
/// synchronously or asynchronously, unless they were read into memory first (<see cref="BufferAsync"/>, which the
/// asynchronous paths of <see cref="QueryExecutor"/> do before LINQ to Objects runs the rest of a query), after which they
/// return what they read.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
[RequiresDynamicCode(Trimming.Dynamic)]
internal abstract class StreamBase<T> : IEnumerable<T>, IAsyncEnumerable<T>, IAsyncRecordStream
{
	private List<T>? buffer;

	/// <summary>The elements read into memory, or <see langword="null"/>.</summary>
	protected List<T>? Buffer
	{
		get => Volatile.Read(ref buffer);
		set => Volatile.Write(ref buffer, value);
	}

	/// <summary>
	/// Whether the stream reads all of its elements into memory the first time it is enumerated, and returns them
	/// afterwards: for a stream inside a lambda, which runs for each element of something else.
	/// </summary>
	public bool Materialize { get; set; }

	/// <summary>Whether the source can be read again; <see langword="true"/> unless a subclass says otherwise.</summary>
	public virtual bool CanReread => true;

	/// <summary>Reads the elements, or returns those read into memory.</summary>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	public IEnumerator<T> GetEnumerator()
	{
		if (Buffer is { } buffered)
		{
			return buffered.GetEnumerator();
		}

		if (!Materialize)
		{
			return Read();
		}

		var elements = new List<T>();
		using (var enumerator = Read())
		{
			while (enumerator.MoveNext())
			{
				elements.Add(enumerator.Current);
			}
		}

		Buffer = elements;

		return elements.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>Reads the elements asynchronously, or returns those read into memory.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
		Buffer is { } buffered ? new Replay(buffered, cancellationToken) : Materialize ? ReadOnceAsync(cancellationToken) : ReadAsync(cancellationToken);

	/// <summary>Reads the elements into memory asynchronously, once; enumerating the stream returns them afterwards.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>A task that completes when the elements are in memory.</returns>
	public virtual async ValueTask BufferAsync(CancellationToken cancellationToken)
	{
		if (Buffer is not null)
		{
			return;
		}

		var elements = new List<T>();
		var enumerator = ReadAsync(cancellationToken);
		try
		{
			while (await enumerator.MoveNextAsync().ConfigureAwait(false))
			{
				elements.Add(enumerator.Current);
			}
		}
		finally
		{
			await enumerator.DisposeAsync().ConfigureAwait(false);
		}

		Buffer = elements;
	}

	/// <summary>Reads the elements asynchronously, as objects.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>The elements.</returns>
	public async IAsyncEnumerable<object?> ReadObjectsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		await foreach (var element in this.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			yield return element;
		}
	}

	/// <summary>Reads the elements from the source.</summary>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	protected abstract IEnumerator<T> Read();

	/// <summary>Reads all the elements into memory, then returns them.</summary>
	private async IAsyncEnumerator<T> ReadOnceAsync(CancellationToken cancellationToken)
	{
		await BufferAsync(cancellationToken).ConfigureAwait(false);
		foreach (var element in Buffer!)
		{
			yield return element;
		}
	}

	/// <summary>Reads the elements from the source asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator that reads the next element each time it moves; disposing it closes the source.</returns>
	protected abstract IAsyncEnumerator<T> ReadAsync(CancellationToken cancellationToken);

	/// <summary>Returns elements in memory as an asynchronous enumerator, which completes each move synchronously.</summary>
	private sealed class Replay : IAsyncEnumerator<T>
	{
		private readonly List<T> elements;
		private readonly CancellationToken cancellationToken;
		private int index = -1;

		/// <summary>Creates the enumerator.</summary>
		/// <param name="elements">The elements.</param>
		/// <param name="cancellationToken">Cancels the enumeration.</param>
		public Replay(List<T> elements, CancellationToken cancellationToken)
		{
			this.elements = elements;
			this.cancellationToken = cancellationToken;
		}

		/// <summary>The current element.</summary>
		public T Current => elements[index];

		/// <summary>Moves to the next element.</summary>
		/// <returns>Whether there is one.</returns>
		public ValueTask<bool> MoveNextAsync()
		{
			cancellationToken.ThrowIfCancellationRequested();

			return new ValueTask<bool>(++index < elements.Count);
		}

		/// <summary>Does nothing.</summary>
		/// <returns>A completed task.</returns>
		public ValueTask DisposeAsync() => default;
	}
}
