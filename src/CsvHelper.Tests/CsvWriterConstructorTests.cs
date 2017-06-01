// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterConstructorTests
	{
		[TestMethod]
		public void EnsureInternalsAreSetupWhenPasingWriterAndConfigTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			{
				var config = new CsvConfiguration();
				using( var csv = new CsvWriter( writer, config ) )
				{
					Assert.AreSame( config, csv.Configuration );
				}
			}
		}
	}
}
