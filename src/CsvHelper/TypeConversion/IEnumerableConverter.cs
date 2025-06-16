// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Collections;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="IEnumerable"/> to and from a <see cref="string"/>.
/// </summary>
public class IEnumerableConverter : DefaultTypeConverter
{
	/// <summary>
	/// Converts the object to a string.
	/// </summary>
	/// <param name="value">The object to convert to a string.</param>
	/// <param name="row"></param>
	/// <param name="memberMapData"></param>
	/// <returns>The string representation of the object.</returns>
	public override ReadOnlySpan<char> ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
	{
		var list = value as IEnumerable;
		if (list == null)
		{
			return base.ConvertToString(value, row, memberMapData);
		}

		foreach (var item in list)
		{
			row.WriteField(item.ToString());
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
	public override object? ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
	{
		var list = new List<string?>();

		if (memberMapData.IsNameSet || row.Configuration.HasHeaderRecord && !memberMapData.IsIndexSet)
		{
			// Use the name.
			var nameIndex = 0;
			while (true)
			{
				if (!row.TryGetField(memberMapData.Names.FirstOrDefault()!, nameIndex, out string? field))
				{
					break;
				}

				list.Add(field);
				nameIndex++;
			}
		}
		else
		{
			// Use the index.
			var indexEnd = memberMapData.IndexEnd < memberMapData.Index
				? row.Parser.Current.Count - 1
				: memberMapData.IndexEnd;

			for (var i = memberMapData.Index; i <= indexEnd; i++)
			{
				if (row.TryGetField(i, out string? field))
				{
					list.Add(field);
				}
			}
		}

		return list;
	}
}
