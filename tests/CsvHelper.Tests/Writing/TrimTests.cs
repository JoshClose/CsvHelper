// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
	public class TrimTests
	{
		[TestMethod]
		public void Test()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				TrimOptions = TrimOptions.Trim,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField("  a b c  ");
				csv.WriteField("  d e f  ");
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var expected = new TestStringBuilder(csv.Configuration.NewLine);
				expected.AppendLine("a b c,d e f");

				Assert.AreEqual(expected.ToString(), reader.ReadToEnd());
			}
		}
	}
}
