using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class StringExtensionTests
	{
		[TestMethod]
		public void IsNullOrWhiteSpaceTest()
		{
			string nullString = null;
			Assert.IsTrue( nullString.IsNullOrWhiteSpace() );
			Assert.IsTrue( "".IsNullOrWhiteSpace() );
			Assert.IsTrue( " ".IsNullOrWhiteSpace() );
			Assert.IsTrue( "	".IsNullOrWhiteSpace() );
			Assert.IsFalse( "a".IsNullOrWhiteSpace() );
		}
	}
}
