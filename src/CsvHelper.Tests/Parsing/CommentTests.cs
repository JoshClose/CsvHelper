using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class CommentTests
	{
		[TestMethod]
		public void CommentThatCrossesBuffersShouldNotAddToFieldTest()
		{
			var config = new CsvConfiguration
			{
				AllowComments = true,
				BufferSize = 10
			};
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var parser = new CsvParser( reader, config ) )
			{
				writer.Write( "1,2\r\n" );
				writer.Write( "#commented line\r\n" );
				writer.Write( "3,4" );
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				var line = parser.Read();
				Assert.AreEqual( "3", line[0] );
				Assert.AreEqual( "4", line[1] );
			}
		}

		[TestMethod]
		public void WriteCommentCharInFieldWithCommentsOffTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.AllowComments = false;
				csv.WriteField( "#no comment" );
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "#no comment\r\n", result );
			}
		}

		[TestMethod]
		public void WriteCommentCharInFieldWithCommentsOnTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.AllowComments = true;
				csv.WriteField( "#no comment" );
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "\"#no comment\"\r\n", result );
			}
		}

		[TestMethod]
		public void WriteCommentWithCommentsOffTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.AllowComments = false;
				csv.WriteComment( "comment\"has\" quote" );
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "#comment\"has\" quote\r\n", result );
			}
		}

		[TestMethod]
		public void WriteCommentWithCommentsOnTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.AllowComments = true;
				csv.WriteComment( "comment\"has\" quote" );
				csv.NextRecord();
				writer.Flush();
				stream.Position = 0;

				var result = reader.ReadToEnd();

				Assert.AreEqual( "#comment\"has\" quote\r\n", result );
			}
		}
	}
}
