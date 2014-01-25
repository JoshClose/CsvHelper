// Copyright 2009-2014 Josh Close and Contributors
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
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.AreEqual( "True", converter.ConvertToString( typeConverterOptions, true ) );

			Assert.AreEqual( "False", converter.ConvertToString( typeConverterOptions, false ) );

			Assert.AreEqual( "", converter.ConvertToString( typeConverterOptions, null ) );
			Assert.AreEqual( "1", converter.ConvertToString( typeConverterOptions, 1 ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new BooleanConverter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "true" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "True" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "TRUE" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "1" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "yes" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "YES" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, "Y" ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, " true " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, " yes " ) );
			Assert.IsTrue( (bool)converter.ConvertFromString( typeConverterOptions, " y " ) );

			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "false" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "False" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "FALSE" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "0" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "no" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "NO" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "n" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, "N" ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, " false " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, " 0 " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, " no " ) );
			Assert.IsFalse( (bool)converter.ConvertFromString( typeConverterOptions, " n " ) );

			try
			{
				converter.ConvertFromString( typeConverterOptions, null );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}
	}
}
