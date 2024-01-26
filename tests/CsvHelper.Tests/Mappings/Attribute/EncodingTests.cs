// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
			Assert.Equal(Encoding.ASCII, CsvConfiguration.FromAttributes<EncodingNameTestClass>(CultureInfo.InvariantCulture).Encoding);
			Assert.Equal(Encoding.ASCII, CsvConfiguration.FromAttributes<EncodingCodepageTestClass>(CultureInfo.InvariantCulture).Encoding);
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
