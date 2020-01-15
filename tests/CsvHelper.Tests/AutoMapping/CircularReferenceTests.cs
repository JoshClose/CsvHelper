// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace CsvHelper.Tests.AutoMapping
{
	[TestClass]
	public class CircularReferenceTests
	{
		[TestMethod]
		public void SelfCircularDependencyTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<SelfCircularA>();
		}

		[TestMethod]
		public void CircularDependencyTest()
		{
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
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
			var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<A>();
			Assert.AreEqual( 1, map.MemberMaps.Count );
			Assert.AreEqual( 3, map.ReferenceMaps.Count );
		}

		private class SelfCircularA
		{
			public SelfCircularB Circular { get; set; }
		}

		private class SelfCircularB
		{
			public SelfCircularB Self { get; set; }

			public SelfCircularC C { get; set; }
		}

		private class SelfCircularC
		{
			public string Id { get; set; }
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
