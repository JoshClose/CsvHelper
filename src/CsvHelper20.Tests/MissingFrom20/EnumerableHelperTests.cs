// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.MissingFrom20;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper20.Tests.MissingFrom20
{
	[TestClass]
	public class EnumerableHelperTests
	{
		[TestMethod]
		public void SequenceEqualsTest()
		{
			var listA = new List<int> { 1, 2, 3 };
			var listB = new List<int?> { 1, 2 };

			Assert.IsFalse( EnumerableHelper.SequenceEqual( listA, listB ) );

			listB.Add( 4 );

			Assert.IsFalse( EnumerableHelper.SequenceEqual( listA, listB ) );

			listB[2] = null;

			Assert.IsFalse( EnumerableHelper.SequenceEqual( listA, listB ) );


			listB[2] = 3;

			Assert.IsTrue( EnumerableHelper.SequenceEqual( listA, listB ) );
		}
	}
}
