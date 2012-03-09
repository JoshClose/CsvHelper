// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using CsvHelper.Configuration;
using Moq;
using Xunit;

namespace CsvHelper.Tests
{
	public class ReferenceMappingClassMapTests
	{
		[Fact]
		public void ReferenceMappingTest()
		{
			var count = 0;
			var parserMock = new Mock<ICsvParser>();
			parserMock.Setup( m => m.Configuration ).Returns( new CsvConfiguration() );
			parserMock.Setup( m => m.Read() ).Returns( () =>
			{
				if( count == 0 )
				{
					return new[]
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
					};
				}
				count++;
				return new[]
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
			} );

			var reader = new CsvReader( parserMock.Object );
			reader.Configuration.ClassMapping<PersonMap>();
			reader.Read();
			var person = reader.GetRecord<Person>();

			Assert.Equal( "FirstName", person.FirstName );
			Assert.Equal( "LastName", person.LastName );
			Assert.Equal( "HomeStreet", person.HomeAddress.Street );
			Assert.Equal( "HomeCity", person.HomeAddress.City );
			Assert.Equal( "HomeState", person.HomeAddress.State );
			Assert.Equal( "HomeZip", person.HomeAddress.Zip );
			Assert.Equal( "WorkStreet", person.WorkAddress.Street );
			Assert.Equal( "WorkCity", person.WorkAddress.City );
			Assert.Equal( "WorkState", person.WorkAddress.State );
			Assert.Equal( "WorkZip", person.WorkAddress.Zip );
		}

		private class Person
		{
			public string FirstName { get; set; }

			public string LastName { get; set; }

			public Address HomeAddress { get; set; }

			public Address WorkAddress { get; set; }
		}

		private class Address
		{
			public string Street { get; set; }

			public string City { get; set; }

			public string State { get; set; }

			public string Zip { get; set; }
		}

		private sealed class PersonMap : CsvClassMap<Person>
		{
			public PersonMap()
			{
				Map( m => m.FirstName );
				Map( m => m.LastName );
				References<HomeAddressMap>( m => m.HomeAddress );
				References<WorkAddressMap>( m => m.WorkAddress );
			}
		}

		private sealed class HomeAddressMap : CsvClassMap<Address>
		{
			public HomeAddressMap()
			{
				Map( m => m.Street ).Name( "HomeStreet" );
				Map( m => m.City ).Name( "HomeCity" );
				Map( m => m.State ).Name( "HomeState" );
				Map( m => m.Zip ).Name( "HomeZip" );
			}
		}

		private sealed class WorkAddressMap : CsvClassMap<Address>
		{
			public WorkAddressMap()
			{
				Map(m => m.Street).Name( "WorkStreet" );
				Map(m => m.City).Name( "WorkCity" );
				Map(m => m.State).Name( "WorkState" );
				Map(m => m.Zip).Name( "WorkZip" );
			}
		}
	}
}
