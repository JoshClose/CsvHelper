using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	
    public class BufferSplittingEscapeAndQuoteTests
    {
		[Fact]
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
				Assert.Equal("a", parser[0]);
				Assert.Equal("bcdefghijklm\"nopqrstuvwxyz", parser[1]);
			}
		}
    }
}
