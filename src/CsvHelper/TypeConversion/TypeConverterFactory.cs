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

		/// <summary>
		/// Gets the available <see cref="ITypeConverter"/>s.
		/// </summary>
		public static Dictionary<Type, ITypeConverter> TypeConverters
		{
			get { return typeConverters; }
		}

		/// <summary>
		/// Initializes the <see cref="TypeConverterFactory" /> class.
		/// </summary>
		static TypeConverterFactory()
		{
			CreateDefaultConverters();
		}

		/// <summary>
		/// Sets the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type the converter converts.</param>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public static void SetConverter( Type type, ITypeConverter typeConverter )
		{
			TypeConverters[type] = typeConverter;
		}

		/// <summary>
		/// Sets the <see cref="ITypeConverter"/> for the given <see cref="Type"/>.
		/// </summary>
		/// <typeparam name="T">The type the converter converts.</typeparam>
		/// <param name="typeConverter">The type converter that converts the type.</param>
		public static void SetConverter<T>( ITypeConverter typeConverter )
		{
			TypeConverters[typeof( T )] = typeConverter;
		}

		/// <summary>
		/// Gets the converter for the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type to get the converter for.</param>
		/// <returns>The <see cref="ITypeConverter"/> for the given <see cref="Type"/>.</returns>
		public static ITypeConverter GetConverter( Type type )
		{
			if( typeConverters.ContainsKey( type ) )
			{
				return typeConverters[type];
			}

			if( typeof( Enum ).IsAssignableFrom( type ) )
			{
				SetConverter( type, new EnumConverter( type ) );
				return GetConverter( type );
			}

#if WINRT_4_5
			var isGenericType = type.GetTypeInfo().IsGenericType;
#else
			var isGenericType = type.IsGenericType;
#endif
			if( isGenericType && type.GetGenericTypeDefinition() == typeof( Nullable<> ) )
			{
				SetConverter( type, new NullableConverter( type ) );
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
			SetConverter( typeof( bool ), new BooleanConverter() );
			SetConverter( typeof( byte ), new ByteConverter() );
			SetConverter( typeof( char ), new CharConverter() );
			SetConverter( typeof( DateTime ), new DateTimeConverter() );
			SetConverter( typeof( decimal ), new DecimalConverter() );
			SetConverter( typeof( double ), new DoubleConverter() );
			SetConverter( typeof( float ), new SingleConverter() );
			SetConverter( typeof( Guid ), new GuidConverter() );
			SetConverter( typeof( short ), new Int16Converter() );
			SetConverter( typeof( int ), new Int32Converter() );
			SetConverter( typeof( long ), new Int64Converter() );
			SetConverter( typeof( sbyte ), new SByteConverter() );
			SetConverter( typeof( string ), new StringConverter() );
			SetConverter( typeof( ushort ), new UInt16Converter() );
			SetConverter( typeof( uint ), new UInt32Converter() );
			SetConverter( typeof( ulong ), new UInt64Converter() );
		} 
	}
}
