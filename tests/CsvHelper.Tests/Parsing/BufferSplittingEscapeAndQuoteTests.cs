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
    public class BufferSplittingEscapeAndQuoteTests
    {
		[TestMethod]
		public void Read_BufferEndsAtEscape_FieldIsNotBadData()
		{
			var s = new StringBuilder();
			s.Append("a,\"bcdefghijklm\"\"nopqrstuvwxyz\"\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				parser.Read();
				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual("bcdefghijklm\"nopqrstuvwxyz", parser[1]);
			}
		}
    }
}
