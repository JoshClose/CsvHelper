using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#else
using System.Buffers.Text;
#endif
using System.Text;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>
/// Writes CSV as UTF-8, RFC 4180 by default. Records end with CRLF (<see cref="CsvWriterOptions.NewLine"/>); fields
/// containing the separator, a quote, CR or LF are enclosed in quotes with inner quotes doubled; every record has the same
/// number of fields. The other modes of <see cref="CsvWriterOptions.Mode"/> escape fields with an escape character, write
/// them as they are, or pad them to fixed widths. Records of <see cref="CsvRecordAttribute"/> types are written with
/// <see cref="WriteRecords{T}(System.Collections.Generic.IEnumerable{T})"/>.
/// </summary>
/// <remarks>
/// <para>
/// Fields are written into an internal buffer; I/O only happens in <see cref="EndRecord"/> (when the buffer
/// is mostly full), <see cref="Flush"/> and their async counterparts, so async callers never block.
/// </para>
/// <para>
/// A writer is not thread-safe: use it from one thread at a time, and finish each async call before starting the next.
/// Nothing checks this; fields from two threads end up mixed in the output. To write records produced on several
/// threads, write them from one thread, or lock around each whole record, from its first field to
/// <see cref="EndRecord"/>. The options and the record writers do not change, so they can be shared between threads.
/// </para>
/// </remarks>
public sealed partial class CsvWriter : IDisposable, IAsyncDisposable
{
	private readonly CsvWriterOptions options;
	private readonly IFormatProvider provider;
	private readonly byte sep;
	private readonly bool quoteAll;
	private readonly bool quoteWhitespace;  // CsvQuoting.WhenNeededOrWhitespace (custom handling)
	private readonly bool quoting;          // Rfc4180 mode
	private readonly byte[]? nullValue;     // the UTF-8 of NullValue, or null for an empty field
	private readonly byte[] trueValue;      // the UTF-8 of TrueValue and FalseValue
	private readonly byte[] falseValue;
	private readonly bool custom;           // injection handling or a ShouldQuote callback: fields go through EncodeCustom
	private readonly bool encodeQuote;      // Encode quotes (Rfc4180 mode without custom handling)
	private readonly bool encodeAll;        // every field goes through Encode: Quoting.Always, FixedWidth (padding) or custom
	private readonly bool narrowFast;       // short ASCII fields take AsciiField.TryNarrow (Rfc4180 without Quoting.Always, NoEscape)
	private readonly bool writeOther;       // Escape or FixedWidth mode, or custom: WriteFieldOther writes strings
	private readonly bool fixedWidth;       // FixedWidth mode
	private readonly ushort newLine;        // the bytes of the line ending, in memory order (the second one 0 for "\n" and "\r")
	private readonly int newLineLength;
	private readonly bool comments;         // CommentPrefix is set
	private readonly byte comment;
	private readonly bool protectComments;  // a first field that starts with the comment prefix is quoted or escaped
	private readonly bool escapedCrBeforeLf; // Escape mode with "\n" line endings: a record that ends with an escaped CR ends with CRLF
	private readonly bool checkRecord;      // FinishRecord calls ValidateRecord for every record: field counts, comments, escaped CRs
	private readonly CsvInjection injection;
	private readonly string injectionCharacters;
	private readonly byte injectionEscape;
	private readonly CsvShouldQuote? shouldQuote;
	private readonly CsvMode mode;
	private readonly byte escape;
	private readonly byte[] escapedBytes;   // Escape: the bytes that get an escape character
	private readonly int[] widths = [];     // FixedWidth: the width of each column
	private readonly bool validate;
	private readonly bool invariant;
	private readonly bool checkNumbers;
	private readonly SearchValues<byte> special;
	private readonly bool leaveOpen;
	private readonly int flushThreshold;

	private Stream? stream;
	private TextWriter? textWriter;
	private char[]? chars;
	private char[]? fieldChars;             // the text of a field, for ShouldQuote
	private byte[] buffer;
	private int pos;
	private int recordStart;
	private int fieldCount;
	private int expectedFields = -1;
	private long records;
	private bool disposed;

	private CsvWriter(CsvWriterOptions? options, bool leaveOpen)
	{
		options ??= CsvWriterOptions.Default;
		options.Validate();
		this.options = options;
		provider = options.FormatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
		sep = (byte)options.Separator;
		mode = options.Mode;
		quoting = mode == CsvMode.Rfc4180;
		quoteAll = quoting && options.Quoting == CsvQuoting.Always;
		quoteWhitespace = quoting && options.Quoting == CsvQuoting.WhenNeededOrWhitespace;
		fixedWidth = mode == CsvMode.FixedWidth;
		injection = options.Injection;
		injectionCharacters = options.InjectionCharacters;
		injectionEscape = (byte)options.InjectionEscape;
		shouldQuote = quoting ? options.ShouldQuote : null;
		custom = injection != CsvInjection.None || shouldQuote is not null || quoteWhitespace;
		nullValue = options.NullValue is { } text ? Encoding.UTF8.GetBytes(text) : null;
		trueValue = Encoding.UTF8.GetBytes(options.TrueValue);
		falseValue = Encoding.UTF8.GetBytes(options.FalseValue);
		encodeQuote = quoting && !custom;
		encodeAll = quoteAll || fixedWidth || custom;
		narrowFast = !quoteAll && !custom && mode is CsvMode.Rfc4180 or CsvMode.NoEscape;
		writeOther = mode is CsvMode.Escape or CsvMode.FixedWidth || custom;
		escape = (byte)options.Escape;
		escapedBytes = [sep, escape, (byte)'\r', (byte)'\n'];
		validate = options.ValidateFieldCount;
		var newLine = options.NewLine;
		byte[] newLineBytes = [(byte)newLine[0], newLine.Length > 1 ? (byte)newLine[1] : (byte)0];
		this.newLine = Unsafe.ReadUnaligned<ushort>(ref newLineBytes[0]);
		newLineLength = newLine.Length;
		comments = options.CommentPrefix.HasValue;
		comment = (byte)options.CommentPrefix.GetValueOrDefault();
		protectComments = comments && mode != CsvMode.NoEscape;
		escapedCrBeforeLf = mode == CsvMode.Escape && newLine == "\n";
		checkRecord = validate || protectComments || escapedCrBeforeLf;
		this.leaveOpen = leaveOpen;
		special = mode switch
		{
			CsvMode.Rfc4180 => SearchValues.Create([sep, (byte)'"', (byte)'\r', (byte)'\n']),
			CsvMode.Escape => SearchValues.Create([sep, escape, (byte)'\r', (byte)'\n']),
			_ => SearchValues.Create(ReadOnlySpan<byte>.Empty),
		};
		if (mode == CsvMode.FixedWidth)
		{
			widths = [.. options.FieldWidths!];
			expectedFields = widths.Length;
		}

		invariant = ReferenceEquals(provider, System.Globalization.CultureInfo.InvariantCulture) ||
					 ReferenceEquals(provider, System.Globalization.NumberFormatInfo.InvariantInfo);
		// Invariant numbers contain only digits, sign, '.', 'E', "NaN" and "Infinity"; check them only if the
		// separator (or the escape character) could appear in that output.
		const string NumberChars = "0123456789+-.EeNaIfinity";
		checkNumbers = NumberChars.Contains(options.Separator) || !invariant ||
					   (mode == CsvMode.Escape && NumberChars.Contains(options.Escape));
		buffer = ArrayPool<byte>.Shared.Rent(options.BufferSize);
		flushThreshold = Math.Max(buffer.Length - 4096, buffer.Length / 2);
	}

