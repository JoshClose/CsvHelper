// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;
using CsvHelper.Configuration;
using CsvHelper.Tests.Mocks;
using Xunit;

namespace CsvHelper.Tests
{
	
	public class ReferenceMappingClassMapTests
	{
		[Fact]
		public void ReferenceMappingTest()
		{
			var parserMock = new ParserMock
			{
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
				},
			};
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
			parserMock.Add(row);

			var reader = new CsvReader(parserMock);
			reader.Context.RegisterClassMap<PersonMap>();
			reader.Read();
			var person = reader.GetRecord<Person>();

			Assert.Equal(row[0], person.FirstName);
			Assert.Equal(row[1], person.LastName);
			Assert.Equal(row[2], person.HomeAddress.Street);
			Assert.Equal(row[3], person.HomeAddress.City);
			Assert.Equal(row[4], person.HomeAddress.State);
			Assert.Equal(row[5], person.HomeAddress.Zip);
			Assert.Equal(row[6], person.WorkAddress.Street);
			Assert.Equal(row[7], person.WorkAddress.City);
			Assert.Equal(row[8], person.WorkAddress.State);
			Assert.Equal(row[9], person.WorkAddress.Zip);
		}

		[Fact]
		public void OnlyReferencesTest()
		{
			var parserMock = new ParserMock()
			{
				new[]
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
				},
				new[]
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
				},
				null
			};

			var reader = new CsvReader(parserMock);
			reader.Context.RegisterClassMap<OnlyReferencesMap>();
			reader.Read();
			var person = reader.GetRecord<Person>();
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

		private sealed class PersonMap : ClassMap<Person>
		{
			public PersonMap()
			{
				Map(m => m.FirstName);
				Map(m => m.LastName);
				References<HomeAddressMap>(m => m.HomeAddress);
				References<WorkAddressMap>(m => m.WorkAddress);
			}
		}

		private sealed class HomeAddressMap : ClassMap<Address>
		{
			public HomeAddressMap()
			{
				Map(m => m.Street).Name("HomeStreet");
				Map(m => m.City).Name("HomeCity");
				Map(m => m.State).Name("HomeState");
				Map(m => m.Zip).Name("HomeZip");
			}
		}

		private sealed class WorkAddressMap : ClassMap<Address>
		{
			public WorkAddressMap()
			{
				Map(m => m.Street).Name("WorkStreet");
				Map(m => m.City).Name("WorkCity");
				Map(m => m.State).Name("WorkState");
				Map(m => m.Zip).Name("WorkZip");
			}
		}

		private sealed class OnlyReferencesMap : ClassMap<Person>
		{
			public OnlyReferencesMap()
			{
				References<HomeAddressMap>(m => m.HomeAddress);
				References<WorkAddressMap>(m => m.WorkAddress);
			}
		}
	}
}
