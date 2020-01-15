// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class ShouldQuoteTests
	{    
		[TestMethod]
		public void QuoteAllFieldsTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.ShouldQuote = (field, context) => true;
				csv.WriteField("one");
				csv.Flush();

				Assert.AreEqual("\"one\"", writer.ToString());
			}
		}

		[TestMethod]
		public void QuoteNoFieldsTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Configuration.ShouldQuote = (field, context) => false;
				csv.WriteField("o\"e");
				csv.Flush();

				Assert.AreEqual("o\"e", writer.ToString());
			}
		}

		[TestMethod]
		public void ContainsQuoteTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField($"o{csv.Configuration.Quote}e");
				csv.Flush();

				Assert.AreEqual($"\"o{csv.Configuration.Quote}{csv.Configuration.Quote}e\"", writer.ToString());
			}
		}

		[TestMethod]
		public void StartsWithSpaceTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField(" one");
				csv.Flush();

				Assert.AreEqual("\" one\"", writer.ToString());
			}
		}

		[TestMethod]
		public void EndsWithSpaceTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("one ");
				csv.Flush();

				Assert.AreEqual("\"one \"", writer.ToString());
			}
		}

		[TestMethod]
		public void ContainsCrTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\re");
				csv.Flush();

				Assert.AreEqual("\"o\re\"", writer.ToString());
			}
		}

		[TestMethod]
		public void ContainsLfTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\ne");
				csv.Flush();

				Assert.AreEqual("\"o\ne\"", writer.ToString());
			}
		}

		[TestMethod]
		public void ContainsCrLfTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\r\ne");
				csv.Flush();

				Assert.AreEqual("\"o\r\ne\"", writer.ToString());
			}
		}

		[TestMethod]
		public void ContainsDelimiterTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField($"o{csv.Configuration.Delimiter}e");
				csv.Flush();

				Assert.AreEqual($"\"o{csv.Configuration.Delimiter}e\"", writer.ToString());
			}
		}
	}
}
