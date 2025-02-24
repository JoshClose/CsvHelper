// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CsvHelper.Tests.Writing
{
	
	public class ShouldQuoteTests
	{    
		[Fact]
		public void QuoteAllFieldsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => true,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("one");
				csv.Flush();

				Assert.Equal("\"one\"", writer.ToString());
			}
		}

		[Fact]
		public void QuoteNoFieldsTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = _ => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("o\"e");
				csv.Flush();

				Assert.Equal("o\"e", writer.ToString());
			}
		}

		[Fact]
		public void ContainsQuoteTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField($"o{csv.Configuration.Quote}e");
				csv.Flush();

				Assert.Equal($"\"o{csv.Configuration.Quote}{csv.Configuration.Quote}e\"", writer.ToString());
			}
		}

		[Fact]
		public void StartsWithSpaceTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField(" one");
				csv.Flush();

				Assert.Equal("\" one\"", writer.ToString());
			}
		}

		[Fact]
		public void EndsWithSpaceTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("one ");
				csv.Flush();

				Assert.Equal("\"one \"", writer.ToString());
			}
		}

		[Fact]
		public void ContainsCrTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\re");
				csv.Flush();

				Assert.Equal("\"o\re\"", writer.ToString());
			}
		}

		[Fact]
		public void ContainsLfTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\ne");
				csv.Flush();

				Assert.Equal("\"o\ne\"", writer.ToString());
			}
		}

		[Fact]
		public void ContainsCrLfTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField("o\r\ne");
				csv.Flush();

				Assert.Equal("\"o\r\ne\"", writer.ToString());
			}
		}

		[Fact]
		public void ContainsDelimiterTest()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteField($"o{csv.Configuration.Delimiter}e");
				csv.Flush();

				Assert.Equal($"\"o{csv.Configuration.Delimiter}e\"", writer.ToString());
			}
		}

		[Fact]
		public void Test1()
		{
			var data = new List<(int row, int column, string? field)>();
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = args =>
				{
					data.Add((args.Row.Row, args.Row.Index, args.Field));

					return ConfigurationFunctions.ShouldQuote(args);
				},
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("Id");
				csv.WriteField("Name");
				csv.NextRecord();

				csv.WriteField("1");
				csv.WriteField("one");
				csv.NextRecord();

				csv.Flush();
			}

			Assert.Equal(4, data.Count);

			Assert.Equal(1, data[0].row);
			Assert.Equal(0, data[0].column);
			Assert.Equal("Id", data[0].field);

			Assert.Equal(1, data[1].row);
			Assert.Equal(1, data[1].column);
			Assert.Equal("Name", data[1].field);

			Assert.Equal(2, data[2].row);
			Assert.Equal(0, data[2].column);
			Assert.Equal("1", data[2].field);

			Assert.Equal(2, data[3].row);
			Assert.Equal(1, data[3].column);
			Assert.Equal("one", data[3].field);
		}
	}
}