	/// <summary>Creates a writer that writes UTF-8 to a stream.</summary>
	/// <param name="stream">The destination, written from its current position.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvWriterOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether <paramref name="stream"/> stays open when the writer is disposed.</param>
	/// <returns>A writer positioned at the start of the first record.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> are not valid: a <see cref="CsvWriterOptions.Separator"/> that is not ASCII or is CR, LF
	/// or (in Rfc4180 mode) the quote, an <see cref="CsvWriterOptions.Escape"/> that cannot be used, or missing or
	/// non-positive <see cref="CsvWriterOptions.FieldWidths"/> in FixedWidth mode.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvWriterOptions.BufferSize"/> is less than 256.</exception>
	public static CsvWriter Create(Stream stream, CsvWriterOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var writer = new CsvWriter(options, leaveOpen) { stream = stream };
		if (writer.options.WriteByteOrderMark)
		{
			"﻿"u8.CopyTo(writer.buffer);
			writer.pos = writer.recordStart = 3;
		}

		return writer;
	}

	/// <summary>Creates a writer that writes to a <see cref="TextWriter"/>.</summary>
	/// <param name="textWriter">
	/// The destination. It gets UTF-16 text, which it encodes itself; <see cref="CsvWriterOptions.WriteByteOrderMark"/>
	/// does not apply to it.
	/// </param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvWriterOptions.Default"/>.</param>
	/// <param name="leaveOpen">Whether <paramref name="textWriter"/> stays open when the writer is disposed.</param>
	/// <returns>A writer positioned at the start of the first record.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="textWriter"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> are not valid: a <see cref="CsvWriterOptions.Separator"/> that is not ASCII or is CR, LF
	/// or (in Rfc4180 mode) the quote, an <see cref="CsvWriterOptions.Escape"/> that cannot be used, or missing or
	/// non-positive <see cref="CsvWriterOptions.FieldWidths"/> in FixedWidth mode.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvWriterOptions.BufferSize"/> is less than 256.</exception>
	public static CsvWriter Create(TextWriter textWriter, CsvWriterOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(textWriter);

		return new CsvWriter(options, leaveOpen) { textWriter = textWriter };
	}

	/// <summary>
	/// Creates (or overwrites) a file, which other processes can read while it is written. The writer closes the
	/// file when it is disposed. See <see cref="Create(Stream, CsvWriterOptions?, bool)"/>.
	/// </summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvWriterOptions.Default"/>.</param>
	/// <param name="fileOptions">How the file is opened.</param>
	/// <returns>A writer positioned at the start of the file.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="path"/> is empty, or <paramref name="options"/> are not valid (see
	/// <see cref="Create(Stream, CsvWriterOptions?, bool)"/>).
	/// </exception>
	/// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvWriterOptions.BufferSize"/> is less than 256.</exception>
	/// <exception cref="DirectoryNotFoundException">The directory of the file does not exist.</exception>
	/// <exception cref="UnauthorizedAccessException">The file cannot be written with the caller's permissions.</exception>
	/// <exception cref="IOException">The file cannot be created, for example because another process has it open.</exception>
	public static CsvWriter ToFile(string path, CsvWriterOptions? options = null, FileOptions fileOptions = FileOptions.None)
	{
		var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 1, fileOptions);

		return Create(stream, options, leaveOpen: false);
	}

	/// <summary>The options this writer was created with.</summary>
	public CsvWriterOptions Options => options;

	/// <summary>The number of complete records written so far (the header counts as a record).</summary>
	public long RecordCount => records;

	/// <summary>The number of fields written to the current record so far.</summary>
	public int FieldCount => fieldCount;

	/// <summary>
	/// Writes a field from UTF-16 text. A <see langword="null"/> string is written as <see cref="CsvWriterOptions.NullValue"/>,
	/// an empty field by default.
	/// </summary>
	/// <param name="value">
	/// The value. It is quoted when it contains the separator, a quote, CR or LF (or always, with
	/// <see cref="CsvQuoting.Always"/>), or escaped or padded as <see cref="CsvWriterOptions.Mode"/> says; invalid
	/// UTF-16 is replaced with U+FFFD.
	/// </param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(string? value)
	{
		// AsSpan tests for null too, so after this test the JIT drops its own: non-null strings pay nothing for it.
		if (value is null)
		{
			WriteNull(typeof(string));

			return;
		}

		WriteField(value.AsSpan());
	}

	/// <summary>Writes a field from UTF-16 text.</summary>
	/// <param name="value">
	/// The value. It is quoted when it contains the separator, a quote, CR or LF (or always, with
	/// <see cref="CsvQuoting.Always"/>), or escaped or padded as <see cref="CsvWriterOptions.Mode"/> says; invalid
	/// UTF-16 is replaced with U+FFFD.
	/// </param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(ReadOnlySpan<char> value)
	{
		// Worst case: every char takes 3 UTF-8 bytes, plus two quotes and a separator.
		var max = value.Length * 3 + 3;
		if (buffer.Length - pos < max) Grow(max);
		if (fieldCount++ > 0) buffer[pos++] = sep;
		var start = pos;
		if (value.Length <= 32 && narrowFast)
		{
			var special = AsciiField.TryNarrow(value, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), start), sep, (byte)'"');
			if (special == 0)
			{
				pos = start + value.Length;

				return;
			}

			if (special > 0)
			{
				pos = start + Encode(start, value.Length, typeof(string));

				return;
			}

			// Not ASCII: fall through to the general path, which overwrites what was written.
		}
		else if (writeOther)
		{
			// Off the RFC 4180 fast path, so its fields pay nothing for the other modes.
			WriteFieldOther(value, start);

			return;
		}

		var written = Transcode(value, start);
		pos = start + written;
		if (encodeAll || buffer.AsSpan(start, written).ContainsAny(special)) pos = start + Encode(start, written, typeof(string));
	}

	/// <summary>Writes a field from UTF-16 text, quoted as <paramref name="quoting"/> says instead of <see cref="CsvWriterOptions.Quoting"/>.</summary>
	/// <param name="value">
	/// The value; <see langword="null"/> is written as <see cref="CsvWriterOptions.NullValue"/> (an empty field, which
	/// <see cref="CsvQuoting.Always"/> quotes).
	/// </param>
	/// <param name="quoting">
	/// <see cref="CsvQuoting.Always"/> to quote the field in <see cref="CsvMode.Rfc4180"/> mode, such as an identifier
	/// with leading zeros; <see cref="CsvQuoting.WhenNeededOrWhitespace"/> to quote it also when it starts or ends with a
	/// space or a tab; <see cref="CsvQuoting.WhenNeeded"/> to quote it as the options say. The other modes ignore it.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="quoting"/> is not a <see cref="CsvQuoting"/> value.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(string? value, CsvQuoting quoting)
	{
		WriteField((value ?? options.NullValue).AsSpan(), quoting);
	}

	/// <summary>Writes a field from UTF-16 text, quoted as <paramref name="quoting"/> says instead of <see cref="CsvWriterOptions.Quoting"/>.</summary>
	/// <param name="value">The value.</param>
	/// <param name="quoting">
	/// <see cref="CsvQuoting.Always"/> to quote the field in <see cref="CsvMode.Rfc4180"/> mode, such as an identifier
	/// with leading zeros; <see cref="CsvQuoting.WhenNeededOrWhitespace"/> to quote it also when it starts or ends with a
	/// space or a tab; <see cref="CsvQuoting.WhenNeeded"/> to quote it as the options say. The other modes ignore it.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="quoting"/> is not a <see cref="CsvQuoting"/> value.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(ReadOnlySpan<char> value, CsvQuoting quoting)
	{
		if (Check(quoting) == CsvQuoting.WhenNeeded)
		{
			WriteField(value);

			return;
		}

		WriteText(value, typeof(string), quoting);
	}

	private static CsvQuoting Check(CsvQuoting quoting) => quoting is >= CsvQuoting.WhenNeeded and <= CsvQuoting.WhenNeededOrWhitespace
		? quoting
		: throw new ArgumentOutOfRangeException(nameof(quoting), quoting, "Unknown quoting.");

	/// <summary>
	/// Writes text that was formatted from a value of type <paramref name="type"/> (or is text, <see cref="string"/>), so
	/// that custom handling (injection, ShouldQuote) knows what it is, quoting it as <paramref name="quoting"/> asks on top
	/// of the options.
	/// </summary>
	private void WriteText(ReadOnlySpan<char> value, Type type, CsvQuoting quoting = CsvQuoting.WhenNeeded)
	{
		if (!custom && quoting == CsvQuoting.WhenNeeded)
		{
			WriteField(value);

			return;
		}

		// Worst case: every char takes 3 UTF-8 bytes, plus an injection escape, two quotes and a separator.
		var max = value.Length * 3 + 4;
		if (buffer.Length - pos < max)
		{
			Grow(max);
		}

		if (fieldCount++ > 0)
		{
			buffer[pos++] = sep;
		}

		var start = pos;
		var written = Transcode(value, start);
		pos = start + EncodeCustom(start, written, type, quoting);
	}

	/// <summary>Writes <paramref name="value"/> as UTF-8 at <paramref name="start"/>; the buffer must have room for it.</summary>
	/// <returns>The number of bytes written.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Transcode(ReadOnlySpan<char> value, int start)
	{
		if (Ascii.FromUtf16(value, buffer.AsSpan(start), out var written) != OperationStatus.Done)
		{
			// Non-ASCII: the ASCII prefix is already in place (one byte per char); transcode the rest.
			Utf8.FromUtf16(value.Slice(written), buffer.AsSpan(start + written), out _, out var rest, replaceInvalidSequences: true);
			written += rest;
		}

		return written;
	}

	/// <summary>
	/// <see cref="WriteField(ReadOnlySpan{char})"/> in Escape and FixedWidth mode, and with custom handling (injection,
	/// ShouldQuote), after the separator: the field starts at <paramref name="start"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void WriteFieldOther(ReadOnlySpan<char> value, int start)
	{
		if (custom)
		{
			// The buffer has room for the value with an injection escape and quotes (WriteField grew it for the worst case).
			var length = Transcode(value, start);
			pos = start + EncodeCustom(start, length, typeof(string), CsvQuoting.WhenNeeded);

			return;
		}

		if (fixedWidth)
		{
			// Fixed-width records have no separators: take back the one just written.
			if (fieldCount > 1) start--;
			pos = start;
			fieldCount--;
			WriteFixedField(value);

			return;
		}

		if (value.Length <= 32)
		{
			var escaped = AsciiField.TryNarrow(value, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), start), sep, escape);
			if (escaped >= 0)
			{
				pos = start + (escaped == 0 ? value.Length : EscapeInPlace(start, value.Length));

				return;
			}
		}

		var written = Transcode(value, start);
		pos = start + written;
		if (buffer.AsSpan(start, written).ContainsAny(special)) pos = start + EscapeInPlace(start, written);
	}

	/// <summary>Writes a field from UTF-8 bytes.</summary>
	/// <param name="utf8">
	/// The value, copied as is (it is not validated). It is quoted when it contains the separator, a quote, CR or LF
	/// (or always, with <see cref="CsvQuoting.Always"/>), or escaped or padded as <see cref="CsvWriterOptions.Mode"/>
	/// says.
	/// </param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(ReadOnlySpan<byte> utf8)
	{
		var max = utf8.Length * 2 + 3;
		if (buffer.Length - pos < max) Grow(max);
		if (fieldCount++ > 0) buffer[pos++] = sep;
		var start = pos;
		utf8.CopyTo(buffer.AsSpan(start));
		pos = start + utf8.Length;
		if (encodeAll || utf8.ContainsAny(special)) pos = start + Encode(start, utf8.Length, typeof(string));
	}

