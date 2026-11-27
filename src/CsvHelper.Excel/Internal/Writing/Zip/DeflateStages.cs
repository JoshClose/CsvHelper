namespace CsvHelper.Excel;

/// <summary>Makes the deflate stage of a chunked entry (<see cref="DeflateStage"/>).</summary>
internal static class DeflateStages
{
	/// <summary>
	/// Makes the stage of a chunked entry of a writer: on net8.0 and net10.0 the chunked stage (<c>ChunkedDeflateStage</c>),
	/// which cuts the data at the writer's <see cref="ZipWriter.ChunkSize"/> and deflates the chunks on up to
	/// <paramref name="threads"/> threads; on the other builds the one-deflater stage (<see cref="SerialDeflateStage"/>),
	/// which deflates the whole entry on one thread, with the serial entry's bytes.
	/// </summary>
	/// <param name="writer">The writer: its level (not <see cref="System.IO.Compression.CompressionLevel.NoCompression"/>), chunk size and pool.</param>
	/// <param name="threads">The most threads the stage deflates on: 2 or more on net8.0 and net10.0; on the other builds, any count is one thread.</param>
	/// <returns>The stage, which starts no thread until it has data to deflate.</returns>
	public static DeflateStage Create(ZipWriter writer, int threads)
	{
#if NET
		DeflateStage stage = new ChunkedDeflateStage(writer, threads);
#else
		DeflateStage stage = new SerialDeflateStage(writer);
#endif
		writer.StageCreated?.Invoke(stage);

		return stage;
	}
}
