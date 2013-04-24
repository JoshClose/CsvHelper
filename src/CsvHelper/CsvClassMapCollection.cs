// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Linq;
#if WINRT_4_5
using System.Reflection;
#endif
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Collection that holds CsvClassMaps for record types.
	/// </summary>
	public class CsvClassMapCollection
	{
		private readonly Dictionary<Type, CsvClassMap> data = new Dictionary<Type, CsvClassMap>();

		/// <summary>
		/// Gets the <see cref="CsvClassMap"/> for the specified record type.
		/// </summary>
		/// <value>
		/// The <see cref="CsvClassMap"/>.
		/// </value>
		/// <param name="type">The record type.</param>
		/// <returns>The <see cref="CsvClassMap"/> for the specified record type.</returns>
		public CsvClassMap this[Type type]
		{
			get
			{
				CsvClassMap map;
				data.TryGetValue( type, out map );
				return map;
			}
		}

		/// <summary>
		/// Adds the specified map for it's record type. If a map
		/// already exists for the record type, the specified
		/// map will replace it.
		/// </summary>
		/// <param name="map">The map.</param>
		public void Add( CsvClassMap map )
		{
#if WINRT_4_5
			var type = map.GetType().GetTypeInfo().BaseType.GenericTypeArguments.First();
#else
			var type = map.GetType().BaseType.GetGenericArguments().First();
#endif

			if( data.ContainsKey( type ) )
			{
				data[type] = map;
			}
			else
			{
				data.Add( type, map );
			}
		}

		/// <summary>
		/// Removes the map for the specified record type.
		/// </summary>
		/// <param name="type">The record type.</param>
		public void Remove( Type type )
		{
			data.Remove( type );
		}

		/// <summary>
		/// Removes all maps.
		/// </summary>
		public void Clear()
		{
			data.Clear();
		}
	}
}
