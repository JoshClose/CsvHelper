using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// Serialier for CSV files.
/// </summary>
public class CsvSerializer : IDisposable
{
	private TextWriter writer;
	private CsvSerializerOptions options;
	private CsvSerializerState state;
	private bool isDisposed;

	/// <summary>
	/// The current 1 based row number.
	/// </summary>
	public int Row => state.row;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvSerializer"/> class with the specified <see cref="TextWriter"/> and optional configuration.
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/>.</param>
	/// <param name="configure">Configuration function.</param>
	public CsvSerializer(TextWriter writer, Func<CsvSerializerOptions, CsvSerializerOptions> configure) : this(writer, configure?.Invoke(new CsvSerializerOptions()) ?? new CsvSerializerOptions()) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvSerializer"/> class with the specified <see cref="TextWriter"/>.
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/> to write to.</param>
	/// <param name="options">The <see cref="CsvSerializerOptions"/> that define the serializing behavior.</param>
	public CsvSerializer(TextWriter writer, CsvSerializerOptions options)
	{
		this.writer = writer;
		this.options = options;

		options.Validate();

		switch (options.Mode)
		{
			case CsvMode.RFC4180:
				options.ModeEscape = Rfc4180Mode.Escape;
				break;
			case CsvMode.Escape:
				options.ModeEscape = EscapeMode.Escape;
				break;
			case CsvMode.NoEscape:
				options.ModeEscape = NoEscapeMode.Escape;
				break;
			default:
				throw new NotSupportedException($"Mode {options.Mode} is not supported.");
		}

		state = new CsvSerializerState(writer, options);
	}

	/// <summary>
	/// Writes a field to the buffer.
	/// </summary>
	/// <param name="field"></param>
	public void Write(ReadOnlySpan<char> field)
	{
		state.Write(field);
	}

	/// <summary>
	/// Moves to the next row.
	/// </summary>
	public void MoveNext()
	{
		state.MoveNext();
	}

	/// <summary>
	/// Flushes the buffer to the underlying <see cref="TextWriter"/>.
	/// </summary>
	public void Flush()
	{
		state.Flush();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(isDisposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the resources used by the current instance of the class.
	/// </summary>
	/// <param name="isDisposing">A value indicating if disposing is happening.</param>
	protected virtual void Dispose(bool isDisposing)
	{
		if (isDisposed)
		{
			return;
		}

		if (isDisposing)
		{
			// Dispose managed state (managed objects)
			state.Flush();
			state.Dispose();
			writer.Dispose();
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
