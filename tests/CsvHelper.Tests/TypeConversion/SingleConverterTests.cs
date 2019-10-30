using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
    [TestClass]
    public class SingleConverterTests
    {
        [TestMethod]
        public void RoundTripMaxValueTest()
        {
            var converter = new SingleConverter();
            var s = converter.ConvertToString(float.MaxValue, null, new MemberMapData(null));
            var f = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.AreEqual(float.MaxValue, f);
        }

        [TestMethod]
        public void RoundTripMinValueTest()
        {
            var converter = new SingleConverter();
            var s = converter.ConvertToString(float.MinValue, null, new MemberMapData(null));
            var f = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.AreEqual(float.MinValue, f);
        }
    }
}
