// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace CsvHelper.Tests
{
	[TestClass]
	public class MappingInheritedClassTests
	{
		[TestMethod]
		public void Test()
		{
			var map = new AMap<A>();
			Assert.AreEqual( 2, map.MemberMaps.Count );
		}

		private interface IA
		{
			int Id { get; set; }
		}

		private class A : IA
		{
			public int Id { get; set; }

			public int Name { get; set; }
		}

		private sealed class AMap<T> : ClassMap<T> where T : IA
		{
			public AMap()
			{
				AutoMap(CultureInfo.InvariantCulture);
				Map( m => m.Id );
			}
		}
	}
}
