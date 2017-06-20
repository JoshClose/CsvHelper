// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="IDictionary{TKey, TValue}"/> to and from a <see cref="string"/>.
	/// </summary>
	public class IDictionaryGenericConverter : IDictionaryConverter
    {
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="ICsvReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData )
		{
			var keyType = propertyMapData.Member.MemberType().GetGenericArguments()[0];
			var valueType = propertyMapData.Member.MemberType().GetGenericArguments()[1];
			var dictionaryType = typeof( Dictionary<,> );
			dictionaryType = dictionaryType.MakeGenericType( keyType, valueType );
			var dictionary = (IDictionary)ReflectionHelper.CreateInstance( dictionaryType );

			var indexEnd = propertyMapData.IndexEnd < propertyMapData.Index
				? row.Context.Record.Length - 1
				: propertyMapData.IndexEnd;

			for( var i = propertyMapData.Index; i <= indexEnd; i++ )
			{
				var field = row.GetField( valueType, i );

				dictionary.Add( row.Context.HeaderRecord[i], field );
			}

			return dictionary;
		}
	}
}
