// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;
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
			var converter = TypeConverterFactory.GetConverter( typeof( TestUnknownClass ) );

			Assert.IsInstanceOfType( converter, typeof( DefaultTypeConverter ) );
		}

		[TestMethod]
		public void GetConverterForKnownTypeTest()
		{
			var converter = TypeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsInstanceOfType( converter, typeof( DefaultTypeConverter ) );

			TypeConverterFactory.SetConverter<TestKnownClass>( new TestKnownConverter() );
			converter = TypeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsInstanceOfType( converter, typeof( TestKnownConverter ) );
		}

		[TestMethod]
		public void GetConverterForByte()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( byte ) );

			Assert.IsInstanceOfType( converter, typeof( ByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForChar()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( char ) );

			Assert.IsInstanceOfType( converter, typeof( CharConverter ) );
		}

		[TestMethod]
		public void GetConverterForDateTime()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( DateTime ) );

			Assert.IsInstanceOfType( converter, typeof( DateTimeConverter ) );
		}

		[TestMethod]
		public void GetConverterForDecimal()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( decimal ) );

			Assert.IsInstanceOfType( converter, typeof( DecimalConverter ) );
		}

		[TestMethod]
		public void GetConverterForDouble()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( double ) );

			Assert.IsInstanceOfType( converter, typeof( DoubleConverter ) );
		}

		[TestMethod]
		public void GetConverterForFloat()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( float ) );

			Assert.IsInstanceOfType( converter, typeof( SingleConverter ) );
		}

		[TestMethod]
		public void GetConverterForGuid()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( Guid ) );

			Assert.IsInstanceOfType( converter, typeof( GuidConverter ) );
		}

		[TestMethod]
		public void GetConverterForInt16()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( short ) );

			Assert.IsInstanceOfType( converter, typeof( Int16Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt32()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( int ) );

			Assert.IsInstanceOfType( converter, typeof( Int32Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt64()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( long ) );

			Assert.IsInstanceOfType( converter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void GetConverterForNullable()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( int? ) );

			Assert.IsInstanceOfType( converter, typeof( NullableConverter ) );
		}

		[TestMethod]
		public void GetConverterForSByte()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( sbyte ) );

			Assert.IsInstanceOfType( converter, typeof( SByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForString()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( string ) );

			Assert.IsInstanceOfType( converter, typeof( StringConverter ) );
		}

		[TestMethod]
		public void GetConverterForUInt16()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( ushort ) );

			Assert.IsInstanceOfType( converter, typeof( UInt16Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt32()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( uint ) );

			Assert.IsInstanceOfType( converter, typeof( UInt32Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt64()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( ulong ) );

			Assert.IsInstanceOfType( converter, typeof( UInt64Converter ) );
		}

		[TestMethod]
		public void GetConverterForEnum()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( TestEnum ) );

			Assert.IsInstanceOfType( converter, typeof( EnumConverter ) );
		}


		private class TestUnknownClass
		{
		}

		private class TestKnownClass
		{
		}

		private class TestKnownConverter : DefaultTypeConverter
		{
		}

		private enum TestEnum
		{
		}
	}
}
