// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class TypeConverterCacheTests
	{
		[Fact]
		public void GetConverterForUnknownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(TestUnknownClass));

			Assert.IsType<DefaultTypeConverter>(converter);
		}

		[Fact]
		public void GetConverterForKnownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsType<DefaultTypeConverter>(converter);

			typeConverterFactory.AddConverter<TestKnownClass>(new TestKnownConverter());
			converter = typeConverterFactory.GetConverter<TestKnownClass>();

			Assert.IsType<TestKnownConverter>(converter);
		}

		[Fact]
		public void RemoveConverterForUnknownTypeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			typeConverterFactory.RemoveConverter<TestUnknownClass>();
			typeConverterFactory.RemoveConverter(typeof(TestUnknownClass));
		}

		[Fact]
		public void GetConverterForByteTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(byte));

			Assert.IsType<ByteConverter>(converter);
		}

		[Fact]
		public void GetConverterForByteArrayTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(byte[]));

			Assert.IsType<ByteArrayConverter>(converter);
		}

		[Fact]
		public void GetConverterForCharTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(char));

			Assert.IsType<CharConverter>(converter);
		}

		[Fact]
		public void GetConverterForDateTimeTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(DateTime));

			Assert.IsType<DateTimeConverter>(converter);
		}

		[Fact]
		public void GetConverterForDecimalTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(decimal));

			Assert.IsType<DecimalConverter>(converter);
		}

		[Fact]
		public void GetConverterForDoubleTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(double));

			Assert.IsType<DoubleConverter>(converter);
		}

		[Fact]
		public void GetConverterForFloatTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(float));

			Assert.IsType<SingleConverter>(converter);
		}

		[Fact]
		public void GetConverterForGuidTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(Guid));

			Assert.IsType<GuidConverter>(converter);
		}

		[Fact]
		public void GetConverterForInt16Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(short));

			Assert.IsType<Int16Converter>(converter);
		}

		[Fact]
		public void GetConverterForInt32Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(int));

			Assert.IsType<Int32Converter>(converter);
		}

		[Fact]
		public void GetConverterForInt64Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(long));

			Assert.IsType<Int64Converter>(converter);
		}

		[Fact]
		public void GetConverterForNullableTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(int?));

			Assert.IsType<NullableConverter>(converter);
		}

		[Fact]
		public void GetConverterForSByteTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(sbyte));

			Assert.IsType<SByteConverter>(converter);
		}

		[Fact]
		public void GetConverterForStringTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(string));

			Assert.IsType<StringConverter>(converter);
		}

		[Fact]
		public void GetConverterForUInt16Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(ushort));

			Assert.IsType<UInt16Converter>(converter);
		}

		[Fact]
		public void GetConverterForUInt32Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(uint));

			Assert.IsType<UInt32Converter>(converter);
		}

		[Fact]
		public void GetConverterForUInt64Test()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(ulong));

			Assert.IsType<UInt64Converter>(converter);
		}

		[Fact]
		public void GetConverterForEnumTest()
		{
			var typeConverterFactory = new TypeConverterCache();
			var converter = typeConverterFactory.GetConverter(typeof(FooEnum));

			Assert.IsType<EnumConverter>(converter);
		}

		[Fact]
		public void GetConverter_ConverterRegisteredForEnum_ReturnCustomConverterForAllEnums()
		{
			var typeConverterFactory = new TypeConverterCache();
			typeConverterFactory.AddConverter<Enum>(new TestKnownConverter());
			var fooConverter = typeConverterFactory.GetConverter(typeof(FooEnum));
			var barConverter = typeConverterFactory.GetConverter(typeof(BarEnum));

			Assert.IsType<TestKnownConverter>(fooConverter);
			Assert.IsType<TestKnownConverter>(barConverter);
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

		private enum FooEnum
		{
		}

		private enum BarEnum
		{
		}
	}
}
