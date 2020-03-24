// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections;
using System.Collections.Generic;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="IDictionary"/> to and from a <see cref="string"/>.
	/// </summary>
	public class IDictionaryConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the object to a string.
		/// </summary>
		/// <param name="value">The object to convert to a string.</param>
		/// <param name="row">The <see cref="IWriterRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being written.</param>
		/// <returns>The string representation of the object.</returns>
		public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
		{
			var dictionary = value as IDictionary;
			if (dictionary == null)
			{
				return base.ConvertToString(value, row, memberMapData);
			}

			foreach (DictionaryEntry entry in dictionary)
			{
				row.WriteField(entry.Value);
			}

			return null;
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
		{
			var dictionary = new Dictionary<string, string>();

			var indexEnd = memberMapData.IndexEnd < memberMapData.Index
				? row.Context.Record.Length - 1
				: memberMapData.IndexEnd;

			for (var i = memberMapData.Index; i <= indexEnd; i++)
			{
				if (row.TryGetField(i, out string field))
				{
					dictionary.Add(row.Context.HeaderRecord[i], field);
				}
			}

			return dictionary;
		}
	}
}
