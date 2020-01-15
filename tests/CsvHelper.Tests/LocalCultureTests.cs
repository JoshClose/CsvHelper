// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class LocalCultureTests
	{
		// In 'uk-UA' decimal separator is the ','
		// For 'Invariant' and many other cultures decimal separator is '.'

		[TestMethod]
		public void ReadRecordsTest()
		{
			const string source = "DateTimeColumn;DecimalColumn\r\n" +
								  "11.11.2010;12,0\r\n";

			var configuration = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("uk-UA"))
			{
				Delimiter = ";",
			};
			var reader = new CsvReader(new CsvParser(new StringReader(source), configuration));

			var records = reader.GetRecords<TestRecordWithDecimal>().ToList();

			Assert.AreEqual(1, records.Count());
			var record = records.First();
			Assert.AreEqual(12.0m, record.DecimalColumn);
			Assert.AreEqual(new DateTime(2010, 11, 11), record.DateTimeColumn);
		}

		[TestMethod]
		public void WriteRecordsTest()
		{
			var records = new List<TestRecordWithDecimal>
			{
				new TestRecordWithDecimal
				{
					DecimalColumn = 12.0m,
					DateTimeColumn = new DateTime( 2010, 11, 11 )
				}
			};

			var writer = new StringWriter();
			var csv = new CsvWriter(writer, new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("uk-UA")) { Delimiter = ";" });

			csv.WriteRecords(records);

			var csvFile = writer.ToString();

			const string expected = "DecimalColumn;DateTimeColumn\r\n" +
									"12,0;11.11.2010 0:00:00\r\n";

			Assert.AreEqual(expected, csvFile);
		}

		private class TestRecordWithDecimal
		{
			public decimal DecimalColumn { get; set; }
			public DateTime DateTimeColumn { get; set; }
		}
	}
}
