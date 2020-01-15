// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
			using( var csv = new CsvWriter(writer, CultureInfo.InvariantCulture) )
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
