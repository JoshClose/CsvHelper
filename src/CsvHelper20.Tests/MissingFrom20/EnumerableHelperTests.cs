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
