using System;
using System.Globalization;
using System.Linq;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Moq;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using CsvHelper.Converters;
using static CsvHelper.Converters.ByteArrayConverter;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class ByteArrayConverterTests
	{
		[TestMethod]
		public void WhenConverting_FromStringToByteArray_ReturnsCorrectValues()
		{
		    
            var testCases = new []
            {
                new
                {
                    Options = Options.HexDecimal | Options.HexInclude0x,
                    FieldStrings = new[] { "0xDEAD", "0xB33FBEEF", "0xEA5EEA5EEA5E", "0xCA75CA75CA75CA75" },
                    Expected = new []
                    {
                        new byte[]{0xDE,0xAD},
                        new byte[]{0xB3,0x3F,0xBE,0xEF},
                        new byte[]{0xEA,0x5E,0xEA,0x5E,0xEA,0x5E},
                        new byte[]{0xCA,0x75, 0xCA, 0x75, 0xCA,0x75,0xCA,0x75}
                    }
                },
                new
                {
                    Options = Options.HexDecimal | Options.HexDashes,
                    FieldStrings = new[] { "DE-AD", "B3-3F-BE-EF", "EA-5E-EA-5E-EA-5E", "CA-75-CA-75-CA-75-CA-75" },
                    Expected = new []
                    {
                        new byte[]{0xDE,0xAD},
                        new byte[]{0xB3,0x3F,0xBE,0xEF},
                        new byte[]{0xEA,0x5E,0xEA,0x5E,0xEA,0x5E},
                        new byte[]{0xCA,0x75, 0xCA, 0x75, 0xCA,0x75,0xCA,0x75}
                    }
                },
                new
                {
                    Options = Options.Base64,
                    FieldStrings = new []
                    {
                        Convert.ToBase64String(new byte[]{0xDE,0xAD}),
                        Convert.ToBase64String(new byte[]{0xB3,0x3F,0xBE,0xEF}),
                        Convert.ToBase64String(new byte[]{0xEA,0x5E,0xEA,0x5E,0xEA,0x5E}),
                        Convert.ToBase64String(new byte[]{0xCA,0x75, 0xCA, 0x75, 0xCA,0x75,0xCA,0x75})
                    },
                    Expected = new []
                    {
                        new byte[]{0xDE,0xAD},
                        new byte[]{0xB3,0x3F,0xBE,0xEF},
                        new byte[]{0xEA,0x5E,0xEA,0x5E,0xEA,0x5E},
                        new byte[]{0xCA,0x75, 0xCA, 0x75, 0xCA,0x75,0xCA,0x75}
                    }
                }
            };
			
		    foreach( var t in testCases )
		    {
		        var converter = new ByteArrayConverter(t.Options);
                foreach ( var f in t.FieldStrings.Zip(t.Expected, (test,expected)=>new{test,expected}) )
                {
                    var actual = (byte[])converter.ConvertFromString( f.test, null, null );
                    foreach( var b in actual.Zip( f.expected, ( a, e ) => new { a, e } ) )
                    {
                        Assert.AreEqual(b.e, b.a);
                    }
                }
		    }
		}
	}
}
