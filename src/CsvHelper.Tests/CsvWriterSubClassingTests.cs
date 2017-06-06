// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