#if NET
	/// <summary>
	/// Writes a value that formats itself as UTF-8 (numbers, <see cref="Guid"/>, ...), with the configured format
	/// provider. Without a format, <see cref="double"/> and <see cref="float"/> are written as the shortest text that
	/// round-trips, and dates and times (<see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>,
	/// <see cref="TimeOnly"/>) in ISO 8601 round-trip format ("O").
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">
	/// The value. Its text is quoted when it contains the separator, a quote, CR or LF, or escaped or padded as
	/// <see cref="CsvWriterOptions.Mode"/> says.
	/// </param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T value, ReadOnlySpan<char> format = default) where T : IUtf8SpanFormattable
	{
		if (format.IsEmpty && IsDateOrTime<T>())
		{
			format = "O";
		}

		if (buffer.Length - pos < 128) Grow(128);
		if (fieldCount++ > 0) buffer[pos++] = sep;
		var start = pos;
		int written;
		// Shortest round-trip floats, byte-identical to the BCL's default invariant output (at most 24 bytes,
		// and 127 are free).
		if (typeof(T) == typeof(double) && format.IsEmpty && invariant)
			FloatFormatter.TryFormat(Unsafe.As<T, double>(ref value), buffer.AsSpan(start), out written);
		else if (typeof(T) == typeof(float) && format.IsEmpty && invariant)
			FloatFormatter.TryFormat(Unsafe.As<T, float>(ref value), buffer.AsSpan(start), out written);
		else
			while (!value.TryFormat(buffer.AsSpan(start), out written, format, provider)) Grow(buffer.Length);
		pos = start + written;
		// Numbers in their default invariant format cannot contain a special character, unless the
		// separator is one of the characters numbers use.
		var plainNumber = IsNumber<T>() && format.IsEmpty && !checkNumbers;
		if (encodeAll || (!plainNumber && buffer.AsSpan(start, written).ContainsAny(special)))
		{
			if (buffer.Length - pos < written + 2) Grow(written + 2);
			pos = start + Encode(start, written, typeof(T));
		}
	}

	/// <summary>
	/// Writes a value that formats itself as UTF-8 with the given format provider instead of
	/// <see cref="CsvWriterOptions.FormatProvider"/>, such as a culture for one column. See
	/// <see cref="WriteField{T}(T, ReadOnlySpan{char})"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <param name="provider">The format provider, or <see langword="null"/> for the writer's.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T value, ReadOnlySpan<char> format, IFormatProvider? provider) where T : IUtf8SpanFormattable
	{
		if (provider is null || ReferenceEquals(provider, this.provider))
		{
			WriteField(value, format);

			return;
		}

		if (format.IsEmpty && IsDateOrTime<T>())
		{
			format = "O";
		}

		if (buffer.Length - pos < 128)
		{
			Grow(128);
		}

		if (fieldCount++ > 0)
		{
			buffer[pos++] = sep;
		}

		var start = pos;
		int written;
		while (!value.TryFormat(buffer.AsSpan(start), out written, format, provider))
		{
			Grow(buffer.Length);
		}

		pos = start + written;
		if (encodeAll || buffer.AsSpan(start, written).ContainsAny(special))
		{
			if (buffer.Length - pos < written + 2)
			{
				Grow(written + 2);
			}

			pos = start + Encode(start, written, typeof(T));
		}
	}

	/// <summary>
	/// Writes a nullable value; <see langword="null"/> is written as <see cref="CsvWriterOptions.NullValue"/>, an empty field by default. See
	/// <see cref="WriteField{T}(T, ReadOnlySpan{char})"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value, or <see langword="null"/>.</param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T? value, ReadOnlySpan<char> format = default) where T : struct, IUtf8SpanFormattable
	{
		if (value.HasValue) WriteField(value.GetValueOrDefault(), format);
		else WriteNull(typeof(T));
	}
