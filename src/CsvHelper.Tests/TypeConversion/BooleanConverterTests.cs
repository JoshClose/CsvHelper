// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;
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

			Assert.AreEqual( "True", converter.ConvertToString( CultureInfo.CurrentCulture, true ) );

			Assert.AreEqual( "False", converter.ConvertToString( CultureInfo.CurrentCulture, false ) );

			Assert.AreEqual( "", converter.ConvertToString( CultureInfo.CurrentCulture, null ) );
			Assert.AreEqual( "1", converter.ConvertToString( CultureInfo.CurrentCulture, 1 ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();

			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "true" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "True" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "TRUE" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "1" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "yes" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "YES" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "Y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " true " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " yes " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " y " ) );

			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "false" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "False" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "FALSE" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "0" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "no" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "NO" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "n" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, "N" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " false " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " 0 " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " no " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( CultureInfo.CurrentCulture, " n " ) );

			try
			{
				converter.ConvertFromString( CultureInfo.CurrentCulture, null );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
