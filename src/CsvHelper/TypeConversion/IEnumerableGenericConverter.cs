// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="IEnumerable{T}"/> to and from a <see cref="string"/>.
	/// </summary>
	public class IEnumerableGenericConverter : IEnumerableConverter
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
			var type = memberMapData.Member.MemberType().GetGenericArguments()[0];
			var listType = typeof(List<>);
			listType = listType.MakeGenericType(type);
			var list = (IList)ReflectionHelper.CreateInstance(listType);

			if (memberMapData.IsNameSet || row.Configuration.HasHeaderRecord && !memberMapData.IsIndexSet)
			{
				// Use the name.
				var nameIndex = 0;
				while (true)
				{
					if (!row.TryGetField(type, memberMapData.Names.FirstOrDefault(), nameIndex, out var field))
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
					? row.Context.Record.Length - 1
					: memberMapData.IndexEnd;

				for (var i = memberMapData.Index; i <= indexEnd; i++)
				{
					var field = row.GetField(type, i);

					list.Add(field);
				}
			}

			return list;
		}
	}
}
