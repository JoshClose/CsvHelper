using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class WriteBufferTests
    {
		[TestMethod]
        public void Write_FieldThatIsLargerThenTwiceTheBuffer_Writes()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				var random = new Random();
				csv.WriteField("one");
				csv.WriteField(new string(Enumerable.Range(0, 1000).Select(i => (char)random.Next((int)'a', (int)'z' + 1)).ToArray()));
			}
		}
    }
}
