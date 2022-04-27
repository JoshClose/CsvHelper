// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
    
    public class BigIntegerConverterTests
    {
        [Fact]
        public void RoundTripMaxValueTest()
        {
            var converter = new BigIntegerConverter();
            var s = converter.ConvertToString((BigInteger)long.MaxValue + 1, null, new MemberMapData(null));
            var bi = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.Equal((BigInteger)long.MaxValue + 1, bi);
        }

        [Fact]
        public void RoundTripMinValueTest()
        {
            var converter = new BigIntegerConverter();
            var s = converter.ConvertToString((BigInteger)long.MinValue - 1, null, new MemberMapData(null));
            var bi = converter.ConvertFromString(s, null, new MemberMapData(null));

            Assert.Equal((BigInteger)long.MinValue - 1, bi);
        }
    }
}
