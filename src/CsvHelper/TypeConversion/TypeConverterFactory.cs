using System;
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
		/// <summary>
		/// Creates an <see cref="ITypeConverter"/> from the given <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of the converter to create.</param>
		/// <returns>The created <see cref="ITypeConverter"/>.</returns>
		/// <exception cref="System.NotSupportedException">Thrown when there is no <see cref="ITypeConverter"/> for the given type.</exception>
		public static ITypeConverter CreateTypeConverter( Type type )
		{
			if( type == typeof( bool ) )
			{
				return new BooleanConverter();
			}
			if( type == typeof( byte ) )
			{
				return new ByteConverter();
			}
			if( type == typeof( char ) )
			{
				return new CharConverter();
			}
			if( type == typeof( DateTime ) )
			{
				return new DateTimeConverter();
			}
			if( type == typeof( decimal ) )
			{
				return new DecimalConverter();
			}
			if( type == typeof( double ) )
			{
				return new DoubleConverter();
			}
			if( typeof( Enum ).IsAssignableFrom( type ) )
			{
				return new EnumConverter( type );
			}
			if( type == typeof( float ) )
			{
				return new SingleConverter();
			}
			if( type == typeof( Guid ) )
			{
				return new GuidConverter();
			}
			if( type == typeof( short ) )
			{
				return new Int16Converter();
			}
			if( type == typeof( int ) )
			{
				return new Int32Converter();
			}
			if( type == typeof( long ) )
			{
				return new Int64Converter();
			}
			if( type == typeof( sbyte ) )
			{
				return new SByteConverter();
			}
			if( type == typeof( string ) )
			{
				return new StringConverter();
			}
			if( type == typeof( ushort ) )
			{
				return new UInt16Converter();
			}
			if( type == typeof( uint ) )
			{
				return new UInt32Converter();
			}
			if( type == typeof( ulong ) )
			{
				return new UInt64Converter();
			}

#if WINRT_4_5
			var isGenericType = type.GetTypeInfo().IsGenericType;
#else
			var isGenericType = type.IsGenericType;
#endif
			if( isGenericType && type.GetGenericTypeDefinition() == typeof( System.Nullable<> ) )
			{
				return new NullableConverter( type );
			}

			return new DefaultTypeConverter();
		}
	}
}
