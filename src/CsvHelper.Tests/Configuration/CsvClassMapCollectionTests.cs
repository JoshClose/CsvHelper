using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Configuration
{
	[TestClass]
	public class CsvClassMapCollectionTests
	{
		[TestMethod]
		public void GetChildMapWhenParentIsMappedBeforeIt()
		{
			var parentMap = new ParentMap();
			var childMap = new ChildMap();
			var c = new CsvClassMapCollection();
			c.Add( parentMap );
			c.Add( childMap );

			var map = c[typeof( Child )];
			Assert.AreEqual( childMap, map );
		}

		private class Parent { }

		private class Child : Parent { }

		private sealed class ParentMap : CsvClassMap<Parent> { }

		private sealed class ChildMap : CsvClassMap<Child> { }
	}
}
