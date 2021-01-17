// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class CrTests
	{
		[TestMethod]
		public void SingleFieldAndSingleRowTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndSingleRowAndFieldIsQuotedTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndMultipleRowsAndFirstFieldInFirstRowIsQuotedAndNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			s.Append("2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);

				parser.Read();
				Assert.AreEqual("2", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndMultipleRowsAndFirstFieldInFirstRowIsQuotedAndHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			s.Append("2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);

				parser.Read();
				Assert.AreEqual("2", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndMultipleRowsTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);

				parser.Read();
				Assert.AreEqual("2", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndMultipleRowsAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);

				parser.Read();
				Assert.AreEqual("2", parser[0]);
			}
		}

		[TestMethod]
		public void SingleFieldAndSecondRowIsQuotedAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("\"2\"");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);

				parser.Read();
				Assert.AreEqual("2", parser[0]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndSingleRowAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndMultipleRowsAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("3,4");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);

				parser.Read();
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndMultipleRowsAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("3,4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);

				parser.Read();
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndMultipleRowsAndLastFieldInFirstRowIsQuotedAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,\"2\"\r");
			s.Append("3,4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);

				parser.Read();
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndMultipleRowsAndSecondRowFirstFieldIsQuotedAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("\"3\",4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);

				parser.Read();
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
			}
		}

		[TestMethod]
		public void MultipleFieldsAndMultipleRowsAndAllFieldsQuotedAndHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\",\"2\"\r");
			s.Append("\"3\",\"4\"\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("2", parser[1]);

				parser.Read();
				Assert.AreEqual("3", parser[0]);
				Assert.AreEqual("4", parser[1]);
			}
		}
	}
}
