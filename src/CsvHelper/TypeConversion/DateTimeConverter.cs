// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts a <see cref="DateTime"/> to and from a <see cref="string"/>.
/// </summary>
public class DateTimeConverter : DefaultTypeConverter
{
	/// <summary>
	/// Converts the string to an object.
	/// </summary>
	/// <param name="text">The string to convert to an object.</param>
	/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
	/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
	/// <returns>The object created from the string.</returns>
	public override object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		if (text == null)
		{
			return base.ConvertFromString(null, row, memberMapData);
		}

		var formatProvider = (IFormatProvider?)memberMapData.TypeConverterOptions.CultureInfo?.GetFormat(typeof(DateTimeFormatInfo)) ?? memberMapData.TypeConverterOptions.CultureInfo;
		var dateTimeStyle = memberMapData.TypeConverterOptions.DateTimeStyle ?? DateTimeStyles.None;

		DateTime dateTime;
		var success = memberMapData.TypeConverterOptions.Formats == null || memberMapData.TypeConverterOptions.Formats.Length == 0
			? DateTime.TryParse(
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
				text,
#else
				text.ToString(),
#endif
				formatProvider, dateTimeStyle, out dateTime)
			: DateTime.TryParseExact(
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
				text,
#else
				text.ToString(),
#endif
				memberMapData.TypeConverterOptions.Formats, formatProvider, dateTimeStyle, out dateTime);

		return success
			? dateTime
			: base.ConvertFromString(text, row, memberMapData);
	}
}
