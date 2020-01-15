// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterConstructorTests
	{
		[TestMethod]
		public void EnsureInternalsAreSetupWhenPassingWriterAndConfigTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			{
				var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
				using( var csv = new CsvWriter( writer, config ) )
				{
					Assert.AreSame( config, csv.Configuration );
				}
			}
		}
	}
}
