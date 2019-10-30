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
    public class DoubleConverterTests
    {
		[TestMethod]
		public void RoundTripMaxValueTest()
        {
            var converter = new DoubleConverter();
            var s = converter.ConvertToString(double.MaxValue, null, new MemberMapData(null));
            var d = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.AreEqual(double.MaxValue, d);
        }

        [TestMethod]
        public void RoundTripMinValueTest()
        {
            var converter = new DoubleConverter();
            var s = converter.ConvertToString(double.MinValue, null, new MemberMapData(null));
            var d = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.AreEqual(double.MinValue, d);
        }
    }
}
