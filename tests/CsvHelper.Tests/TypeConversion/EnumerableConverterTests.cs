// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumerableConverterTests
	{
		[TestMethod]
		public void ConvertTest()
		{
			var converter = new EnumerableConverter();

			var propertyMapData = new MemberMapData( null );
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var mockReaderRow = new Mock<IReaderRow>();
			var mockWriterRow = new Mock<IWriterRow>();

			try
			{
				converter.ConvertFromString( "", mockReaderRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException )
			{
			}
			try
			{
				converter.ConvertToString( 5, mockWriterRow.Object, propertyMapData );
				Assert.Fail();
			}
			catch( TypeConverterException )
			{
			}
		}
	}
}
