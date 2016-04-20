// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class properties to CSV fields.
	///</summary>
	public abstract class CsvClassMap
	{
		private readonly CsvPropertyMapCollection propertyMaps = new CsvPropertyMapCollection();
		private readonly List<CsvPropertyReferenceMap> referenceMaps = new List<CsvPropertyReferenceMap>();

		/// <summary>
		/// Called to create the mappings.
		/// </summary>
		[Obsolete( "This method is deprecated and will be removed in the next major release. Specify your mappings in the constructor instead.", false )]
		public virtual void CreateMap() {}

		/// <summary>
		/// Gets the constructor expression.
		/// </summary>
		public virtual NewExpression Constructor { get; protected set; } 

		/// <summary>
		/// The class property mappings.
		/// </summary>
		public virtual CsvPropertyMapCollection PropertyMaps
		{
			get { return propertyMaps; }
		}

		/// <summary>
		/// The class property reference mappings.
		/// </summary>
		public virtual List<CsvPropertyReferenceMap> ReferenceMaps
		{
			get { return referenceMaps; }
		}

		/// <summary>
		/// Allow only internal creation of CsvClassMap.
		/// </summary>
		internal CsvClassMap() {}

		/// <summary>
		/// Gets the property map for the given property expression.
		/// </summary>
		/// <typeparam name="T">The type of the class the property belongs to.</typeparam>
		/// <param name="expression">The property expression.</param>
		/// <returns>The CsvPropertyMap for the given expression.</returns>
		[Obsolete( "This method is deprecated and will be removed in the next major release.", false )]
		public virtual CsvPropertyMap PropertyMap<T>( Expression<Func<T, object>> expression )
		{
			var property = ReflectionHelper.GetProperty( expression );

			var existingMap = PropertyMaps.SingleOrDefault( m =>
				m.Data.Property == property
				|| m.Data.Property.Name == property.Name
				&& ( m.Data.Property.DeclaringType.IsAssignableFrom( property.DeclaringType ) || property.DeclaringType.IsAssignableFrom( m.Data.Property.DeclaringType ) ) );
			if( existingMap != null )
			{
				return existingMap;
			}

			var propertyMap = new CsvPropertyMap( property );
			propertyMap.Data.Index = GetMaxIndex() + 1;
			PropertyMaps.Add( propertyMap );

			return propertyMap;
		}

		/// <summary>
		/// Auto maps all properties for the given type. If a property
		/// is mapped again it will override the existing map.
		/// </summary>
		/// <param name="ignoreReferences">A value indicating if references should be ignored when auto mapping. 
		/// True to ignore references, otherwise false.</param>
		/// <param name="prefixReferenceHeaders">A value indicating if headers of reference properties should
		/// get prefixed by the parent property name.
		/// True to prefix, otherwise false.</param>
		public virtual void AutoMap( bool ignoreReferences = false, bool prefixReferenceHeaders = false )
		{
			var mapParents = new LinkedList<Type>();
			AutoMapInternal( this, ignoreReferences, prefixReferenceHeaders, mapParents );
		}

		/// <summary>
		/// Get the largest index for the
		/// properties and references.
		/// </summary>
		/// <returns>The max index.</returns>
		internal int GetMaxIndex()
		{
			if( PropertyMaps.Count == 0 && ReferenceMaps.Count == 0 )
			{
				return -1;
			}

			var indexes = new List<int>();
			if( PropertyMaps.Count > 0 )
			{
				indexes.Add( PropertyMaps.Max( pm => pm.Data.Index ) );
			}
			indexes.AddRange( ReferenceMaps.Select( referenceMap => referenceMap.GetMaxIndex() ) );

			return indexes.Max();
		}

		/// <summary>
		/// Resets the indexes based on the given start index.
		/// </summary>
		/// <param name="indexStart">The index start.</param>
		/// <returns>The last index + 1.</returns>
		internal int ReIndex( int indexStart = 0 )
		{
			foreach( var propertyMap in PropertyMaps )
			{
				if( !propertyMap.Data.IsIndexSet )
				{
					propertyMap.Data.Index = indexStart + propertyMap.Data.Index;
				}
			}

			foreach( var referenceMap in ReferenceMaps )
			{
				indexStart = referenceMap.Data.Mapping.ReIndex( indexStart );
			}

			return indexStart;
		}

		/// <summary>
		/// Auto maps the given map and checks for circular references as it goes.
		/// </summary>
		/// <param name="map">The map to auto map.</param>
		/// <param name="ignoreReferences">A value indicating if references should be ignored when auto mapping. 
		/// True to ignore references, otherwise false.</param>
		/// <param name="prefixReferenceHeaders">A value indicating if headers of reference properties should
		/// get prefixed by the parent property name.
		/// True to prefix, otherwise false.</param>
		/// <param name="mapParents">The list of parents for the map.</param>
		internal static void AutoMapInternal( CsvClassMap map, bool ignoreReferences, bool prefixReferenceHeaders, LinkedList<Type> mapParents, int indexStart = 0 )
		{
			var type = map.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0];
			if( typeof( IEnumerable ).IsAssignableFrom( type ) )
			{
				throw new CsvConfigurationException( "Types that inherit IEnumerable cannot be auto mapped. " +
													 "Did you accidentally call GetRecord or WriteRecord which " +
													 "acts on a single record instead of calling GetRecords or " +
													 "WriteRecords which acts on a list of records?" );
			}

			var properties = type.GetProperties( BindingFlags.Instance | BindingFlags.Public );
			foreach( var property in properties )
			{
				var typeConverterType = TypeConverterFactory.GetConverter( property.PropertyType ).GetType();
				if( typeConverterType == typeof( EnumerableConverter ) )
				{
					// The IEnumerable converter just throws an exception so skip it.
					continue;
				}

				var isDefaultConverter = typeConverterType == typeof( DefaultTypeConverter );
				var hasDefaultConstructor = property.PropertyType.GetConstructor( new Type[0] ) != null;
				if( isDefaultConverter && hasDefaultConstructor )
				{
					if( ignoreReferences )
					{
						continue;
					}

					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.
					if( CheckForCircularReference( property.PropertyType, mapParents ) )
					{
						continue;
					}

					mapParents.AddLast( type );
					var refMapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( property.PropertyType );
					var refMap = (CsvClassMap)ReflectionHelper.CreateInstance( refMapType );
					AutoMapInternal( refMap, false, prefixReferenceHeaders, mapParents, map.GetMaxIndex() + 1 );

					if( refMap.PropertyMaps.Count > 0 || refMap.ReferenceMaps.Count > 0 )
					{
						var referenceMap = new CsvPropertyReferenceMap( property, refMap );
						if( prefixReferenceHeaders )
						{
							referenceMap.Prefix();
						}

						map.ReferenceMaps.Add( referenceMap );
					}
				}
				else
				{
					var propertyMap = new CsvPropertyMap( property );
					propertyMap.Data.Index = map.GetMaxIndex() + 1;
					if( propertyMap.Data.TypeConverter.CanConvertFrom( typeof( string ) ) ||
						propertyMap.Data.TypeConverter.CanConvertTo( typeof( string ) ) && !isDefaultConverter )
					{
						// Only add the property map if it can be converted later on.
						// If the property will use the default converter, don't add it because
						// we don't want the .ToString() value to be used when auto mapping.
						map.PropertyMaps.Add( propertyMap );
					}
				}
			}

			map.ReIndex( indexStart );
		}

		/// <summary>
		/// Checks for circular references.
		/// </summary>
		/// <param name="type">The type to check for.</param>
		/// <param name="mapParents">The list of parents to check against.</param>
		/// <returns>A value indicating if a circular reference was found.
		/// True if a circular reference was found, otherwise false.</returns>
		internal static bool CheckForCircularReference( Type type, LinkedList<Type> mapParents )
		{
			if( mapParents.Count == 0 )
			{
				return false;
			}

			var node = mapParents.Last;
			while( true )
			{
				if( node.Value == type )
				{
					return true;
				}

				node = node.Previous;
				if( node == null )
				{
					break;
				}
			}

			return false;
		}
	}
}
#endif // !NET_2_0
