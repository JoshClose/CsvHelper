// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class properties to CSV fields.
	///</summary>
	public abstract class CsvClassMap
	{
		private static readonly List<Type> enumerableConverters = new List<Type>
		{
			typeof( ArrayConverter ),
			typeof( CollectionGenericConverter ),
			typeof( EnumerableConverter ),
			typeof( IDictionaryConverter ),
			typeof( IDictionaryGenericConverter ),
			typeof( IEnumerableConverter ),
			typeof( IEnumerableGenericConverter )
		};

		/// <summary>
		/// The type of the class this map is for.
		/// </summary>
		public virtual Type ClassType { get; private set; }

		/// <summary>
		/// Gets the constructor expression.
		/// </summary>
		public virtual Expression Constructor { get; protected set; } 

		/// <summary>
		/// The class property/field mappings.
		/// </summary>
		public virtual CsvPropertyMapCollection PropertyMaps { get; } = new CsvPropertyMapCollection();

		/// <summary>
		/// The class property/field reference mappings.
		/// </summary>
		public virtual CsvPropertyReferenceMapCollection ReferenceMaps { get; } = new CsvPropertyReferenceMapCollection();

		/// <summary>
		/// Allow only internal creation of CsvClassMap.
		/// </summary>
		/// <param name="classType">The type of the class this map is for.</param>
		internal CsvClassMap( Type classType )
		{
			ClassType = classType;
		}

		/// <summary>
		/// Maps a property/field to a CSV field.
		/// </summary>
		/// <param name="classType">The type of the class this map is for. This may not be the same type
		/// as the member.DeclaringType or the current ClassType due to nested property mappings.</param>
		/// <param name="member">The property/field to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same property/field.</param>
		/// <returns>The property/field mapping.</returns>
		public CsvPropertyMap Map( Type classType, MemberInfo member, bool useExistingMap = true )
		{
			if( useExistingMap )
			{
				var existingMap = PropertyMaps.Find( member );
				if( existingMap != null )
				{
					return existingMap;
				}
			}

			var propertyMap = CsvPropertyMap.CreateGeneric( classType, member );
			propertyMap.Data.Index = GetMaxIndex() + 1;
			PropertyMaps.Add( propertyMap );

			return propertyMap;
		}

		/// <summary>
		/// Maps a non-member to a CSV field. This allows for writing
		/// data that isn't mapped to a class property/field.
		/// </summary>
		/// <returns>The property mapping.</returns>
		public virtual CsvPropertyMap<object, object> Map()
		{
			var propertyMap = new CsvPropertyMap<object, object>( null );
			propertyMap.Data.Index = GetMaxIndex() + 1;
			PropertyMaps.Add( propertyMap );

			return propertyMap;
		}

		/// <summary>
		/// Maps a property/field to another class map.
		/// </summary>
		/// <param name="classMapType">The type of the class map.</param>
		/// <param name="member">The property/field.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the reference map.</param>
		/// <returns>The reference mapping for the property/field.</returns>
		public virtual CsvPropertyReferenceMap References( Type classMapType, MemberInfo member, params object[] constructorArgs )
		{
			if( !typeof( CsvClassMap ).IsAssignableFrom( classMapType ) )
			{
				throw new InvalidOperationException( $"Argument {nameof( classMapType )} is not a CsvClassMap." );
			}

			var existingMap = ReferenceMaps.Find( member );

			if( existingMap != null )
			{
				return existingMap;
			}

			var map = (CsvClassMap)ReflectionHelper.CreateInstance( classMapType, constructorArgs );
			map.ReIndex( GetMaxIndex() + 1 );
			var reference = new CsvPropertyReferenceMap( member, map );
			ReferenceMaps.Add( reference );

			return reference;
		}

		/// <summary>
		/// Auto maps all properties/fields for the given type. If a property/field 
		/// is mapped again it will override the existing map.
		/// </summary>
		public virtual void AutoMap()
		{
			AutoMap( new AutoMapOptions() );
		}

		/// <summary>
		/// Auto maps all properties/fields for the given type. If a property/field 
		/// is mapped again it will override the existing map.
		/// </summary>
		/// <param name="options">Options for auto mapping.</param>
		public virtual void AutoMap( AutoMapOptions options )
		{
			var mapParents = new LinkedList<Type>();
			AutoMapInternal( this, options, mapParents );
		}

		/// <summary>
		/// Get the largest index for the
		/// properties/fields and references.
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
		/// <param name="options">Options for auto mapping.</param>
		/// <param name="mapParents">The list of parents for the map.</param>
		/// <param name="indexStart">The index starting point.</param>
		internal static void AutoMapInternal( CsvClassMap map, AutoMapOptions options, LinkedList<Type> mapParents, int indexStart = 0 )
		{
			var type = map.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0];
			if( typeof( IEnumerable ).IsAssignableFrom( type ) )
			{
				throw new CsvConfigurationException( "Types that inherit IEnumerable cannot be auto mapped. " +
													 "Did you accidentally call GetRecord or WriteRecord which " +
													 "acts on a single record instead of calling GetRecords or " +
													 "WriteRecords which acts on a list of records?" );
			}

			var flags = BindingFlags.Instance | BindingFlags.Public;
			if( options.IncludePrivateProperties )
			{
				flags = flags | BindingFlags.NonPublic;
			}

			var members = new List<MemberInfo>();
			if( ( options.MemberTypes & MemberTypes.Properties ) == MemberTypes.Properties )
			{
				var properties = type.GetProperties( flags );
				members.AddRange( properties );
			}

			if( ( options.MemberTypes & MemberTypes.Fields ) == MemberTypes.Fields )
			{
				var fields = new List<MemberInfo>();
				foreach( var field in type.GetFields( flags ) )
				{
					if( !field.GetCustomAttributes( typeof( CompilerGeneratedAttribute ), false ).Any() )
					{
						fields.Add( field );
					}
				}

				members.AddRange( fields );
			}

			foreach( var member in members )
			{
				var typeConverterType = TypeConverterFactory.GetConverter( member.MemberType() ).GetType();

				if( options.HasHeaderRecord && enumerableConverters.Contains( typeConverterType ) )
				{
					// Enumerable converters can't write the header properly, so skip it.
					continue;
				}

				var memberTypeInfo = member.MemberType().GetTypeInfo();
				var isDefaultConverter = typeConverterType == typeof( DefaultTypeConverter );
				var hasDefaultConstructor = member.MemberType().GetConstructor( new Type[0] ) != null;
				var isUserDefinedStruct = memberTypeInfo.IsValueType && !memberTypeInfo.IsPrimitive && !memberTypeInfo.IsEnum;
				if( isDefaultConverter && ( hasDefaultConstructor || isUserDefinedStruct ) )
				{
					if( options.IgnoreReferences )
					{
						continue;
					}

					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.
					if( CheckForCircularReference( member.MemberType(), mapParents ) )
					{
						continue;
					}

					mapParents.AddLast( type );
					var refMapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( member.MemberType() );
					var refMap = (CsvClassMap)ReflectionHelper.CreateInstance( refMapType );
					var refOptions = options.Copy();
					refOptions.IgnoreReferences = false;
					AutoMapInternal( refMap, options, mapParents, map.GetMaxIndex() + 1 );
					mapParents.Drop( mapParents.Find( type ) );

					if( refMap.PropertyMaps.Count > 0 || refMap.ReferenceMaps.Count > 0 )
					{
						var referenceMap = new CsvPropertyReferenceMap( member, refMap );
						if( options.PrefixReferenceHeaders )
						{
							referenceMap.Prefix();
						}

						map.ReferenceMaps.Add( referenceMap );
					}
				}
				else
				{
					var propertyMap = CsvPropertyMap.CreateGeneric( map.ClassType, member );
					// Use global values as the starting point.
					propertyMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), options.TypeConverterOptionsFactory.GetOptions( member.MemberType() ), propertyMap.Data.TypeConverterOptions );
					propertyMap.Data.Index = map.GetMaxIndex() + 1;
					if( !isDefaultConverter )
					{
						// Only add the property/field map if it can be converted later on.
						// If the property/field will use the default converter, don't add it because
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
