// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterSubClassingTests
	{
		[TestMethod]
		public void WriteRecordTest()
		{
			var data = new List<Test>
			{
				new Test { Id = 1, Name = "one" },
				new Test { Id = 2, Name = "two" }
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );
			var csvWriter = new MyCsvWriter( writer );

			csvWriter.WriteRecords( data );
		}

		private class MyCsvWriter : CsvWriter
		{
			public MyCsvWriter( TextWriter writer ) : base( writer ){}
		}

		private class Test
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
	}
}
