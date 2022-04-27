// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using Xunit;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using CsvHelper.Tests.Mocks;
using System.IO;

namespace CsvHelper.Tests.TypeConversion
{
	
	public class EnumerableConverterTests
	{
		[Fact]
		public void ConvertTest()
		{
			var converter = new EnumerableConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var readerRow = new CsvReader(new ParserMock());
			var writerRow = new CsvWriter(new StringWriter(), CultureInfo.InvariantCulture);

			Assert.Throws<TypeConverterException>(() => converter.ConvertFromString("", readerRow, propertyMapData));
			Assert.Throws<TypeConverterException>(() => converter.ConvertToString(5, writerRow, propertyMapData));
		}
	}
}
