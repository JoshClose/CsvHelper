﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace CsvHelper.Tests.AttributeMapping
{
	public class DelimiterTests
	{
		[Fact]
		public void DelimiterReaderTest()
		{
			var configuration = CsvConfiguration.FromAttributes<DelimiterTestClass>(CultureInfo.InvariantCulture);

			Assert.Equal("§", configuration.Delimiter);
		}

		[Delimiter("§")]
		private class DelimiterTestClass
		{
			public int Id { get; set; }

			public string? Name { get; set; }
		}
	}
}
