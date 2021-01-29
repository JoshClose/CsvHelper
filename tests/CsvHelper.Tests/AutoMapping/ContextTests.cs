// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
    public class ContextTests
    {
		[TestMethod]
        public void AutoMap_UsesContext()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
			context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("Bar");

			var map = context.AutoMap<Foo>();

			Assert.IsTrue(map.MemberMaps.Find<Foo>(x => x.Name).Data.TypeConverterOptions.NullValues.Contains("Bar"));
		}

		private class Foo
		{
			public int Id { get; set; }
			public string Name { get; set; }
		}
    }
}
