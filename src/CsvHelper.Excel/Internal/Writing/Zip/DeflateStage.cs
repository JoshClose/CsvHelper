using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The deflate of a chunked entry's data off the writing thread (<see cref="ZipWriter.Open(string, bool)"/>): the entry
/// appends its data in order, and takes the deflated chunks back in the same order to write them. On net8.0 and net10.0
/// the data is cut at every <see cref="ZipWriter.ChunkSize"/> bytes, by byte count, and each chunk is deflated on its own
/// (<c>ChunkDeflater</c>), so that the bytes depend on the data alone: not on how its writes were cut, the thread
/// count or timing. On the other builds one deflater takes the whole entry, fed as the serial entry feeds its own, so that
/// the bytes are the serial entry's, and each chunk is what it gave for an append.
/// </summary>
/// <remarks>
/// <para>
/// Every member but <see cref="Return"/> and <see cref="Dispose"/> is called on the writing thread only, one call at a
/// time. Synchronous waits block for real (the stage's threads finish chunks on their own), so a synchronous write never
/// runs an incomplete task through <c>SyncOrAsync.Wait</c>.
/// </para>
/// <para>
/// A stage thread's exception is held, and the writing thread's next call on the stage throws it (<see cref="Append(OwnedSegment)"/>,
/// <see cref="Complete"/>, <see cref="TryTake"/>, <see cref="Take"/> or <see cref="TakeAsync"/>); the entry then faults
/// its writer and disposes the stage. <see cref="Dispose"/> stops and joins every thread before any buffer it holds goes
/// back to the pool, on every path.
/// </para>
/// </remarks>
internal abstract class DeflateStage : IDisposable
{
	/// <summary>The most threads the stage deflates on: 1 or more.</summary>
	public abstract int Threads { get; }

	/// <summary>
	/// Whether the writing thread must take a chunk (<see cref="Take"/> or <see cref="TakeAsync"/>) before it appends more:
	/// <see cref="DeflateSizes.ChunksPerThread"/> chunks a thread are cut and not yet taken, which bounds the memory the
	/// stage holds.
	/// </summary>
	public abstract bool MustWait { get; }

	/// <summary>
	/// Raises the most threads the stage deflates on, for a parallel write whose degree is above the writer's threads
	/// (<see cref="ZipWriter.ParallelThreads"/>). A count at or below <see cref="Threads"/> changes nothing.
	/// </summary>
	/// <param name="threads">The threads.</param>
	public abstract void RaiseThreads(int threads);

	/// <summary>
	/// Appends a segment of the entry's data, after the data before it, taking the segment whatever happens: the stage
	/// returns it (<see cref="ZipWriter.Return(OwnedSegment)"/>) once its bytes are gathered into their chunks, or as it is
	/// disposed. A chunk that fills is handed to a stage thread. It never waits.
	/// </summary>
	/// <param name="segment">The data, which the writer counted (<see cref="ZipWriter.Hold"/>).</param>
	/// <exception cref="InvalidOperationException">The stage is complete.</exception>
	public abstract void Append(OwnedSegment segment);

	/// <summary>Appends a copy of bytes of the entry's data, after the data before them. It never waits.</summary>
	/// <param name="bytes">The data.</param>
	/// <exception cref="InvalidOperationException">The stage is complete.</exception>
	public abstract void Append(ReadOnlySpan<byte> bytes);

	/// <summary>
	/// Ends the data: what is left after the last full chunk is the last chunk, deflated whole (empty, it is <c>03 00</c>).
	/// Called once, as the entry closes; the chunks are then taken until none is left.
	/// </summary>
	/// <exception cref="InvalidOperationException">The stage is complete already.</exception>
	public abstract void Complete();

	/// <summary>Takes the next chunk in order when it is deflated, without waiting.</summary>
	/// <param name="chunk">The chunk, to write and then <see cref="Return"/>; <see langword="null"/> when there is none to take now.</param>
	/// <returns>Whether a chunk was taken.</returns>
	public abstract bool TryTake(out DeflatedChunk chunk);

	/// <summary>
	/// Takes the next chunk in order, waiting for its deflate: the next one cut, which is every chunk after
	/// <see cref="Complete"/>. It doesn't wait for data: when no chunk is cut and not taken, it returns
	/// <see langword="null"/> at once.
	/// </summary>
	/// <returns>The chunk, to write and then <see cref="Return"/>; or <see langword="null"/>.</returns>
	public abstract DeflatedChunk? Take();

	/// <summary>Takes the next chunk in order as <see cref="Take"/> does, waiting asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the wait, which leaves the chunk for the next call.</param>
	/// <returns>A task whose result is the chunk, or <see langword="null"/>.</returns>
	public abstract ValueTask<DeflatedChunk?> TakeAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Returns a chunk the writing thread has written: its output buffers go back to the pool, and the chunk to the stage
	/// for reuse. Never called after <see cref="Dispose"/>: a chunk taken and still held then is cleared by its entry.
	/// </summary>
	/// <param name="chunk">A chunk taken from this stage.</param>
	public abstract void Return(DeflatedChunk chunk);

	/// <summary>
	/// Stops the stage: its threads stop and are joined, and then every buffer it holds goes back to the pool (segments,
	/// chunks being gathered, deflated or not taken). Calling it again does nothing. A chunk taken and not returned is the
	/// entry's to return. It may run on another thread while the writing thread is in no call, or waits in
	/// <see cref="Take"/> or <see cref="TakeAsync"/> (which then return <see langword="null"/>); not during another call.
	/// </summary>
	public abstract void Dispose();
}
