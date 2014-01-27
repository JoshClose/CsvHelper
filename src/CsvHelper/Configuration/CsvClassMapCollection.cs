﻿// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Linq;
#if WINRT_4_5
using System.Reflection;
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper.Configuration
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
		public virtual CsvClassMap this[Type type]
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
		internal virtual void Add( CsvClassMap map )
		{
			var type = GetGenericCsvClassMapType( map.GetType() ).GetGenericArguments().First();

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
		/// Removes the class map.
		/// </summary>
		/// <param name="classMapType">The class map type.</param>
		internal virtual void Remove( Type classMapType )
		{
			if( !typeof( CsvClassMap ).IsAssignableFrom( classMapType ) )
			{
				throw new ArgumentException( "The class map type must inherit from CsvClassMap." );
			}

			var type = GetGenericCsvClassMapType( classMapType ).GetGenericArguments().First();

			data.Remove( type );
		}

		/// <summary>
		/// Removes all maps.
		/// </summary>
		internal virtual void Clear()
		{
			data.Clear();
		}

		/// <summary>
		/// Goes up the inheritance tree to find the type instance of CsvClassMap{}.
		/// </summary>
		/// <param name="type">The type to traverse.</param>
		/// <returns>The type that is CsvClassMap{}.</returns>
		protected virtual Type GetGenericCsvClassMapType( Type type )
		{
#if WINRT_4_5
			var typeInfo = type.GetTypeInfo();
			if( typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof( CsvClassMap<> ) )
			{
				return type;
			}

			return GetGenericCsvClassMapType( typeInfo.BaseType );
#else
			if( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( CsvClassMap<> ) )
			{
				return type;
			}

			return GetGenericCsvClassMapType( type.BaseType );
#endif
		}
	}
}
