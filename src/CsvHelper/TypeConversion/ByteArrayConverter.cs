// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Text;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts a <see cref="T:Byte[]"/> to and from a <see cref="string"/>.
/// </summary>
public class ByteArrayConverter : DefaultTypeConverter
{
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
			var text = (options & ByteArrayConverterOptions.Base64) == ByteArrayConverterOptions.Base64
				? Convert.ToBase64String(byteArray).AsSpan()
				: ByteArrayToHexString(byteArray);

			return text;
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
		if (text != null)
		{
			return (options & ByteArrayConverterOptions.Base64) == ByteArrayConverterOptions.Base64
				? Convert.FromBase64String(text.ToString())
				: HexStringToByteArray(text);
		}

		return base.ConvertFromString(text, row, memberMapData);
	}

	private ReadOnlySpan<char> ByteArrayToHexString(byte[] byteArray)
	{
#if NET6_0_OR_GREATER
		ReadOnlySpan<char> hexString = Convert.ToHexString(byteArray);
		if ((options & ByteArrayConverterOptions.HexInclude0x) == ByteArrayConverterOptions.HexInclude0x)
		{
			hexString = $"0x{hexString}";
		}

		return hexString;
#else
		var hexString = new StringBuilder();

		if ((options & ByteArrayConverterOptions.HexInclude0x) == ByteArrayConverterOptions.HexInclude0x)
		{
			hexString.Append("0x");
		}

		if (byteArray.Length >= 1)
		{
			hexString.Append(byteArray[0].ToString("X2"));
		}

		for (var i = 1; i < byteArray.Length; i++)
		{
			hexString.Append(HexStringPrefix + byteArray[i].ToString("X2"));
		}

		return hexString.ToString().AsSpan();
#endif
	}

	private byte[] HexStringToByteArray(ReadOnlySpan<char> hex)
	{
#if NET6_0_OR_GREATER
		if (hex.StartsWith("0x"))
		{
			hex.Slice(2);
		}

		return Convert.FromHexString(hex);
#else

		var has0x = hex.StartsWith("0x".AsSpan());

		var length = has0x
			? (hex.Length - 1) / ByteLength
			: hex.Length + 1 / ByteLength;
		var byteArray = new byte[length];
		var has0xOffset = has0x ? 1 : 0;

		for (var stringIndex = has0xOffset * 2; stringIndex < hex.Length; stringIndex += ByteLength)
		{
			byteArray[(stringIndex - has0xOffset) / ByteLength] = Convert.ToByte(hex.Slice(stringIndex, 2).ToString(), 16);
		}

		return byteArray;
#endif
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
}
