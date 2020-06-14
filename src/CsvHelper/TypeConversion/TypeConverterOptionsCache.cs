// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Concurrent;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Caches <see cref="TypeConverterOptions"/> for a given type.
	/// </summary>
	public class TypeConverterOptionsCache
	{
		private ConcurrentDictionary<Type, TypeConverterOptions> typeConverterOptions = new ConcurrentDictionary<Type, TypeConverterOptions>();

		/// <summary>
		/// Adds the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the options are for.</param>
		/// <param name="options">The options.</param>
		public void AddOptions(Type type, TypeConverterOptions options)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			typeConverterOptions[type] = options ?? throw new ArgumentNullException(nameof(options));
		}

		/// <summary>
		/// Adds the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the options are for.</typeparam>
		/// <param name="options">The options.</param>
		public void AddOptions<T>(TypeConverterOptions options)
		{
			AddOptions(typeof(T), options);
		}

		/// <summary>
		/// Removes the <see cref="TypeConverterOptions"/> for the given type.
		/// </summary>
		/// <param name="type">The type to remove the options for.</param>
		public void RemoveOptions(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			typeConverterOptions.TryRemove(type, out TypeConverterOptions _);
		}

		/// <summary>
		/// Removes the <see cref="TypeConverterOptions"/> for the given type.
		/// </summary>
		/// <typeparam name="T">The type to remove the options for.</typeparam>
		public void RemoveOptions<T>()
		{
			RemoveOptions(typeof(T));
		}

		/// <summary>
		/// Get the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the options are for.</param>
		/// <returns>The options for the given type.</returns>
		public TypeConverterOptions GetOptions(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException();
			}

			return typeConverterOptions.GetOrAdd(type, _ => new TypeConverterOptions());
		}

		/// <summary>
		/// Get the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the options are for.</typeparam>
		/// <returns>The options for the given type.</returns>
		public TypeConverterOptions GetOptions<T>()
		{
			return GetOptions(typeof(T));
		}
	}
}
