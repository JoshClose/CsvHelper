// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
    public class UriConverterTests
    {
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture },
			};

			Assert.AreEqual("https://test.com/", converter.ConvertToString(new Uri("https://test.com"), null, propertyMapData));
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture },
			};

			Assert.AreEqual(new Uri("https://test.com"), converter.ConvertFromString("https://test.com", null, propertyMapData));
		}

		[TestMethod]
		public void ConvertFromStringUriKindRelativeTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture, UriKind = UriKind.Relative },
			};

			Assert.AreEqual(new Uri("/a/b/c", UriKind.Relative), converter.ConvertFromString("/a/b/c", null, propertyMapData));
		}

		[TestMethod]
		public void ConvertFromStringUriKindAbsoluteTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture, UriKind = UriKind.Absolute },
			};

			Assert.AreEqual(new Uri("https://test.com"), converter.ConvertFromString("https://test.com", null, propertyMapData));
		}

		[TestMethod]
		public void TypeConverterCacheTest()
		{
			var cache = new TypeConverterCache();
			var converter = cache.GetConverter<Uri>();

			Assert.IsInstanceOfType(converter, typeof(UriConverter));
		}
	}
}
