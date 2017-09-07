using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
    public class CircularReferenceTests
    {
		[TestMethod]
		public void CircularDependencyTest()
		{
			var config = new CsvHelper.Configuration.Configuration();
			var map = config.AutoMap<ACircular>();
			Assert.IsNotNull( map );
			Assert.AreEqual( 1, map.MemberMaps.Count );
			Assert.AreEqual( 1, map.ReferenceMaps.Count );
			Assert.AreEqual( 1, map.ReferenceMaps[0].Data.Mapping.MemberMaps.Count );
			Assert.AreEqual( 0, map.ReferenceMaps[0].Data.Mapping.ReferenceMaps.Count );
		}

		[TestMethod]
		public void CircularDependencyWithMultiplePropertiesTest()
		{
			var config = new CsvHelper.Configuration.Configuration();
			var map = config.AutoMap<A>();
			Assert.AreEqual( 1, map.MemberMaps.Count );
			Assert.AreEqual( 3, map.ReferenceMaps.Count );
		}

		private class ACircular
		{
			public string Id { get; set; }

			public BCircular B { get; set; }
		}

		private class BCircular
		{
			public string Id { get; set; }

			public ACircular A { get; set; }
		}

		private class A
		{
			public string Id { get; set; }
			
			public B B1 { get; set; }

			public B B2 { get; set; }

			public B B3 { get; set; }
		}

		private class B
		{
			public string Id { get; set; }

			public C C { get; set; }
		}

		private class C
		{
			public string Id { get; set; }
		}
	}
}
