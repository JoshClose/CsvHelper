using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvConfigurationTests
	{
		[TestMethod]
		public void Adding_Mappings_with_generic_method_1()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings, TestClass>();
			
			Assert.AreEqual(2, config.Properties.Count);
		}

		[TestMethod]
		public void Adding_Mappings_with_generic_method_2()
		{
			var config = new CsvConfiguration();
			config.ClassMapping<TestClassMappings>();

			Assert.AreEqual(2, config.Properties.Count);
		}

		[TestMethod]
		public void Adding_Mappings_from_class_map_instance()
		{
			var config = new CsvConfiguration();
			config.ClassMapping(new TestClassMappings());

			Assert.AreEqual(2, config.Properties.Count);
		}

		
		private class TestClass
		{
			public string StringColumn { get; set; }
			public int IntColumn { get; set; }
		}

		private class TestClassMappings : CsvClassMap<TestClass>
		{
			public TestClassMappings()
			{
				Map(c => c.StringColumn);
				Map(c => c.IntColumn);
			}
		}
	}
}