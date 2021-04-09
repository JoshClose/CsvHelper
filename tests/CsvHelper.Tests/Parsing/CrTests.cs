// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests.Parsing
{
	
	public class CrTests
	{
		[Fact]
		public void SingleFieldAndSingleRowTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndSingleRowAndFieldIsQuotedTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndMultipleRowsAndFirstFieldInFirstRowIsQuotedAndNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			s.Append("2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);

				parser.Read();
				Assert.Equal("2", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndMultipleRowsAndFirstFieldInFirstRowIsQuotedAndHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\"\r");
			s.Append("2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);

				parser.Read();
				Assert.Equal("2", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndMultipleRowsTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);

				parser.Read();
				Assert.Equal("2", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndMultipleRowsAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("2");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);

				parser.Read();
				Assert.Equal("2", parser[0]);
			}
		}

		[Fact]
		public void SingleFieldAndSecondRowIsQuotedAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1\r");
			s.Append("\"2\"");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);

				parser.Read();
				Assert.Equal("2", parser[0]);
			}
		}

		[Fact]
		public void MultipleFieldsAndSingleRowAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);
			}
		}

		[Fact]
		public void MultipleFieldsAndMultipleRowsAndLastRowHasNoLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("3,4");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);

				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}

		[Fact]
		public void MultipleFieldsAndMultipleRowsAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("3,4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);

				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}

		[Fact]
		public void MultipleFieldsAndMultipleRowsAndLastFieldInFirstRowIsQuotedAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,\"2\"\r");
			s.Append("3,4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);

				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}

		[Fact]
		public void MultipleFieldsAndMultipleRowsAndSecondRowFirstFieldIsQuotedAndLastRowHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("1,2\r");
			s.Append("\"3\",4\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);

				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}

		[Fact]
		public void MultipleFieldsAndMultipleRowsAndAllFieldsQuotedAndHasLineEndingTest()
		{
			var s = new StringBuilder();
			s.Append("\"1\",\"2\"\r");
			s.Append("\"3\",\"4\"\r");
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				parser.Read();
				Assert.Equal("1", parser[0]);
				Assert.Equal("2", parser[1]);

				parser.Read();
				Assert.Equal("3", parser[0]);
				Assert.Equal("4", parser[1]);
			}
		}
	}
}
