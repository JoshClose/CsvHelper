using System;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class TypeConverterFactoryTests
	{
		[Fact]
		public void GetConverterForUnknownTypeTest()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( TestClass ) );

			Assert.IsType<DefaultTypeConverter>( converter );
		}

		[Fact]
		public void GetConverterForByte()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( byte ) );

			Assert.IsType<ByteConverter>( converter );
		}

		[Fact]
		public void GetConverterForChar()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( char ) );

			Assert.IsType<CharConverter>( converter );
		}

		[Fact]
		public void GetConverterForDateTime()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( DateTime ) );

			Assert.IsType<DateTimeConverter>( converter );
		}

		[Fact]
		public void GetConverterForDecimal()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( decimal ) );

			Assert.IsType<DecimalConverter>( converter );
		}

		[Fact]
		public void GetConverterForDouble()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( double ) );

			Assert.IsType<DoubleConverter>( converter );
		}

		[Fact]
		public void GetConverterForFloat()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( float ) );

			Assert.IsType<SingleConverter>( converter );
		}

		[Fact]
		public void GetConverterForGuid()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( Guid ) );

			Assert.IsType<GuidConverter>( converter );
		}

		[Fact]
		public void GetConverterForInt16()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( short ) );

			Assert.IsType<Int16Converter>( converter );
		}

		[Fact]
		public void GetConverterForInt32()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( int ) );

			Assert.IsType<Int32Converter>( converter );
		}

		[Fact]
		public void GetConverterForInt64()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( long ) );

			Assert.IsType<Int64Converter>( converter );
		}

		[Fact]
		public void GetConverterForNullable()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( int? ) );

			Assert.IsType<NullableConverter>( converter );
		}

		[Fact]
		public void GetConverterForSByte()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( sbyte ) );

			Assert.IsType<SByteConverter>( converter );
		}

		[Fact]
		public void GetConverterForString()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( string ) );

			Assert.IsType<StringConverter>( converter );
		}

		[Fact]
		public void GetConverterForUInt16()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( ushort ) );

			Assert.IsType<UInt16Converter>( converter );
		}

		[Fact]
		public void GetConverterForUInt32()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( uint ) );

			Assert.IsType<UInt32Converter>( converter );
		}

		[Fact]
		public void GetConverterForUInt64()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( ulong ) );

			Assert.IsType<UInt64Converter>( converter );
		}


		private class TestClass
		{
		}
	}
}
