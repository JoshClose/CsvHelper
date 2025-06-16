using CsvHelper.Configuration;
using System.Buffers;
using System.Collections.Concurrent;

namespace CsvHelper;

/// <summary>
/// Parser for CSV files.
/// </summary>
public sealed class CsvParser : IParser, IDisposable
{
	private TextReader reader;
	private CsvParserOptions options;
	private bool isDisposed;
	private int rowNumber;

	private CsvParserState state;

	/// <inheritdoc />
	public int Row => rowNumber;

	/// <inheritdoc />
	public IParserRow Current => state;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvParser"/> class with the specified <see cref="TextReader"/> and optional configuration.
	/// </summary>
	/// <param name="reader">The <see cref="TextReader"/>.</param>
	/// <param name="configure">A configration function.</param>
	/// <exception cref="NotSupportedException">Throws when the <see cref="CsvMode"/> is not supported.</exception>
	public CsvParser(TextReader reader, Func<CsvParserOptions, CsvParserOptions>? configure = null) : this(reader, configure?.Invoke(new CsvParserOptions()) ?? new CsvParserOptions()) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvParser"/> class with the specified <see cref="TextReader"/> and parsing options.
	/// </summary>
	/// <param name="reader">The <see cref="TextReader"/> instance used to read the CSV data. This parameter cannot be <see langword="null"/>.</param>
	/// <param name="options">The <see cref="CsvParserOptions"/> that define the parsing behavior.</param>
	/// <exception cref="NotSupportedException">Throws when the <see cref="CsvMode"/> is not supported.</exception>
	public CsvParser(TextReader reader, CsvParserOptions options)
	{
		this.reader = reader;
		this.options = options;

		options.Validate();

		switch (options.Mode)
		{
			case CsvMode.RFC4180:
				options.ModeParse = Rfc4180Mode.Parse;
				break;
			case CsvMode.Escape:
				options.ModeParse = EscapeMode.Parse;
				break;
			case CsvMode.NoEscape:
				options.ModeParse = NoEscapeMode.Parse;
				break;
			default:
				throw new NotSupportedException($"Mode {options.Mode} is not supported.");
		}

		options.ParsingStrategyImplementation = StrategyFactory.Create(options);

		if (options.CacheFields)
		{
			var stringCache = new StringCache();
			options = options with
			{
				StringCreator = (chars, _) => stringCache.GetString(chars)
			};
		}

		state = new CsvParserState(reader, options);
	}

	/// <inheritdoc />
	public bool MoveNext()
	{
		if (state.charsRead == 0)
		{
			return false;
		}

		if (!state.NextRow())
		{
			var charsRead = state.CopyRemainingFrom(state);
			if (charsRead == 0)
			{
				return false;
			}

			state.Parse();
			state.NextRow();
		}

		rowNumber++;

		return true;
	}

	/// <inheritdoc />
	public IParser GetEnumerator() => this;

	/// <inheritdoc />
	public IEnumerable<TRecord> AsParallel<TRecord>(Func<IParserRow, TRecord> createRecord)
	{
		var usedStates = new ConcurrentBag<CsvParserState>();
		var stateRecords = GetStates(usedStates)
			.AsParallel()
			.AsOrdered()
			.Select(state =>
			{
				var records = ArrayPool<TRecord>.Shared.Rent(state.parsedRowsCount);
				var count = 0;
				while (state.NextRow())
				{
					records[count] = createRecord(state);
					count++;
				}

				usedStates.Add(state);

				return (records, count);
			});

		foreach (var (records, count) in stateRecords)
		{
			for (var i = 0; i < count; i++)
			{
				yield return records[i];
			}

			ArrayPool<TRecord>.Shared.Return(records);
		}
	}

	private IEnumerable<CsvParserState> GetStates(ConcurrentBag<CsvParserState> usedStates)
	{
		var options = this.options;
		if (options.CacheFields)
		{
			var stringCreator = new StringColumnCache();
			options = options with
			{
				StringCreator = stringCreator.GetString
			};
		}

		var prevState = new CsvParserState(reader, options);

		while (true)
		{
			if (!usedStates.TryTake(out var state))
			{
				state = new CsvParserState(reader, options with
				{
					BufferSize = prevState.buffer.Length,
					ParsedRowsSize = prevState.parsedRows.Length,
					ParsedFieldsSize = prevState.parsedFields.Length,
				});
			}

			if (state.CopyRemainingFrom(prevState) == 0)
			{
				break;
			}

			state.Parse();

			prevState = state;

			yield return state;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(isDisposing: true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool isDisposing)
	{
		if (isDisposed)
		{
			return;
		}

		if (isDisposing)
		{
			// Dispose managed state (managed objects)
			state.Dispose();
			reader.Dispose();
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
