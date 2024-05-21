// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System.Collections;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="IDictionary{TKey, TValue}"/> to and from a <see cref="string"/>.
/// </summary>
public class IDictionaryGenericConverter : IDictionaryConverter
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
		var keyType = memberMapData.Member!.MemberType().GetGenericArguments()[0];
		var valueType = memberMapData.Member!.MemberType().GetGenericArguments()[1];
		var dictionaryType = typeof(Dictionary<,>);
		dictionaryType = dictionaryType.MakeGenericType(keyType, valueType);
		var dictionary = (IDictionary)ObjectResolver.Current.Resolve(dictionaryType);
		var converter = row.Context.TypeConverterCache.GetConverter(valueType);

		var indexEnd = memberMapData.IndexEnd < memberMapData.Index
			? row.Parser.Current.Count - 1
			: memberMapData.IndexEnd;

		for (var i = memberMapData.Index; i <= indexEnd; i++)
		{
			var field = converter.ConvertFromString(row.Parser.Current[i], row, memberMapData);

			dictionary.Add(row.HeaderRecord![i], field);
		}

		return dictionary;
	}
}
