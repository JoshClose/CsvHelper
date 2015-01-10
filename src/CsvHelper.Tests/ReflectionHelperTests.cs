// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
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
