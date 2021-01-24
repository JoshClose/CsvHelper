// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class BooleanConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new BooleanConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture }
			};

			Assert.AreEqual("True", converter.ConvertToString(true, null, memberMapData));

			Assert.AreEqual("False", converter.ConvertToString(false, null, memberMapData));

			Assert.AreEqual("", converter.ConvertToString(null, null, memberMapData));
			Assert.AreEqual("1", converter.ConvertToString(1, null, memberMapData));
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			var memberMapData = new MemberMapData(null);
			memberMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var row = new CsvReader(new ParserMock());

			Assert.IsTrue((bool)converter.ConvertFromString("true", null, memberMapData));
			Assert.IsTrue((bool)converter.ConvertFromString("True", null, memberMapData));
			Assert.IsTrue((bool)converter.ConvertFromString("TRUE", null, memberMapData));
			Assert.IsTrue((bool)converter.ConvertFromString("1", null, memberMapData));
			Assert.IsTrue((bool)converter.ConvertFromString(" true ", null, memberMapData));

			Assert.IsFalse((bool)converter.ConvertFromString("false", null, memberMapData));
			Assert.IsFalse((bool)converter.ConvertFromString("False", null, memberMapData));
			Assert.IsFalse((bool)converter.ConvertFromString("FALSE", null, memberMapData));
			Assert.IsFalse((bool)converter.ConvertFromString("0", null, memberMapData));
			Assert.IsFalse((bool)converter.ConvertFromString(" false ", null, memberMapData));
			Assert.IsFalse((bool)converter.ConvertFromString(" 0 ", null, memberMapData));

			Assert.ThrowsException<TypeConverterException>(() => converter.ConvertFromString(null, row, memberMapData));
		}

		[TestMethod]
		public void ConvertToString_SingleBooleanTrueValue_UsesValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanTrueValues = { "Foo" },
				},
			};

			var value = converter.ConvertToString(true, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void ConvertToString_MultipleBooleanTrueValues_UsesFirstValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanTrueValues = { "Foo", "Bar" },
				},
			};

			var value = converter.ConvertToString(true, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void ConvertToString_SingleBooleanFalseValue_UsesValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanFalseValues = { "Foo" },
				},
			};

			var value = converter.ConvertToString(false, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void ConvertToString_MultipleBooleanFalseValues_UsesFirstValue()
		{
			var converter = new BooleanConverter();
			var memberMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions =
				{
					BooleanFalseValues = { "Foo", "Bar" },
				},
			};

			var value = converter.ConvertToString(false, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void WriteField_TrueValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanTrueValues.Add("Foo");

				csv.WriteField(true);
				csv.Flush();
				writer.Flush();

				Assert.AreEqual("Foo", writer.ToString());
			}
		}

		[TestMethod]
		public void WriteField_FalseValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<bool>().BooleanFalseValues.Add("Foo");

				csv.WriteField(false);
				csv.Flush();
				writer.Flush();

				Assert.AreEqual("Foo", writer.ToString());
			}
		}
	}
}
