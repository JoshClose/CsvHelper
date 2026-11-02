using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// The input of a <see cref="CsvReader"/> that is not CSV text, such as the rows of a spreadsheet. In place of the CSV
/// scanner, it writes whole records of decoded fields, each with a <see cref="CsvCellKind"/>, into the reader's buffer
/// and index through a <see cref="CsvReader.RecordBatch"/>; everything above the index (field access, parsing, record
/// mappers, dynamic records, <see cref="CsvDataReader"/>) then works as for CSV.
/// </summary>
/// <remarks>
/// <para>
/// Internal, for CsvHelper.Excel: a public source would freeze the encoding of the index, which is free to change.
/// </para>
/// <para>
/// The reader owns its source and disposes it. A reader over a source has no quoting, escapes, comments, fixed widths or
/// separator detection; it trims as <see cref="CsvReaderOptions.TrimWhitespace"/> says, and always tracks line numbers:
/// the line of a record is the one the source gives <see cref="CsvReader.RecordBatch.EndRecord"/>, such as a sheet's row
/// number. <see cref="CsvReader.ByteOffset"/> counts the bytes of the decoded fields, which is not meaningful for a sheet.
/// </para>
/// </remarks>
internal abstract class CsvRecordSource : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Writes the next records into <paramref name="batch"/>: at least one, unless no records remain, and usually until
	/// <see cref="CsvReader.RecordBatch.IsFull"/>. Records must be whole: the reader starts the next batch over in its buffer.
	/// </summary>
	/// <param name="batch">Takes the fields and the ends of the records.</param>
	/// <returns>
	/// <see langword="false"/> when the source has no records left (the last ones can be written in the same call);
	/// otherwise <see langword="true"/>.
	/// </returns>
	public abstract bool Fill(CsvReader.RecordBatch batch);

	/// <summary>Asynchronously writes the next records into <paramref name="batch"/>. See <see cref="Fill"/>.</summary>
	/// <param name="batch">Takes the fields and the ends of the records.</param>
	/// <param name="cancellationToken">Cancels reading from the underlying input.</param>
	/// <returns>
	/// A task whose result is <see langword="false"/> when the source has no records left, otherwise <see langword="true"/>.
	/// </returns>
	public abstract ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken);

	/// <summary>
	/// Describes where a record or one of its fields is, such as <c>Orders!C14</c>, for <see cref="CsvFormatException.Location"/>,
	/// <see cref="CsvRecordError.Location"/> and error messages. It can be called from any thread (the workers of
	/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>), also after the record's batch is gone.
	/// </summary>
	/// <param name="line">The line of the record, as the source gave it to <see cref="CsvReader.RecordBatch.EndRecord"/>.</param>
	/// <param name="field">The 0-based index of the field in the record, or -1 for the whole record.</param>
	/// <returns>The location, or <see langword="null"/> when the source cannot tell.</returns>
	public abstract string? Locate(long line, int field);

	/// <summary>Releases the input of the source. The reader calls it when it is disposed; the default does nothing.</summary>
	public virtual void Dispose()
	{
	}

	/// <summary>Asynchronously releases the input of the source. The default calls <see cref="Dispose"/>.</summary>
	/// <returns>A task that completes when the input is released.</returns>
	public virtual ValueTask DisposeAsync()
	{
		Dispose();

		return default;
	}
}
