// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterFactoryTests
	{
		[TestMethod]
		public void GetConverterForUnknownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( TestUnknownClass ) );

			Assert.IsInstanceOfType( converter, typeof( DefaultTypeConverter ) );
		}

		[TestMethod]
		public void GetConverterForKnownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsInstanceOfType( converter, typeof( DefaultTypeConverter ) );

			typeConverterFactory.AddConverter<TestKnownClass>( new TestKnownConverter() );
			converter = typeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsInstanceOfType( converter, typeof( TestKnownConverter ) );
		}

		[TestMethod]
		public void RemoveConverterForUnknownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			typeConverterFactory.RemoveConverter<TestUnknownClass>();
			typeConverterFactory.RemoveConverter( typeof( TestUnknownClass ) );
		}

		[TestMethod]
		public void GetConverterForByteTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( byte ) );

			Assert.IsInstanceOfType( converter, typeof( ByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForByteArrayTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( byte[] ) );

			Assert.IsInstanceOfType( converter, typeof( ByteArrayConverter ) );
		}

		[TestMethod]
		public void GetConverterForCharTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( char ) );

			Assert.IsInstanceOfType( converter, typeof( CharConverter ) );
		}

		[TestMethod]
		public void GetConverterForDateTimeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( DateTime ) );

			Assert.IsInstanceOfType( converter, typeof( DateTimeConverter ) );
		}

		[TestMethod]
		public void GetConverterForDecimalTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( decimal ) );

			Assert.IsInstanceOfType( converter, typeof( DecimalConverter ) );
		}

		[TestMethod]
		public void GetConverterForDoubleTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( double ) );

			Assert.IsInstanceOfType( converter, typeof( DoubleConverter ) );
		}

		[TestMethod]
		public void GetConverterForFloatTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( float ) );

			Assert.IsInstanceOfType( converter, typeof( SingleConverter ) );
		}

		[TestMethod]
		public void GetConverterForGuidTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( Guid ) );

			Assert.IsInstanceOfType( converter, typeof( GuidConverter ) );
		}

		[TestMethod]
		public void GetConverterForInt16Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( short ) );

			Assert.IsInstanceOfType( converter, typeof( Int16Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt32Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( int ) );

			Assert.IsInstanceOfType( converter, typeof( Int32Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt64Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( long ) );

			Assert.IsInstanceOfType( converter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void GetConverterForNullableTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( int? ) );

			Assert.IsInstanceOfType( converter, typeof( NullableConverter ) );
		}

		[TestMethod]
		public void GetConverterForSByteTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( sbyte ) );

			Assert.IsInstanceOfType( converter, typeof( SByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForStringTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( string ) );

			Assert.IsInstanceOfType( converter, typeof( StringConverter ) );
		}

		[TestMethod]
		public void GetConverterForUInt16Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( ushort ) );

			Assert.IsInstanceOfType( converter, typeof( UInt16Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt32Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( uint ) );

			Assert.IsInstanceOfType( converter, typeof( UInt32Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt64Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( ulong ) );

			Assert.IsInstanceOfType( converter, typeof( UInt64Converter ) );
		}

		[TestMethod]
		public void GetConverterForEnumTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter( typeof( TestEnum ) );

			Assert.IsInstanceOfType( converter, typeof( EnumConverter ) );
		}

		private class TestListConverter : DefaultTypeConverter
		{
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