#else
	/// <summary>
	/// Writes a formattable value (numbers, <see cref="Guid"/>, ...). Numbers in the invariant culture without a
	/// format are written straight to UTF-8; anything else through <see cref="IFormattable.ToString(string, IFormatProvider)"/>.
	/// In the invariant culture without a format, <see cref="double"/> and <see cref="float"/> are written as the
	/// shortest text that round-trips, as on .NET Core 3.0 and later, and without a format, <see cref="DateTime"/> and
	/// <see cref="DateTimeOffset"/> in ISO 8601 round-trip format ("O").
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">
	/// The value. Its text is quoted when it contains the separator, a quote, CR or LF, or escaped or padded as
	/// <see cref="CsvWriterOptions.Mode"/> says.
	/// </param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T value, ReadOnlySpan<char> format = default) where T : IFormattable
	{
		if (!format.IsEmpty || !invariant || !IsDirectNumber<T>())
		{
			var text = format.IsEmpty ? IsDateOrTime<T>() ? "O" : null : format.ToString();
			WriteText(value.ToString(text, provider).AsSpan(), typeof(T));

			return;
		}

		if (buffer.Length - pos < 128) Grow(128);
		if (fieldCount++ > 0) buffer[pos++] = sep;
		var start = pos;
		var destination = buffer.AsSpan(start);
		int written;
		// Shortest round-trip floats, as .NET Core 3.0 and later format them (not .NET Framework's 15 digits).
		if (typeof(T) == typeof(double)) FloatFormatter.TryFormat(Unsafe.As<T, double>(ref value), destination, out written);
		else if (typeof(T) == typeof(float)) FloatFormatter.TryFormat(Unsafe.As<T, float>(ref value), destination, out written);
		else if (typeof(T) == typeof(int)) Utf8Formatter.TryFormat(Unsafe.As<T, int>(ref value), destination, out written);
		else if (typeof(T) == typeof(long)) Utf8Formatter.TryFormat(Unsafe.As<T, long>(ref value), destination, out written);
		else if (typeof(T) == typeof(short)) Utf8Formatter.TryFormat(Unsafe.As<T, short>(ref value), destination, out written);
		else if (typeof(T) == typeof(byte)) Utf8Formatter.TryFormat(Unsafe.As<T, byte>(ref value), destination, out written);
		else if (typeof(T) == typeof(uint)) Utf8Formatter.TryFormat(Unsafe.As<T, uint>(ref value), destination, out written);
		else if (typeof(T) == typeof(ulong)) Utf8Formatter.TryFormat(Unsafe.As<T, ulong>(ref value), destination, out written);
		else if (typeof(T) == typeof(ushort)) Utf8Formatter.TryFormat(Unsafe.As<T, ushort>(ref value), destination, out written);
		else Utf8Formatter.TryFormat(Unsafe.As<T, sbyte>(ref value), destination, out written);
		pos = start + written;
		// Numbers in their default invariant format cannot contain a special character, unless the separator is one
		// of the characters numbers use.
		if (encodeAll || (checkNumbers && buffer.AsSpan(start, written).ContainsAny(special)))
		{
			if (buffer.Length - pos < written + 2) Grow(written + 2);
			pos = start + Encode(start, written, typeof(T));
		}
	}

	/// <summary>
	/// Writes a formattable value with the given format provider instead of <see cref="CsvWriterOptions.FormatProvider"/>,
	/// such as a culture for one column. See <see cref="WriteField{T}(T, ReadOnlySpan{char})"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <param name="provider">The format provider, or <see langword="null"/> for the writer's.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T value, ReadOnlySpan<char> format, IFormatProvider? provider) where T : IFormattable
	{
		if (provider is null || ReferenceEquals(provider, this.provider))
		{
			WriteField(value, format);

			return;
		}

		var text = format.IsEmpty ? IsDateOrTime<T>() ? "O" : null : format.ToString();
		WriteText(value.ToString(text, provider).AsSpan(), typeof(T));
	}

	/// <summary>
	/// Writes a nullable value; <see langword="null"/> is written as <see cref="CsvWriterOptions.NullValue"/>, an empty field by default. See
	/// <see cref="WriteField{T}(T, ReadOnlySpan{char})"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value, or <see langword="null"/>.</param>
	/// <param name="format">The format string, or empty for the default format of <typeparamref name="T"/>.</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not valid for <typeparamref name="T"/>.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField<T>(T? value, ReadOnlySpan<char> format = default) where T : struct, IFormattable
	{
		if (value.HasValue) WriteField(value.GetValueOrDefault(), format);
		else WriteNull(typeof(T));
	}

	private static bool IsDirectNumber<T>() =>
		typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(int) || typeof(T) == typeof(long) ||
		typeof(T) == typeof(short) || typeof(T) == typeof(byte) || typeof(T) == typeof(uint) || typeof(T) == typeof(ulong) ||
		typeof(T) == typeof(ushort) || typeof(T) == typeof(sbyte);
#endif

	/// <summary>Writes a date and time in ISO 8601 round-trip format ("O") unless another format is given.</summary>
	/// <param name="value">The value.</param>
	/// <param name="format">The format string, or empty for "O".</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not a valid date and time format.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(DateTime value, ReadOnlySpan<char> format = default) =>
		WriteField<DateTime>(value, format.IsEmpty ? "O" : format);

	/// <summary>Writes a date and time with offset in ISO 8601 round-trip format ("O") unless another format is given.</summary>
	/// <param name="value">The value.</param>
	/// <param name="format">The format string, or empty for "O".</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not a valid date and time format.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(DateTimeOffset value, ReadOnlySpan<char> format = default) =>
		WriteField<DateTimeOffset>(value, format.IsEmpty ? "O" : format);

#if NET
	/// <summary>Writes a date as yyyy-MM-dd unless another format is given.</summary>
	/// <param name="value">The value.</param>
	/// <param name="format">The format string, or empty for "O" (yyyy-MM-dd).</param>
	/// <exception cref="FormatException"><paramref name="format"/> is not a valid date format.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(DateOnly value, ReadOnlySpan<char> format = default) =>
		WriteField<DateOnly>(value, format.IsEmpty ? "O" : format);
#endif

	/// <summary>Writes <see cref="CsvWriterOptions.TrueValue"/> or <see cref="CsvWriterOptions.FalseValue"/>: <c>True</c> or <c>False</c> by default.</summary>
	/// <param name="value">The value.</param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(bool value) => WriteUtf8(value ? trueValue : falseValue, typeof(bool));

	/// <summary>
	/// Writes UTF-8 text formatted from a value of type <paramref name="type"/> (a boolean, or a null value), which custom
	/// handling (injection, ShouldQuote) gets.
	/// </summary>
	private void WriteUtf8(ReadOnlySpan<byte> text, Type type)
	{
		if (!custom)
		{
			WriteField(text);

			return;
		}

		var max = text.Length * 2 + 4;
		if (buffer.Length - pos < max)
		{
			Grow(max);
		}

		if (fieldCount++ > 0)
		{
			buffer[pos++] = sep;
		}

		var start = pos;
		text.CopyTo(buffer.AsSpan(start));
		pos = start + EncodeCustom(start, text.Length, type, CsvQuoting.WhenNeeded);
	}

	/// <summary>
	/// Writes any value: strings and UTF-8 formattable values directly, other <see cref="IFormattable"/> values with
	/// the configured format provider, anything else via <see cref="object.ToString"/>.
	/// </summary>
	/// <param name="value">The value, or <see langword="null"/> for <see cref="CsvWriterOptions.NullValue"/> (an empty field by default).</param>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteField(object? value)
	{
		switch (value)
		{
			case null: WriteNull(typeof(object)); break;
			case string s: WriteField(s.AsSpan()); break;
			case bool b: WriteField(b); break;
			case DateTime dt: WriteField(dt); break;
			case DateTimeOffset dto: WriteField(dto); break;
#if NET
			case DateOnly d: WriteField(d); break;
			case TimeOnly t: WriteField(t); break;
#endif
			case double d: WriteField(d); break;
			case float f: WriteField(f); break;
#if NET
			case IUtf8SpanFormattable f: WriteFormattable(f); break;
#else
			case IFormattable f when IsDateOrTimeByName(f.GetType()): WriteText(f.ToString("O", provider).AsSpan(), f.GetType()); break;
#endif
			case IFormattable f: WriteText(f.ToString(null, provider).AsSpan(), f.GetType()); break;
			default: WriteText((value.ToString() ?? "").AsSpan(), value.GetType()); break;
		}
	}

