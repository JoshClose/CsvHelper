// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Caches <see cref="ITypeConverter"/>s for a given type.
	/// </summary>
	public class TypeConverterCache
	{
		private readonly Dictionary<Type, ITypeConverter> typeConverters = new Dictionary<Type, ITypeConverter>();
		private readonly List<ITypeConverterFactory> typeConverterFactories = new List<ITypeConverterFactory>();
		private readonly Dictionary<Type, ITypeConverterFactory> typeConverterFactoryCache = new Dictionary<Type, ITypeConverterFactory>();

		/// <summary>
		/// Initializes the <see cref="TypeConverterCache" /> class.
		/// </summary>
		public TypeConverterCache()
		{
			CreateDefaultConverters();
		}

		/// <summary>
		/// Determines if there is a converter registered for the given type.
		/// </summary>
		/// <param name="type">The type to check.</param>
		/// <returns><c>true</c> if the converter is registered, otherwise false.</returns>
		public bool Contains(Type type)
		{
			return typeConverters.ContainsKey(type);
		}

		/// <summary>
		/// Adds the <see cref="ITypeConverterFactory"/>.
		/// Factories are queried in order of being added and first factory that handles the type is used for creating the <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="typeConverterFactory">Type converter factory</param>
		public void AddConverterFactory(ITypeConverterFactory typeConverterFactory)
		{
			if (typeConverterFactory == null)
			{
				throw new ArgumentNullException(nameof(typeConverterFactory));
			}

			typeConverterFactories.Add(typeConverterFactory);
		}

		/// <summary>
		/// Adds the <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type">The type the converter converts.</param>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public void AddConverter(Type type, ITypeConverter typeConverter)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (typeConverter == null)
			{
				throw new ArgumentNullException(nameof(typeConverter));
			}

			typeConverters[type] = typeConverter;
		}

		/// <summary>
		/// Adds the <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the converter converts.</typeparam>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public void AddConverter<T>(ITypeConverter typeConverter)
		{
			if (typeConverter == null)
			{
				throw new ArgumentNullException(nameof(typeConverter));
			}

			typeConverters[typeof(T)] = typeConverter;
		}

		/// <summary>
		/// Adds the given <see cref="ITypeConverter"/> to all registered types.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		public void AddConverter(ITypeConverter typeConverter)
		{
			foreach (var type in typeConverters.Keys)
			{
				typeConverters[type] = typeConverter;
			}
		}

		/// <summary>
		/// Removes the <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type">The type to remove the converter for.</param>
		public void RemoveConverter(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			typeConverters.Remove(type);
		}

		/// <summary>
		/// Removes the <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.
		/// </summary>
		/// <typeparam name="T">The type to remove the converter for.</typeparam>
		public void RemoveConverter<T>()
		{
			RemoveConverter(typeof(T));
		}

		/// <summary>
		/// Removes the ITypeConverterFactory.
		/// </summary>
		/// <param name="typeConverterFactory">The ITypeConverterFactory to remove.</param>
		public void RemoveConverterFactory(ITypeConverterFactory typeConverterFactory)
		{
			typeConverterFactories.Remove(typeConverterFactory);
			var toRemove = typeConverterFactoryCache.Where(pair => pair.Value == typeConverterFactory);
			foreach (var pair in toRemove)
			{
				typeConverterFactoryCache.Remove(pair.Key);
			}
		}

		/// <summary>
		/// Gets the converter for the given <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type">The type to get the converter for.</param>
		/// <returns>The <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.</returns>
		public ITypeConverter GetConverter(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (typeConverters.TryGetValue(type, out ITypeConverter typeConverter))
			{
				return typeConverter;
			}

			if (!typeConverterFactoryCache.TryGetValue(type, out var factory))
			{
				factory = typeConverterFactories.FirstOrDefault(f => f.CanCreate(type));
				if (factory != null)
				{
					typeConverterFactoryCache[type] = factory;
				}
			}

			if (factory != null)
			{
				if (factory.Create(type, this, out typeConverter))
				{
					AddConverter(type, typeConverter);
				}

				return typeConverter;
			}

			return new DefaultTypeConverter();
		}

		/// <summary>
		/// Gets the converter for the given member. If an attribute is
		/// found on the member, that will be used, otherwise the cache
		/// will be used.
		/// </summary>
		/// <param name="member">The member to get the converter for.</param>
		public ITypeConverter GetConverter(MemberInfo member)
		{
			var typeConverterAttribute = member.GetCustomAttribute<TypeConverterAttribute>();
			if (typeConverterAttribute != null)
			{
				return typeConverterAttribute.TypeConverter;
			}

			return GetConverter(member.MemberType());
		}

		/// <summary>
		/// Gets the converter for the given <see cref="System.Type"/>.
		/// </summary>
		/// <typeparam name="T">The type to get the converter for.</typeparam>
		/// <returns>The <see cref="ITypeConverter"/> for the given <see cref="System.Type"/>.</returns>
		public ITypeConverter GetConverter<T>()
		{
			return GetConverter(typeof(T));
		}

		private void CreateDefaultConverters()
		{
			AddConverter(typeof(BigInteger), new BigIntegerConverter());
			AddConverter(typeof(bool), new BooleanConverter());
			AddConverter(typeof(byte), new ByteConverter());
			AddConverter(typeof(byte[]), new ByteArrayConverter());
			AddConverter(typeof(char), new CharConverter());
			AddConverter(typeof(DateTime), new DateTimeConverter());
			AddConverter(typeof(DateTimeOffset), new DateTimeOffsetConverter());
			AddConverter(typeof(decimal), new DecimalConverter());
			AddConverter(typeof(double), new DoubleConverter());
			AddConverter(typeof(float), new SingleConverter());
			AddConverter(typeof(Guid), new GuidConverter());
			AddConverter(typeof(short), new Int16Converter());
			AddConverter(typeof(int), new Int32Converter());
			AddConverter(typeof(long), new Int64Converter());
			AddConverter(typeof(sbyte), new SByteConverter());
			AddConverter(typeof(string), new StringConverter());
			AddConverter(typeof(TimeSpan), new TimeSpanConverter());
			AddConverter(typeof(Type), new TypeConverter());
			AddConverter(typeof(ushort), new UInt16Converter());
			AddConverter(typeof(uint), new UInt32Converter());
			AddConverter(typeof(ulong), new UInt64Converter());
			AddConverter(typeof(Uri), new UriConverter());
#if NET6_0_OR_GREATER
			AddConverter(typeof(DateOnly), new DateOnlyConverter());
			AddConverter(typeof(TimeOnly), new TimeOnlyConverter());
#endif

			AddConverterFactory(new EnumConverterFactory());
			AddConverterFactory(new NullableConverterFactory());
			AddConverterFactory(new CollectionConverterFactory());
		}
	}
}
