// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	[TestClass]
    public class ArrayHelperTests
    {
		[TestMethod]
        public void Contains_HasValue_ReturnsTrue()
		{
			var array = new char[] { 'a' };

			var contains = ArrayHelper.Contains(array, 'a');

			Assert.IsTrue(contains);
		}

		[TestMethod]
		public void Contains_DoesNotHaveValue_ReturnsFalse()
		{
			var array = new char[] { 'a' };

			var contains = ArrayHelper.Contains(array, 'b');

			Assert.IsFalse(contains);
		}

		[TestMethod]
		public void Trim_FullBuffer_TrimsChars()
		{
			var buffer = " a ".ToCharArray();
			var trimChars = new char[] { ' ' };
			var start = 0;
			var length = buffer.Length;

			ArrayHelper.Trim(buffer, ref start, ref length, trimChars);

			Assert.AreEqual(1, start);
			Assert.AreEqual(1, length);
		}

		[TestMethod]
		public void Trim_MidBuffer_TrimsChars()
		{
			var buffer = "a b c".ToCharArray();
			var trimChars = new char[] { ' ' };
			var start = 1;
			var length = 3;

			ArrayHelper.Trim(buffer, ref start, ref length, trimChars);

			Assert.AreEqual(2, start);
			Assert.AreEqual(1, length);
		}

	}
}
