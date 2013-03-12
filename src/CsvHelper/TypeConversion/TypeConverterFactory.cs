// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Reflection;
#if WINRT_4_5
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Creates <see cref="ITypeConverter"/>s.
	/// </summary>
	public static class TypeConverterFactory
	{
		private static readonly Dictionary<Type, ITypeConverter> typeConverters = new Dictionary<Type, ITypeConverter>();
		private static readonly object locker = new object();

		/// <summary>
		/// Initializes the <see cref="TypeConverterFactory" /> class.
		/// </summary>
		static TypeConverterFactory()
		{
			CreateDefaultConverters();
		}

		/// <summary>
		/// Adds the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the converter converts.</param>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public static void AddConverter( Type type, ITypeConverter typeConverter )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			if( typeConverter == null )
			{
				throw new ArgumentNullException( "typeConverter" );
			}

			lock( locker )
			{
				typeConverters[type] = typeConverter;
			}
		}

		/// <summary>
		/// Adds the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the converter converts.</typeparam>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public static void AddConverter<T>( ITypeConverter typeConverter )
		{
			if( typeConverter == null )
			{
				throw new ArgumentNullException( "typeConverter" );
			}

			lock( locker )
			{
				typeConverters[typeof( T )] = typeConverter;
			}
		}

		/// <summary>
		/// Removes the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type to remove the converter for.</param>
		public static void RemoveConverter( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			lock( locker )
			{
				typeConverters.Remove( type );
			}
		}

		/// <summary>
		/// Removes the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type to remove the converter for.</typeparam>
		public static void RemoveConverter<T>()
		{
			RemoveConverter( typeof( T ) );
		}

		/// <summary>
		/// Gets the converter for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type to get the converter for.</param>
		/// <returns>The <see cref="ITypeConverter"/> for the given <see cref="Type"/>.</returns>
		public static ITypeConverter GetConverter( Type type )
		{
			if( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			lock( locker )
			{
				ITypeConverter typeConverter;
				if( typeConverters.TryGetValue( type, out typeConverter ) )
				{
					return typeConverter;
				}
			}

			if( typeof( Enum ).IsAssignableFrom( type ) )
			{
				AddConverter( type, new EnumConverter( type ) );
				return GetConverter( type );
			}

#if WINRT_4_5
			var isGenericType = type.GetTypeInfo().IsGenericType;
#else
			var isGenericType = type.IsGenericType;
#endif
			if( isGenericType && type.GetGenericTypeDefinition() == typeof( Nullable<> ) )
			{
				AddConverter( type, new NullableConverter( type ) );
				return GetConverter( type );
			}

			return new DefaultTypeConverter();
		}

		/// <summary>
		/// Gets the converter for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type to get the converter for.</typeparam>
		/// <returns>The <see cref="ITypeConverter"/> for the given <see cref="Type"/>.</returns>
		public static ITypeConverter GetConverter<T>()
		{
			return GetConverter( typeof( T ) );
		}

		private static void CreateDefaultConverters()
		{
			AddConverter( typeof( bool ), new BooleanConverter() );
			AddConverter( typeof( byte ), new ByteConverter() );
			AddConverter( typeof( char ), new CharConverter() );
			AddConverter( typeof( DateTime ), new DateTimeConverter() );
			AddConverter( typeof( decimal ), new DecimalConverter() );
			AddConverter( typeof( double ), new DoubleConverter() );
			AddConverter( typeof( float ), new SingleConverter() );
			AddConverter( typeof( Guid ), new GuidConverter() );
			AddConverter( typeof( short ), new Int16Converter() );
			AddConverter( typeof( int ), new Int32Converter() );
			AddConverter( typeof( long ), new Int64Converter() );
			AddConverter( typeof( sbyte ), new SByteConverter() );
			AddConverter( typeof( string ), new StringConverter() );
			AddConverter( typeof( ushort ), new UInt16Converter() );
			AddConverter( typeof( uint ), new UInt32Converter() );
			AddConverter( typeof( ulong ), new UInt64Converter() );
		} 
	}
}
