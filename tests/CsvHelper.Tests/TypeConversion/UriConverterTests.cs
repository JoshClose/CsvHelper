// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CsvHelper.Tests.TypeConversion
{
	
    public class UriConverterTests
    {
		[Fact]
		public void ConvertToStringTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture },
			};

			Assert.Equal("https://test.com/", converter.ConvertToString(new Uri("https://test.com"), null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture },
			};

			Assert.Equal(new Uri("https://test.com"), converter.ConvertFromString("https://test.com", null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringUriKindRelativeTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture, UriKind = UriKind.Relative },
			};

			Assert.Equal(new Uri("/a/b/c", UriKind.Relative), converter.ConvertFromString("/a/b/c", null!, propertyMapData));
		}

		[Fact]
		public void ConvertFromStringUriKindAbsoluteTest()
		{
			var converter = new UriConverter();

			var propertyMapData = new MemberMapData(null)
			{
				TypeConverter = converter,
				TypeConverterOptions = { CultureInfo = CultureInfo.CurrentCulture, UriKind = UriKind.Absolute },
			};

			Assert.Equal(new Uri("https://test.com"), converter.ConvertFromString("https://test.com", null!, propertyMapData));
		}

		[Fact]
		public void TypeConverterCacheTest()
		{
			var cache = new TypeConverterCache();
			var converter = cache.GetConverter<Uri>();

			Assert.IsType<UriConverter>(converter);
		}

		[Fact]
		public void AnonymousTypeTest()
		{
			var sw = new StringWriter();
			var entries = new[]
			{
				new { Uri = new Uri("http://host/path") }
			};
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Delimiter = ";",
			};
			using (var cw = new CsvWriter(sw, config))
			{
				cw.WriteRecords(entries);
			}

			Assert.Equal("Uri\r\nhttp://host/path\r\n", sw.ToString());
		}
	}
}
