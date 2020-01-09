// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reflection
{
	[TestClass]
	public class GetPropertiesTests
	{
		[TestMethod]
		public void FirstLevelTest()
		{
			var stack = ReflectionHelper.GetMembers<A, string>( a => a.P1 );

			Assert.AreEqual( 1, stack.Count );
			Assert.AreEqual( "P1", stack.Pop().Name );
		}

		[TestMethod]
		public void LastLevelTest()
		{
			var stack = ReflectionHelper.GetMembers<A, string>( a => a.B.C.D.P4 );

			Assert.AreEqual( 4, stack.Count );
			Assert.AreEqual( "B", stack.Pop().Name );
			Assert.AreEqual( "C", stack.Pop().Name );
			Assert.AreEqual( "D", stack.Pop().Name );
			Assert.AreEqual( "P4", stack.Pop().Name );
		}

		public void ThirdLevelTest()
		{
		}

		private class A
		{
			public string P1 { get; set; }
			public B B { get; set; }
		}

		private class B
		{
			public string P2 { get; set; }
			public C C { get; set; }
		}

		private class C
		{
			public string P3 { get; set; }
			public D D { get; set; }
		}

		private class D
		{
			public string P4 { get; set; }
		}
	}
}