#if NET
	private void WriteFormattable(IUtf8SpanFormattable value)
	{
		if (buffer.Length - pos < 128) Grow(128);
		if (fieldCount++ > 0) buffer[pos++] = sep;
		var start = pos;
		int written;
		while (!value.TryFormat(buffer.AsSpan(start), out written, default, provider)) Grow(buffer.Length);
		pos = start + written;
		if (encodeAll || buffer.AsSpan(start, written).ContainsAny(special))
		{
			if (buffer.Length - pos < written + 2) Grow(written + 2);
			pos = start + Encode(start, written, value.GetType());
		}
	}
#endif

	/// <summary>
	/// Writes a <see langword="null"/> value of type <paramref name="type"/>: <see cref="CsvWriterOptions.NullValue"/>, or an
	/// empty field.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void WriteNull(Type type) => WriteUtf8(nullValue, type);

	/// <summary>
	/// Writes a complete record: the fields (see <see cref="WriteField(string)"/>) and then <see cref="EndRecord"/>.
	/// Fields already written to the current record come first.
	/// </summary>
	/// <param name="fields">The values of the fields; a <see langword="null"/> value is written as <see cref="CsvWriterOptions.NullValue"/> (an empty field by default).</param>
	/// <exception cref="InvalidOperationException">
	/// The record has no fields, or its field count differs from the first record. The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteRecord(params ReadOnlySpan<string?> fields)
	{
		foreach (var field in fields) WriteField(field);
		EndRecord();
	}

	/// <summary>
	/// Writes a complete record and flushes asynchronously when the buffer is mostly full. See
	/// <see cref="WriteRecord(ReadOnlySpan{string})"/>.
	/// </summary>
	/// <param name="fields">The values of the fields; a <see langword="null"/> value is written as <see cref="CsvWriterOptions.NullValue"/> (an empty field by default).</param>
	/// <param name="cancellationToken">Cancels writing to the destination.</param>
	/// <returns>A task that completes when the record is in the buffer and, if the buffer was mostly full, written out.</returns>
	/// <exception cref="InvalidOperationException">
	/// The record has no fields, or its field count differs from the first record. The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask WriteRecordAsync(string?[] fields, CancellationToken cancellationToken = default)
	{
		foreach (var field in fields) WriteField(field);

		return EndRecordAsync(cancellationToken);
	}

	/// <summary>Writes a header record. It is an ordinary record: later records must have the same field count.</summary>
	/// <param name="names">The column names.</param>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="names"/> is empty, or the number of names differs from the first record. The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteHeader(params ReadOnlySpan<string> names)
	{
		foreach (var name in names) WriteField(name.AsSpan());
		EndRecord();
	}

	/// <summary>
	/// Writes a complete record of strings from an array (also for callers before C# 13, which cannot pass a
	/// <c>params</c> span). See <see cref="WriteRecord(ReadOnlySpan{string})"/>.
	/// </summary>
	/// <param name="fields">The values of the fields; a <see langword="null"/> value is written as <see cref="CsvWriterOptions.NullValue"/> (an empty field by default).</param>
	/// <exception cref="InvalidOperationException">
	/// The record has no fields, or its field count differs from the first record. The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteRecord(params string?[] fields) => WriteRecord(new ReadOnlySpan<string?>(fields));

	/// <summary>
	/// Writes a complete record of one field (see <see cref="WriteField(string)"/>) and then <see cref="EndRecord"/>.
	/// Fields already written to the current record come first.
	/// </summary>
	/// <param name="field">The value of the field; <see langword="null"/> is written as <see cref="CsvWriterOptions.NullValue"/> (an empty field by default).</param>
	/// <exception cref="InvalidOperationException">The field count differs from the first record. The record is discarded.</exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteRecord(string? field)
	{
		WriteField(field.AsSpan());
		EndRecord();
	}

#if !NET
	/// <summary>Writes a header record (for callers before C# 13, which cannot pass a <c>params</c> span).</summary>
	/// <param name="names">The column names.</param>
	/// <exception cref="InvalidOperationException">
	/// <paramref name="names"/> is empty, or the number of names differs from the first record. The record is discarded.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteHeader(params string[] names) => WriteHeader(new ReadOnlySpan<string>(names));
