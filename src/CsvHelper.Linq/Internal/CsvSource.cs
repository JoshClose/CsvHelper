using System;
using System.IO;
using System.Threading;

namespace CsvHelper.Linq;

/// <summary>Where a query reads its CSV from: a reader is opened each time the query runs.</summary>
internal abstract class CsvSource
{
	/// <summary>The source, as plans show it, such as <c>file 'orders.csv'</c>.</summary>
	public abstract string Description { get; }

	/// <summary>Whether the source can be read more than once, so a query over it can run again and read it twice.</summary>
	public virtual bool CanReopen => true;

	/// <summary>Whether the reader that <see cref="Open"/> returns is disposed after use; not the caller's own reader.</summary>
	public virtual bool OwnsReader => true;

	/// <summary>The path of a file source, for the changes a table makes to it; <see langword="null"/> for others.</summary>
	public virtual string? Path => null;

	/// <summary>Opens a reader positioned before the header (or the first record).</summary>
	/// <param name="options">The options of the reader.</param>
	/// <returns>The reader; dispose it when <see cref="OwnsReader"/>.</returns>
	/// <exception cref="InvalidOperationException">The source can be read once and has been.</exception>
	public abstract CsvReader Open(CsvReaderOptions options);

	/// <summary>The description of the source.</summary>
	/// <returns>Such as <c>file 'orders.csv'</c>.</returns>
	public override string ToString() => Description;

	/// <summary>A file, opened again for each query.</summary>
	public sealed class File : CsvSource
	{
		private readonly string path;

		/// <summary>Creates the source.</summary>
		/// <param name="path">The path of the file.</param>
		public File(string path) => this.path = path;

		/// <inheritdoc/>
		public override string Description => $"file '{System.IO.Path.GetFileName(path)}'";

		/// <inheritdoc/>
		public override string? Path => path;

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) => CsvReader.FromFile(path, options);
	}

	/// <summary>UTF-8 bytes in memory, read in place.</summary>
	public sealed class Bytes : CsvSource
	{
		private readonly ReadOnlyMemory<byte> utf8;

		/// <summary>Creates the source.</summary>
		/// <param name="utf8">The bytes of the CSV.</param>
		public Bytes(ReadOnlyMemory<byte> utf8) => this.utf8 = utf8;

		/// <inheritdoc/>
		public override string Description => $"{utf8.Length} bytes";

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) => CsvReader.Create(utf8, options);
	}

	/// <summary>A string.</summary>
	public sealed class Text : CsvSource
	{
		private readonly string text;

		/// <summary>Creates the source.</summary>
		/// <param name="text">The CSV.</param>
		public Text(string text) => this.text = text;

		/// <inheritdoc/>
		public override string Description => $"text of {text.Length} characters";

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) => CsvReader.FromText(text, options);
	}

	/// <summary>A stream from a factory, which is called for each query and the stream disposed after it.</summary>
	public sealed class StreamFactory : CsvSource
	{
		private readonly Func<Stream> open;

		/// <summary>Creates the source.</summary>
		/// <param name="open">Opens the stream.</param>
		public StreamFactory(Func<Stream> open) => this.open = open;

		/// <inheritdoc/>
		public override string Description => "stream";

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) =>
			CsvReader.Create(open() ?? throw new InvalidOperationException("The stream factory of the CSV query returned null."), options);
	}

	/// <summary>A text reader from a factory, which is called for each query and the reader disposed after it.</summary>
	public sealed class TextReaderFactory : CsvSource
	{
		private readonly Func<TextReader> open;

		/// <summary>Creates the source.</summary>
		/// <param name="open">Opens the text reader.</param>
		public TextReaderFactory(Func<TextReader> open) => this.open = open;

		/// <inheritdoc/>
		public override string Description => "text reader";

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) =>
			CsvReader.Create(open() ?? throw new InvalidOperationException("The text reader factory of the CSV query returned null."), options);
	}

	/// <summary>The caller's own reader, which a query reads from where it is, once, and leaves open.</summary>
	public sealed class Reader : CsvSource
	{
		private CsvReader? reader;

		/// <summary>Creates the source.</summary>
		/// <param name="reader">The reader.</param>
		public Reader(CsvReader reader) => this.reader = reader;

		/// <inheritdoc/>
		public override string Description => "reader";

		/// <inheritdoc/>
		public override bool CanReopen => false;

		/// <inheritdoc/>
		public override bool OwnsReader => false;

		/// <inheritdoc/>
		public override CsvReader Open(CsvReaderOptions options) =>
			Interlocked.Exchange(ref reader, null) ?? throw new InvalidOperationException(
				"A query over a CsvReader reads it once, and it has been read. Query a file, a stream factory or text to run a query more than once.");
	}
}
