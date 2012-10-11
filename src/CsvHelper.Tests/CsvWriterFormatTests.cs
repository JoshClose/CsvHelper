﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests
{
    public class CsvWriterFormatTests
    {
        [Fact]
        public void WriteFieldTest()
        {
            var record = new TestRecord
            {
                IntColumn = 1,
                DateColumn = new DateTime(2012, 10, 1, 12, 12, 12),
                DecimalColumn = 150.99m,
                FirstColumn = "first column",
                TypeConvertedColumn = "string"
            };

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var csv = new CsvWriter(writer);

            csv.WriteRecord(record);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();
            var expected = "FirstColumn,Int Column,DateColumn,DecimalColumn,TypeConvertedColumn\r\n";
            expected += "first column,0001,10/1/2012,$150.99,Type-string\r\n";

            Assert.Equal(expected, csvFile);
        }

        [Fact]
        public void WriteRecordWithReferencesTest()
        {
            var record = new Person
            {
                FirstName = "First Name",
                LastName = "Last Name",
                Updated = new DateTime(2012, 10, 1, 12, 12, 12, 123),
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
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var csv = new CsvWriter(writer);
            csv.Configuration.ClassMapping<PersonMap>();

            csv.WriteRecord(record);

            stream.Position = 0;
            var reader = new StreamReader(stream);
            var csvFile = reader.ReadToEnd();

            var expected = "FirstName,LastName,Updated,HomeStreet,HomeCity,HomeState,HomeZip,WorkStreet,WorkCity,WorkState,WorkZip\r\n" +
                           "First Name,Last Name,2012-10-01 12:12:12.123,Home Street,Home City,Home State,02201,Work Street,Work City,Work State,04100\r\n";

            Assert.Equal(expected, csvFile);

        }

        private class TestRecord
        {
            [CsvField(Index = 1, Name = "Int Column", Format = "{0:0000}")]
            public int IntColumn { get; set; }

            [CsvField(Format = "{0:d}")]
            public DateTime DateColumn { get; set; }

            [CsvField(Format = "{0:c}")]
            public decimal DecimalColumn { get; set; }

            [CsvField(Index = 0)]
            public string FirstColumn { get; set; }

            [CsvField(Format = "Type-{0}")]
            public string TypeConvertedColumn { get; set; }
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
            public PersonMap()
            {
                Map(m => m.FirstName);
                Map(m => m.LastName);
                Map(m => m.Updated).Format("{0:yyyy-MM-dd HH:mm:ss.fff}");
                References<HomeAddressMap>(m => m.HomeAddress);
                References<WorkAddressMap>(m => m.WorkAddress);
            }
        }

        private sealed class HomeAddressMap : CsvClassMap<Address>
        {
            public HomeAddressMap()
            {
                Map(m => m.Street).Name("HomeStreet");
                Map(m => m.City).Name("HomeCity");
                Map(m => m.State).Name("HomeState");
                Map(m => m.Zip).Name("HomeZip").Format("{0:00000}");
            }
        }

        private sealed class WorkAddressMap : CsvClassMap<Address>
        {
            public WorkAddressMap()
            {
                Map(m => m.Street).Name("WorkStreet");
                Map(m => m.City).Name("WorkCity");
                Map(m => m.State).Name("WorkState");
                Map(m => m.Zip).Name("WorkZip").Format("{0:00000}");
            }
        }

    }
}
