// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com

using CsvHelper.Configuration.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Tests.AttributeMapping
{
    [TestClass]
    public class EncodingTests
    {
        [TestMethod]
        public void EncodingTest()
        {
            using (var reader = new StringReader("Id,Name\r\n1,one\r\n"))
            using (var csv = new CsvReader(reader))
            {
                csv.Configuration.Delimiter = ",";
                var records = csv.GetRecords<EncodingTestClass>().ToList();
                var actual = csv.Configuration.Encoding;

                Assert.AreEqual(Encoding.ASCII, actual);
            }
        }

        [Encoding("ASCII")]
        private class EncodingTestClass
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
