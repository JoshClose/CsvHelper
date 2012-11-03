// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using Xunit;

namespace CsvHelper.Tests
{
	public class StringExtensionTests
	{
		[Fact]
		public void IsNullOrWhiteSpaceTest()
		{
			string nullString = null;
			Assert.True( nullString.IsNullOrWhiteSpace() );
			Assert.True( "".IsNullOrWhiteSpace() );
			Assert.True( " ".IsNullOrWhiteSpace() );
			Assert.True( "	".IsNullOrWhiteSpace() );
			Assert.False( "a".IsNullOrWhiteSpace() );
		}
	}
}
