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
	public class CsvReaderConstructorTests
	{
		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				Assert.AreSame(csv.Configuration, csv.Parser.Configuration);
			}
		}

		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingTextReaderAndConfigurationTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)))
			{
				Assert.AreSame(csv.Configuration, csv.Parser.Configuration);
			}
		}

		[TestMethod]
		public void EnsureInternalsAreSetupCorrectlyWhenPassingParserTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			{
				var parser = new CsvParser(reader, CultureInfo.InvariantCulture);

				using (var csv = new CsvReader(parser))
				{
					Assert.AreSame(csv.Configuration, csv.Parser.Configuration);
					Assert.AreSame(parser, csv.Parser);
				}
			}
		}
	}
}
