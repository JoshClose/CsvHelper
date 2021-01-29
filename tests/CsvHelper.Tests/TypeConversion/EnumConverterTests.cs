// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.TypeConversion;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;

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
			var propertyMapData = new MemberMapData(null);

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

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var row = new CsvReader(new ParserMock());

			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("One", null, propertyMapData));
			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("one", null, propertyMapData));
			Assert.AreEqual(TestEnum.One, converter.ConvertFromString("1", null, propertyMapData));
			Assert.ThrowsException<TypeConverterException>(() => converter.ConvertFromString("", row, propertyMapData));
			Assert.ThrowsException<TypeConverterException>(() => Assert.AreEqual(TestEnum.One, converter.ConvertFromString(null, row, propertyMapData)));
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

		[TestMethod]
		public void ConvertFromString_DuplicateNames_IgnoreCase_ReturnsNameWithLowestValue()
		{
			var converter = new EnumConverter(typeof(DuplicateNames));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null, memberMapData);

			Assert.AreEqual(DuplicateNames.one, value);
		}

		[TestMethod]
		public void ConvertFromString_DuplicateValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateValues));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null, memberMapData);

			Assert.AreEqual(DuplicateValues.One, value);
		}

		[TestMethod]
		public void ConvertFromString_UsingValue_DuplicateNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValues));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null, memberMapData);

			Assert.AreEqual(DuplicateNamesAndValues.One, value);
		}

		[TestMethod]
		public void ConvertFromString_UsingName_DuplicateNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValues));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null, memberMapData);

			Assert.AreEqual(DuplicateNamesAndValues.One, value);
		}

		[TestMethod]
		public void ConvertFromString_DuplicateAttributeNames_IgnoreCase_ReturnsNameWithLowestValue()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null, memberMapData);

			Assert.AreEqual(DuplicateNamesAttributeEnum.One, value);
		}

		[TestMethod]
		public void ConvertFromString_DuplicateAttributeValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null, memberMapData);

			Assert.AreEqual(DuplicateValuesAttributeEnum.One, value);
		}

		[TestMethod]
		public void ConvertFromString_UsingValue_DuplicateAttributeNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null, memberMapData);

			Assert.AreEqual(DuplicateNamesAndValuesAttributeEnum.One, value);
		}

		[TestMethod]
		public void ConvertFromString_UsingName_DuplicateAttributeNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null, memberMapData);

			Assert.AreEqual(DuplicateNamesAndValuesAttributeEnum.One, value);
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

		private enum DuplicateNames
		{
			None = 0,
			One = 2,
			one = 1,
			Three = 3
		}

		private enum DuplicateValues
		{
			None = 0,
			One = 1,
			Two = 1,
			Three = 3
		}

		private enum DuplicateNamesAndValues
		{
			None = 0,
			One = 1,
			one = 1,
			Two = 2
		}

		private enum DuplicateNamesAttributeEnum
		{
			None = 0,
			[Name("Foo")]
			One = 2,
			[Name("foo")]
			Two = 1,
			Three = 3
		}

		private enum DuplicateValuesAttributeEnum
		{
			None = 0,
			[Name("Foo")]
			One = 1,
			[Name("Bar")]
			Two = 1,
			Three = 3
		}

		private enum DuplicateNamesAndValuesAttributeEnum
		{
			None = 0,
			[Name("Foo")]
			One = 1,
			[Name("foo")]
			Two = 1,
			Three = 3
		}
	}
}
