namespace CsvHelper.Excel;

/// <summary>The sizes of a chunked entry's deflate (<see cref="DeflateStage"/>).</summary>
internal static class DeflateSizes
{
	/// <summary>
	/// The input a chunk takes: an entry's XML is cut at every 1 MB of it, by byte count, however its writes are cut
	/// (<see cref="ZipWriter.ChunkSize"/>, which tests lower).
	/// </summary>
	public const int ChunkSize = 1024 * 1024;

	/// <summary>The writes a chunk's deflater takes its input in, from the chunk's start, so that its output doesn't depend on how the input came.</summary>
	public const int WriteSize = 64 * 1024;

	/// <summary>The chunks a stage's threads may have cut and not yet written, per thread, before the writing thread waits for the oldest.</summary>
	public const int ChunksPerThread = 2;
}
