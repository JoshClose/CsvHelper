// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Creates <see cref="TypeConverterOptions"/>.
	/// </summary>
	public static class TypeConverterOptionsFactory
	{
		private static readonly Dictionary<Type, TypeConverterOptions> typeConverterOptions = new Dictionary<Type, TypeConverterOptions>();
		private static readonly object locker = new object();

		/// <summary>
		/// Adds the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the options are for.</param>
		/// <param name="options">The options.</param>
		public static void AddOptions( Type type, TypeConverterOptions options )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			if( options == null )
			{
				throw new ArgumentNullException( "options" );
			}

			lock( locker )
			{
				typeConverterOptions[type] = options;
			}
		}

		/// <summary>
		/// Adds the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the options are for.</typeparam>
		/// <param name="options">The options.</param>
		public static void AddOptions<T>( TypeConverterOptions options )
		{
			AddOptions( typeof( T ), options );
		}

		/// <summary>
		/// Removes the <see cref="TypeConverterOptions"/> for the given type.
		/// </summary>
		/// <param name="type">The type to remove the options for.</param>
		public static void RemoveOptions( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			lock( locker )
			{
				typeConverterOptions.Remove( type );
			}
		}

		/// <summary>
		/// Removes the <see cref="TypeConverterOptions"/> for the given type.
		/// </summary>
		/// <typeparam name="T">The type to remove the options for.</typeparam>
		public static void RemoveOptions<T>()
		{
			RemoveOptions( typeof( T ) );
		}

		/// <summary>
		/// Get the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the options are for.</param>
		/// <returns>The options for the given type.</returns>
		public static TypeConverterOptions GetOptions( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException();
			}

			lock( locker )
			{
				TypeConverterOptions options;

				if( !typeConverterOptions.TryGetValue( type, out options ) )
				{
					options = new TypeConverterOptions();
					typeConverterOptions.Add( type, options );
				}

                return TypeConverterOptions.Merge( options );
			}
		}

		/// <summary>
		/// Get the <see cref="TypeConverterOptions"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the options are for.</typeparam>
		/// <returns>The options for the given type.</returns>
		public static TypeConverterOptions GetOptions<T>()
		{
			return GetOptions( typeof( T ) );
		}
	}
}
