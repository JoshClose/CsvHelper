#region License
// Copyright 2009 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class CsvWriterTests
	{
		[TestMethod]
		public void WriteFieldTest()
		{
			var stream = new MemoryStream();
			var writer = new StreamWriter( stream );

			var csv = new CsvWriter( writer );

			var date = DateTime.Now.ToString();
			var guid = Guid.NewGuid().ToString();
			csv.WriteField( "one" );
			csv.WriteField( "one, two" );
			csv.WriteField( "one \"two\" three" );
			csv.WriteField( " one " );
			csv.WriteField( date );
			csv.WriteField( (short)1 );
			csv.WriteField( 1 );
			csv.WriteField( (long)1 );
			csv.WriteField( (float)1 );
			csv.WriteField( (double)1 );
			csv.WriteField( guid );
			csv.NextRecord();

			var reader = new StreamReader( stream );
			stream.Position = 0;
			var data = reader.ReadToEnd();

			Assert.AreEqual( "one,\"one, two\",\"one \"\"two\"\" three\",\" one \"," + date + ",1,1,1,1,1," + guid + "\r\n", data );
		}
	}
}
