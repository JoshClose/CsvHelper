// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="bool"/> to and from a <see cref="string"/>.
	/// </summary>
	public class BooleanConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
		{
			if (bool.TryParse(text, out var b))
			{
				return b;
			}

			if (short.TryParse(text, out var sh))
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

			var t = (text ?? string.Empty).Trim();
			foreach (var trueValue in memberMapData.TypeConverterOptions.BooleanTrueValues)
			{
				if (memberMapData.TypeConverterOptions.CultureInfo.CompareInfo.Compare(trueValue, t, CompareOptions.IgnoreCase) == 0)
				{
					return true;
				}
			}

			foreach (var falseValue in memberMapData.TypeConverterOptions.BooleanFalseValues)
			{
				if (memberMapData.TypeConverterOptions.CultureInfo.CompareInfo.Compare(falseValue, t, CompareOptions.IgnoreCase) == 0)
				{
					return false;
				}
			}

			return base.ConvertFromString(text, row, memberMapData);
		}
	}
}
