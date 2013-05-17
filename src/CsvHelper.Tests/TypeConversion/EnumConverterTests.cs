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
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.AreEqual( "None", converter.ConvertToString( typeConverterOptions, (TestEnum)0 ) );
			Assert.AreEqual( "None", converter.ConvertToString( typeConverterOptions, TestEnum.None ) );
			Assert.AreEqual( "One", converter.ConvertToString( typeConverterOptions, (TestEnum)1 ) );
			Assert.AreEqual( "One", converter.ConvertToString( typeConverterOptions, TestEnum.One ) );
			Assert.AreEqual( "", converter.ConvertToString( typeConverterOptions, null ) );
		}

		[TestMethod]
		public void ConvertFromStringTest()
		{
			var converter = new EnumConverter( typeof( TestEnum ) );
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = CultureInfo.CurrentCulture
			};

			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( typeConverterOptions, "One" ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( typeConverterOptions, "one" ) );
			Assert.AreEqual( TestEnum.One, converter.ConvertFromString( typeConverterOptions, "1" ) );
			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( typeConverterOptions, "" ) );
				Assert.Fail();
			}
			catch( CsvTypeConverterException )
			{
			}

			try
			{
				Assert.AreEqual( TestEnum.One, converter.ConvertFromString( typeConverterOptions, null ) );
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
