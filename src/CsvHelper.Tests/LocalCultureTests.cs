// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
    [TestClass]
	public class LocalCultureTests
	{
	    private static readonly CultureTestCase[] TestCases =
	    {
            new CultureTestCase( "en-US", "12.5;10/25/2000 12:00:00 AM" ), 
	        new CultureTestCase( "en-GB", "12.5;25/10/2000 00:00:00" ), 
            new CultureTestCase( "uk-UA", "12,5;25.10.2000 0:00:00" )
	    };

        [TestMethod]
        public void ReadSpecifiedCultureRecords()
        {
            foreach (var testCase in TestCases)
            {
                var configuration = new CsvConfiguration
                {
                    Delimiter = testCase.Delimiter,
                    CultureInfo = testCase.Culture
                };
                ReadRecordsTestBody( testCase, configuration );
            }
        }

        [TestMethod]
        public void ReadThreadCultureRecords()
        {
            foreach (var cultureTestCase in TestCases)
            {
                RunTestInSpecificCulture( c => ReadRecordsTestBody(c), cultureTestCase);
            }
        }

        [TestMethod]
        public void ReadTypeConverterOptionsCultureRecords()
        {
            foreach (var testCase in TestCases)
            {
                var configuration = new CsvConfiguration { Delimiter = testCase.Delimiter };
                configuration.RegisterClassMap(testCase.ClassMap);
                ReadRecordsTestBody(testCase, configuration);
            }
        }

        private static void ReadRecordsTestBody( CultureTestCase testCase, CsvConfiguration configuration = null )
        {
            configuration = configuration ?? new CsvConfiguration { Delimiter = testCase.Delimiter };
            var reader = new CsvReader( new CsvParser( new StringReader( testCase.Csv ), configuration ) );

            var records = reader.GetRecords<TestRecordWithDecimal>().ToList();

            Assert.AreEqual( 1, records.Count() );
            var record = records.First();
            Assert.AreEqual( testCase.Number, record.DecimalColumn );
            Assert.AreEqual( testCase.Date, record.DateTimeColumn );
        }

        [TestMethod]
        public void WriteSpecifiedCultureRecords()
        {
            foreach (var testCase in TestCases)
            {
                var configuration = new CsvConfiguration
                {
                    Delimiter = testCase.Delimiter,
                    CultureInfo = testCase.Culture
                };
                WriteRecordsTestBody( testCase, configuration );
            }
        }

        [TestMethod]
        public void WriteThreadCultureRecords()
        {
            foreach (var cultureTestCase in TestCases)
            {
                RunTestInSpecificCulture( c => WriteRecordsTestBody(c), cultureTestCase );
            }
        }

        [TestMethod]
        public void WriteTypeConverterOptionsCultureRecords()
        {
            foreach (var testCase in TestCases)
            {
                var configuration = new CsvConfiguration { Delimiter = testCase.Delimiter };
                configuration.RegisterClassMap(testCase.ClassMap);
                WriteRecordsTestBody(testCase, configuration);
            }
        }

        private static void WriteRecordsTestBody( CultureTestCase testCase, CsvConfiguration configuration = null )
        {
            configuration = configuration ?? new CsvConfiguration { Delimiter = testCase.Delimiter };
            var records = new List<TestRecordWithDecimal>
			{
				new TestRecordWithDecimal
				{
					DecimalColumn = testCase.Number,
					DateTimeColumn = testCase.Date
				}
			};

            var writer = new StringWriter();
            var csv = new CsvWriter( writer, configuration );

            csv.WriteRecords( records );

            var csvFile = writer.ToString();

            Assert.AreEqual( testCase.Csv, csvFile );
        }

        private static void RunTestInSpecificCulture(Action<CultureTestCase> action, CultureTestCase testCase)
        {
            var originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = testCase.Culture;
                action(testCase);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

		private class TestRecordWithDecimal
		{
			public decimal DecimalColumn { get; set; }
			public DateTime DateTimeColumn { get; set; }
		}

        private sealed class CultureTestCase
        {
            private const decimal DecimalValue = 12.5m;
            private static readonly DateTime DateValue = new DateTime(2000, 10, 25);

            public CultureTestCase(string cultureName, string csv)
            {
                Culture = new CultureInfo(cultureName);
                Number = DecimalValue;
                Date = DateValue;
                Csv = String.Format("DecimalColumn;DateTimeColumn\r\n{0}\r\n", csv);
                Delimiter = ";";
                ClassMap = new TestClassMap( Culture );
            }

            public CultureInfo Culture { get; private set; }

            public decimal Number { get; private set; }

            public DateTime Date { get; private set; }

            public string Csv { get; private set; }

            public string Delimiter { get; private set; }

            public CsvClassMap<TestRecordWithDecimal> ClassMap { get; private set; }

            private class TestClassMap : CsvClassMap<TestRecordWithDecimal>
            {
                public TestClassMap(CultureInfo culture)
                {
                    Map( m => m.DecimalColumn ).TypeConverterOption( culture );
                    Map( m => m.DateTimeColumn ).TypeConverterOption( culture );
                }
            }
        }
	}
}