#endif

	/// <summary>
	/// Ends the current record with CRLF (<see cref="CsvWriterOptions.NewLine"/>). In Rfc4180 mode, a record whose only
	/// field is empty is written as <c>""</c>, so it does not read as an empty line. Writes the buffer to the destination
	/// when it is mostly full.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The record has no fields, or its field count differs from the first record, or (in FixedWidth mode with a
	/// <see cref="CsvWriterOptions.CommentPrefix"/>) its first field starts with the comment prefix. The record is discarded.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void EndRecord()
	{
		FinishRecord();
		if (pos >= flushThreshold) FlushBuffer();
	}

	/// <summary>Ends the current record, flushing asynchronously when the buffer is mostly full. See <see cref="EndRecord"/>.</summary>
	/// <param name="cancellationToken">Cancels writing to the destination.</param>
	/// <returns>A task that completes when the record is in the buffer and, if the buffer was mostly full, written out.</returns>
	/// <exception cref="InvalidOperationException">
	/// The record has no fields, or its field count differs from the first record, or (in FixedWidth mode with a
	/// <see cref="CsvWriterOptions.CommentPrefix"/>) its first field starts with the comment prefix. The record is discarded.
	/// </exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public ValueTask EndRecordAsync(CancellationToken cancellationToken = default)
	{
		FinishRecord();

		return pos >= flushThreshold ? FlushBufferAsync(cancellationToken) : default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void FinishRecord()
	{
		var count = fieldCount;
		if (count <= 1 || checkRecord) ValidateRecord(count);
		if (buffer.Length - pos < 4) Grow(4);
		WriteNewLine();
		recordStart = pos;
		fieldCount = 0;
		records++;
	}

	/// <summary>Writes the line ending at <c>pos</c>; the buffer must have room for two bytes.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void WriteNewLine()
	{
		// One 2-byte store, without bounds checks: a one-byte ending moves pos past its first byte only.
		Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), newLine);
		pos += newLineLength;
	}

	private void ValidateRecord(int count)
	{
		if (count == 0)
			throw new InvalidOperationException("A CSV record must contain at least one field.");
		if (count == 1 && pos == recordStart && quoting)
		{
			// A lone empty field would look like an empty line; RFC 4180 quoting keeps it a record.
			if (buffer.Length - pos < 2) Grow(2);
			buffer[pos++] = (byte)'"';
			buffer[pos++] = (byte)'"';
		}

		if (validate)
		{
			if (expectedFields < 0)
			{
				expectedFields = count;
			}
			else if (count != expectedFields)
			{
				// Drop the invalid record so the output stays well formed (unless part of it was already flushed).
				DiscardRecord();
				throw new InvalidOperationException(mode == CsvMode.FixedWidth
					? $"Record {records + 1} has {count} fields but FieldWidths has {expectedFields} columns " +
					  "(set ValidateFieldCount = false to allow fewer)."
					: $"Record {records + 1} has {count} fields but the first record has {expectedFields}. " +
					  "RFC 4180 requires the same number of fields in every record (set ValidateFieldCount = false to allow it).");
			}
		}

		if (protectComments)
		{
			ProtectFromComment(canThrow: true);
		}

		if (escapedCrBeforeLf)
		{
			EndEscapedCr();
		}
	}

	/// <summary>
	/// In Escape mode with "\n" line endings, ends a record whose last value ends with a CR (escaped: Escape mode escapes
	/// every CR) with CRLF instead: a reader takes an LF right after an escaped CR as an escaped CRLF, part of the value.
	/// </summary>
	private void EndEscapedCr()
	{
		if (pos > Math.Max(recordStart, 0) && buffer[pos - 1] == (byte)'\r')
		{
			if (buffer.Length - pos < 1)
			{
				Grow(1);
			}

			buffer[pos++] = (byte)'\r';
		}
	}

	/// <summary>
	/// Keeps the current record from reading as a comment when its first field starts with the comment prefix: quotes
	/// that field (Rfc4180 mode) or escapes the prefix (Escape mode). FixedWidth mode cannot do either, so it throws
	/// when <paramref name="canThrow"/> is set, discarding the record.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ProtectFromComment(bool canThrow)
	{
		var start = recordStart;
		// Unknown start: the beginning of the record was flushed (Flush in the middle of a record), and checked then.
		if (start < 0 || pos == start || buffer[start] != comment)
		{
			return;
		}

		if (quoting)
		{
			// The field is not quoted, since it starts with the prefix, so it holds no separator: it ends at the first one.
			var end = buffer.AsSpan(start, pos - start).IndexOf(sep);
			end = end < 0 ? pos : start + end;
			if (buffer.Length - pos < 2)
			{
				Grow(2);
			}

			buffer.AsSpan(end, pos - end).CopyTo(buffer.AsSpan(end + 2));
			buffer.AsSpan(start, end - start).CopyTo(buffer.AsSpan(start + 1));
			buffer[start] = (byte)'"';
			buffer[end + 1] = (byte)'"';
			pos += 2;
		}
		else if (mode == CsvMode.Escape)
		{
			if (buffer.Length - pos < 1)
			{
				Grow(1);
			}

			buffer.AsSpan(start, pos - start).CopyTo(buffer.AsSpan(start + 1));
			buffer[start] = escape;
			pos++;
		}
		else if (canThrow)
		{
			DiscardRecord();
			throw new InvalidOperationException(
				$"Record {records + 1} starts with the comment prefix '{(char)comment}', which FixedWidth mode cannot quote or escape, " +
				"so a reader would skip it as a comment. The record is discarded.");
		}
	}

	/// <summary>
	/// Writes a comment line: <see cref="CsvWriterOptions.CommentPrefix"/>, the text and the line ending. A text with
	/// line breaks becomes several comment lines, one per line. Comments are not records: they do not count in
	/// <see cref="RecordCount"/> or for the field count of records.
	/// </summary>
	/// <param name="text">The text of the comment, written as it is (without quotes or escapes); <see langword="null"/> writes the prefix alone.</param>
	/// <exception cref="InvalidOperationException">
	/// <see cref="CsvWriterOptions.CommentPrefix"/> is not set, or fields of a record have been written and the record
	/// has not ended.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteComment(string? text) => WriteComment(text.AsSpan());

	/// <summary>
	/// Writes a comment line: <see cref="CsvWriterOptions.CommentPrefix"/>, the text and the line ending. A text with
	/// line breaks becomes several comment lines, one per line. Comments are not records: they do not count in
	/// <see cref="RecordCount"/> or for the field count of records.
	/// </summary>
	/// <param name="text">The text of the comment, written as it is (without quotes or escapes).</param>
	/// <exception cref="InvalidOperationException">
	/// <see cref="CsvWriterOptions.CommentPrefix"/> is not set, or fields of a record have been written and the record
	/// has not ended.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void WriteComment(ReadOnlySpan<char> text)
	{
		ThrowIfDisposed();
		if (!comments)
		{
			throw new InvalidOperationException("Set CommentPrefix in the writer options to write comments.");
		}

		if (fieldCount > 0)
		{
			throw new InvalidOperationException("A comment cannot be written in the middle of a record: end the record first.");
		}

		while (true)
		{
			var lineBreak = text.IndexOfAny('\r', '\n');
			var line = lineBreak < 0 ? text : text.Slice(0, lineBreak);
			// The prefix, every char taking up to 3 UTF-8 bytes, and the line ending.
			var max = line.Length * 3 + 3;
			if (buffer.Length - pos < max)
			{
				Grow(max);
			}

			buffer[pos++] = comment;
			pos += Transcode(line, pos);
			WriteNewLine();
			if (lineBreak < 0)
			{
				break;
			}

			var next = lineBreak + 1;
			if (text[lineBreak] == '\r' && next < text.Length && text[next] == '\n')
			{
				next++;
			}

			text = text.Slice(next);
		}

		recordStart = pos;
	}

	private void DiscardRecord()
	{
		if (recordStart >= 0) pos = recordStart;
		fieldCount = 0;
	}

	/// <summary>
	/// Applies the mode to the <paramref name="length"/> bytes at <paramref name="start"/>, in place: quotes them,
	/// escapes them or pads them, after the custom handling of the options (injection, ShouldQuote) for a value of type
	/// <paramref name="type"/>. Returns the new length.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int Encode(int start, int length, Type type) => encodeQuote ? Quote(start, length) : EncodeOther(start, length, type);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private int EncodeOther(int start, int length, Type type) => custom ? EncodeCustom(start, length, type, CsvQuoting.WhenNeeded) : mode switch
	{
		CsvMode.Escape => EscapeInPlace(start, length),
		CsvMode.FixedWidth => PadInPlace(start, length),
		_ => length,
	};

	/// <summary>
	/// <see cref="Encode"/> with the custom handling of the options: protects text that starts like a formula
	/// (<see cref="CsvWriterOptions.Injection"/>), then quotes the field in Rfc4180 mode when it needs quotes, when
	/// <see cref="CsvWriterOptions.Quoting"/> or <paramref name="quoting"/> asks for it (every field, or fields with
	/// whitespace at either end) or when <see cref="CsvWriterOptions.ShouldQuote"/> does, or escapes or pads it in the
	/// other modes. The separator before the field has been written. Returns the new length.
	/// </summary>
	/// <param name="start">The position of the field's UTF-8 bytes in the buffer.</param>
	/// <param name="length">The number of bytes of the field.</param>
	/// <param name="type">The type of the value, <see cref="string"/> for text.</param>
	/// <param name="quoting">The quoting of this field on top of the options (<see cref="CsvQuoting.WhenNeeded"/> for none).</param>
	/// <returns>The length of the field after quoting, escaping or padding.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private int EncodeCustom(int start, int length, Type type, CsvQuoting quoting)
	{
		var always = quoteAll || quoting == CsvQuoting.Always;
		if (injection != CsvInjection.None && length > 0 && IsInjectionCharacter(buffer[start]) && IsText(type))
		{
			length = ProtectFromInjection(start, length);
			always |= injection == CsvInjection.Escape;
		}

		// Room for quotes and doubled quotes, or escapes, after the field.
		pos = start + length;
		if (buffer.Length - pos < length + 2)
		{
			Grow(length + 2);
		}

		switch (mode)
		{
			case CsvMode.Rfc4180:
				var whitespace = (quoteWhitespace || quoting == CsvQuoting.WhenNeededOrWhitespace) && length > 0 &&
								 (IsSpaceOrTab(buffer[start]) || IsSpaceOrTab(buffer[start + length - 1]));
				var quote = always || whitespace || buffer.AsSpan(start, length).ContainsAny(special) || AskShouldQuote(start, length, type);

				return quote ? Quote(start, length) : length;
			case CsvMode.Escape:
				return EscapeInPlace(start, length);
			case CsvMode.FixedWidth:
				return PadInPlace(start, length);
			default:
				return length;
		}
	}

	/// <summary>
	/// Escapes, strips or rejects the field at <paramref name="start"/>, which starts with an injection character, as
	/// <see cref="CsvWriterOptions.Injection"/> says. Returns its new length.
	/// </summary>
	private int ProtectFromInjection(int start, int length)
	{
		if (injection == CsvInjection.Throw)
		{
			var first = (char)buffer[start];
			var column = fieldCount - 1;
			DiscardRecord();
			throw new InvalidOperationException(
				$"Field {column} of record {records + 1} starts with '{Printable(first)}', so a spreadsheet would run it as a formula " +
				"(Injection = Throw). The record is discarded.");
		}

		if (injection == CsvInjection.Strip)
		{
			var skip = 1;
			while (skip < length && IsInjectionCharacter(buffer[start + skip]))
			{
				skip++;
			}

			buffer.AsSpan(start + skip, length - skip).CopyTo(buffer.AsSpan(start));

			return length - skip;
		}

		pos = start + length;
		if (buffer.Length - pos < 1)
		{
			Grow(1);
		}

		buffer.AsSpan(start, length).CopyTo(buffer.AsSpan(start + 1));
		buffer[start] = injectionEscape;

		return length + 1;
	}

	private bool IsInjectionCharacter(byte b) => b < 0x80 && injectionCharacters.IndexOf((char)b) >= 0;

	private static bool IsSpaceOrTab(byte b) => b is (byte)' ' or (byte)'\t';

	private static string Printable(char c) => c switch
	{
		'\t' => "\\t",
		'\r' => "\\r",
		_ => c.ToString(),
	};

	/// <summary>
	/// Whether a value of <paramref name="type"/> is text for <see cref="CsvWriterOptions.Injection"/>: anything but the
	/// numbers, dates, times, <see cref="Guid"/>s and booleans that the writer formats itself, which cannot be formulas.
	/// </summary>
	private static bool IsText(Type type) =>
		!(type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) || type == typeof(decimal) ||
		  type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
		  type == typeof(sbyte) || type == typeof(bool) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
		  type == typeof(TimeSpan) || type == typeof(Guid)
