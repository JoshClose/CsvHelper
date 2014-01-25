// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

			TypeConverterFactory.AddConverter<TestKnownClass>( new TestKnownConverter() );
			converter = TypeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsInstanceOfType( converter, typeof( TestKnownConverter ) );
		}

		[TestMethod]
		public void RemoveConverterForUnknownTypeTest()
		{
			TypeConverterFactory.RemoveConverter<TestUnknownClass>();
			TypeConverterFactory.RemoveConverter( typeof( TestUnknownClass ) );
		}

		[TestMethod]
		public void GetConverterForByteTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( byte ) );

			Assert.IsInstanceOfType( converter, typeof( ByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForCharTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( char ) );

			Assert.IsInstanceOfType( converter, typeof( CharConverter ) );
		}

		[TestMethod]
		public void GetConverterForDateTimeTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( DateTime ) );

			Assert.IsInstanceOfType( converter, typeof( DateTimeConverter ) );
		}

		[TestMethod]
		public void GetConverterForDecimalTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( decimal ) );

			Assert.IsInstanceOfType( converter, typeof( DecimalConverter ) );
		}

		[TestMethod]
		public void GetConverterForDoubleTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( double ) );

			Assert.IsInstanceOfType( converter, typeof( DoubleConverter ) );
		}

		[TestMethod]
		public void GetConverterForFloatTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( float ) );

			Assert.IsInstanceOfType( converter, typeof( SingleConverter ) );
		}

		[TestMethod]
		public void GetConverterForGuidTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( Guid ) );

			Assert.IsInstanceOfType( converter, typeof( GuidConverter ) );
		}

		[TestMethod]
		public void GetConverterForInt16Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( short ) );

			Assert.IsInstanceOfType( converter, typeof( Int16Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt32Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( int ) );

			Assert.IsInstanceOfType( converter, typeof( Int32Converter ) );
		}

		[TestMethod]
		public void GetConverterForInt64Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( long ) );

			Assert.IsInstanceOfType( converter, typeof( Int64Converter ) );
		}

		[TestMethod]
		public void GetConverterForNullableTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( int? ) );

			Assert.IsInstanceOfType( converter, typeof( NullableConverter ) );
		}

		[TestMethod]
		public void GetConverterForSByteTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( sbyte ) );

			Assert.IsInstanceOfType( converter, typeof( SByteConverter ) );
		}

		[TestMethod]
		public void GetConverterForStringTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( string ) );

			Assert.IsInstanceOfType( converter, typeof( StringConverter ) );
		}

		[TestMethod]
		public void GetConverterForUInt16Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( ushort ) );

			Assert.IsInstanceOfType( converter, typeof( UInt16Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt32Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( uint ) );

			Assert.IsInstanceOfType( converter, typeof( UInt32Converter ) );
		}

		[TestMethod]
		public void GetConverterForUInt64Test()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( ulong ) );

			Assert.IsInstanceOfType( converter, typeof( UInt64Converter ) );
		}

		[TestMethod]
		public void GetConverterForEnumTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( TestEnum ) );

			Assert.IsInstanceOfType( converter, typeof( EnumConverter ) );
		}

		[TestMethod]
		public void GetConverterForEnumerableTypesTest()
		{
			var converter = TypeConverterFactory.GetConverter( typeof( IEnumerable ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( IList ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( List<int> ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( ICollection ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( Collection<int> ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( IDictionary ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( Dictionary<int, string> ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( Array ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );
		}

		[TestMethod]
		public void GetConverterForCustomListConverterThatIsNotEnumerableConverterTest()
		{
			TypeConverterFactory.AddConverter<List<string>>( new TestListConverter() );
			var converter = TypeConverterFactory.GetConverter( typeof( List<string> ) );
			Assert.IsInstanceOfType( converter, typeof( TestListConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( List<int> ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );

			converter = TypeConverterFactory.GetConverter( typeof( Array ) );
			Assert.IsInstanceOfType( converter, typeof( EnumerableConverter ) );
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
