// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
	public class DynamicTests
	{
		[TestMethod]
		public void PrepareHeaderTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args => args.Header.Replace(" ", string.Empty),
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, config))
			{
				writer.WriteLine("O ne,Tw o,Thr ee");
				writer.WriteLine("1,2,3");
				writer.Flush();
				stream.Position = 0;

				var records = csv.GetRecords<dynamic>().ToList();
				Assert.AreEqual("1", records[0].One);
				Assert.AreEqual("2", records[0].Two);
				Assert.AreEqual("3", records[0].Three);
			}
		}

		[TestMethod]
		public void BlankHeadersTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				PrepareHeaderForMatch = args =>
				{
					if (string.IsNullOrWhiteSpace(args.Header))
					{
						return $"Blank{args.FieldIndex}";
					}

					return args.Header;
				},
			};
			var s = new StringBuilder();
			s.AppendLine("Id,,");
			s.AppendLine("1,2");
			s.AppendLine("3");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, config))
			{
				var records = csv.GetRecords<dynamic>().ToList();

				var record = records[0];
				Assert.AreEqual("1", record.Id);
				Assert.AreEqual("2", record.Blank1);
				Assert.AreEqual(null, record.Blank2);

				record = records[1];
				Assert.AreEqual("3", record.Id);
				Assert.AreEqual(null, record.Blank1);
				Assert.AreEqual(null, record.Blank2);
			}
		}

		[TestMethod]
		public void DuplicateFieldNamesTest()
		{
			var headerNameCounts = new Dictionary<string, int>();
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				GetDynamicPropertyName = args =>
				{
					var header = args.Context.Reader.HeaderRecord[args.FieldIndex];
					header = args.Context.Reader.Configuration.PrepareHeaderForMatch(new PrepareHeaderForMatchArgs(header, args.FieldIndex));
					var name = headerNameCounts[header] > 1 ? $"{header}{args.FieldIndex}" : header;

					return name;
				},
			};
			var parser = new ParserMock(config)
			{
				{ "Id", "Name", "Name" },
				{ "1", "foo", "bar" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				csv.Read();
				csv.ReadHeader();
				var counts =
					(from header in csv.Context.Reader.HeaderRecord.Select((h, i) => csv.Configuration.PrepareHeaderForMatch(new PrepareHeaderForMatchArgs(h, i)))
					 group header by header into g
					 select new
					 {
						 Header = g.Key,
						 Count = g.Count()
					 }).ToDictionary(x => x.Header, x => x.Count);
				foreach (var count in counts)
				{
					headerNameCounts.Add(count.Key, count.Value);
				}

				var records = csv.GetRecords<dynamic>().ToList();
				var record = records[0];
				Assert.AreEqual("1", record.Id);
				Assert.AreEqual("foo", record.Name1);
				Assert.AreEqual("bar", record.Name2);
			}
		}
	}
}
