using CsvHelper.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
			using (var csv = new CsvReader(reader))
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
			using (var csv = new CsvReader(reader))
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

				var records = csv.GetRecords<dynamic>();
				var enumerator = records.GetEnumerator();
				enumerator.MoveNext();
				Assert.AreEqual("1", enumerator.Current.Id);
				Assert.AreEqual("2", enumerator.Current.Blank1);
				Assert.AreEqual(null, enumerator.Current.Blank2);
				enumerator.MoveNext();
				Assert.AreEqual("3", enumerator.Current.Id);
				Assert.AreEqual(null, enumerator.Current.Blank1);
				Assert.AreEqual(null, enumerator.Current.Blank2);
			}
		}
	}
}