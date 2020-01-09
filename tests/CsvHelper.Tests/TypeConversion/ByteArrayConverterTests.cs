// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class ByteArrayConverterTests
	{
		[TestMethod]
		public void WhenConverting_FromStringToByteArray_ReturnsCorrectValues()
		{
			var testCases = new[]
			{
				new
				{
					Options = ByteArrayConverterOptions.Hexadecimal | ByteArrayConverterOptions.HexInclude0x,
					FieldStrings = new[] { "0xDEAD", "0xB33FBEEF", "0xEA5EEA5EEA5E", "0xCA75CA75CA75CA75" },
					Expected = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F, 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				},
				new
				{
					Options = ByteArrayConverterOptions.Hexadecimal | ByteArrayConverterOptions.HexDashes,
					FieldStrings = new[] { "DE-AD", "B3-3F-BE-EF", "EA-5E-EA-5E-EA-5E", "CA-75-CA-75-CA-75-CA-75" },
					Expected = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F, 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				},
				new
				{
					Options = ByteArrayConverterOptions.Base64,
					FieldStrings = new []
					{
						Convert.ToBase64String( new byte[] { 0xDE, 0xAD } ),
						Convert.ToBase64String( new byte[] { 0xB3, 0x3F, 0xBE, 0xEF } ),
						Convert.ToBase64String( new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E } ),
						Convert.ToBase64String( new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 } )
					},
					Expected = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F, 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				}
			};

			foreach( var t in testCases )
			{
				var converter = new ByteArrayConverter( t.Options );
				foreach( var f in t.FieldStrings.Zip( t.Expected, ( test, expected ) => new { test, expected } ) )
				{
					var actual = (byte[])converter.ConvertFromString( f.test, null, null );
					foreach( var b in actual.Zip( f.expected, ( a, e ) => new { a, e } ) )
					{
						Assert.AreEqual( b.e, b.a );
					}
				}
			}
		}

		[TestMethod]
		public void WhenConverting_FromByteArrayToString_ReturnsCorrectValues()
		{
			var testCases = new[]
			{
				new
				{
					Options = ByteArrayConverterOptions.Hexadecimal | ByteArrayConverterOptions.HexInclude0x,
					Expected = new[] { "0xDEAD", "0xB33FBEEF", "0xEA5EEA5EEA5E", "0xCA75CA75CA75CA75" },
					FieldBytes = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F, 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				},
				new
				{
					Options = ByteArrayConverterOptions.Hexadecimal | ByteArrayConverterOptions.HexDashes,
					Expected = new[] { "DE-AD", "B3-3F-BE-EF", "EA-5E-EA-5E-EA-5E", "CA-75-CA-75-CA-75-CA-75" },
					FieldBytes = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F, 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				},
				new
				{
					Options = ByteArrayConverterOptions.Base64,
					Expected = new []
					{
						Convert.ToBase64String( new byte[] { 0xDE, 0xAD } ),
						Convert.ToBase64String( new byte[] { 0xB3, 0x3F, 0xBE, 0xEF } ),
						Convert.ToBase64String( new byte[] { 0xEA, 0x5E, 0xEA, 0x5E, 0xEA, 0x5E } ),
						Convert.ToBase64String( new byte[] { 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 } )
					},
					FieldBytes = new []
					{
						new byte[] { 0xDE, 0xAD },
						new byte[] { 0xB3, 0x3F , 0xBE, 0xEF },
						new byte[] { 0xEA, 0x5E , 0xEA, 0x5E, 0xEA, 0x5E },
						new byte[] { 0xCA, 0x75 , 0xCA, 0x75, 0xCA, 0x75, 0xCA, 0x75 }
					}
				}
			};

			foreach( var t in testCases )
			{
				var converter = new ByteArrayConverter( t.Options );
				foreach( var f in t.Expected.Zip( t.FieldBytes, ( expected, test ) => new { test, expected } ) )
				{
					var actual = converter.ConvertToString( f.test, null, null );

					Assert.AreEqual( actual, f.expected );
				}
			}
		}
	}
}
