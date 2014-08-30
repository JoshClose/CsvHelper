using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class MappingInheritedClassTests
	{
		[TestMethod]
		public void Test()
		{
			var map = new AMap<A>();
			Assert.AreEqual( 2, map.PropertyMaps.Count );
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

		private sealed class AMap<T> : CsvClassMap<T> where T : IA
		{
			public AMap()
			{
				AutoMap();
				Map( m => m.Id );
			}
		}
	}
}
