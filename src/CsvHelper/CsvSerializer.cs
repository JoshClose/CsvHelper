using CsvHelper.Configuration;

namespace CsvHelper;

public class CsvSerializer : IDisposable
{
	private TextWriter writer;
	private CsvSerializerOptions options;
	private CsvSerializerState state;
	private bool isDisposed;

	public int Row => state.row;

	public CsvSerializer(TextWriter writer) : this(writer, options => options) { }

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

	public void Write(ReadOnlySpan<char> field)
	{
		state.Write(field);
	}

	public void MoveNext()
	{
		state.MoveNext();
	}

	public void Flush()
	{
		state.Flush();
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(isDisposing: true);
		GC.SuppressFinalize(this);
	}

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
