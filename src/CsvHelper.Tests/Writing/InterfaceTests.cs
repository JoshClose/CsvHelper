using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class InterfaceTests
    {
		[TestMethod]
		public void WriteRecordsGenericTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				var records = new List<IRecord>();
				IRecord record = new Record { A = 1, B = 2 };
				records.Add( record );
				record = new Record { A = 3, B = 4 };
				records.Add( record );

				csv.Configuration.RegisterClassMap<RecordMap>();
				csv.WriteRecords( records );
				writer.Flush();
				stream.Position = 0;

				var expected = "RenameA\r\n1\r\n3\r\n";
				Assert.AreEqual( expected, reader.ReadToEnd() );
			}
		}

		[TestMethod]
		public void WriteRecordTest()
		{
			using( var stream = new MemoryStream() )
			using( var reader = new StreamReader( stream ) )
			using( var writer = new StreamWriter( stream ) )
			using( var csv = new CsvWriter( writer ) )
			{
				csv.Configuration.RegisterClassMap<RecordMap>();

				csv.WriteHeader<IRecord>();
				csv.NextRecord();

				IRecord record = new Record { A = 1, B = 2 };
				csv.WriteRecord( record );
				csv.NextRecord();

				record = new Record { A = 3, B = 4 };
				csv.WriteRecord( record );
				csv.NextRecord();

				writer.Flush();
				stream.Position = 0;

				var expected = "RenameA\r\n1\r\n3\r\n";
				Assert.AreEqual( expected, reader.ReadToEnd() );
			}
		}

		private interface IRecord
		{
			int A { get; set; }
			int B { get; set; }
		}

		private class Record : IRecord
		{
			public int A { get; set; }
			public int B { get; set; }
		}

		private sealed class RecordMap : ClassMap<IRecord>
		{
			public RecordMap()
			{
				Map( m => m.A ).Name( "RenameA" );
			}
		}

	}
}
