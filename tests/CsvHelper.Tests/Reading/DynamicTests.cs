// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				writer.WriteLine("O ne,Tw o,Thr ee");
				writer.WriteLine("1,2,3");
				writer.Flush();
				stream.Position = 0;

				csv.Configuration.PrepareHeaderForMatch = (header, index) => header.Replace(" ", string.Empty);
				var records = csv.GetRecords<dynamic>().ToList();
				Assert.AreEqual("1", records[0].One);
				Assert.AreEqual("2", records[0].Two);
				Assert.AreEqual("3", records[0].Three);
			}
		}

		[TestMethod]
		public void BlankHeadersTest()
		{
			var s = new StringBuilder();
			s.AppendLine("Id,,");
			s.AppendLine("1,2");
			s.AppendLine("3");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.PrepareHeaderForMatch = (header, index) =>
				{
					if (string.IsNullOrWhiteSpace(header))
					{
						return $"Blank{index}";
					}

					return header;
				};

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
	}
}
