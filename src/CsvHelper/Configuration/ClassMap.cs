// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CsvHelper.Configuration
{
	///<summary>
	/// Maps class members to CSV fields.
	///</summary>
	public abstract class ClassMap
	{
		private static readonly List<Type> enumerableConverters = new List<Type>
		{
			typeof(ArrayConverter),
			typeof(CollectionGenericConverter),
			typeof(EnumerableConverter),
			typeof(IDictionaryConverter),
			typeof(IDictionaryGenericConverter),
			typeof(IEnumerableConverter),
			typeof(IEnumerableGenericConverter)
		};

		/// <summary>
		/// The type of the class this map is for.
		/// </summary>
		public virtual Type ClassType { get; private set; }

		/// <summary>
		/// The class constructor parameter mappings.
		/// </summary>
		public virtual List<ParameterMap> ParameterMaps { get; } = new List<ParameterMap>();

		/// <summary>
		/// The class member mappings.
		/// </summary>
		public virtual MemberMapCollection MemberMaps { get; } = new MemberMapCollection();

		/// <summary>
		/// The class member reference mappings.
		/// </summary>
		public virtual MemberReferenceMapCollection ReferenceMaps { get; } = new MemberReferenceMapCollection();

		/// <summary>
		/// Allow only internal creation of CsvClassMap.
		/// </summary>
		/// <param name="classType">The type of the class this map is for.</param>
		internal ClassMap(Type classType)
		{
			ClassType = classType;
		}

		/// <summary>
		/// Maps a member to a CSV field.
		/// </summary>
		/// <param name="classType">The type of the class this map is for. This may not be the same type
		/// as the member.DeclaringType or the current ClassType due to nested member mappings.</param>
		/// <param name="member">The member to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same member.</param>
		/// <returns>The member mapping.</returns>
		public MemberMap Map(Type classType, MemberInfo member, bool useExistingMap = true)
		{
			if (useExistingMap)
			{
				var existingMap = MemberMaps.Find(member);
				if (existingMap != null)
				{
					return existingMap;
				}
			}

			var memberMap = MemberMap.CreateGeneric(classType, member);
			memberMap.Data.Index = GetMaxIndex() + 1;
			MemberMaps.Add(memberMap);

			return memberMap;
		}

		/// <summary>
		/// Maps a non-member to a CSV field. This allows for writing
		/// data that isn't mapped to a class member.
		/// </summary>
		/// <returns>The member mapping.</returns>
		public virtual MemberMap<object, object> Map()
		{
			var memberMap = new MemberMap<object, object>(null);
			memberMap.Data.Index = GetMaxIndex() + 1;
			MemberMaps.Add(memberMap);

			return memberMap;
		}

		/// <summary>
		/// Maps a member to another class map.
		/// </summary>
		/// <param name="classMapType">The type of the class map.</param>
		/// <param name="member">The member.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the reference map.</param>
		/// <returns>The reference mapping for the member.</returns>
		public virtual MemberReferenceMap References(Type classMapType, MemberInfo member, params object[] constructorArgs)
		{
			if (!typeof(ClassMap).IsAssignableFrom(classMapType))
			{
				throw new InvalidOperationException($"Argument {nameof(classMapType)} is not a CsvClassMap.");
			}

			var existingMap = ReferenceMaps.Find(member);

			if (existingMap != null)
			{
				return existingMap;
			}

			var map = (ClassMap)ReflectionHelper.CreateInstance(classMapType, constructorArgs);
			map.ReIndex(GetMaxIndex() + 1);
			var reference = new MemberReferenceMap(member, map);
			ReferenceMaps.Add(reference);

			return reference;
		}

		/// <summary>
		/// Auto maps all members for the given type. If a member
		/// is mapped again it will override the existing map.
		/// </summary>
		/// <param name="culture">The culture.</param>
		public virtual void AutoMap(CultureInfo culture)
		{
			AutoMap(new CsvConfiguration(culture));
		}

		/// <summary>
		/// Auto maps all members for the given type. If a member 
		/// is mapped again it will override the existing map.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		public virtual void AutoMap(CsvConfiguration configuration)
		{
			var type = GetGenericType();
			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				throw new ConfigurationException("Types that inherit IEnumerable cannot be auto mapped. " +
													 "Did you accidentally call GetRecord or WriteRecord which " +
													 "acts on a single record instead of calling GetRecords or " +
													 "WriteRecords which acts on a list of records?");
			}

			var mapParents = new LinkedList<Type>();
			if (configuration.ShouldUseConstructorParameters(type))
			{
				// This type doesn't have a parameterless constructor so we can't create an
				// instance and set it's member. Constructor parameters need to be created
				// instead. Writing only uses getters, so members will also be mapped
				// for writing purposes.
				AutoMapConstructorParameters(this, configuration, mapParents);
			}

			AutoMapMembers(this, configuration, mapParents);
		}

		/// <summary>
		/// Get the largest index for the
		/// members and references.
		/// </summary>
		/// <returns>The max index.</returns>
		public virtual int GetMaxIndex()
		{
			if (ParameterMaps.Count == 0 && MemberMaps.Count == 0 && ReferenceMaps.Count == 0)
			{
				return -1;
			}

			var indexes = new List<int>();
			if (ParameterMaps.Count > 0)
			{
				indexes.AddRange(ParameterMaps.Select(parameterMap => parameterMap.GetMaxIndex()));
			}

			if (MemberMaps.Count > 0)
			{
				indexes.Add(MemberMaps.Max(pm => pm.Data.Index));
			}

			if (ReferenceMaps.Count > 0)
			{
				indexes.AddRange(ReferenceMaps.Select(referenceMap => referenceMap.GetMaxIndex()));
			}

			return indexes.Max();
		}

		/// <summary>
		/// Resets the indexes based on the given start index.
		/// </summary>
		/// <param name="indexStart">The index start.</param>
		/// <returns>The last index + 1.</returns>
		public virtual int ReIndex(int indexStart = 0)
		{
			foreach (var parameterMap in ParameterMaps)
			{
				parameterMap.Data.Index = indexStart + parameterMap.Data.Index;
			}

			foreach (var memberMap in MemberMaps)
			{
				if (!memberMap.Data.IsIndexSet)
				{
					memberMap.Data.Index = indexStart + memberMap.Data.Index;
				}
			}

			foreach (var referenceMap in ReferenceMaps)
			{
				indexStart = referenceMap.Data.Mapping.ReIndex(indexStart);
			}

			return indexStart;
		}

		/// <summary>
		/// Auto maps the given map and checks for circular references as it goes.
		/// </summary>
		/// <param name="map">The map to auto map.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="mapParents">The list of parents for the map.</param>
		/// <param name="indexStart">The index starting point.</param>
		protected virtual void AutoMapMembers(ClassMap map, CsvConfiguration configuration, LinkedList<Type> mapParents, int indexStart = 0)
		{
			var type = map.GetGenericType();

			var flags = BindingFlags.Instance | BindingFlags.Public;
			if (configuration.IncludePrivateMembers)
			{
				flags = flags | BindingFlags.NonPublic;
			}

			var members = new List<MemberInfo>();
			if ((configuration.MemberTypes & MemberTypes.Properties) == MemberTypes.Properties)
			{
				// We need to go up the declaration tree and find the actual type the property
				// exists on and use that PropertyInfo instead. This is so we can get the private
				// set method for the property.
				var properties = new List<PropertyInfo>();
				foreach (var property in ReflectionHelper.GetUniqueProperties(type, flags))
				{
					if (properties.Any(p => p.Name == property.Name))
					{
						// Multiple properties could have the same name if a child class property
						// is hiding a parent class property by using `new`. It's possible that
						// the order of the properties returned 
						continue;
					}

					properties.Add(ReflectionHelper.GetDeclaringProperty(type, property, flags));
				}

				members.AddRange(properties);
			}

			if ((configuration.MemberTypes & MemberTypes.Fields) == MemberTypes.Fields)
			{
				var fields = new List<MemberInfo>();
				foreach (var field in type.GetFields(flags))
				{
					if (!field.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
					{
						fields.Add(field);
					}
				}

				members.AddRange(fields);
			}

			foreach (var member in members)
			{
				var typeConverterType = configuration.TypeConverterCache.GetConverter(member).GetType();

				if (configuration.HasHeaderRecord && enumerableConverters.Contains(typeConverterType))
				{
					// Enumerable converters can't write the header properly, so skip it.
					continue;
				}

				var memberTypeInfo = member.MemberType().GetTypeInfo();
				var isDefaultConverter = typeConverterType == typeof(DefaultTypeConverter);
				if (isDefaultConverter)
				{
					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.

					if (configuration.IgnoreReferences)
					{
						continue;
					}

					if (CheckForCircularReference(member.MemberType(), mapParents))
					{
						continue;
					}

					mapParents.AddLast(type);
					var refMapType = typeof(DefaultClassMap<>).MakeGenericType(member.MemberType());
					var refMap = (ClassMap)ReflectionHelper.CreateInstance(refMapType);

					if (memberTypeInfo.HasConstructor() && !memberTypeInfo.HasParameterlessConstructor() && !memberTypeInfo.IsUserDefinedStruct())
					{
						AutoMapConstructorParameters(refMap, configuration, mapParents, Math.Max(map.GetMaxIndex() + 1, indexStart));
					}

					// Need to use Max here for nested types.
					AutoMapMembers(refMap, configuration, mapParents, Math.Max(map.GetMaxIndex() + 1, indexStart));
					mapParents.Drop(mapParents.Find(type));

					if (refMap.MemberMaps.Count > 0 || refMap.ReferenceMaps.Count > 0)
					{
						var referenceMap = new MemberReferenceMap(member, refMap);
						if (configuration.ReferenceHeaderPrefix != null)
						{
							referenceMap.Data.Prefix = configuration.ReferenceHeaderPrefix(member.MemberType(), member.Name);
						}

						ApplyAttributes(referenceMap);

						map.ReferenceMaps.Add(referenceMap);
					}
				}
				else
				{
					// Only add the member map if it can be converted later on.
					// If the member will use the default converter, don't add it because
					// we don't want the .ToString() value to be used when auto mapping.

					// Use the top of the map tree. This will maps that have been auto mapped
					// to later on get a reference to a map by doing map.Map( m => m.A.B.C.Id )
					// and it will return the correct parent map type of A instead of C.
					var classType = mapParents.First?.Value ?? map.ClassType;
					var memberMap = MemberMap.CreateGeneric(classType, member);

					// Use global values as the starting point.
					memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions(), configuration.TypeConverterOptionsCache.GetOptions(member.MemberType()), memberMap.Data.TypeConverterOptions);
					memberMap.Data.Index = map.GetMaxIndex() + 1;

					ApplyAttributes(memberMap);

					map.MemberMaps.Add(memberMap);
				}
			}

			map.ReIndex(indexStart);
		}

		/// <summary>
		/// Auto maps the given map using constructor parameters.
		/// </summary>
		/// <param name="map">The map.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="mapParents">The list of parents for the map.</param>
		/// <param name="indexStart">The index starting point.</param>
		protected virtual void AutoMapConstructorParameters(ClassMap map, CsvConfiguration configuration, LinkedList<Type> mapParents, int indexStart = 0)
		{
			var type = map.GetGenericType();
			var constructor = configuration.GetConstructor(map.ClassType);
			var parameters = constructor.GetParameters();

			foreach (var parameter in parameters)
			{
				var typeConverterType = configuration.TypeConverterCache.GetConverter(parameter.ParameterType).GetType();

				var parameterMap = new ParameterMap(parameter);

				var memberTypeInfo = parameter.ParameterType.GetTypeInfo();
				var isDefaultConverter = typeConverterType == typeof(DefaultTypeConverter);
				if (isDefaultConverter && (memberTypeInfo.HasParameterlessConstructor() || memberTypeInfo.IsUserDefinedStruct()))
				{
					// If the type is not one covered by our type converters
					// and it has a parameterless constructor, create a
					// reference map for it.

					if (configuration.IgnoreReferences)
					{
						throw new InvalidOperationException($"Configuration '{nameof(configuration.IgnoreReferences)}' can't be true " +
															  "when using types without a default constructor. Constructor parameters " +
															  "are used and all members including references must be used.");
					}

					if (CheckForCircularReference(parameter.ParameterType, mapParents))
					{
						throw new InvalidOperationException($"A circular reference was detected in constructor paramter '{parameter.Name}'." +
															  "Since all parameters must be supplied for a constructor, this parameter can't be skipped.");
					}

					mapParents.AddLast(type);
					var refMapType = typeof(DefaultClassMap<>).MakeGenericType(parameter.ParameterType);
					var refMap = (ClassMap)ReflectionHelper.CreateInstance(refMapType);
					AutoMapMembers(refMap, configuration, mapParents, Math.Max(map.GetMaxIndex() + 1, indexStart));
					mapParents.Drop(mapParents.Find(type));

					var referenceMap = new ParameterReferenceMap(parameter, refMap);
					if (configuration.ReferenceHeaderPrefix != null)
					{
						referenceMap.Data.Prefix = configuration.ReferenceHeaderPrefix(memberTypeInfo.MemberType(), memberTypeInfo.Name);
					}

					parameterMap.ReferenceMap = referenceMap;
				}
				else if (configuration.ShouldUseConstructorParameters(parameter.ParameterType))
				{
					mapParents.AddLast(type);
					var constructorMapType = typeof(DefaultClassMap<>).MakeGenericType(parameter.ParameterType);
					var constructorMap = (ClassMap)ReflectionHelper.CreateInstance(constructorMapType);
					// Need to use Max here for nested types.
					AutoMapConstructorParameters(constructorMap, configuration, mapParents, Math.Max(map.GetMaxIndex() + 1, indexStart));
					mapParents.Drop(mapParents.Find(type));

					parameterMap.ConstructorTypeMap = constructorMap;
				}
				else
				{
					parameterMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions(), configuration.TypeConverterOptionsCache.GetOptions(parameter.ParameterType), parameterMap.Data.TypeConverterOptions);
					parameterMap.Data.Index = map.GetMaxIndex() + 1;
				}

				map.ParameterMaps.Add(parameterMap);
			}

			map.ReIndex(indexStart);
		}

		/// <summary>
		/// Checks for circular references.
		/// </summary>
		/// <param name="type">The type to check for.</param>
		/// <param name="mapParents">The list of parents to check against.</param>
		/// <returns>A value indicating if a circular reference was found.
		/// True if a circular reference was found, otherwise false.</returns>
		protected virtual bool CheckForCircularReference(Type type, LinkedList<Type> mapParents)
		{
			if (mapParents.Count == 0)
			{
				return false;
			}

			var node = mapParents.Last;
			while (true)
			{
				if (node.Value == type)
				{
					return true;
				}

				node = node.Previous;
				if (node == null)
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

		/// <summary>
		/// Applies attribute configurations to the map.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		protected virtual void ApplyAttributes(MemberMap memberMap)
		{
			var member = memberMap.Data.Member;
			var attributes = member.GetCustomAttributes().OfType<IMemberMapper>();

			foreach (var attribute in attributes)
			{
				attribute.ApplyTo(memberMap);
			}
		}

		/// <summary>
		/// Applies attribute configurations to the map.
		/// </summary>
		/// <param name="referenceMap">The reference map.</param>
		protected virtual void ApplyAttributes(MemberReferenceMap referenceMap)
		{
			var member = referenceMap.Data.Member;
			var attributes = member.GetCustomAttributes().OfType<IMemberReferenceMapper>();

			foreach (var attribute in attributes)
			{
				attribute.ApplyTo(referenceMap);
			}
		}
	}
}
