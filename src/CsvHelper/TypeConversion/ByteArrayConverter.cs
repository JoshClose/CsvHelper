// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts a <see cref="T:Byte[]"/> to and from a <see cref="string"/>.
/// </summary>
public class ByteArrayConverter : DefaultTypeConverter
{
	private readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();
	private readonly ByteArrayConverterOptions options;
	private readonly string HexStringPrefix;
	private readonly byte ByteLength;

	/// <summary>
	/// Creates a new ByteArrayConverter using the given <see cref="ByteArrayConverterOptions"/>.
	/// </summary>
	/// <param name="options">The options.</param>
	public ByteArrayConverter(ByteArrayConverterOptions options = ByteArrayConverterOptions.Hexadecimal | ByteArrayConverterOptions.HexInclude0x)
	{
		// Defaults to the literal format used by C# for whole numbers, and SQL Server for binary data.
		this.options = options;
		ValidateOptions();

		HexStringPrefix = (options & ByteArrayConverterOptions.HexDashes) == ByteArrayConverterOptions.HexDashes ? "-" : string.Empty;
		ByteLength = (options & ByteArrayConverterOptions.HexDashes) == ByteArrayConverterOptions.HexDashes ? (byte)3 : (byte)2;
	}

	/// <summary>
	/// Converts the object to a string.
	/// </summary>
	/// <param name="value">The object to convert to a string.</param>
	/// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
	/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
	/// <returns>The string representation of the object.</returns>
	public override ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		if (value is byte[] byteArray)
		{
			if ((options & ByteArrayConverterOptions.Base64) == ByteArrayConverterOptions.Base64)
			{
#if NET8_0_OR_GREATER
				var length = GetBase64Length(byteArray);
				EnsureBufferSize(length);
				if (Convert.TryToBase64Chars(byteArray.AsSpan(), Buffer.AsSpan(), out int charsWritten))
				{
					return Buffer.AsSpan(0, charsWritten);
				}
				else
				{
					throw new InvalidOperationException("Failed to convert byte array to Base64 string.");
				}
#else
				return Convert.ToBase64String(byteArray).AsSpan();
#endif
			}

			return BytesToHex(byteArray);
		}

		return base.ConvertToString(value, row, memberMapData);
	}

	/// <summary>
	/// Converts the string to an object.
	/// </summary>
	/// <param name="text">The string to convert to an object.</param>
	/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
	/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
	/// <returns>The object created from the string.</returns>
	public override object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		if ((options & ByteArrayConverterOptions.Base64) == ByteArrayConverterOptions.Base64)
		{
#if NET8_0_OR_GREATER
			var length = (text.Length * 3) / 4;
			var buffer = new byte[length];
			var bufferSpan = new Span<byte>(buffer);
			if (Convert.TryFromBase64Chars(text, bufferSpan, out int bytesWritten))
			{
				return bufferSpan.Slice(0, bytesWritten).ToArray();
			}
			else
			{
				throw new FormatException("Invalid Base64 string.");
			}
#else
			return Convert.FromBase64String(text.ToString());
#endif
		}

		return HexToBytes(text);
	}

	private ReadOnlySpan<char> BytesToHex(byte[] bytes)
	{
		if (bytes.Length == 0)
		{
			return ReadOnlySpan<char>.Empty;
		}

		var length = bytes.Length * 2;
		if ((options & ByteArrayConverterOptions.HexInclude0x) == ByteArrayConverterOptions.HexInclude0x)
		{
			length += 2;
		}

		var result = new char[length];
		result[0] = '0';
		result[1] = 'x';

		for (int i = 0; i < bytes.Length; i++)
		{
			byte b = bytes[i];
			result[i * 2] = HexDigits[b >> 4];      // High nibble
			result[i * 2 + 1] = HexDigits[b & 0xF]; // Low nibble
		}

		return result;
	}

	private byte[] HexToBytes(ReadOnlySpan<char> hex)
	{
		hex = hex.Trim();

		Span<char> prefix = ['0', 'x'];
		if (hex.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			hex = hex.Slice(2);
		}

		if (hex.IsEmpty)
		{
			return Array.Empty<byte>();
		}

		if (hex.Length % 2 != 0)
		{
			throw new ArgumentException("Hex string must have an even number of characters.", nameof(hex));
		}

		var result = new byte[hex.Length / 2];

		for (int i = 0; i < hex.Length; i += 2)
		{
			var hexPair = hex.Slice(i, 2);

			try
			{
				result[i / 2] = byte.Parse(
#if NET8_0_OR_GREATER
					hexPair
#else
					hexPair.ToString()
#endif
					, NumberStyles.HexNumber);
			}
			catch (FormatException)
			{
				throw new FormatException($"Invalid hex characters at position {i}: '{hexPair.ToString()}'.");
			}
		}

		return result;
	}

	private int GetBase64Length(byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			return 0;
		}

		return ((bytes.Length + 2) / 3) * 4;
	}

	private void ValidateOptions()
	{
		if ((options & ByteArrayConverterOptions.Base64) == ByteArrayConverterOptions.Base64)
		{
			if ((options & (ByteArrayConverterOptions.HexInclude0x | ByteArrayConverterOptions.HexDashes | ByteArrayConverterOptions.Hexadecimal)) != ByteArrayConverterOptions.None)
			{
				throw new ConfigurationException($"{nameof(ByteArrayConverter)} must be configured exclusively with HexDecimal options, or exclusively with Base64 options.  Was {options.ToString()}")
				{
					Data = { { "options", options } }
				};
			}
		}
	}

	private
}
