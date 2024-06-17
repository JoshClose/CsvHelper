using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// Serializes objects into CSV records.
/// </summary>
public class CsvSerializer : IDisposable
{
	private TextWriter writer;
	private CsvSerializerOptions options;
	private CsvSerializerState state;
	private bool isDisposed;

	/// <summary>
	/// Current row number.
	/// </summary>
	public int Row => state.row;

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvSerializer"/> class.
	/// </summary>
	/// <param name="writer">The writer.</param>
	public CsvSerializer(TextWriter writer) : this(writer, options => options) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvSerializer"/> class.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="configureOptions">Configure options.</param>
	/// <exception cref="NotSupportedException"></exception>
	public CsvSerializer(TextWriter writer, Func<CsvSerializerOptions, CsvSerializerOptions> configureOptions)
	{
		this.writer = writer;

		options = configureOptions(new CsvSerializerOptions());
		options.Validate();

		switch (options.Mode)
		{
			case CsvMode.RFC4180:
				options.ModeEscape = ModeRfc4180.Escape;
				break;
			case CsvMode.Escape:
				options.ModeEscape = ModeEscape.Escape;
				break;
			case CsvMode.NoEscape:
				options.ModeEscape = ModeNoEscape.Escape;
				break;
			default:
				throw new NotSupportedException($"Mode {options.Mode} is not supported.");
		}

		state = new CsvSerializerState(writer, options);
	}

	/// <summary>
	/// Writes the given field.
	/// </summary>
	/// <param name="field">The field to write.</param>
	public void Write(ReadOnlySpan<char> field)
	{
		state.Write(field);
	}

	/// <summary>
	/// Moves to the next record.
	/// </summary>
	public void MoveNext()
	{
		state.MoveNext();
	}

	/// <summary>
	/// Flushes the buffer to the writer.
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
			state.Flush();
			state.Dispose();
			writer.Dispose();
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
