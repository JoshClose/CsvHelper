#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterWriteRecordsCultureTests
	{
		private static readonly CultureInfo systemCurrentCulture = Thread.CurrentThread.CurrentCulture;

		[ClassInitialize]
		public static void InitCurrentCulture( TestContext context )
		{
			// Lets setup some culture with different decimal point.
			Thread.CurrentThread.CurrentCulture = new CultureInfo( "uk-UA" );
		}

		[TestMethod]
		public void WriteRecordsWithSomeCultureSpecificValuesTest()
		{
			var records = new List<TestRecordWithDecimal>
            {
                new TestRecordWithDecimal
                {                    
                    DecimalColumn = 12.0m,
                    DateTimeColumn = new DateTime(2010, 11, 11)
                }                
            };

			var writer = new StringWriter();
			var csv = new CsvWriter( writer, new CsvWriterOptions { HasHeaderRecord = true } );

			csv.WriteRecords( records );

			var csvFile = writer.ToString();

			var expected = "DecimalColumn,DateTimeColumn\r\n";
			expected += "12.0,11/11/2010 00:00:00\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		private class TestRecordWithDecimal
		{
			public decimal DecimalColumn { get; set; }

			public DateTime DateTimeColumn { get; set; }
		}

		[ClassCleanup]
		public static void ResetCurrentCulture()
		{
			Thread.CurrentThread.CurrentCulture = systemCurrentCulture;
		}
	}
}
