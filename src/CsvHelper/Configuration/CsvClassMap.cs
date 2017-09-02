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
		/// The class constructor parameter mappings.
		/// </summary>
		public virtual List<CsvParameterMap> ParameterMaps { get; } = new List<CsvParameterMap>();

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
			var type = GetGenericType();
			if( typeof( IEnumerable ).IsAssignableFrom( type ) )
			{
				throw new CsvConfigurationException( "Types that inherit IEnumerable cannot be auto mapped. " +
													 "Did you accidentally call GetRecord or WriteRecord which " +
													 "acts on a single record instead of calling GetRecords or " +
													 "WriteRecords which acts on a list of records?" );
			}

			var mapParents = new LinkedList<Type>();
			if( options.ShouldUseConstructorParameters( type ) )
			{
				// This type doesn't have a parameterless constructor so we can't create an
				// instance and set it's property. Constructor parameters need to be created
				// instead. Writing only uses getters, so properties will also be mapped
				// for writing purposes.
				AutoMapConstructorParameters( this, options, mapParents );
			}

			AutoMapProperties( this, options, mapParents );
		}

		/// <summary>
		/// Get the largest index for the
		/// properties/fields and references.
		/// </summary>
		/// <returns>The max index.</returns>
		public virtual int GetMaxIndex()
		{
			if( ParameterMaps.Count == 0 && PropertyMaps.Count == 0 && ReferenceMaps.Count == 0 )
			{
				return -1;
			}

			var indexes = new List<int>();
			if( ParameterMaps.Count > 0 )
			{
				indexes.AddRange( ParameterMaps.Select( parameterMap => parameterMap.GetMaxIndex() ) );
			}

			if( PropertyMaps.Count > 0 )
			{
				indexes.Add( PropertyMaps.Max( pm => pm.Data.Index ) );
			}

			if( ReferenceMaps.Count > 0 )
			{
				indexes.AddRange( ReferenceMaps.Select( referenceMap => referenceMap.GetMaxIndex() ) );
			}

			return indexes.Max();
		}

		/// <summary>
		/// Resets the indexes based on the given start index.
		/// </summary>
		/// <param name="indexStart">The index start.</param>
		/// <returns>The last index + 1.</returns>
		public virtual int ReIndex( int indexStart = 0 )
		{
			foreach( var parameterMap in ParameterMaps )
			{
				parameterMap.Data.Index = indexStart + parameterMap.Data.Index;
			}

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
		protected virtual void AutoMapProperties( CsvClassMap map, AutoMapOptions options, LinkedList<Type> mapParents, int indexStart = 0 )
		{
			var type = map.GetGenericType();

			var flags = BindingFlags.Instance | BindingFlags.Public;
			if( options.IncludePrivateProperties )
			{
				flags = flags | BindingFlags.NonPublic;
			}

			var members = new List<MemberInfo>();
			if( options.MemberTypes.HasFlag( MemberTypes.Properties ) )
			{
				// We need to go up the declaration tree and find the actual type the property
				// exists on and use that PropertyInfo instead. This is so we can get the private
				// set method for the property.
				var properties = new List<PropertyInfo>();
				foreach( var property in type.GetProperties( flags ) )
				{
					properties.Add( ReflectionHelper.GetDeclaringProperty( type, property, flags ) );
				}

				members.AddRange( properties );
			}

			if( options.MemberTypes.HasFlag( MemberTypes.Fields ) )
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
				var typeConverterType = TypeConverterFactory.Current.GetConverter( member.MemberType() ).GetType();

				if( options.HasHeaderRecord && enumerableConverters.Contains( typeConverterType ) )
				{
					// Enumerable converters can't write the header properly, so skip it.
					continue;
				}

				var memberTypeInfo = member.MemberType().GetTypeInfo();
				var isDefaultConverter = typeConverterType == typeof( DefaultTypeConverter );
				if( isDefaultConverter && ( memberTypeInfo.HasParameterlessConstructor() || memberTypeInfo.IsUserDefinedStruct() ) )
				{
					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.

					if( options.IgnoreReferences )
					{
						continue;
					}

					if( CheckForCircularReference( member.MemberType(), mapParents ) )
					{
						continue;
					}

					mapParents.AddLast( type );
					var refMapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( member.MemberType() );
					var refMap = (CsvClassMap)ReflectionHelper.CreateInstance( refMapType );
					var refOptions = options.Copy();
					refOptions.IgnoreReferences = false;
					// Need to use Max here for nested types.
					AutoMapProperties( refMap, options, mapParents, Math.Max( map.GetMaxIndex() + 1, indexStart ) );
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
		/// Auto maps the given map using constructor parameters.
		/// </summary>
		/// <param name="map">The map to auto map.</param>
		/// <param name="options">Options for auto mapping.</param>
		/// <param name="mapParents">The list of parents for the map.</param>
		/// <param name="indexStart">The index starting point.</param>
		protected virtual void AutoMapConstructorParameters( CsvClassMap map, AutoMapOptions options, LinkedList<Type> mapParents, int indexStart = 0 )
		{
			var type = map.GetGenericType();
			var constructor = options.GetConstructor( map.ClassType );
			var parameters = constructor.GetParameters();

			foreach( var parameter in parameters )
			{
				var typeConverterType = TypeConverterFactory.Current.GetConverter( parameter.ParameterType ).GetType();

				var parameterMap = new CsvParameterMap( parameter );

				var memberTypeInfo = parameter.ParameterType.GetTypeInfo();
				var isDefaultConverter = typeConverterType == typeof( DefaultTypeConverter );
				if( isDefaultConverter && ( memberTypeInfo.HasParameterlessConstructor() || memberTypeInfo.IsUserDefinedStruct() ) )
				{
					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.

					if( options.IgnoreReferences )
					{
						throw new InvalidOperationException( $"Configuration '{nameof( options.IgnoreReferences )}' can't be true " +
															  "when using types without a default constructor. Constructor parameters " +
															  "are used and all properties including references must be used." );
					}

					if( CheckForCircularReference( parameter.ParameterType, mapParents ) )
					{
						throw new InvalidOperationException( $"A circular reference was detected in constructor paramter '{parameter.Name}'." +
															  "Since all parameters must be supplied for a constructor, this parameter can't be skipped." );
					}

					mapParents.AddLast( type );
					var refMapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( parameter.ParameterType );
					var refMap = (CsvClassMap)ReflectionHelper.CreateInstance( refMapType );
					var refOptions = options.Copy();
					refOptions.IgnoreReferences = false;
					AutoMapProperties( refMap, options, mapParents, Math.Max( map.GetMaxIndex() + 1, indexStart ) );
					mapParents.Drop( mapParents.Find( type ) );

					var referenceMap = new CsvParameterReferenceMap( parameter, refMap );
					if( options.PrefixReferenceHeaders )
					{
						referenceMap.Prefix();
					}

					parameterMap.ReferenceMap = referenceMap;
				}
				else if( options.ShouldUseConstructorParameters( parameter.ParameterType ) )
				{
					mapParents.AddLast( type );
					var constructorMapType = typeof( DefaultCsvClassMap<> ).MakeGenericType( parameter.ParameterType );
					var constructorMap = (CsvClassMap)ReflectionHelper.CreateInstance( constructorMapType );
					var constructorOptions = options.Copy();
					constructorOptions.IgnoreReferences = false;
					// Need to use Max here for nested types.
					AutoMapConstructorParameters( constructorMap, constructorOptions, mapParents, Math.Max( map.GetMaxIndex() + 1, indexStart ) );
					mapParents.Drop( mapParents.Find( type ) );

					parameterMap.ConstructorTypeMap = constructorMap;
				}
				else
				{
					parameterMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), options.TypeConverterOptionsFactory.GetOptions( parameter.ParameterType ), parameterMap.Data.TypeConverterOptions );
					parameterMap.Data.Index = map.GetMaxIndex() + 1;
				}

				map.ParameterMaps.Add( parameterMap );
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
		protected virtual bool CheckForCircularReference( Type type, LinkedList<Type> mapParents )
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

		/// <summary>
		/// Gets the generic type for this class map.
		/// </summary>
		protected virtual Type GetGenericType()
		{
			return GetType().GetTypeInfo().BaseType.GetGenericArguments()[0];
		}
	}
}
