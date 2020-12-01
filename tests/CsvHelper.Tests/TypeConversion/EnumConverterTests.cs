// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.TypeConversion;
using Moq;
using CsvHelper.Configuration.Attributes;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumConverterTests
	{
		[TestMethod]
		public void ConstructorTest()
		{
			try
			{
				new EnumConverter(typeof(string));
				Assert.Fail();
			}
			catch (ArgumentException ex)
			{
				Assert.AreEqual("'System.String' is not an Enum.", ex.Message);
			}
		}

		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new EnumConverter(typeof(TestEnum));
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.AreEqual("None", converter.ConvertToString((TestEnum)0, null, propertyMapData));
			Assert.AreEqual("None", converter.ConvertToString(TestEnum.None, null, propertyMapData));
			Assert.AreEqual("One", converter.ConvertToString((TestEnum)1, null, propertyMapData));
			Assert.AreEqual("One", converter.ConvertToString(TestEnum.One, null, propertyMapData));
			Assert.AreEqual("", converter.ConvertToString(null, null, propertyMapData));
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new EnumConverter(typeof(TestEnum));

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockRow = new Mock<IReaderRow>();

			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("One", null, propertyMapData));
			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("one", null, propertyMapData));
			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("1", null, propertyMapData));
			try
			{
				Assert.AreEqual(TestEnum.One, converter.ConvertFromString("", mockRow.Object, propertyMapData));
				Assert.Fail();
			}
			catch (TypeConverterException)
			{
			}

			try
			{
				Assert.AreEqual(TestEnum.One, converter.ConvertFromString(null, mockRow.Object, propertyMapData));
				Assert.Fail();
			}
			catch (TypeConverterException)
			{
			}
		}

		[TestMethod]
		public void ConvertToString_NameAttribute_ReturnsNameFromNameAttribute()
		{
			var converter = new EnumConverter(typeof(NameAttributeEnum));
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(NameAttributeEnum.Foo, null, propertyMapData);

			Assert.AreEqual("Bar", value);
		}

		[TestMethod]
		public void ConvertFromString_NameAttribute_ReturnsValueFromNameAttribute()
		{
			var converter = new EnumConverter(typeof(NameAttributeEnum));
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertFromString("Bar", null, propertyMapData);

			Assert.AreEqual(NameAttributeEnum.Foo, value);
		}

		[TestMethod]
		public void ConvertFromString_Int16Enum_ReturnsEnumValue()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertFromString("1", null, propertyMapData);

			Assert.AreEqual(Int16Enum.One, value);
		}

		[TestMethod]
		public void ConvertToString_Int16Enum_ReturnsString()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(Int16Enum.One, null, propertyMapData);

			Assert.AreEqual("One", value);
		}

		[TestMethod]
		public void ConvertFromString_Int16EnumWithNameAttribute_ReturnsEnumValue()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertFromString("Bar", null, propertyMapData);

			Assert.AreEqual(Int16Enum.Foo, value);
		}

		[TestMethod]
		public void ConvertToString_Int16EnumWithNameAttribute_ReturnsString()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(Int16Enum.Foo, null, propertyMapData);

			Assert.AreEqual("Bar", value);
		}

		private enum TestEnum
		{
			None = 0,
			One = 1,
		}

		private enum NameAttributeEnum
		{
			None = 0,
			[Name("Bar")]
			Foo = 1
		}

		private enum Int16Enum : short
		{
			None = 0,
			One = 1,
			[Name("Bar")]
			Foo = 2
		}
	}
}
