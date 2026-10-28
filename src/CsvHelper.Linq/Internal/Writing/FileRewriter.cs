using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Linq;

/// <summary>What a rewrite does with each record of a file: keep it, remove it, or replace its bytes.</summary>
internal abstract class RowChange
{
	/// <summary>Prepares for the records of a reader, whose header has been read: creates the mappers.</summary>
	/// <param name="reader">The reader.</param>
	public abstract void Start(CsvReader reader);

	/// <summary>Decides what happens to the current record.</summary>
	/// <param name="reader">The reader, on the record.</param>
	/// <param name="replacement">
	/// The bytes that replace the record (without a line break), or <see langword="null"/> to remove it with its line break.
	/// </param>
	/// <returns>Whether the record changes; <see langword="false"/> keeps it as it is.</returns>
	public abstract bool TryChange(CsvReader reader, out byte[]? replacement);
}

/// <summary>
/// Rewrites a CSV file: the records that a <see cref="RowChange"/> changes are removed or replaced, and everything else
/// (the byte order mark, the header, comments, blank lines, the records that do not change) is copied byte for byte. The
/// new file is written next to the old one and replaces it when it is complete, so a failure leaves the file as it was.
/// </summary>
internal static class FileRewriter
{
	/// <summary>Rewrites a file.</summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options to read it with: UTF-8 (or no encoding, which reads UTF-8).</param>
	/// <param name="change">What happens to each record.</param>
	/// <param name="useAsync">Whether to read and write asynchronously; when <see langword="false"/>, the task completes synchronously.</param>
	/// <param name="cancellationToken">Cancels the rewrite, which leaves the file as it was.</param>
	/// <returns>The number of records removed or replaced.</returns>
	/// <exception cref="NotSupportedException">The file is not UTF-8.</exception>
	public static async ValueTask<long> RewriteAsync(string path, CsvReaderOptions options, RowChange change, bool useAsync, CancellationToken cancellationToken)
	{
		CheckUtf8(path, options);
		var full = Path.GetFullPath(path);
		var temporary = Path.Combine(Path.GetDirectoryName(full)!, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
		var fileOptions = useAsync ? FileOptions.Asynchronous | FileOptions.SequentialScan : FileOptions.SequentialScan;
		long changed = 0;
		try
		{
			using (var source = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, fileOptions))
			using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, fileOptions))
			using (var reader = CsvReader.FromFile(full, options))
			{
				if (useAsync)
				{
					await reader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
				}
				else
				{
					reader.ReadHeader();
				}

				change.Start(reader);
				var copied = 0L;
				while (useAsync ? await reader.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read())
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!change.TryChange(reader, out var replacement))
					{
						continue;
					}

					var start = reader.ByteOffset;
					var end = start + reader.RawRecord.Length;
					await CopyAsync(source, output, copied, start, useAsync, cancellationToken).ConfigureAwait(false);
					if (replacement is null)
					{
						// A removed record goes with its line break.
						end += await LineBreakAsync(source, end, useAsync, cancellationToken).ConfigureAwait(false);
					}
					else if (useAsync)
					{
						await output.WriteAsync(replacement, 0, replacement.Length, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						output.Write(replacement, 0, replacement.Length);
					}

					copied = end;
					changed++;
				}

