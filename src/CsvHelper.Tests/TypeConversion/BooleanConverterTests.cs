// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using CsvHelper.TypeConversion;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class BooleanConverterTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new BooleanConverter();

			Assert.AreEqual( "True", converter.ConvertToString( true ) );

			Assert.AreEqual( "False", converter.ConvertToString( false ) );

			Assert.AreEqual( "", converter.ConvertToString( null ) );
			Assert.AreEqual( "1", converter.ConvertToString( 1 ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			Assert.IsTrue( (bool)converter.ConvertFromString( "true" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "True" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "TRUE" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "1" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "yes" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "YES" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( "Y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " true " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " yes " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( " y " ) );

			Assert.IsFalse( (bool)converter.ConvertFromString( "false" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "False" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "FALSE" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "0" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "no" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "NO" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "n" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( "N" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " false " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " 0 " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " no " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( " n " ) );

			try
			{
				converter.ConvertFromString( null );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
