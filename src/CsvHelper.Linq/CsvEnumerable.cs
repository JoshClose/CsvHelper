using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>
/// Writes the results of any query (of CSV, of a database through Entity Framework Core, or in memory) as CSV: records
/// of <see cref="CsvRecordAttribute"/> types with their generated writers, dictionaries and dynamic records by their keys,
/// and anything else, such as anonymous types, tuples and entities, by their public members of simple types.
/// </summary>
/// <remarks>
/// Writing records of types without a generated writer uses reflection on their public members, which
/// <see cref="DynamicallyAccessedMembersAttribute"/> keeps for trimming and native AOT.
/// </remarks>
public static class CsvEnumerable
{
	/// <summary>Writes records to a new CSV file, with a header (unless the options say otherwise), replacing any file at <paramref name="path"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/> (<see cref="CsvWriterOptions.For{T}"/>).</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static long WriteCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, string path, CsvWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);
		using var writer = CsvWriter.ToFile(path, options ?? CsvWriterOptions.For<T>());

		return Write(records, writer);
	}

	/// <summary>Writes records to a stream, as UTF-8, with a header (unless the options say otherwise).</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="stream">The stream.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="leaveOpen">Whether to leave the stream open; it is flushed either way.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static long WriteCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, Stream stream, CsvWriterOptions? options = null,
		bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(stream);
		using var writer = CsvWriter.Create(stream, options ?? CsvWriterOptions.For<T>(), leaveOpen);

		return Write(records, writer);
	}

	/// <summary>Writes records to a text writer, with a header (unless the options say otherwise).</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="textWriter">The text writer.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="leaveOpen">Whether to leave the text writer open; it is flushed either way.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="textWriter"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static long WriteCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, TextWriter textWriter,
		CsvWriterOptions? options = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(textWriter);
		using var writer = CsvWriter.Create(textWriter, options ?? CsvWriterOptions.For<T>(), leaveOpen);

		return Write(records, writer);
	}

	/// <summary>
	/// Writes records to a CSV writer, with a header first when nothing has been written yet and its options have one. The
	/// writer stays open, and is not flushed.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="writer">The writer.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="writer"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static long WriteCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, CsvWriter writer)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(writer);

		return Write(records, writer);
	}

	/// <summary>
	/// Writes records to a new CSV file asynchronously, replacing any file at <paramref name="path"/>. Records that can be
	/// read asynchronously (the results of CSV queries and of Entity Framework Core) are.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the file.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static async ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, string path,
		CsvWriterOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);
		var writer = CsvWriter.ToFile(path, options ?? CsvWriterOptions.For<T>(), FileOptions.Asynchronous);
		await using (writer.ConfigureAwait(false))
		{
			return await WriteAsync(records, writer, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Writes an asynchronous sequence of records to a new CSV file asynchronously, replacing any file at <paramref name="path"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the file.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	[OverloadResolutionPriority(1)]
	public static async ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IAsyncEnumerable<T> records, string path,
		CsvWriterOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);
		var writer = CsvWriter.ToFile(path, options ?? CsvWriterOptions.For<T>(), FileOptions.Asynchronous);
		await using (writer.ConfigureAwait(false))
		{
			return await WriteAsync(records, writer, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Writes records to a stream asynchronously, as UTF-8. Records that can be read asynchronously are.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="stream">The stream.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="leaveOpen">Whether to leave the stream open; it is flushed either way.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the stream.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static async ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, Stream stream,
		CsvWriterOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(stream);
		var writer = CsvWriter.Create(stream, options ?? CsvWriterOptions.For<T>(), leaveOpen);
		await using (writer.ConfigureAwait(false))
		{
			return await WriteAsync(records, writer, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Writes an asynchronous sequence of records to a stream asynchronously, as UTF-8.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="stream">The stream.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/>.</param>
	/// <param name="leaveOpen">Whether to leave the stream open; it is flushed either way.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the stream.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	[OverloadResolutionPriority(1)]
	public static async ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IAsyncEnumerable<T> records, Stream stream,
		CsvWriterOptions? options = null, bool leaveOpen = false, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(stream);
		var writer = CsvWriter.Create(stream, options ?? CsvWriterOptions.For<T>(), leaveOpen);
		await using (writer.ConfigureAwait(false))
		{
			return await WriteAsync(records, writer, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Writes records to a CSV writer asynchronously, with a header first when nothing has been written yet and its options
	/// have one. Records that can be read asynchronously are. The writer stays open; full buffers are written out.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="writer">The writer.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="writer"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	public static ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, CsvWriter writer,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(writer);

		return WriteAsync(records, writer, cancellationToken);
	}

	/// <summary>Writes an asynchronous sequence of records to a CSV writer asynchronously. The writer stays open.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="writer">The writer.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing.</param>
	/// <returns>The number of records written, not counting the header.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="writer"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has nothing to write, or its records cannot be written.</exception>
	[OverloadResolutionPriority(1)]
	public static ValueTask<long> WriteCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IAsyncEnumerable<T> records, CsvWriter writer,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(writer);

		return WriteAsync(records, writer, cancellationToken);
	}

	/// <summary>
	/// Adds records to the end of a CSV file, creating it (with a header) when it does not exist or is empty. The records
	/// are written with the file's separator and line break, after a line break when the file does not end with one, and
	/// must be written with the header the file has.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">
	/// The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/> with the file's separator
	/// and line break.
	/// </param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The file's header is not the one that the records are written with, or <typeparamref name="T"/> has nothing to write.
	/// </exception>
	public static long AppendCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, string path, CsvWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);

		return AppendCsv(records, path, options, null);
	}

	/// <summary>Adds records to the end of a CSV file asynchronously; see <see cref="AppendCsv{T}(IEnumerable{T}, string, CsvWriterOptions)"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/> with the file's separator and line break.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the file.</param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The file's header is not the one that the records are written with, or <typeparamref name="T"/> has nothing to write.
	/// </exception>
	public static ValueTask<long> AppendCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IEnumerable<T> records, string path,
		CsvWriterOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);

		return AppendCsvAsync(records, null, path, options, null, cancellationToken);
	}

	/// <summary>Adds an asynchronous sequence of records to the end of a CSV file asynchronously; see <see cref="AppendCsv{T}(IEnumerable{T}, string, CsvWriterOptions)"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="records">The records, enumerated once.</param>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of <typeparamref name="T"/> with the file's separator and line break.</param>
	/// <param name="cancellationToken">Cancels reading the records and writing the file.</param>
	/// <returns>The number of records added.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="records"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// The file's header is not the one that the records are written with, or <typeparamref name="T"/> has nothing to write.
	/// </exception>
	[OverloadResolutionPriority(1)]
	public static ValueTask<long> AppendCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(this IAsyncEnumerable<T> records, string path,
		CsvWriterOptions? options = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(records);
		ArgumentNullException.ThrowIfNull(path);

		return AppendCsvAsync(null, records, path, options, null, cancellationToken);
	}

	/// <summary>Adds records to the end of a file, reading its header with <paramref name="readerOptions"/>.</summary>
	internal static long AppendCsv<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(IEnumerable<T> records, string path, CsvWriterOptions? options,
		CsvReaderOptions? readerOptions)
	{
		var target = Target<T>(path, options, readerOptions);
		var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
		stream.Write(target.Prefix, 0, target.Prefix.Length);
		using var writer = CsvWriter.Create(stream, target.Options);

		return Write(records, writer);
	}

	/// <summary>Adds records (of one of the two sequences) to the end of a file asynchronously, reading its header with <paramref name="readerOptions"/>.</summary>
	internal static async ValueTask<long> AppendCsvAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(IEnumerable<T>? records,
		IAsyncEnumerable<T>? asyncRecords, string path, CsvWriterOptions? options, CsvReaderOptions? readerOptions, CancellationToken cancellationToken)
	{
		var target = Target<T>(path, options, readerOptions);
		var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
		await stream.WriteAsync(target.Prefix, 0, target.Prefix.Length, cancellationToken).ConfigureAwait(false);
		var writer = CsvWriter.Create(stream, target.Options);
		await using (writer.ConfigureAwait(false))
		{
			return asyncRecords is not null
				? await WriteAsync(asyncRecords, writer, cancellationToken).ConfigureAwait(false)
				: await WriteAsync(records!, writer, cancellationToken).ConfigureAwait(false);
		}
	}

	private static AppendTarget Target<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(string path, CsvWriterOptions? options, CsvReaderOptions? readerOptions) =>
		Appender.Prepare(path, options, CsvWriterOptions.For<T>(), readerOptions, writerOptions =>
		{
			if (RecordWriters.For<T>() is { } recordWriter)
			{
				return Appender.HeaderOf(recordWriter, writerOptions);
			}

			// The generated writer's header; dynamic records have theirs only with the first record.
			return CsvRecordWriter.IsRegistered<T>() ? Appender.HeaderOf(CsvRecordWriter.Create<T>(), writerOptions) : null;
		});

	private static long Write<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(IEnumerable<T> records, CsvWriter writer)
	{
		var header = Header(writer);
		var before = writer.RecordCount;
		if (RecordWriters.For<T>() is { } recordWriter)
		{
			writer.WriteRecords(records, recordWriter);
		}
		else
		{
			writer.WriteRecords(records);
		}

		return Count(writer, before, header);
	}

	private static async ValueTask<long> WriteAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(IEnumerable<T> records, CsvWriter writer,
		CancellationToken cancellationToken)
	{
		// The results of CSV queries and of Entity Framework Core are read asynchronously too.
		if (records is IAsyncEnumerable<T> asynchronous)
		{
			return await WriteAsync(asynchronous, writer, cancellationToken).ConfigureAwait(false);
		}

		if (records is IAsyncSource<T> source)
		{
			return await WriteAsync(new AsyncSource<T>(source), writer, cancellationToken).ConfigureAwait(false);
		}

		var header = Header(writer);
		var before = writer.RecordCount;
		if (RecordWriters.For<T>() is { } recordWriter)
		{
			await writer.WriteRecordsAsync(records, recordWriter, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await writer.WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
		}

		return Count(writer, before, header);
	}

	private static async ValueTask<long> WriteAsync<[DynamicallyAccessedMembers(RecordWriters.Members)] T>(IAsyncEnumerable<T> records, CsvWriter writer,
		CancellationToken cancellationToken)
	{
		var header = Header(writer);
		var before = writer.RecordCount;
		if (RecordWriters.For<T>() is { } recordWriter)
		{
			await writer.WriteRecordsAsync(records, recordWriter, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			await writer.WriteRecordsAsync(records, cancellationToken).ConfigureAwait(false);
		}

		return Count(writer, before, header);
	}

	// Whether the writer writes a header before the records: nothing has been written yet, and its options have one.
	private static bool Header(CsvWriter writer) => writer.Options.HasHeader && writer.RecordCount == 0 && writer.FieldCount == 0;

	// The header counts as a record of the writer; dynamic records write it only with the first record.
	private static long Count(CsvWriter writer, long before, bool header) => Math.Max(0, writer.RecordCount - before - (header ? 1 : 0));

	/// <summary>A CSV query read asynchronously, for writers that take an <see cref="IAsyncEnumerable{T}"/>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	private sealed class AsyncSource<T> : IAsyncEnumerable<T>
	{
		private readonly IAsyncSource<T> source;

		/// <summary>Creates the sequence.</summary>
		/// <param name="source">The query.</param>
		public AsyncSource(IAsyncSource<T> source) => this.source = source;

		/// <summary>Runs the query asynchronously.</summary>
		/// <param name="cancellationToken">Cancels the reading.</param>
		/// <returns>An enumerator of the results.</returns>
		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => source.GetAsyncEnumerator(cancellationToken);
	}
}

/// <summary>
/// A sequence that can also be read asynchronously, without being an <see cref="IAsyncEnumerable{T}"/>: a
/// <see cref="CsvQuery{T}"/>, which is not one so that the LINQ operators of <see cref="System.Linq.Queryable"/> and of
/// .NET 10's <c>AsyncEnumerable</c> are not both candidates for it.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
internal interface IAsyncSource<out T>
{
	/// <summary>Reads the sequence asynchronously.</summary>
	/// <param name="cancellationToken">Cancels the reading.</param>
	/// <returns>An enumerator of the elements.</returns>
	IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);
}
