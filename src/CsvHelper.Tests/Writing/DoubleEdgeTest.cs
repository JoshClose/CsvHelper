using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Writing
{
    [TestClass]
    public class DoubleEdgeTest
    {
        [TestMethod]
        public void Test1()
        {
            var records = new List<Double>
            {
                123d,
                Double.MaxValue,
                Double.MinValue
            };

            using (var writeStream = new MemoryStream())
            {

                using (var writer = new StreamWriter(writeStream))
                {
                    using (var csv = new CsvWriter(writer))
                    {
                        csv.WriteRecords(records);
                    }
                }

                writeStream.Flush();
                var csvText = Encoding.ASCII.GetString(writeStream.ToArray());
                using (var reader = new StringReader(csvText))
                using (var csv = new CsvReader(reader))
                {
                    csv.Configuration.HasHeaderRecord = false;
                    var readRecords = csv.GetRecords<Double>().ToList();
                    Assert.AreEqual(3, readRecords.Count);
                }

            }
        }
    }
}
