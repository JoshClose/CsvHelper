using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class ShouldQuoteTests
    {    
		[TestMethod]
		public void QuoteAllFieldsTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
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
			using (var csv = new CsvWriter(writer))
			{
				csv.WriteField($"o{csv.Configuration.Delimiter}e");
				csv.Flush();

				Assert.AreEqual($"\"o{csv.Configuration.Delimiter}e\"", writer.ToString());
			}
		}
	}
}
