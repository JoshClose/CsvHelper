// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="Collection{T}"/> to and from a <see cref="string"/>.
	/// </summary>
	public class CollectionGenericConverter : IEnumerableConverter
    {
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="propertyMapData">The <see cref="CsvPropertyMapData"/> for the property/field being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( string text, IReaderRow row, CsvPropertyMapData propertyMapData )
		{
			// Since we're using the PropertyType here, this converter can be used for multiple types
			// as long as they implement IList.
			var list = (IList)ReflectionHelper.CreateInstance( propertyMapData.Member.MemberType() );
			var type = propertyMapData.Member.MemberType().GetGenericArguments()[0];

			if( propertyMapData.IsNameSet || row.Configuration.HasHeaderRecord && !propertyMapData.IsIndexSet )
			{
				// Use the name.
				var nameIndex = 0;
				while( true )
				{
					object field;
					if( !row.TryGetField( type, propertyMapData.Names.FirstOrDefault(), nameIndex, out field ) )
					{
						break;
					}

					list.Add( field );
					nameIndex++;
				}
			}
			else
			{
				// Use the index.
				var indexEnd = propertyMapData.IndexEnd < propertyMapData.Index
					? row.Context.Record.Length - 1
					: propertyMapData.IndexEnd;

				for( var i = propertyMapData.Index; i <= indexEnd; i++ )
				{
					var field = row.GetField( type, i );

					list.Add( field );
				}
			}

			return list;
		}
	}
}
