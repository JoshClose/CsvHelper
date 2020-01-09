// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
