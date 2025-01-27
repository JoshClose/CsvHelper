﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
    
    public class DoubleConverterTests
    {
		[Fact]
		public void RoundTripMaxValueTest()
        {
            var converter = new DoubleConverter();
            var s = converter.ConvertToString(double.MaxValue, null!, new MemberMapData(null));
            var d = converter.ConvertFromString(s, null!, new MemberMapData(null));

            Assert.Equal(double.MaxValue, d);
        }

        [Fact]
        public void RoundTripMinValueTest()
        {
            var converter = new DoubleConverter();
            var s = converter.ConvertToString(double.MinValue, null!, new MemberMapData(null));
            var d = converter.ConvertFromString(s, null!, new MemberMapData(null));

            Assert.Equal(double.MinValue, d);
        }
    }
}
