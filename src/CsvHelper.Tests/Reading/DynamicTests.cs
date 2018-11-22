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

				csv.Configuration.PrepareHeaderForMatch = header => header.Replace(" ", string.Empty);
				var records = csv.GetRecords<dynamic>().ToList();
				Assert.AreEqual("1", records[0].One);
				Assert.AreEqual("2", records[0].Two);
				Assert.AreEqual("3", records[0].Three);
			}
		}
	}
}