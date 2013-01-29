using System;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterFactoryTests
	{
		[TestMethod]
		public void GetConverterForUnknownTypeTest()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( TestClass ) );

			Assert.IsInstanceOfType( converter, typeof( DefaultTypeConverter ) );
		}

		[TestMethod]
		public void GetConverterForByte()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( byte ) );

			Assert.IsInstanceOfType( converter, typeof( ByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForChar()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( char ) );

			Assert.IsInstanceOfType( converter, typeof( CharConverter ) );
		}

		[TestMethod]
		public void GetConverterForDateTime()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( DateTime ) );

			Assert.IsInstanceOfType( converter, typeof( DateTimeConverter ) );
		}

		[TestMethod]
		public void GetConverterForDecimal()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( decimal ) );

			Assert.IsInstanceOfType( converter, typeof( DecimalConverter ) );
		}

		[TestMethod]
		public void GetConverterForDouble()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( double ) );

			Assert.IsInstanceOfType( converter, typeof( DoubleConverter ) );
		}

		[TestMethod]
		public void GetConverterForFloat()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( float ) );

			Assert.IsInstanceOfType( converter, typeof( SingleConverter ) );
		}

		[TestMethod]
		public void GetConverterForGuid()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( Guid ) );

			Assert.IsInstanceOfType( converter, typeof( GuidConverter ) );
		}

		[TestMethod]
		public void GetConverterForInt16()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( short ) );

			Assert.IsInstanceOfType( converter, typeof( Int16Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt32()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( int ) );

			Assert.IsInstanceOfType( converter, typeof( Int32Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt64()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( long ) );

			Assert.IsInstanceOfType( converter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void GetConverterForNullable()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( int? ) );

			Assert.IsInstanceOfType( converter, typeof( NullableConverter ) );
		}

		[TestMethod]
		public void GetConverterForSByte()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( sbyte ) );

			Assert.IsInstanceOfType( converter, typeof( SByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForString()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( string ) );

			Assert.IsInstanceOfType( converter, typeof( StringConverter ) );
		}

		[TestMethod]
		public void GetConverterForUInt16()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( ushort ) );

			Assert.IsInstanceOfType( converter, typeof( UInt16Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt32()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( uint ) );

			Assert.IsInstanceOfType( converter, typeof( UInt32Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt64()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( ulong ) );

			Assert.IsInstanceOfType( converter, typeof( UInt64Converter ) );
		}

		[TestMethod]
		public void GetConverterForEnum()
		{
			var converter = TypeConverterFactory.CreateTypeConverter( typeof( TestEnum ) );

			Assert.IsInstanceOfType( converter, typeof( EnumConverter ) );
		}


		private class TestClass
		{
		}

		private enum TestEnum
		{
		}
	}
}
