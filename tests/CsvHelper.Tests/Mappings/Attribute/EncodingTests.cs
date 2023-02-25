// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.Text;
using Xunit;

namespace CsvHelper.Tests.AttributeMapping
{
	public class EncodingTests
	{
		[Fact]
		public void EncodingTest()
		{
			Assert.Equal(Encoding.ASCII, new CsvConfiguration(CultureInfo.InvariantCulture, typeof(EncodingNameTestClass)).Encoding);
			Assert.Equal(Encoding.ASCII, new CsvConfiguration(CultureInfo.InvariantCulture, typeof(EncodingCodepageTestClass)).Encoding);
		}

		[Encoding("ASCII")]
		private class EncodingNameTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}

		[Encoding(20127)]
		private class EncodingCodepageTestClass
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
