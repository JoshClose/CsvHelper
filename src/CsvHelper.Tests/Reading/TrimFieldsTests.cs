using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Reading
{
	[TestClass]
    public class TrimFieldsTests
    {
		[TestMethod]
		public void GetFieldTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { " 1 " } );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.HasHeaderRecord = false;
			reader.Configuration.TrimFields = true;
			reader.Configuration.WillThrowOnMissingField = false;
			reader.Read();

			Assert.AreEqual( "1", reader.GetField( 0 ) );
			Assert.AreEqual( null, reader.GetField( 1 ) );
			Assert.AreEqual( 1, reader.GetField<int>( 0 ) );
		}

		[TestMethod]
		public void GetRecordTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[] { "Id", "Name" } );
			queue.Enqueue( new[] { " 1 ", "one" } );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Configuration.RegisterClassMap<TestMap>();
			reader.Configuration.TrimFields = true;
			reader.Configuration.WillThrowOnMissingField = false;
			reader.Read();
			var record = reader.GetRecord<Test>();

			Assert.AreEqual( "1", record.Id );
			Assert.AreEqual( " one ", record.Name );
		}

		private class Test
		{
			public string Id { get; set; }

			public string Name { get; set; }
		}

		private sealed class TestMap : CsvClassMap<Test>
		{
			public TestMap()
			{
				Map( m => m.Id );
				Map( m => m.Name ).ConvertUsing( row => $" {row.GetField( 1 )} " );
			}
		}
	}
}
