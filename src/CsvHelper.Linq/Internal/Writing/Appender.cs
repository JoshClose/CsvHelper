using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Linq;

/// <summary>How records are added to the end of a CSV file: the options of the writer, and what goes in front of them.</summary>
/// <param name="Options">The options of the writer: the file's separator and line break, and a header only for a new file.</param>
/// <param name="Prefix">The line break that a file which does not end with one needs before the first new record.</param>
internal sealed record AppendTarget(CsvWriterOptions Options, byte[] Prefix);

/// <summary>Works out how to add records to the end of a CSV file so that they fit what is there.</summary>
internal static class Appender
{
	private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

	/// <summary>
	/// Works out how to add records to a file: a new or empty file gets the header; a file with records keeps its separator
	/// (found from its header, unless the options set one) and its line break, gets one at its end first if it has none
	/// there, and must have the header that the records would be written with. The records are written as
	/// <paramref name="readerOptions"/> read the file, when they are given: with its mode, quote, escape character, column
	/// widths and comment prefix. Otherwise the writer's options say how the file is written, and its header is read
	/// with them.
	/// </summary>
	/// <param name="path">The path of the file.</param>
	/// <param name="options">The options of the writer, or <see langword="null"/> for those of the records' type, with the file's separator.</param>
	/// <param name="defaults">The options of the writer when <paramref name="options"/> is <see langword="null"/>.</param>
	/// <param name="readerOptions">The options that a table reads the file with, or <see langword="null"/> for none.</param>
	/// <param name="header">The names of the columns that the records are written with, or <see langword="null"/> when they are not known in advance.</param>
	/// <returns>How to add the records.</returns>
	/// <exception cref="InvalidOperationException">The file's header is not the one that the records are written with.</exception>
	public static AppendTarget Prepare(string path, CsvWriterOptions? options, CsvWriterOptions defaults, CsvReaderOptions? readerOptions, Func<CsvWriterOptions, string[]?> header)
	{
		var writerOptions = options ?? defaults;
		if (readerOptions is not null)
		{
			writerOptions = writerOptions with
			{
				Separator = readerOptions.Separator ?? writerOptions.Separator,
				Mode = readerOptions.Mode,
				Quote = readerOptions.Quote,
				Escape = readerOptions.Escape,
				FieldWidths = readerOptions.Mode == CsvMode.FixedWidth ? readerOptions.FieldWidths : writerOptions.FieldWidths,
				CommentPrefix = readerOptions.CommentPrefix,
			};
		}

		var file = new FileInfo(path);
		if (!file.Exists || file.Length == 0)
		{
			return new AppendTarget(writerOptions, []);
		}

		var start = new byte[3];
		int read;
		using (var stream = file.OpenRead())
		{
			read = stream.Read(start, 0, start.Length);
		}

		// A file with only a byte order mark has no records yet: the header comes after the mark.
		if (file.Length == 3 && read == 3 && start.AsSpan().SequenceEqual(Bom))
		{
			return new AppendTarget(writerOptions with { WriteByteOrderMark = false }, []);
		}

		var (lineBreak, endsWithLineBreak) = LineBreaks(path);
		var readOptions = readerOptions is not null
			? readerOptions with { HasHeader = writerOptions.HasHeader }
			: ReadAs(writerOptions, options is not null ? options.Separator : null) with { HasHeader = writerOptions.HasHeader };
		using (var reader = CsvReader.FromFile(path, readOptions))
		{
			var existing = writerOptions.HasHeader ? reader.Headers : null;
			if (!writerOptions.HasHeader)
			{
				reader.ReadHeader();
			}

			writerOptions = writerOptions with
			{
				// A fixed-width reader has no separator ('\0'), which the header's reader would refuse.
				Separator = writerOptions.Mode == CsvMode.FixedWidth ? writerOptions.Separator : reader.Separator,
				NewLine = options is null && lineBreak is not null ? lineBreak : writerOptions.NewLine,
				HasHeader = false,
				WriteByteOrderMark = false,
			};
			if (existing is not null && header(writerOptions) is { } names && !Same(existing, names))
			{
				throw new InvalidOperationException(
					$"The header of '{path}' is {string.Join(",", existing)}, but the records are written as {string.Join(",", names)}.");
			}
		}

		var prefix = endsWithLineBreak ? [] : Encoding.UTF8.GetBytes(writerOptions.NewLine);

		return new AppendTarget(writerOptions, prefix);
	}

	/// <summary>Whether a header is the one that records are written with: the same names, ignoring case and surrounding spaces, as headers are matched.</summary>
	private static bool Same(IReadOnlyList<string> existing, string[] names) =>
		existing.Count == names.Length &&
		existing.Zip(names, (a, b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase)).All(same => same);

	/// <summary>The first line break of a file (<c>"\r\n"</c>, <c>"\n"</c> or <c>"\r"</c>, or <see langword="null"/> without one), and whether it ends with one.</summary>
	private static (string? LineBreak, bool EndsWithLineBreak) LineBreaks(string path)
	{
		using var stream = File.OpenRead(path);
		var buffer = new byte[64 * 1024];
		string? lineBreak = null;
		int read;
		while (lineBreak is null && (read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			for (var i = 0; i < read; i++)
			{
				if (buffer[i] == '\n')
				{
					lineBreak = "\n";
					break;
				}

				if (buffer[i] == '\r')
				{
					// A CR at the end of the buffer needs the next byte to tell CR from CR LF.
					var next = i + 1 < read ? buffer[i + 1] : stream.ReadByte();
					lineBreak = next == '\n' ? "\r\n" : "\r";
					break;
				}
			}
		}

		stream.Seek(-1, SeekOrigin.End);
		var last = stream.ReadByte();

		return (lineBreak, last is '\n' or '\r');
	}

	/// <summary>The names of the columns that a record writer writes, as text: its header, written and read back.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="recordWriter">The record writer.</param>
	/// <param name="options">The options of the writer.</param>
	/// <returns>The names.</returns>
	public static string[] HeaderOf<T>(CsvRecordWriter<T> recordWriter, CsvWriterOptions options)
	{
		var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, options with { HasHeader = true, WriteByteOrderMark = false }, leaveOpen: true))
		{
			recordWriter.WriteHeader(writer);
			writer.EndRecord();
		}

		using var reader = CsvReader.FromText(text.ToString(), ReadAs(options, options.Separator));

		return reader.Read() ? Enumerable.Range(0, reader.FieldCount).Select(reader.GetString).ToArray() : [];
	}

	/// <summary>The options of a reader that reads what a writer with <paramref name="options"/> writes, without a header.</summary>
	/// <param name="options">The options of the writer.</param>
	/// <param name="separator">The separator, or <see langword="null"/> to detect it.</param>
	/// <returns>The options of the reader.</returns>
	private static CsvReaderOptions ReadAs(CsvWriterOptions options, char? separator) => new()
	{
		HasHeader = false,
		Separator = separator,
		Mode = options.Mode,
		Quote = options.Quote,
		Escape = options.Escape,
		FieldWidths = options.FieldWidths,
		CommentPrefix = options.CommentPrefix,
	};
}
