// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="Uri"/> to and from a <see cref="string"/>.
	/// </summary>
	public class UriConverter : DefaultTypeConverter
    {
		/// <summary>
		/// Converts the <see cref="string"/>  to a <see cref="Uri"/>.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow" /> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData" /> for the member being created.</param>
		/// <returns>
		/// The <see cref="Uri"/> created from the string.
		/// </returns>
		public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
		{
			var uriKind = memberMapData.TypeConverterOptions.UriKind ?? default;

			if (Uri.TryCreate(text, uriKind, out var uri))
			{
				return uri;
			}

			return base.ConvertFromString(text, row, memberMapData);
		}
	}
}
