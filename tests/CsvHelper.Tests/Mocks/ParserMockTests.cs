// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mocks
{
	[TestClass]
    public class ParserMockTests
    {
		[TestMethod]
        public void Test()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
			};
			Assert.IsTrue(parser.Read());
			Assert.AreEqual("Id", parser[0]);
			Assert.AreEqual("Name", parser[1]);

			Assert.IsTrue(parser.Read());
			Assert.AreEqual("1", parser[0]);
			Assert.AreEqual("one", parser[1]);

			Assert.IsFalse(parser.Read());
			Assert.IsFalse(parser.Read());
		}
	}
}
