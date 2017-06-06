// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Reflection
{
	[TestClass]
	public class ReflectionHelperTests
	{
		[TestMethod]
		public void CreateInstanceTests()
		{
			var test = ReflectionHelper.CreateInstance<Test>();

			Assert.IsNotNull( test );
			Assert.AreEqual( "name", test.Name );

			test = (Test)ReflectionHelper.CreateInstance( typeof( Test ) );
			Assert.IsNotNull( test );
			Assert.AreEqual( "name", test.Name );
		}

		private class Test
		{
			public string Name
			{
				get { return "name"; }
			}
		}
	}
}
