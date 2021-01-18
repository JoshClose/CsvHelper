using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
    public class NewLineTests
    {
		[TestMethod]
		public void Read_RfcMode_2CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one\a\b");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.RFC4180,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void Read_RfcMode_1CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a");
			s.Append("1,one\a");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.RFC4180,
				NewLine = "\a",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void Read_RfcMode_2CharNewLine_NoneOnLastLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.RFC4180,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
        public void Read_EscapeMode_2CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one\a\b");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.Escape,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void Read_EscapeMode_1CharNewLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a");
			s.Append("1,one\a");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.Escape,
				NewLine = "\a",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void Read_EscapeMode_2CharNewLine_NoneOnLastLine_Parses()
		{
			var s = new StringBuilder();
			s.Append("Id,Name\a\b");
			s.Append("1,one");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				Mode = ParserMode.Escape,
				NewLine = "\a\b",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual("Id", parser[0]);
				Assert.AreEqual("Name", parser[1]);

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("one", parser[1]);

				Assert.IsFalse(parser.Read());
			}
		}
	}
}
