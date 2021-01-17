// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Serializing
{
	[TestClass]
	public class SanitizationTests
	{
		[TestMethod]
		public void NoQuoteTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("=one");
				csv.Flush();
				writer.Flush();

				Assert.AreEqual("\t=one", writer.ToString());
			}
		}

		[TestMethod]
		public void QuoteTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				ShouldQuote = (field, context) => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("\"=one\"");
				csv.Flush();

				Assert.AreEqual("\"\t=one\"", writer.ToString());
			}
		}

		[TestMethod]
		public void NoQuoteChangeEscapeCharacterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				InjectionEscapeCharacter = '\'',
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("=one");
				csv.Flush();

				Assert.AreEqual("'=one", writer.ToString());
			}
		}

		[TestMethod]
		public void QuoteChangeEscapeCharacterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				SanitizeForInjection = true,
				InjectionEscapeCharacter = '\'',
				ShouldQuote = (field, context) => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("\"=one\"");
				csv.Flush();

				Assert.AreEqual("\"'=one\"", writer.ToString());
			}
		}
	}
}
