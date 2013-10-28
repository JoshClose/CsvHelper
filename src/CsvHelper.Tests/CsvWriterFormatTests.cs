// Copyright 2009-2013 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
    using System.Threading;

    [TestClass]
	public class CsvWriterFormatTests
	{
		[TestMethod]
		public void WriteFieldTest()
		{
			var record = new TestRecord
			{
				IntColumn = 1,
				DateColumn = new DateTime( 2012, 10, 1, 12, 12, 12 ),
				DecimalColumn = 150.99m,
				FirstColumn = "first column",
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream ) { AutoFlush = true };
			var csv = new CsvWriter( writer );
			csv.Configuration.CultureInfo = new CultureInfo( "en-US" );
			csv.Configuration.RegisterClassMap<TestRecordMap>();

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();
			var expected = "first column,0001,10/1/2012,$150.99\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		[TestMethod]
		public void WriteRecordWithReferencesTest()
		{
			var record = new Person
			{
				FirstName = "First Name",
				LastName = "Last Name",
				Updated = new DateTime( 2012, 10, 1, 12, 12, 12, 123 ),
				HomeAddress = new Address
				{
					Street = "Home Street",
					City = "Home City",
					State = "Home State",
					Zip = 02201,
				},
				WorkAddress = new Address
				{
					Street = "Work Street",
					City = "Work City",
					State = "Work State",
					Zip = 04100,
				}
			};

			var stream = new MemoryStream();
			var writer = new StreamWriter( stream ) { AutoFlush = true };
			var csv = new CsvWriter( writer );
			csv.Configuration.RegisterClassMap<PersonMap>();

			csv.WriteRecord( record );

			stream.Position = 0;
			var reader = new StreamReader( stream );
			var csvFile = reader.ReadToEnd();

			var expected = "First Name,Last Name,2012-10-01 12:12:12.123,Home Street,Home City,Home State,02201,Work Street,Work City,Work State,04100\r\n";

			Assert.AreEqual( expected, csvFile );
		}

		private class TestRecord
		{
			public int IntColumn { get; set; }

			public DateTime DateColumn { get; set; }

			public decimal DecimalColumn { get; set; }

			public string FirstColumn { get; set; }
		}

		private sealed class TestRecordMap : CsvClassMap<TestRecord>
		{
			public override void CreateMap()
			{
				Map( m => m.IntColumn ).Name( "Int Column" ).Index( 1 ).TypeConverterOption( "0000" );
				Map( m => m.DateColumn ).Index( 2 ).TypeConverterOption( "d" );
				Map( m => m.DecimalColumn ).Index( 3 ).TypeConverterOption( "c" );
				Map( m => m.FirstColumn ).Index( 0 );
			}
		}

		private class Person
		{
			public string FirstName { get; set; }

			public string LastName { get; set; }

			public DateTime Updated { get; set; }

			public Address HomeAddress { get; set; }

			public Address WorkAddress { get; set; }
		}

		private class Address
		{
			public string Street { get; set; }

			public string City { get; set; }

			public string State { get; set; }

			public int Zip { get; set; }
		}

		private sealed class PersonMap : CsvClassMap<Person>
		{
			public override void CreateMap()
			{
				Map( m => m.FirstName );
				Map( m => m.LastName );
				Map( m => m.Updated ).TypeConverterOption( "yyyy-MM-dd HH:mm:ss.fff" );
				References<HomeAddressMap>( m => m.HomeAddress );
				References<WorkAddressMap>( m => m.WorkAddress );
			}
		}

		private sealed class HomeAddressMap : CsvClassMap<Address>
		{
			public override void CreateMap()
			{
				Map( m => m.Street ).Name( "HomeStreet" );
				Map( m => m.City ).Name( "HomeCity" );
				Map( m => m.State ).Name( "HomeState" );
				Map( m => m.Zip ).Name( "HomeZip" ).TypeConverterOption( "00000" );
			}
		}

		private sealed class WorkAddressMap : CsvClassMap<Address>
		{
			public override void CreateMap()
			{
				Map( m => m.Street ).Name( "WorkStreet" );
				Map( m => m.City ).Name( "WorkCity" );
				Map( m => m.State ).Name( "WorkState" );
				Map( m => m.Zip ).Name( "WorkZip" ).TypeConverterOption( "00000" );
			}
		}

	}
}