#if NET
		  || type == typeof(Half) || type == typeof(Int128) || type == typeof(UInt128) || type == typeof(DateOnly) || type == typeof(TimeOnly)
#endif
		 );

	/// <summary>Asks <see cref="CsvWriterOptions.ShouldQuote"/>, if it is set, whether to quote the field at <paramref name="start"/>.</summary>
	private bool AskShouldQuote(int start, int length, Type type)
	{
		if (shouldQuote is null)
		{
			return false;
		}

		// A UTF-8 field never has more chars than bytes.
		var text = fieldChars is { } existing && existing.Length >= length ? existing : fieldChars = new char[Math.Max(length, 256)];
		var count = Encoding.UTF8.GetChars(buffer, start, length, text, 0);

		return shouldQuote(new ReadOnlySpan<char>(text, 0, count), type, fieldCount - 1, records);
	}

	/// <summary>
	/// Writes the escape character before every separator, escape character, CR and LF of the <paramref name="length"/>
	/// bytes at <paramref name="start"/>, in place, growing the buffer if needed. Returns the new length.
	/// </summary>
	private int EscapeInPlace(int start, int length)
	{
		ReadOnlySpan<byte> escaped = escapedBytes;
		var count = 0;
		for (var rest = buffer.AsSpan(start, length); ;)
		{
			var k = rest.IndexOfAny(escaped);
			if (k < 0) break;
			count++;
			rest = rest.Slice(k + 1);
		}

		if (count == 0) return length;
		pos = start + length;
		if (buffer.Length - pos < count) Grow(count);
		Span<byte> b = buffer;
		// From the back: each special byte moves right by the number of escapes before it, with its escape.
		var total = length + count;
		var dst = start + total;
		var srcEnd = start + length;
		while (count > 0)
		{
			var s = start + b.Slice(start, srcEnd - start).LastIndexOfAny(escaped);
			var segment = srcEnd - s;
			dst -= segment;
			b.Slice(s, segment).CopyTo(b.Slice(dst));
			b[--dst] = escape;
			srcEnd = s;
			count--;
		}

		return total;
	}

	/// <summary>
	/// Pads the <paramref name="length"/> bytes at <paramref name="start"/> with spaces to the width of their column,
	/// growing the buffer if needed. Fixed-width records have no separators: the one written before the field (as in the
	/// other modes) is overwritten by moving the field back over it. Returns the new length, counted from
	/// <paramref name="start"/>.
	/// </summary>
	private int PadInPlace(int start, int length)
	{
		var column = fieldCount - 1;
		if (column >= widths.Length) throw TooManyFields();
		var width = widths[column];
		var field = buffer.AsSpan(start, length);
		if (field.IndexOfAny((byte)'\r', (byte)'\n') >= 0) throw LineBreakInFixedWidth(column);
		var chars = FixedWidth.Utf16Length(field);
		if (chars > width) throw TooLong(column, chars, width);
		var shift = column > 0 ? 1 : 0;
		if (shift > 0) field.CopyTo(buffer.AsSpan(start - 1));
		var padding = width - chars;
		pos = start - shift + length;
		if (buffer.Length - pos < padding) Grow(padding);
		buffer.AsSpan(pos, padding).Fill((byte)' ');

		return length + padding - shift;
	}

	/// <summary>
	/// <see cref="WriteField(ReadOnlySpan{char})"/> in FixedWidth mode: the value padded with spaces to the width of its
	/// column (which counts UTF-16 code units, as the length of the value does), without a separator.
	/// </summary>
	private void WriteFixedField(ReadOnlySpan<char> value)
	{
		var column = fieldCount;
		if (column >= widths.Length) throw TooManyFields();
		var width = widths[column];
		if (value.Length > width) throw TooLong(column, value.Length, width);
		var max = value.Length * 3 + width;
		if (buffer.Length - pos < max) Grow(max);
		var start = pos;
		// Short ASCII values: TryNarrow, given CR and LF as the characters to look for, reports line breaks.
		var narrow = value.Length <= 32
			? AsciiField.TryNarrow(value, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), start), (byte)'\r', (byte)'\n')
			: -1;
		int written;
		if (narrow >= 0)
		{
			if (narrow > 0) throw LineBreakInFixedWidth(column);
			written = value.Length;
		}
		else
		{
			if (value.IndexOfAny('\r', '\n') >= 0) throw LineBreakInFixedWidth(column);
			written = Transcode(value, start);
		}

		var padding = width - value.Length;
		buffer.AsSpan(start + written, padding).Fill((byte)' ');
		pos = start + written + padding;
		fieldCount++;
	}

	private InvalidOperationException TooManyFields()
	{
		DiscardRecord();

		return new InvalidOperationException(
			$"Record {records + 1} has more fields than the {widths.Length} columns of FieldWidths. The record is discarded.");
	}

	private InvalidOperationException LineBreakInFixedWidth(int column)
	{
		DiscardRecord();

		return new InvalidOperationException(
			$"Field {column} of record {records + 1} contains a line break, which FixedWidth mode cannot write. The record is discarded.");
	}

	private InvalidOperationException TooLong(int column, int chars, int width)
	{
		DiscardRecord();

		return new InvalidOperationException(
			$"Field {column} of record {records + 1} is {chars} characters long, but its column is {width} wide. The record is discarded.");
	}

	/// <summary>
	/// Encloses the <paramref name="length"/> bytes at <paramref name="start"/> in quotes, doubling inner quotes,
	/// in place. The buffer must have room for the expansion. Returns the new length.
	/// </summary>
	private int Quote(int start, int length)
	{
		Span<byte> b = buffer;
		var quotes = b.Slice(start, length).Count((byte)'"');
		var total = length + quotes + 2;
		var dst = start + total - 1;
		b[dst] = (byte)'"';
		var srcEnd = start + length;
		while (quotes > 0)
		{
			var q = start + b.Slice(start, srcEnd - start).LastIndexOf((byte)'"');
			var segment = srcEnd - (q + 1);
			dst -= segment;
			b.Slice(q + 1, segment).CopyTo(b.Slice(dst));
			b[--dst] = (byte)'"';
			b[--dst] = (byte)'"';
			srcEnd = q;
			quotes--;
		}

		var rest = srcEnd - start;
		dst -= rest;
		b.Slice(start, rest).CopyTo(b.Slice(dst));
		b[start] = (byte)'"';

		return total;
	}

	// Dates and times, which are written in ISO 8601 round-trip format ("O") when no format is given. Inlined so that
	// the checks fold to a constant: a call for every number was 1.5% slower.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsDateOrTime<T>() =>
		typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset)
