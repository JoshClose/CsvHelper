// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.Configuration;
using Xunit;
using CsvHelper.TypeConversion;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using System.Text;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class EnumConverterTests
	{
		[Fact]
		public void ConstructorTest()
		{
			try
			{
				new EnumConverter(typeof(string));
				throw new XUnitException();
			}
			catch (ArgumentException ex)
			{
				Assert.Equal("'System.String' is not an Enum.", ex.Message);
			}
		}

		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new EnumConverter(typeof(TestEnum));
			var propertyMapData = new MemberMapData(null);

			Assert.Equal("None", converter.ConvertToString((TestEnum)0, null!, propertyMapData));
			Assert.Equal("None", converter.ConvertToString(TestEnum.None, null!, propertyMapData));
			Assert.Equal("One", converter.ConvertToString((TestEnum)1, null!, propertyMapData));
			Assert.Equal("One", converter.ConvertToString(TestEnum.One, null!, propertyMapData));
			Assert.Equal("", converter.ConvertToString(null, null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new EnumConverter(typeof(TestEnum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var row = new CsvReader(new ParserMock());

			Assert.Equal(TestEnum.One, converter.ConvertFromString("One", null!, propertyMapData));
			Assert.Equal(TestEnum.One, converter.ConvertFromString("one", null!, propertyMapData));
			Assert.Equal(TestEnum.One, converter.ConvertFromString("1", null!, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString("", row, propertyMapData));
			Assert.Throws<TypeConverterException>(() => Assert.Equal(TestEnum.One, converter.ConvertFromString(null, row, propertyMapData)));
		}

		[Fact]
		public void ConvertToString_NameAttribute_ReturnsNameFromNameAttribute()
		{
			var converter = new EnumConverter(typeof(NameAttributeEnum));
			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(NameAttributeEnum.Foo, null!, propertyMapData);

			Assert.Equal("Bar", value);
		}

		[Fact]
		public void ConvertFromString_NameAttribute_ReturnsValueFromNameAttribute()
		{
			var converter = new EnumConverter(typeof(NameAttributeEnum));
			var propertyMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("Bar", null!, propertyMapData);

			Assert.Equal(NameAttributeEnum.Foo, value);
		}

		[Fact]
		public void ConvertFromString_Int16Enum_ReturnsEnumValue()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertFromString("1", null!, propertyMapData);

			Assert.Equal(Int16Enum.One, value);
		}

		[Fact]
		public void ConvertToString_Int16Enum_ReturnsString()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(Int16Enum.One, null!, propertyMapData);

			Assert.Equal("One", value);
		}

		[Fact]
		public void ConvertFromString_Int16EnumWithNameAttribute_ReturnsEnumValue()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertFromString("Bar", null!, propertyMapData);

			Assert.Equal(Int16Enum.Foo, value);
		}

		[Fact]
		public void ConvertToString_Int16EnumWithNameAttribute_ReturnsString()
		{
			var converter = new EnumConverter(typeof(Int16Enum));

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			var value = converter.ConvertToString(Int16Enum.Foo, null!, propertyMapData);

			Assert.Equal("Bar", value);
		}

		[Fact]
		public void ConvertFromString_DuplicateNames_IgnoreCase_ReturnsNameWithLowestValue()
		{
			var converter = new EnumConverter(typeof(DuplicateNames));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null!, memberMapData);

			Assert.Equal(DuplicateNames.one, value);
		}

		[Fact]
		public void ConvertFromString_DuplicateValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateValues));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null!, memberMapData);

			Assert.Equal(DuplicateValues.One, value);
		}

		[Fact]
		public void ConvertFromString_UsingValue_DuplicateNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValues));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null!, memberMapData);

			Assert.Equal(DuplicateNamesAndValues.One, value);
		}

		[Fact]
		public void ConvertFromString_UsingName_DuplicateNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValues));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null!, memberMapData);

			Assert.Equal(DuplicateNamesAndValues.One, value);
		}

		[Fact]
		public void ConvertFromString_DuplicateAttributeNames_IgnoreCase_ReturnsNameWithLowestValue()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null!, memberMapData);

			Assert.Equal(DuplicateNamesAttributeEnum.One, value);
		}

		[Fact]
		public void ConvertFromString_DuplicateAttributeValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null!, memberMapData);

			Assert.Equal(DuplicateValuesAttributeEnum.One, value);
		}

		[Fact]
		public void ConvertFromString_UsingValue_DuplicateAttributeNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertFromString("1", null!, memberMapData);

			Assert.Equal(DuplicateNamesAndValuesAttributeEnum.One, value);
		}

		[Fact]
		public void ConvertFromString_UsingName_DuplicateAttributeNamesAndValues_ReturnsNameThatAppearsFirst()
		{
			var converter = new EnumConverter(typeof(DuplicateNamesAndValuesAttributeEnum));

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { EnumIgnoreCase = true },
			};

			var value = converter.ConvertFromString("oNe", null!, memberMapData);

			Assert.Equal(DuplicateNamesAndValuesAttributeEnum.One, value);
		}

		private class TestClass
		{
			public int Id { get; set; }
			public string? Name { get; set; }
			public TestEnum TestEnum { get; set; } = new TestEnum();
		}

		private class TestClassMap : ClassMap<TestClass>
		{
			public TestClassMap()
			{
				Map(m => m.Id);
				Map(m => m.Name);
				Map(m => m.TestEnum).TypeConverterOption.EnumIgnoreCase();
			}
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
