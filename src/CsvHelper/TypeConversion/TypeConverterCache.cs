// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		/// <summary>
		/// Initializes the <see cref="TypeConverterCache" /> class.
		/// </summary>
		public TypeConverterCache()
		{
			CreateDefaultConverters();
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

			if (typeof(Enum).IsAssignableFrom(type))
			{
				AddConverter(type, new EnumConverter(type));
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				AddConverter(type, new NullableConverter(type, this));
				return GetConverter(type);
			}

			if (type.IsArray)
			{
				AddConverter(type, new ArrayConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
			{
				AddConverter(type, new IDictionaryGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
			{
				AddConverter(type, new IDictionaryGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				AddConverter(type, new CollectionGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Collection<>))
			{
				AddConverter(type, new CollectionGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
			{
				AddConverter(type, new IEnumerableGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
			{
				AddConverter(type, new IEnumerableGenericConverter());
				return GetConverter(type);
			}

			if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				AddConverter(type, new IEnumerableGenericConverter());
				return GetConverter(type);
			}

			// A specific IEnumerable converter doesn't exist.
			if (typeof(IEnumerable).IsAssignableFrom(type))
			{
				return new EnumerableConverter();
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
			// Collection types need to come after value types.
			AddConverter(typeof(IList), new IEnumerableConverter());
			AddConverter(typeof(ICollection), new IEnumerableConverter());
			AddConverter(typeof(IEnumerable), new IEnumerableConverter());
			AddConverter(typeof(IDictionary), new IDictionaryConverter());
		}
	}
}