				await CopyAsync(source, output, copied, source.Length, useAsync, cancellationToken).ConfigureAwait(false);
				if (useAsync)
				{
					await output.FlushAsync(cancellationToken).ConfigureAwait(false);
				}
			}

			if (changed > 0)
			{
				File.Replace(temporary, full, null);
			}
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}

		return changed;
	}

	/// <summary>Rewrites only UTF-8 files, whose byte offsets are the reader's.</summary>
	private static void CheckUtf8(string path, CsvReaderOptions options)
	{
		if (options.Encoding is { } encoding && encoding.CodePage != Encoding.UTF8.CodePage)
		{
			throw new NotSupportedException($"Only UTF-8 files can be changed in place; the reader's encoding is {encoding.WebName}.");
		}

		var start = new byte[4];
		int read;
		using (var stream = File.OpenRead(path))
		{
			read = stream.Read(start, 0, start.Length);
		}

		// The byte order marks of UTF-16 and UTF-32.
		if (read >= 2 && ((start[0] == 0xFF && start[1] == 0xFE) || (start[0] == 0xFE && start[1] == 0xFF) ||
						  (read == 4 && start[0] == 0 && start[1] == 0 && start[2] == 0xFE && start[3] == 0xFF)))
		{
			throw new NotSupportedException("Only UTF-8 files can be changed in place; the file is UTF-16 or UTF-32.");
		}
	}

	/// <summary>Copies the bytes of <paramref name="source"/> from <paramref name="from"/> up to <paramref name="to"/>.</summary>
	private static async ValueTask CopyAsync(Stream source, Stream output, long from, long to, bool useAsync, CancellationToken cancellationToken)
	{
		source.Position = from;
		var buffer = new byte[(int)Math.Min(Math.Max(to - from, 1), 64 * 1024)];
		var remaining = to - from;
		while (remaining > 0)
		{
			var count = (int)Math.Min(buffer.Length, remaining);
			var read = useAsync
				? await source.ReadAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false)
				: source.Read(buffer, 0, count);
			if (read == 0)
			{
				break;
			}

			if (useAsync)
			{
				await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				output.Write(buffer, 0, read);
			}

			remaining -= read;
		}
	}

	/// <summary>The length of the line break at <paramref name="position"/>: 2 for CR LF, 1 for LF or CR, 0 at the end of the file.</summary>
	private static async ValueTask<int> LineBreakAsync(Stream source, long position, bool useAsync, CancellationToken cancellationToken)
	{
		source.Position = position;
		var bytes = new byte[2];
		var read = useAsync ? await source.ReadAsync(bytes, 0, 2, cancellationToken).ConfigureAwait(false) : source.Read(bytes, 0, 2);
		if (read == 0)
		{
			return 0;
		}

		if (bytes[0] == '\r')
		{
			return read == 2 && bytes[1] == '\n' ? 2 : 1;
		}

		return bytes[0] == '\n' ? 1 : 0;
	}
}

/// <summary>Removes the records that match a condition.</summary>
/// <typeparam name="T">The type of the records.</typeparam>
internal sealed class DeleteRows<T> : RowChange
{
	private readonly Func<T, bool> predicate;
	private readonly IReadOnlyCollection<string>? members;
	private CsvRecordMapper<T>? mapper;

	/// <summary>Creates the change.</summary>
	/// <param name="predicate">The condition of the records to remove.</param>
	/// <param name="members">The members that the condition reads, or <see langword="null"/> for all of them.</param>
	public DeleteRows(Func<T, bool> predicate, IReadOnlyCollection<string>? members)
	{
		this.predicate = predicate;
		this.members = members;
	}

	/// <inheritdoc/>
	public override void Start(CsvReader reader) =>
		mapper = members is null ? CsvRecordMapper.Create<T>(reader) : CsvRecordMapper.CreateProjection<T>(reader, members);

	/// <inheritdoc/>
	public override bool TryChange(CsvReader reader, out byte[]? replacement)
	{
		replacement = null;

		// A record that cannot be read, and that OnRecordError skips, is kept.
		return reader.TryGetRecord(mapper!, out var record) && predicate(record);
	}
}

/// <summary>
/// The fields of records encoded as a writer writes them, and the columns of the file they belong to: an updated record
/// keeps the bytes of each field whose value the update did not change, so its formatting, quoting and the columns that
/// the record type does not map stay as they are.
/// </summary>
internal abstract class FieldPatch : RowChange
{
	private byte[] separator = [];
	private CsvReaderOptions scratchOptions = CsvReaderOptions.Default;

	/// <summary>The options of the writer that encodes the fields.</summary>
	protected CsvWriterOptions WriterOptions { get; private set; } = CsvWriterOptions.Default;

