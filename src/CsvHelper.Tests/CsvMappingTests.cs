using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvMappingTests
	{
		[TestMethod]
		public void Test()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestMappingClass, TestClass>();

			var map = new TestMappingClass();
		}

		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
			public Guid GuidColumn { get; set; }
		}

		private sealed class TestMappingClass : CsvClassMap<TestClass>
		{
			public TestMappingClass()
			{
				Map( m => m.GuidColumn );
				Map( m => m.IntColumn );
				Map( m => m.StringColumn );
			}
		}
	}
}
