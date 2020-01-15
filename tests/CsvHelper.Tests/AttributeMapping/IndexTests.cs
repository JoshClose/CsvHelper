// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.AttributeMapping
{
	public class IndexTests
	{
		[TestMethod]
		public void IndexTest()
		{
			using( var reader = new StringReader( "a,1,b,one,c\r\n" ) )
			using( var csv = new CsvReader(reader, CultureInfo.InvariantCulture) )
			{
				csv.Configuration.HasHeaderRecord = false;
				var records = csv.GetRecords<IndexTestClass>().ToList();

				Assert.AreEqual( 1, records[0].Id );
				Assert.AreEqual( "one", records[0].Name );
			}
		}

		private class IndexTestClass
		{
			[Index( 1 )]
			public int Id { get; set; }

			[Index( 3 )]
			public string Name { get; set; }
		}
	}
}