	/// <summary>Takes the file's separator, mode and escape for the writer.</summary>
	/// <param name="reader">The reader of the file.</param>
	/// <param name="options">The writer options to start from.</param>
	/// <exception cref="NotSupportedException">The file is fixed-width, or quotes with another character than <c>"</c>.</exception>
	protected void StartFields(CsvReader reader, CsvWriterOptions options)
	{
		var readerOptions = reader.Options;
		if (readerOptions.Mode == CsvMode.FixedWidth)
		{
			throw new NotSupportedException("Records of fixed-width files cannot be changed in place.");
		}

		if (readerOptions.Mode == CsvMode.Rfc4180 && readerOptions.Quote != '"')
		{
			throw new NotSupportedException($"Records of files quoted with {readerOptions.Quote} cannot be changed in place: the writer quotes with \".");
		}

		WriterOptions = options with
		{
			Separator = reader.Separator,
			Mode = readerOptions.Mode,
			Escape = readerOptions.Escape,
			HasHeader = false,
			WriteByteOrderMark = false,
			NewLine = "\n",
		};
		separator = Encoding.UTF8.GetBytes([reader.Separator]);
		scratchOptions = new CsvReaderOptions
		{
			HasHeader = false,
			Separator = reader.Separator,
			Mode = readerOptions.Mode,
			Escape = readerOptions.Escape,
			AllowLineBreaksInQuotes = true,
		};
	}

	/// <summary>Reads back the records that a writer wrote: the decoded value and the raw bytes of each field.</summary>
	/// <param name="written">The records, as the writer wrote them.</param>
	/// <returns>The fields of each record.</returns>
	protected List<(string Value, byte[] Raw)[]> ReadBack(byte[] written)
	{
		var records = new List<(string, byte[])[]>();
		using var reader = CsvReader.Create(written, scratchOptions);
		while (reader.Read())
		{
			var fields = new (string, byte[])[reader.FieldCount];
			for (var i = 0; i < fields.Length; i++)
			{
				fields[i] = (reader.GetString(i), reader.GetRawSpan(i).ToArray());
			}

			records.Add(fields);
		}

		return records;
	}

	/// <summary>
	/// The bytes of the current record with some fields replaced: each file column keeps its raw bytes, unless
	/// <paramref name="replacements"/> has a field for it.
	/// </summary>
	/// <param name="reader">The reader, on the record.</param>
	/// <param name="replacements">The raw bytes of the changed fields, by file column.</param>
	/// <returns>The record, without a line break.</returns>
	protected byte[] Patch(CsvReader reader, Dictionary<int, byte[]> replacements)
	{
		var count = Math.Max(reader.FieldCount, replacements.Count == 0 ? 0 : replacements.Keys.Max() + 1);
		var record = new MemoryStream();
		for (var column = 0; column < count; column++)
		{
			if (column > 0)
			{
				record.Write(separator, 0, separator.Length);
			}

			if (replacements.TryGetValue(column, out var raw))
			{
				record.Write(raw, 0, raw.Length);
			}
			else if (column < reader.FieldCount)
			{
				var field = reader.GetRawSpan(column).ToArray();
				record.Write(field, 0, field.Length);
			}
		}

		return record.ToArray();
	}
}

/// <summary>Replaces the records that match a condition by what an update makes of them.</summary>
/// <typeparam name="T">The type of the records.</typeparam>
[RequiresUnreferencedCode(Trimming.Unreferenced)]
internal sealed class UpdateRows<[DynamicallyAccessedMembers(RecordWriters.Members)] T> : FieldPatch
{
	private readonly Func<T, bool> predicate;
	private readonly Func<T, T> update;
	private readonly CsvWriterOptions options;
	private CsvRecordMapper<T>? mapper;
	private CsvRecordWriter<T>? recordWriter;
	private int[] columns = [];
	private string[] names = [];

	/// <summary>Creates the change.</summary>
	/// <param name="predicate">The condition of the records to update.</param>
	/// <param name="update">Makes the new record of a record; it may change the record it gets and return it.</param>
	/// <param name="options">The options of the writer that encodes the changed fields.</param>
	public UpdateRows(Func<T, bool> predicate, Func<T, T> update, CsvWriterOptions options)
	{
		this.predicate = predicate;
		this.update = update;
		this.options = options;
	}

