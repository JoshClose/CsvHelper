using System;

namespace CsvHelper;

/// <summary>
/// Thrown when CSV input is malformed and the reader was configured to reject it, and when a record cannot be read as
/// its <see cref="CsvRecordAttribute"/> type (the cause is then the <see cref="Exception.InnerException"/>).
/// </summary>
public sealed class CsvFormatException : FormatException
{
	/// <summary>Creates an exception that is not tied to a position in the input.</summary>
	/// <param name="message">The message that describes the problem.</param>
	public CsvFormatException(string message) : base(message) { }

	/// <summary>Creates an exception for a problem at a known record and byte offset.</summary>
	/// <param name="message">The message that describes the problem.</param>
	/// <param name="recordNumber">The 1-based number of the record (header included) where the problem was found.</param>
	/// <param name="byteOffset">The offset, in bytes of UTF-8 input, near where the problem was found.</param>
	public CsvFormatException(string message, long recordNumber, long byteOffset) : base(message)
	{
		RecordNumber = recordNumber;
		ByteOffset = byteOffset;
	}

	/// <summary>Creates an exception for a record that could not be read, caused by <paramref name="innerException"/>.</summary>
	/// <param name="message">The message that describes the problem.</param>
	/// <param name="recordNumber">The 1-based number of the record (header included).</param>
	/// <param name="byteOffset">The offset, in bytes of UTF-8 input, where the record starts.</param>
	/// <param name="innerException">The exception that the field or member threw.</param>
	public CsvFormatException(string message, long recordNumber, long byteOffset, Exception? innerException) : base(message, innerException)
	{
		RecordNumber = recordNumber;
		ByteOffset = byteOffset;
	}

	/// <summary>
	/// The 1-based number of the record where the problem was found, counting the header and the records returned
	/// before it (not skipped records, empty lines or comments); 0 when the problem is not tied to a record.
	/// </summary>
	public long RecordNumber { get; }

	/// <summary>The offset, in bytes of UTF-8 input, near where the problem was found, or 0 when it is not known.</summary>
	public long ByteOffset { get; }

	/// <summary>
	/// The 1-based line of the input where the record with the problem starts, when
	/// <see cref="CsvReaderOptions.TrackLineNumbers"/> is on; 0 otherwise.
	/// </summary>
	public long LineNumber { get; init; }

	/// <summary>
	/// For a record that could not be read as its <see cref="CsvRecordAttribute"/> type, the member whose field could not
	/// be read, as error messages name it (<c>Order.Price</c>); <see langword="null"/> for malformed input, and when no
	/// member was at fault on its own (a constructor or a setter threw).
	/// </summary>
	public string? Member { get; init; }

	/// <summary>
	/// The 0-based index of the field that <see cref="Member"/> reads, or -1 when it is not known (malformed input, no
	/// member, or a member that reads several fields, such as a nested record).
	/// </summary>
	public int FieldIndex { get; init; } = -1;
}
