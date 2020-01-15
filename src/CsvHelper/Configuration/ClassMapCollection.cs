// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Collection that holds CsvClassMaps for record types.
	/// </summary>
	public class ClassMapCollection
	{
		private readonly Dictionary<Type, ClassMap> data = new Dictionary<Type, ClassMap>();
		private readonly CsvConfiguration configuration;

		/// <summary>
		/// Gets the <see cref="ClassMap"/> for the specified record type.
		/// </summary>
		/// <value>
		/// The <see cref="ClassMap"/>.
		/// </value>
		/// <param name="type">The record type.</param>
		/// <returns>The <see cref="ClassMap"/> for the specified record type.</returns>
		public virtual ClassMap this[Type type]
		{
			get
			{
				// Go up the inheritance tree to find the matching type.
				// We can't use IsAssignableFrom because both a child
				// and it's parent/grandparent/etc could be mapped.
				var currentType = type;
				while( true )
				{
					if( data.ContainsKey( currentType ) )
					{
						return data[currentType];
					}

					currentType = currentType.GetTypeInfo().BaseType;
					if( currentType == null )
					{
						return null;
					}
				}
			}
		}

		/// <summary>
		/// Creates a new instance using the given configuration.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		public ClassMapCollection( CsvConfiguration configuration )
		{
			this.configuration = configuration;
		}

		/// <summary>
		/// Finds the <see cref="ClassMap"/> for the specified record type.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <returns>The <see cref="ClassMap"/> for the specified record type.</returns>
		public virtual ClassMap<T> Find<T>()
		{
			return (ClassMap<T>)this[typeof( T )];
		}

		/// <summary>
		/// Adds the specified map for it's record type. If a map
		/// already exists for the record type, the specified
		/// map will replace it.
		/// </summary>
		/// <param name="map">The map.</param>
		internal virtual void Add( ClassMap map )
		{
			SetMapDefaults( map );

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
			if( !typeof( ClassMap ).IsAssignableFrom( classMapType ) )
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
		private Type GetGenericCsvClassMapType( Type type )
		{
			if( type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof( ClassMap<> ) )
			{
				return type;
			}

			return GetGenericCsvClassMapType( type.GetTypeInfo().BaseType );
		}

		/// <summary>
		/// Sets defaults for the mapping tree. The defaults used
		/// to be set inside the classes, but this didn't allow for
		/// the TypeConverter to be created from the Configuration's
		/// TypeConverterFactory.
		/// </summary>
		/// <param name="map">The map to set defaults on.</param>
		private void SetMapDefaults( ClassMap map )
		{
			foreach( var memberMap in map.MemberMaps )
			{
				if( memberMap.Data.Member == null )
				{
					continue;
				}

				if( memberMap.Data.TypeConverter == null )
				{
					memberMap.Data.TypeConverter = configuration.TypeConverterCache.GetConverter( memberMap.Data.Member.MemberType() );
				}

				if( memberMap.Data.Names.Count == 0 )
				{
					memberMap.Data.Names.Add( memberMap.Data.Member.Name );
				}
			}

			foreach( var parameterMap in map.ParameterMaps )
			{
				if( parameterMap.ConstructorTypeMap != null )
				{
					SetMapDefaults( parameterMap.ConstructorTypeMap );
				}
				else if( parameterMap.ReferenceMap != null )
				{
					SetMapDefaults( parameterMap.ReferenceMap.Data.Mapping );
				}
				else
				{ 
					if( parameterMap.Data.TypeConverter == null )
					{
						parameterMap.Data.TypeConverter = configuration.TypeConverterCache.GetConverter( parameterMap.Data.Parameter.ParameterType );
					}

					if( parameterMap.Data.Name == null )
					{
						parameterMap.Data.Name = parameterMap.Data.Parameter.Name;
					}
				}
			}

			foreach( var referenceMap in map.ReferenceMaps )
			{
				SetMapDefaults( referenceMap.Data.Mapping );

				if( configuration.ReferenceHeaderPrefix != null )
				{
					referenceMap.Data.Prefix = configuration.ReferenceHeaderPrefix( referenceMap.Data.Member.MemberType(), referenceMap.Data.Member.Name );
				}
			}
		}
	}
}
