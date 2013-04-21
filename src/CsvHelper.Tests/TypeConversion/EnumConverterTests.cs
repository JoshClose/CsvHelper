// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;
using System.IO;
using System.Text;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CsvHelper.TypeConversion;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumConverterTests
	{
		[TestMethod]
		public void ConstructorTest()
		{
			try
			{
				new EnumConverter( typeof( string ) );
				Assert.Fail();
			}
			catch( ArgumentException ex )
			{
				Assert.AreEqual( "'System.String' is not an Enum.", ex.Message );
			}
		}

		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new EnumConverter( typeof( TestEnum ) );

			Assert.AreEqual( "None", converter.ConvertToString( CultureInfo.CurrentCulture, (TestEnum)0 ) );
			Assert.AreEqual( "None", converter.ConvertToString( CultureInfo.CurrentCulture, TestEnum.None ) );
			Assert.AreEqual( "One", converter.ConvertToString( CultureInfo.CurrentCulture, (TestEnum)1 ) );
			Assert.AreEqual( "One", converter.ConvertToString( CultureInfo.CurrentCulture, TestEnum.One ) );
			Assert.AreEqual( "", converter.ConvertToString( CultureInfo.CurrentCulture, null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new EnumConverter( typeof( TestEnum ) );

			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( CultureInfo.CurrentCulture, "One" ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( CultureInfo.CurrentCulture, "one" ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( CultureInfo.CurrentCulture, "1" ) );
			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( CultureInfo.CurrentCulture, "" ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}

			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( CultureInfo.CurrentCulture, null ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}
		}

		private enum TestEnum
		{
			None = 0,
			One = 1,
		}
	}
}
