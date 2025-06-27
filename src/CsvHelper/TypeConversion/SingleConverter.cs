// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts a <see cref="Single"/> to and from a <see cref="string"/>.
/// </summary>
public class SingleConverter : DefaultTypeConverter
{
	private Lazy<string> defaultFormat = new Lazy<string>(() => float.TryParse(float.MaxValue.ToString("R"), out var _) ? "R" : "G9");

	/// <summary>
	/// Converts the object to a string.
	/// </summary>
	/// <param name="value">The object to convert to a string.</param>
	/// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
	/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
	/// <returns>The string representation of the object.</returns>
	public override ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		var format = memberMapData.TypeConverterOptions.Formats?.FirstOrDefault() ?? defaultFormat.Value;

		if (value is float f)
		{
#if NET8_0_OR_GREATER
			if (f.TryFormat(Buffer, out int charsWritten, format.AsSpan(), memberMapData.TypeConverterOptions.CultureInfo))
			{
				return Buffer.AsSpan(0, charsWritten);
			}
#else
			return f.ToString(format.ToString(), memberMapData.TypeConverterOptions.CultureInfo).AsSpan();
#endif
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
		var numberStyle = memberMapData.TypeConverterOptions.NumberStyles ?? NumberStyles.Float | NumberStyles.AllowThousands;

		if (float.TryParse(
#if NET8_0_OR_GREATER
			text
#else
			text.ToString()
#endif
			, numberStyle, memberMapData.TypeConverterOptions.CultureInfo, out var f))
		{
			return f;
		}

		return base.ConvertFromString(text, row, memberMapData);
	}
}
