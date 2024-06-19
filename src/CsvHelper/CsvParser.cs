using CsvHelper.Configuration;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CsvHelper;

/// <summary>
/// Parses a CSV file.
/// </summary>
public class CsvParser : IParser, IDisposable
{
	private TextReader reader;
	private CsvOptions options;
	private bool isDisposed;
	private int rowNumber;
	private bool leaveOpen;
	private bool detectDelimiter;
	private bool delimiterDetected;

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
	/// <param name="options">Options for the parser.</param>
	/// <param name="leaveOpen">If set to <c>true</c>, leave the reader open when CsvParser is disposed.</param>
	public CsvParser(TextReader reader, CsvOptions? options, bool leaveOpen = false)
	{
		this.reader = reader;
		this.leaveOpen = leaveOpen;
		this.options = options ?? new CsvOptions();

		this.options.Validate();

		detectDelimiter = this.options.DetectDelimiter;

		switch (this.options.Mode)
		{
			case CsvMode.RFC4180:
				this.options.ModeParse = ModeRfc4180.Parse;
				break;
			case CsvMode.Escape:
				this.options.ModeParse = ModeEscape.Parse;
				break;
			case CsvMode.NoEscape:
				this.options.ModeParse = ModeNoEscape.Parse;
				break;
			default:
				throw new ConfigurationException($"Mode {this.options.Mode} is not supported.");
		}

		this.options.ParsingStrategyImplementation = ParsingStrategyFactory.Create(this.options);

		if (this.options.CacheFields)
		{
			var stringCache = new StringCache();
			this.options = this.options with
			{
				StringCreator = (chars, _) => stringCache.GetString(chars)
			};
		}

		state = new CsvParserState(reader, this.options);
	}

	/// <summary>
	/// Initializes a new instance of CsvParser.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <param name="configure">Action to configure options for the parser.</param>
	/// <param name="leaveOpen">If set to <c>true</c>, leave the reader open when CsvParser is disposed.</param>
	public CsvParser(TextReader reader, Action<IParserOptions>? configure, bool leaveOpen = false) : this(reader, o => { configure?.Invoke(o); return o; }, leaveOpen) { }

	private CsvParser(TextReader reader, Func<IParserOptions, IParserOptions>? configure, bool leaveOpen = false) : this(reader, (CsvOptions?)configure?.Invoke(new CsvOptions()), leaveOpen) { }

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

			DetectDelimiter();

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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DetectDelimiter()
	{
		if (delimiterDetected)
		{
			return;
		}

		delimiterDetected = true;

		if (!detectDelimiter)
		{
			return;
		}

		var args = new GetDelimiterArgs(state.buffer.AsSpan(), options);
		options.Delimiter = options.GetDelimiter(args);
	}
}
