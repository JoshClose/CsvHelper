// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;

namespace CsvHelper.TypeConversion
{
	public interface ISpanTypeConverter
	{
		/// <summary>
		/// Converts the ReadOnlySpan to an object.
		/// </summary>
		/// <param name="text">The ReadOnlySpan to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		object ConvertFromSpan(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData);
	}
}
