using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
    public class EscapeCharacterTests
    {
		[TestMethod]
		public void EscapeTest()
		{
			using (var reader = new StringReader("\"|\"a|\"\"\r\n"))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Escape = '|';
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeNoNewlineTest()
		{
			using (var reader = new StringReader("\"|\"a|\"\""))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Escape = '|';
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimOutsideTest()
		{
			using (var reader = new StringReader(" \"|\"a|\"\" \r\n"))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.Trim;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimInsideTest()
		{
			using (var reader = new StringReader("\" |\"a|\" \"\r\n"))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.InsideQuotes;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeTrimBothTest()
		{
			using (var reader = new StringReader(" \" |\"a|\" \" \r\n"))
			using (var parser = new CsvParser(reader))
			{
				parser.Configuration.Escape = '|';
				parser.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
				var record = parser.Read();
				Assert.AreEqual("\"a\"", record[0]);
			}
		}

		[TestMethod]
		public void EscapeWriteTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Escape = '|';
				csv.WriteField("\"a\"");
				csv.Flush();

				Assert.AreEqual("\"|\"a|\"\"", writer.ToString());
			}
		}
	}
}