using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// The output of a <see cref="CsvWriter"/> that is not CSV text, such as the cells of a spreadsheet. In place of writing
/// CSV, the writer hands the sink each field as CSV formats it, before quoting, with the type it was formatted from, and
/// tells it where each record ends, so that generated <see cref="CsvRecordWriter{T}"/>s, converters and dynamic records
/// write cells unchanged.
/// </summary>
/// <remarks>
/// <para>
/// Internal, for CsvHelper.Excel, which depends on an exact version of CsvHelper.
/// </para>
/// <para>
/// Fields come as text, not as typed values, because the writer gets them to the sink without adding code to the
/// methods that CSV record loops inline: any code added there, even a test that is never taken, changed how the JIT
/// compiled those loops (2-8% slower CSV writes). The text round-trips for the default formats: numbers in the invariant
/// culture are the shortest text that reads back as the same value (the text a spreadsheet stores), dates and times are
/// ISO 8601 ("O"), and booleans are <see cref="bool.TrueString"/> and <see cref="bool.FalseString"/>. A field written
/// with a format string, or with a culture, comes as that text; the format string itself does not reach the sink.
/// </para>
/// <para>
/// The writer does not say which record is a header: a sink takes its first record as the header when
/// <see cref="CsvWriterOptions.HasHeader"/> is set, which is where the record methods write one and where a caller of
/// <see cref="CsvWriter.WriteHeader(ReadOnlySpan{string})"/> puts one. Marking headers in <c>WriteHeader</c>, even with a
/// single store, changed how the JIT compiled CSV record loops that call it (up to 7% slower writes).
/// </para>
/// <para>
/// The writer keeps its own rules for records: it counts fields, checks field counts as
/// <see cref="CsvWriterOptions.ValidateFieldCount"/> says (discarding a record that fails), and does I/O only when a
/// record ends and the sink asks to be flushed (<see cref="EndRecord"/>), at <see cref="CsvWriter.Flush"/> and when it
/// is disposed, each in the sync or async form the caller used. The CSV options of the writer do not apply to the
/// fields: no quoting, escapes, padding or injection protection, no null text (a null value is an empty field), and no
/// true and false text. A writer with a sink writes no comments and no parallel batches (both throw
/// <see cref="NotSupportedException"/>).
/// </para>
/// </remarks>
internal abstract class CsvCellSink
{
	/// <summary>Writes a field.</summary>
	/// <param name="utf8">
	/// The field as CSV formats it, before quoting: empty for a null value or an empty string. Valid UTF-8, except for the
	/// bytes of a UTF-8 field (<see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/>), which the writer does not validate.
	/// </param>
	/// <param name="type">
	/// The type the field was formatted from: <see cref="string"/> for text (strings, spans of chars and UTF-8, and a null
	/// string or URI), the type of a formattable value (a number, a date or time, <see cref="Guid"/>, ...) or of a nullable
	/// one that is null, <see cref="bool"/>, <see cref="Uri"/> for a URI, and the runtime type of a value of
	/// <see cref="CsvWriter.WriteField(object)"/> (<see cref="object"/> when it is null).
	/// </param>
	public abstract void WriteField(ReadOnlySpan<byte> utf8, Type type);

	/// <summary>Ends the current record, which has at least one field.</summary>
	/// <returns>
	/// Whether the sink has buffered enough to write it out: the writer then calls <see cref="Flush"/> or
	/// <see cref="FlushAsync"/> before the next record, as the caller is sync or async.
	/// </returns>
	public abstract bool EndRecord();

	/// <summary>
	/// Drops the fields written to the current record since it began: the record failed (a field count that does not
	/// match, or an exception while its fields were written), and the writer discards it.
	/// </summary>
	public abstract void DiscardRecord();

	/// <summary>Writes out what the sink has buffered (whole records).</summary>
	public abstract void Flush();

	/// <summary>Asynchronously writes out what the sink has buffered (whole records).</summary>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>A task that completes when the buffered records are written out.</returns>
	public abstract ValueTask FlushAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Finishes the output: the writer is being disposed, after it ended any open record and flushed. Called once.
	/// </summary>
	public abstract void Complete();

	/// <summary>Asynchronously finishes the output. See <see cref="Complete"/>.</summary>
	/// <returns>A task that completes when the output is finished.</returns>
	public abstract ValueTask CompleteAsync();
}
