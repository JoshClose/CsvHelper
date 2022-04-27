// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	
    public class WriteBufferTests
    {
		[Fact]
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