	/// <inheritdoc/>
	public override void Start(CsvReader reader)
	{
		StartFields(reader, options);
		mapper = CsvRecordMapper.Create<T>(reader);
		recordWriter = RecordWriters.For<T>() ?? CsvRecordWriter.Create<T>();
		names = Appender.HeaderOf(recordWriter, WriterOptions);
		// The file column of each column the writer writes: by name with a header, else by position.
		columns = names.Select((name, i) => reader.Options.HasHeader ? (reader.TryGetFieldIndex(name, out var index) ? index : -1) : i).ToArray();
	}

	/// <inheritdoc/>
	public override bool TryChange(CsvReader reader, out byte[]? replacement)
	{
		replacement = null;
		if (!reader.TryGetRecord(mapper!, out var record) || !predicate(record))
		{
			return false;
		}

		// The record is encoded before the update, which may change it.
		var written = new MemoryStream();
		using (var writer = CsvWriter.Create(written, WriterOptions, leaveOpen: true))
		{
			recordWriter!.Write(writer, record);
			writer.EndRecord();
			recordWriter.Write(writer, update(record) ?? throw new InvalidOperationException("The update returned null."));
			writer.EndRecord();
		}

		var read = ReadBack(written.ToArray());
		var (before, after) = (read[0], read[1]);
		var replacements = new Dictionary<int, byte[]>();
		for (var i = 0; i < after.Length && i < columns.Length; i++)
		{
			if (before[i].Value == after[i].Value)
			{
				continue;
			}

			if (columns[i] < 0)
			{
				throw new InvalidOperationException($"The update changes {names[i]}, which the file has no column for.");
			}

			replacements[columns[i]] = after[i].Raw;
		}

		replacement = Patch(reader, replacements);

		return true;
	}
}

/// <summary>Replaces the dynamic rows that match a condition by what an update makes of their values.</summary>
internal sealed class UpdateDynamicRows : FieldPatch
{
	private readonly Func<CsvDynamicRecord, bool> predicate;
	private readonly Action<IDictionary<string, string?>> update;
	private readonly CsvWriterOptions options;
	private CsvRecordMapper<CsvDynamicRecord>? mapper;

	/// <summary>Creates the change.</summary>
	/// <param name="predicate">The condition of the rows to update.</param>
	/// <param name="update">Changes the values of a row, by column name.</param>
	/// <param name="options">The options of the writer that encodes the changed fields.</param>
	public UpdateDynamicRows(Func<CsvDynamicRecord, bool> predicate, Action<IDictionary<string, string?>> update, CsvWriterOptions options)
	{
		this.predicate = predicate;
		this.update = update;
		this.options = options;
	}

	/// <inheritdoc/>
	public override void Start(CsvReader reader)
	{
		StartFields(reader, options);
		mapper = CsvRecordMapper.Create<CsvDynamicRecord>(reader);
	}

	/// <inheritdoc/>
	public override bool TryChange(CsvReader reader, out byte[]? replacement)
	{
		replacement = null;
		var row = reader.GetRecord(mapper!);
		if (!predicate(row))
		{
			return false;
		}

		var before = row.ToDictionary();
		var after = new Dictionary<string, string?>(before);
		update(after);
		var changed = after.Where(pair => !before.TryGetValue(pair.Key, out var value) || value != pair.Value).ToList();
		var replacements = new Dictionary<int, byte[]>();
		if (changed.Count > 0)
		{
			var written = new MemoryStream();
			using (var writer = CsvWriter.Create(written, WriterOptions, leaveOpen: true))
			{
				foreach (var pair in changed)
				{
					writer.WriteField(pair.Value);
				}

				writer.EndRecord();
			}

			var fields = ReadBack(written.ToArray())[0];
			for (var i = 0; i < changed.Count; i++)
			{
				var name = changed[i].Key;
				var column = reader.Options.HasHeader ? (reader.TryGetFieldIndex(name, out var index) ? index : -1) : ColumnOf(name);
				if (column < 0)
				{
					throw new InvalidOperationException($"The update sets {name}, which the file has no column for.");
				}

				replacements[column] = fields[i].Raw;
			}
		}

		replacement = Patch(reader, replacements);

		return true;
	}

	// Without a header, the columns are Field1, Field2 and so on.
	private static int ColumnOf(string name) =>
		name.StartsWith("Field", StringComparison.Ordinal) && int.TryParse(name.Substring(5), out var number) && number > 0 ? number - 1 : -1;
}
