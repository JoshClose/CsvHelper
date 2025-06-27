// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Globalization;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts a <see cref="bool"/> to and from a <see cref="string"/>.
/// </summary>
public class BooleanConverter : DefaultTypeConverter
{
	/// <inheritdoc/>
	public override object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		if (bool.TryParse(
#if NET8_0
			text
#else
			text.ToString()
#endif
			, out var b))
		{
			return b;
		}

		if (short.TryParse(
#if NET8_0
			text
#else
			text.ToString()
#endif
			, out var sh))
		{
			if (sh == 0)
			{
				return false;
			}
			if (sh == 1)
			{
				return true;
			}
		}

#if NET8_0_OR_GREATER
		var t = text.Trim();
#else
		var t = text.ToString().Trim().ToString();
#endif
		foreach (var trueValue in memberMapData.TypeConverterOptions.BooleanTrueValues)
		{
			if (memberMapData.TypeConverterOptions.CultureInfo!.CompareInfo.Compare(
#if NET8_0_OR_GREATER
				trueValue.AsSpan(), t
#else
				trueValue, t.ToString()
#endif
				, CompareOptions.IgnoreCase) == 0)
			{
				return true;
			}
		}

		foreach (var falseValue in memberMapData.TypeConverterOptions.BooleanFalseValues)
		{
			if (memberMapData.TypeConverterOptions.CultureInfo!.CompareInfo.Compare(
#if NET8_0_OR_GREATER
				falseValue.AsSpan(), t
#else
				falseValue, t.ToString()
#endif
				, CompareOptions.IgnoreCase) == 0)
			{
				return false;
			}
		}

		return base.ConvertFromString(text, row, memberMapData);
	}

	/// <inheritdoc/>
	public override ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		var b = value as bool?;
		if (b == true && memberMapData.TypeConverterOptions.BooleanTrueValues.Count > 0)
		{
			return memberMapData.TypeConverterOptions.BooleanTrueValues.First().AsSpan();
		}
		else if (b == false && memberMapData.TypeConverterOptions.BooleanFalseValues.Count > 0)
		{
			return memberMapData.TypeConverterOptions.BooleanFalseValues.First().AsSpan();
		}

		return base.ConvertToString(value, row, memberMapData);
	}
}
