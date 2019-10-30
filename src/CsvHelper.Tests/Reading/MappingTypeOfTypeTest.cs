using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Tests.Issues;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reading
{
    [TestClass]
    public class MappingTypeOfTypeTest
    {
        [TestMethod]
        public void TypeTypeShouldBeSkipped()
        {
            string csvFile = @"Equipment
MyTestEquipment4";
            using (var reader = new StringReader(csvFile))
            using (var csv = new CsvReader(reader))
            {
                csv.Configuration.Delimiter = ",";
                try
                {
                    var list = csv.GetRecords<EquipmentDataPoint>().ToList();
                }
                catch (Exception)
                {
                    Assert.Fail("No exception should be thrown here");
                }
            }
        }

        public class EquipmentDataPoint
        {
            public string Equipment { get; set; }
            public Type ValueType { get; set; }
        }
    }
}