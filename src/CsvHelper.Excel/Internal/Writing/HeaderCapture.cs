using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// A cell sink that keeps the fields of the record written to it: the names that a record writer's
/// <see cref="CsvRecordWriter{T}.WriteHeader"/> writes, for a sheet without a header (<see cref="MemberFormats"/>), which
/// say which member each column is.
/// </summary>
internal sealed class HeaderCapture : CsvCellSink
{
	private readonly List<string> names = [];

	/// <summary>The names that a type's record writer writes in its header, in column order.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <returns>
	/// The names, or <see langword="null"/> when the type has no record writer, or one that can't be created (writing its
	/// records then throws, and says why).
	/// </returns>
	public static IReadOnlyList<string>? Of<T>()
	{
		CsvRecordWriter<T> recordWriter;
		try
		{
			recordWriter = CsvRecordWriter.Create<T>();
		}
		catch (InvalidOperationException)
		{
			return null;
		}

		var capture = new HeaderCapture();
		using (var writer = new CsvWriter(capture, null))
		{
			recordWriter.WriteHeader(writer);
		}

		return capture.names;
	}

	/// <summary>Keeps a field's text.</summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="type">The type it was formatted from.</param>
	public override void WriteField(ReadOnlySpan<byte> utf8, Type type) => names.Add(Encoding.UTF8.GetString(utf8.ToArray()));

	/// <summary>Ends the record.</summary>
	/// <returns><see langword="false"/>: nothing is written out.</returns>
	public override bool EndRecord() => false;

	/// <summary>Drops the record's fields.</summary>
	public override void DiscardRecord() => names.Clear();

	/// <summary>Does nothing: nothing is written out.</summary>
	public override void Flush()
	{
	}

	/// <summary>Does nothing: nothing is written out.</summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>A completed task.</returns>
	public override ValueTask FlushAsync(CancellationToken cancellationToken) => default;

	/// <summary>Does nothing: there is no output to finish.</summary>
	public override void Complete()
	{
	}

	/// <summary>Does nothing: there is no output to finish.</summary>
	/// <returns>A completed task.</returns>
	public override ValueTask CompleteAsync() => default;
}