#if NET
		|| typeof(T) == typeof(DateOnly) || typeof(T) == typeof(TimeOnly)
#else
		|| DateOrTimeByName<T>.Value
#endif
		;

#if !NET
	/// <summary>
	/// Whether <typeparamref name="T"/> is <c>DateOnly</c> or <c>TimeOnly</c>, which this build cannot name but the .NET
	/// 6+ runtimes that run it have (.NET 8 and 9 apps use the netstandard2.1 build). Computed once per type.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	private static class DateOrTimeByName<T>
	{
		/// <summary>Whether <typeparamref name="T"/> is the runtime's <c>DateOnly</c> or <c>TimeOnly</c>.</summary>
		public static readonly bool Value = IsDateOrTimeByName(typeof(T));
	}

	/// <summary>
	/// Whether <paramref name="type"/> is the runtime's <c>DateOnly</c> or <c>TimeOnly</c>: by name, in the core library
	/// (not a polyfill package's type of the same name on .NET Framework).
	/// </summary>
	/// <param name="type">The type of the value.</param>
	/// <returns>Whether the type is <c>System.DateOnly</c> or <c>System.TimeOnly</c> of .NET 6 or later.</returns>
	private static bool IsDateOrTimeByName(Type type) =>
		type.Assembly == typeof(object).Assembly && type.FullName is "System.DateOnly" or "System.TimeOnly";
#endif

	private static bool IsNumber<T>() =>
		typeof(T) == typeof(int) || typeof(T) == typeof(long) || typeof(T) == typeof(double) || typeof(T) == typeof(float) ||
		typeof(T) == typeof(decimal) || typeof(T) == typeof(short) || typeof(T) == typeof(byte) || typeof(T) == typeof(uint) ||
		typeof(T) == typeof(ulong) || typeof(T) == typeof(ushort) || typeof(T) == typeof(sbyte)
#if NET
		|| typeof(T) == typeof(Half) || typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128)
#endif
		;

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Grow(int needed)
	{
		// After Dispose the buffer is empty, so every write ends up here.
		ThrowIfDisposed();
		// Records are kept whole in the buffer (no I/O mid-record), so grow instead of flushing.
		var size = Math.Max((long)buffer.Length * 2, (long)pos + needed + 1024);
		if (size > Array.MaxLength) throw new InvalidOperationException("The CSV record is too large to buffer.");
		var bigger = ArrayPool<byte>.Shared.Rent((int)size);
		buffer.AsSpan(0, pos).CopyTo(bigger);
		ArrayPool<byte>.Shared.Return(buffer);
		buffer = bigger;
	}

	/// <summary>
	/// Writes buffered records to the destination and flushes it. The fields of an unfinished record are written too.
	/// </summary>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	public void Flush()
	{
		ThrowIfDisposed();
		FlushBuffer();
		stream?.Flush();
		textWriter?.Flush();
	}

	/// <summary>Asynchronously writes buffered records to the destination and flushes it. See <see cref="Flush"/>.</summary>
	/// <param name="cancellationToken">Cancels writing to the destination.</param>
	/// <returns>A task that completes when the destination has been flushed.</returns>
	/// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		await FlushBufferAsync(cancellationToken).ConfigureAwait(false);
		if (stream is not null) await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
		if (textWriter is not null) await textWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	private void FlushBuffer()
	{
		PrepareFlush();
		var n = pos;
		if (n == 0) return;
		if (stream is not null)
		{
			stream.Write(buffer, 0, n);
		}
		else if (textWriter is not null)
		{
			// Flushes happen between fields, so the bytes never end inside a UTF-8 sequence.
			this.chars ??= new char[Math.Max(buffer.Length, 1024)];
			if (this.chars.Length < n) this.chars = new char[n];
			var chars = Encoding.UTF8.GetChars(buffer, 0, n, this.chars, 0);
			textWriter.Write(this.chars, 0, chars);
		}

		ShiftAfterFlush(n);
	}

	private async ValueTask FlushBufferAsync(CancellationToken cancellationToken)
	{
		PrepareFlush();
		var n = pos;
		if (n == 0) return;
		if (stream is not null)
		{
			await stream.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
		}
		else if (textWriter is not null)
		{
			this.chars ??= new char[Math.Max(buffer.Length, 1024)];
			if (this.chars.Length < n) this.chars = new char[n];
			var chars = Encoding.UTF8.GetChars(buffer, 0, n, this.chars, 0);
			await textWriter.WriteAsync(this.chars.AsMemory(0, chars), cancellationToken).ConfigureAwait(false);
		}

		ShiftAfterFlush(n);
	}

	private void PrepareFlush()
	{
		// The start of an unfinished record is about to be written out, and EndRecord cannot change it afterwards.
		if (fieldCount > 0 && protectComments)
		{
			ProtectFromComment(canThrow: false);
		}
	}

	private void ShiftAfterFlush(int flushed)
	{
		// Everything buffered was written. If an unfinished record already had bytes (Flush called in the
		// middle of a record), mark its start as unknown (-1).
		recordStart = fieldCount > 0 && recordStart < flushed ? -1 : 0;
		pos = 0;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

	/// <summary>
	/// Ends an unfinished record (without checking its field count), writes the buffer to the destination, flushes it
	/// and disposes it, unless the writer was created with <c>leaveOpen</c>. Calling it again does nothing.
	/// </summary>
	public void Dispose()
	{
		if (disposed) return;
		try
		{
			if (fieldCount > 0) TerminateOpenRecord();
			FlushBuffer();
			stream?.Flush();
			textWriter?.Flush();
		}
		finally
		{
			disposed = true;
			Release();
			if (!leaveOpen)
			{
				stream?.Dispose();
				textWriter?.Dispose();
			}
		}
	}

	/// <summary>
	/// Ends an unfinished record (without checking its field count), asynchronously writes the buffer to the
	/// destination, flushes it and disposes it, unless the writer was created with <c>leaveOpen</c>. Calling it again
	/// does nothing.
	/// </summary>
	/// <returns>A task that completes when the destination has been flushed and, unless left open, disposed.</returns>
	public async ValueTask DisposeAsync()
	{
		if (disposed) return;
		try
		{
			if (fieldCount > 0) TerminateOpenRecord();
			await FlushBufferAsync(default).ConfigureAwait(false);
			if (stream is not null) await stream.FlushAsync().ConfigureAwait(false);
			if (textWriter is not null) await textWriter.FlushAsync().ConfigureAwait(false);
		}
		finally
		{
			disposed = true;
			Release();
			if (!leaveOpen)
			{
				if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false);
				if (textWriter is not null) await textWriter.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	private void TerminateOpenRecord()
	{
		// Fields were written without EndRecord: finish the record (no field count check while disposing).
		if (fieldCount == 1 && pos == recordStart && quoting)
		{
			if (buffer.Length - pos < 2) Grow(2);
			buffer[pos++] = (byte)'"';
			buffer[pos++] = (byte)'"';
		}

		if (protectComments)
		{
			ProtectFromComment(canThrow: false);
		}

		if (escapedCrBeforeLf)
		{
			EndEscapedCr();
		}

		if (buffer.Length - pos < 2) Grow(2);
		WriteNewLine();
		fieldCount = 0;
		records++;
	}

	private void Release()
	{
		if (buffer.Length > 0) ArrayPool<byte>.Shared.Return(buffer);
		buffer = [];
		pos = 0;
	}
}
