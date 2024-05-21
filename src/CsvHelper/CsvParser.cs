using CsvHelper.Configuration;
using System.Buffers;
using System.Collections.Concurrent;

namespace CsvHelper;

/// <summary>
/// Parses a CSV file.
/// </summary>
public class CsvParser : IParser, IDisposable
{
	private TextReader reader;
	private CsvParserOptions options;
	private bool isDisposed;
	private int rowNumber;
	private bool leaveOpen;

	private CsvParserState state;

	/// <summary>
	/// Gets the field at the given index.
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	public ReadOnlySpan<char> this[int index] => state[index];

	/// <summary>
	/// Gets the row number the parser is currently on.
	/// </summary>
	public int Row => rowNumber;

	/// <summary>
	/// Gets the current row.
	/// </summary>
	public IRow Current => state;

	/// <summary>
	/// Initializes a new instance of CsvParser.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <param name="leaveOpen">If set to <c>true</c>, leave the reader open when CsvParser is disposed.</param>
	public CsvParser(TextReader reader, bool leaveOpen = false) : this(reader, options => options, leaveOpen) { }

	/// <summary>
	/// Initializes a new instance of CsvParser.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <param name="configure">Function to configure the parser.</param>
	/// <param name="leaveOpen">If set to <c>true</c>, leave the reader open when CsvParser is disposed.</param>
	public CsvParser(TextReader reader, Func<CsvParserOptions, CsvParserOptions> configure, bool leaveOpen = false)
	{
		this.reader = reader;
		this.leaveOpen = leaveOpen;

		options = configure(new CsvParserOptions());
		options.Validate();

		switch (options.Mode)
		{
			case CsvMode.RFC4180:
				options.ModeParse = ModeRfc4180.Parse;
				break;
			case CsvMode.Escape:
				options.ModeParse = ModeEscape.Parse;
				break;
			case CsvMode.NoEscape:
				options.ModeParse = ModeNoEscape.Parse;
				break;
			default:
				throw new ConfigurationException($"Mode {options.Mode} is not supported.");
		}

		options.ParsingStrategyImplementation = ParsingStrategyFactory.Create(options);

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

	/// <summary>
	/// Moves to the next record.
	/// </summary>
	/// <returns><c>true</c> if there are more records to read, otherwise <c>false</c>.</returns>
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

	/// <summary>
	/// Gets the enumerator.
	/// </summary>
	public CsvParser GetEnumerator() => this;

	/// <summary>
	/// Gets an enumerable that will parse the CSV asynchronously.
	/// </summary>
	/// <typeparam name="TRecord"></typeparam>
	/// <param name="createRecord"></param>
	/// <returns></returns>
	public IEnumerable<TRecord> AsParallel<TRecord>(Func<IRow, TRecord> createRecord)
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

	/// <inheritdoc />
	protected virtual void Dispose(bool isDisposing)
	{
		if (isDisposed)
		{
			return;
		}

		if (isDisposing)
		{
			// Dispose managed state (managed objects)
			state.Dispose();
			if (!leaveOpen)
			{
				reader.Dispose();
			}
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
