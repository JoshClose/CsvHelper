// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	
    public class ArrayHelperTests
    {
		[Fact]
        public void Contains_HasValue_ReturnsTrue()
		{
			var array = new char[] { 'a' };

			var contains = ArrayHelper.Contains(array, 'a');

			Assert.True(contains);
		}

		[Fact]
		public void Contains_DoesNotHaveValue_ReturnsFalse()
		{
			var array = new char[] { 'a' };

			var contains = ArrayHelper.Contains(array, 'b');

			Assert.False(contains);
		}

		[Fact]
		public void Trim_FullBuffer_TrimsChars()
		{
			var buffer = " a ".ToCharArray();
			var trimChars = new char[] { ' ' };
			var start = 0;
			var length = buffer.Length;

			ArrayHelper.Trim(buffer, ref start, ref length, trimChars);

			Assert.Equal(1, start);
			Assert.Equal(1, length);
		}

		[Fact]
		public void Trim_MidBuffer_TrimsChars()
		{
			var buffer = "a b c".ToCharArray();
			var trimChars = new char[] { ' ' };
			var start = 1;
			var length = 3;

			ArrayHelper.Trim(buffer, ref start, ref length, trimChars);

			Assert.Equal(2, start);
			Assert.Equal(1, length);
		}

		[Fact]
		public void Trim_AllWhitespace_EmptyString()
		{
			var buffer = new string(' ', 100).ToCharArray();
			var trimChars = new char[] { ' ' };
			var start = 0;
			var length = buffer.Length;

			ArrayHelper.Trim(buffer, ref start, ref length, trimChars);

			Assert.Equal(100, start);
			Assert.Equal(0, length);
		}
	}
}
