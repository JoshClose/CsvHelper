using CsvHelper.Configuration;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace CsvHelper;

internal class CsvSerializerState : IDisposable
{
	internal CsvModeEscape modeEscape;
	internal CsvSerializerOptions options;
	internal TextWriter writer;
	internal char[] escape;
	internal char[] delimiter;
	internal char[] newLine;
	internal char[] specialChars;

	internal char[] buffer;
	internal int bufferPosition;
	internal int row = 1;
	internal int fieldCount = 0;
	private bool isDisposed;

	internal CsvSerializerState(TextWriter writer, CsvSerializerOptions options)
	{
		this.options = options;
		this.writer = writer;
		modeEscape = options.ModeEscape;

		buffer = ArrayPool<char>.Shared.Rent(options.BufferSize);

		escape = [options.Escape];
		delimiter = [options.Delimiter];
		newLine = options.NewLine.HasValue ? [options.NewLine.Value] : ['\r', '\n'];

		var specialChars = new List<char>
		{
			options.Delimiter,
			options.Escape,
		};
		if (options.NewLine.HasValue)
		{
			specialChars.Add(options.NewLine.Value);
		}
		else
		{
			specialChars.Add('\r');
			specialChars.Add('\n');
		}

		this.specialChars = specialChars.ToArray();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void Write(ReadOnlySpan<char> field)
	{
		if (fieldCount > 0)
		{
			AddChars(new Span<char>(delimiter));
		}

		modeEscape(this, field);

		fieldCount++;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void AddChars(ReadOnlySpan<char> chars)
	{
		var charLength = chars.Length;
		var bufferLength = buffer.Length;
		var bufferLeft = bufferLength - bufferPosition;

		if (charLength > bufferLeft)
		{
			// Flush buffer.
			writer.Write(buffer, 0, bufferPosition);
			bufferPosition = 0;
		}

		if (charLength > bufferLength)
		{
			// The chars are larger than the buffer.
			// Write chars directly to writer.
			writer.Write(chars);

			return;
		}

		chars.CopyTo(new Span<char>(buffer, bufferPosition, charLength));

		bufferPosition += charLength;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void MoveNext()
	{
		AddChars(new Span<char>(newLine));

		row++;
		fieldCount = 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void Flush()
	{
		writer.Write(buffer, 0, bufferPosition);
		bufferPosition = 0;
		writer.Flush();
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
			ArrayPool<char>.Shared.Return(buffer);
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
