// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Collections.Generic;
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class ReferenceMappingAttributeTests
	{
		[Fact]
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
			queue.Enqueue( new[]
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
			} );
			queue.Enqueue( null );
			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( queue.Dequeue );

			var reader = new CsvReader( parserMock.Object );
			reader.Read();
			var person = reader.GetRecord<Person>();

			Assert.Equal( "John", person.FirstName );
			Assert.Equal( "Doe", person.LastName );
			Assert.Equal( "1234 Home St", person.HomeAddress.Street );
			Assert.Equal( "Home Town", person.HomeAddress.City );
			Assert.Equal( "Home State", person.HomeAddress.State );
			Assert.Equal( "12345", person.HomeAddress.Zip );
			Assert.Equal( "5678 Work Rd", person.WorkAddress.Street );
			Assert.Equal( "Work City", person.WorkAddress.City );
			Assert.Equal( "Work State", person.WorkAddress.State );
			Assert.Equal( "67890", person.WorkAddress.Zip );
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
