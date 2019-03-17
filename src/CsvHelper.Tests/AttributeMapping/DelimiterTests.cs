// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using System.IO;
using System.Linq;
using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.AttributeMapping {
    [TestClass]
    public class DelimiterTests
    {
        [TestMethod]
        public void DelimiterReaderTest()
        {
            using (var reader = new StringReader("Id§Name\r\n1§one\r\n"))
            using (var csv = new CsvReader(reader))
            {
                var records = csv.GetRecords<DelimiterTestClass>().ToList();
                var actual = csv.Configuration.Delimiter;

                Assert.AreEqual("§", actual);
            }
        }

        [TestMethod]
        public void DelimiterReaderWithConfigurationTest()
        {
            var configuration = new CsvHelper.Configuration.Configuration
            {
                Delimiter = ":"
            };

            using (var reader = new StringReader("Id§Name\r\n1§one\r\n"))
            using (var csv = new CsvReader(reader, configuration))
            {
                var records = csv.GetRecords<DelimiterTestClass>().ToList();
                var actual = csv.Configuration.Delimiter;

                Assert.AreEqual("§", actual);
            }
        }

        [TestMethod]
        public void DelimiterWriteHeaderTest()
        {
            using( var writer = new StringWriter() )
            {
                using (var csv = new CsvWriter(writer))
                {
                    csv.WriteHeader<DelimiterTestClass>();
                }

                Assert.AreEqual("Id§Name", writer.ToString());
            }
        }

        [TestMethod]
        public void DelimiterWriteRecordsTest()
        {
            using( var writer = new StringWriter() )
            {
                using (var csv = new CsvWriter(writer))
                {
                    csv.WriteRecords(new [] { new DelimiterTestClass { Id = 1, Name = "Grumpy" } });
                }

                Assert.AreEqual("Id§Name\r\n1§Grumpy\r\n", writer.ToString());
            }
        }

        [TestMethod]
        public void DelimiterWriteRecordsWithConfigurationTest()
        {
            using( var writer = new StringWriter() )
            {
                var configuration = new CsvHelper.Configuration.Configuration
                {
                    Delimiter = ":"
                };

                using (var csv = new CsvWriter(writer, configuration))
                {
                    csv.WriteRecords(new [] { new DelimiterTestClass { Id = 1, Name = "Grumpy" } });
                }

                Assert.AreEqual("Id§Name\r\n1§Grumpy\r\n", writer.ToString());
            }
        }

        [Delimiter("§")]
        private class DelimiterTestClass
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
