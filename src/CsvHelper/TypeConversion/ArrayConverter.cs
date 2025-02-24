// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion;

/// <summary>
/// Converts an <see cref="Array"/> to and from a <see cref="string"/>.
/// </summary>
public class ArrayConverter : IEnumerableConverter
{
	/// <summary>
	/// Converts the string to an object.
	/// </summary>
	/// <param name="text">The string to convert to an object.</param>
	/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
	/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
	/// <returns>The object created from the string.</returns>
	public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
	{
		Array array;
		var type = memberMapData.Member!.MemberType().GetElementType()!;
		var converter = row.Context.TypeConverterCache.GetConverter(type);

		if (memberMapData.IsNameSet || row.Configuration.HasHeaderRecord && !memberMapData.IsIndexSet)
		{
			// Use the name.
			var list = new List<object?>();
			var nameIndex = 0;
			while (true)
			{
				if (!row.TryGetField(type, memberMapData.Names.FirstOrDefault() ?? string.Empty, nameIndex, out var field))
				{
					break;
				}

				list.Add(field);
				nameIndex++;
			}

			array = (Array)ObjectResolver.Current.Resolve(memberMapData.Member!.MemberType(), list.Count);
			for (var i = 0; i < list.Count; i++)
			{
				array.SetValue(list[i], i);
			}
		}
		else
		{
			// Use the index.
			var indexEnd = memberMapData.IndexEnd < memberMapData.Index
				? row.Parser.Count - 1
				: memberMapData.IndexEnd;

			var arraySize = indexEnd - memberMapData.Index + 1;
			array = (Array)ObjectResolver.Current.Resolve(memberMapData.Member!.MemberType(), arraySize);
			var arrayIndex = 0;
			for (var i = memberMapData.Index; i <= indexEnd; i++)
			{
				var field = converter.ConvertFromString(row.GetField(i), row, memberMapData);
				array.SetValue(field, arrayIndex);
				arrayIndex++;
			}
		}

		return array;
	}
}
