// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.AttributeMapping
{
	public class TrimOptionsTests
	{
		[Fact]
		public void TrimOptionsTest()
		{
			var config = CsvConfiguration.FromType<TrimOptionsTestClass>(CultureInfo.InvariantCulture);
			Assert.Equal(TrimOptions.InsideQuotes, config.TrimOptions);
		}

		[TrimOptions(TrimOptions.InsideQuotes)]
		private class TrimOptionsTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
