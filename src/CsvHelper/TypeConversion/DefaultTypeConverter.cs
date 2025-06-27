// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Buffers;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="object"/> to and from a <see cref="string"/>.
/// </summary>
public class DefaultTypeConverter : ITypeConverter, IDisposable
{
	private bool isDisposed;

	/// <summary>
	/// The buffer used for conversion.
	/// </summary>
	protected char[] Buffer { get; private set; }

	/// <summary>
	/// Initializes a new instance creating a buffer using given size.
	/// </summary>
	/// <param name="bufferSize"></param>
	public DefaultTypeConverter(int bufferSize = 256)
	{
		Buffer = ArrayPool<char>.Shared.Rent(bufferSize);
	}

	/// <inheritdoc/>
	public virtual object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		// Conversion has failed
		// Check if a default value should be returned
		if (!memberMapData.UseDefaultOnConversionFailure || !memberMapData.IsDefaultSet)
		{
			throw CreateTypeConverterException(text, row, memberMapData);
		}

		// Try to get a valid default value from the memberMapData
		var memberType = memberMapData.Member!.MemberType();

		if (memberMapData.Default is null)
		{
			if (TypeAllowsNull(memberType))
			{
				return null;
			}
		}
		else if (memberType.IsAssignableFrom(memberMapData.Default.GetType()))
		{
			return memberMapData.Default;
		}

		// No valid default value was configured, throw a TypeConverterException
		throw CreateTypeConverterException(text, row, memberMapData);
	}

	/// <inheritdoc/>
	public virtual ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		if (value == null)
		{
			if (memberMapData.TypeConverterOptions.NullValues.Count > 0)
			{
				return memberMapData.TypeConverterOptions.NullValues.First().AsSpan();
			}

			return Span<char>.Empty;
		}

		if (value is string s)
		{
			return s.AsSpan();
		}

#if NET8_0_OR_GREATER
		if (value is ISpanFormattable spanFormattable)
		{
			var format = memberMapData.TypeConverterOptions.Formats?.FirstOrDefault();
			if (spanFormattable.TryFormat(Buffer, out int charsWritten, format.AsSpan(), memberMapData.TypeConverterOptions.CultureInfo))
			{
				return Buffer.AsSpan(0, charsWritten);
			}
		}
#endif

		if (value is IFormattable formattable)
		{
			var format = memberMapData.TypeConverterOptions.Formats?.FirstOrDefault();
			return formattable.ToString(format, memberMapData.TypeConverterOptions.CultureInfo).AsSpan();
		}

		return value != null ? value.ToString().AsSpan() : Span<char>.Empty;
	}

	/// <summary>
	/// Ensures that the buffer is at least the specified size.
	/// </summary>
	/// <param name="size">The size of the buffer.</param>
	protected void EnsureBufferSize(int size)
	{
		if (size < Buffer.Length)
		{
			return;
		}

		ArrayPool<char>.Shared.Return(Buffer);

		Buffer = ArrayPool<char>.Shared.Rent(size);
	}

	private TypeConverterException CreateTypeConverterException(ReadOnlySpan<char> chars, IReaderRow row, MemberMapData memberMapData)
	{
		var text = chars.ToString();

		if (!row.Configuration.ExceptionMessagesContainRawData)
		{
			text = $"Hidden because {nameof(IParserConfiguration.ExceptionMessagesContainRawData)} is false.";
		}

		text ??= string.Empty;

		var message =
			$"The conversion cannot be performed.{Environment.NewLine}" +
			$"    Text: '{text}'{Environment.NewLine}" +
			$"    MemberName: {memberMapData.Member?.Name}{Environment.NewLine}" +
			$"    MemberType: {memberMapData.Member?.MemberType().FullName}{Environment.NewLine}" +
			$"    TypeConverter: '{memberMapData.TypeConverter?.GetType().FullName}'";

		return new TypeConverterException(this, memberMapData, text, row.Context, message);
	}

	private static bool TypeAllowsNull(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the object.
	/// </summary>
	/// <param name="disposing">A value indicating if currently disposing.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (isDisposed)
		{
			return;
		}

		if (disposing)
		{
			// TODO: dispose managed state (managed objects)
			ArrayPool<char>.Shared.Return(Buffer);
		}

		// TODO: free unmanaged resources (unmanaged objects) and override finalizer
		// TODO: set large fields to null
		Buffer = Array.Empty<char>();

		isDisposed = true;
	}
}
