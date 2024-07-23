// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.Reflection;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class NullableConverterTests
	{
		private const string InvalidIntString = "abc";

		private readonly MemberInfo _nullableIntMemberInfo = typeof(NullableConverterTests)
			.GetProperty(nameof(NullableInt), BindingFlags.Instance | BindingFlags.NonPublic)!;

		// this property is used to create a MemberMapData instance for a nullable int member
		private int? NullableInt { get; set; }

		[Fact]
		public void ConvertNullableTypeFromStringThrowsWithUseDefaultOnConversionFailureFalse()
		{
			// setup
			var converter = new NullableConverter(typeof(int?), new TypeConverterCache());
			var propertyMapData = new MemberMapData(_nullableIntMemberInfo)
			{
				TypeConverter = converter,
				TypeConverterOptions = {
					CultureInfo = CultureInfo.InvariantCulture,
				},
				Default = null,
				IsDefaultSet = true,
				UseDefaultOnConversionFailure = false,
			};
			var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ExceptionMessagesContainRawData = false,
			};
			var row = new ReaderRowMock(new ParserMock(csvConfiguration));

			// act
			var exception = Record.Exception(() => converter.ConvertFromString(InvalidIntString, row, propertyMapData));

			// assert
			Assert.IsType<TypeConverterException>(exception);
		}

		[Fact]
		public void ConvertNullableTypeFromStringWithConfiguredNullValueDoesNotThrow()
		{
			// setup
			var converter = new NullableConverter(typeof(int?), new TypeConverterCache());
			var configuredNullValue = InvalidIntString;
			var propertyMapData = new MemberMapData(_nullableIntMemberInfo)
			{
				TypeConverter = converter,
				TypeConverterOptions = {
					// configure InvalidIntString as an expected null value for this member.
					NullValues = { configuredNullValue },
					CultureInfo = CultureInfo.InvariantCulture,
				},
				Default = null,
				IsDefaultSet = true,
				UseDefaultOnConversionFailure = false,
			};

			// act
			object? result = null;
			var exception = Record.Exception(() => result = converter.ConvertFromString(configuredNullValue, null!, propertyMapData));

			// assert
			Assert.Null(exception);
			Assert.Null(result);
		}

		[Fact]
		public void ConvertNullableTypeFromStringWithUseDefaultOnConversionFailureTrueDoesNotThrow()
		{
			// setup
			var converter = new NullableConverter(typeof(int?), new TypeConverterCache());
			var propertyMapData = new MemberMapData(_nullableIntMemberInfo)
			{
				TypeConverter = converter,
				TypeConverterOptions = {
					CultureInfo = CultureInfo.InvariantCulture,
				},
				Default = null,
				IsDefaultSet = true,
				UseDefaultOnConversionFailure = true,
			};

			// act
			var exception = Record.Exception(() => converter.ConvertFromString(InvalidIntString, null!, propertyMapData));

			// assert
			Assert.Null(exception);
		}

		[Theory]
		[InlineData(null)]
		[InlineData(-1)]
		[InlineData(99)]
		public void ConvertNullableTypeFromStringWithUseDefaultOnConversionFailureTrueReturnsDefaultValue(object? defaultValue)
		{
			// setup
			var converter = new NullableConverter(typeof(int?), new TypeConverterCache());
			var propertyMapData = new MemberMapData(_nullableIntMemberInfo)
			{
				TypeConverter = converter,
				TypeConverterOptions = {
					CultureInfo = CultureInfo.InvariantCulture,
				},
				Default = defaultValue,
				IsDefaultSet = true,
				UseDefaultOnConversionFailure = true,
			};

			// act
			object? result = null;
			var exception = Record.Exception(() => result = converter.ConvertFromString(InvalidIntString, null!, propertyMapData));

			// assert
			Assert.Null(exception);
			Assert.StrictEqual(defaultValue, result);
		}
	}
}
