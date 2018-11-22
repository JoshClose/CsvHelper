using CsvHelper.Configuration;
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
	public class TrimTests
	{
		[TestMethod]
		public void Test()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer))
			{
				csv.Configuration.Delimiter = ",";
				csv.Configuration.TrimOptions = TrimOptions.Trim;
				csv.WriteField("  a b c  ");
				csv.WriteField("  d e f  ");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var expected = new StringBuilder();
				expected.AppendLine("a b c,d e f");

				Assert.AreEqual(expected.ToString(), reader.ReadToEnd());
			}
		}
	}
}