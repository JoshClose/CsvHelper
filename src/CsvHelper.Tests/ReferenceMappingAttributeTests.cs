// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

using System.Collections.Generic;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace CsvHelper.Tests
{
	[TestClass]
	public class ReferenceMappingAttributeTests
	{
		[TestMethod]
		public void ReferenceMappingTest()
		{
			var queue = new Queue<string[]>();
			queue.Enqueue( new[]
			{
				"FirstName",
				"LastName",
				"HomeStreet",
				"HomeCity",
				"HomeState",
				"HomeZip",
				"WorkStreet",
				"WorkCity",
				"WorkState",
				"WorkZip"
			} );
			var row = new[]
			{
				"John",
				"Doe",
				"1234 Home St",
				"Home Town",
				"Home State",
				"12345",
				"5678 Work Rd",
				"Work City",
				"Work State",
				"67890"
			};
			queue.Enqueue( row );
			queue.Enqueue( null );
			var parserMock = new ParserMock( queue );

			var reader = new CsvReader( parserMock );
			reader.Read();
			var person = reader.GetRecord<Person>();

			Assert.AreEqual( "John", person.FirstName );
			Assert.AreEqual( "Doe", person.LastName );
			Assert.AreEqual( "1234 Home St", person.HomeAddress.Street );
			Assert.AreEqual( "Home Town", person.HomeAddress.City );
			Assert.AreEqual( "Home State", person.HomeAddress.State );
			Assert.AreEqual( "12345", person.HomeAddress.Zip );
			Assert.AreEqual( "5678 Work Rd", person.WorkAddress.Street );
			Assert.AreEqual( "Work City", person.WorkAddress.City );
			Assert.AreEqual( "Work State", person.WorkAddress.State );
			Assert.AreEqual( "67890", person.WorkAddress.Zip );
		}

		private class Person
		{
			[CsvField( Name = "FirstName" )]
			public string FirstName { get; set; }

			[CsvField( Name = "LastName" )]
			public string LastName { get; set; }

			[CsvField( ReferenceKey = "Home" )]
			public Address HomeAddress { get; set; }

			[CsvField( ReferenceKey = "Work" )]
			public Address WorkAddress { get; set; }
		}

		private class Address
		{
			[CsvField( ReferenceKey = "Home", Name = "HomeStreet" )]
			[CsvField( ReferenceKey = "Work", Name = "WorkStreet" )]
			public string Street { get; set; }

			[CsvField( ReferenceKey = "Home", Name = "HomeCity" )]
			[CsvField( ReferenceKey = "Work", Name = "WorkCity" )]
			public string City { get; set; }

			[CsvField( ReferenceKey = "Home", Name = "HomeState" )]
			[CsvField( ReferenceKey = "Work", Name = "WorkState" )]
			public string State { get; set; }

			[CsvField( ReferenceKey = "Home", Name = "HomeZip" )]
			[CsvField( ReferenceKey = "Work", Name = "WorkZip" )]
			public string Zip { get; set; }
		}
	}
}
