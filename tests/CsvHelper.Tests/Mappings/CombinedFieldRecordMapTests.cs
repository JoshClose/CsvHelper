using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.Mappings
{
    public class CombinedFieldRecordMapTests
    {
        [Fact]
        public void Write_WithAttributes_ShouldRespectAttributes()
        {
            var records = new List<CombinedFieldRecord>
            {
                new CombinedFieldRecord
                {
                    Name = "Dana",
                    Birthday = new DateTime(2005, 5, 5),
                    Age = 50,
                    Country = "AU",
                    IsActive = true
                }
            };

            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var writer = new StringWriter();
            using var csvWriter = new CsvWriter(writer, config);
            csvWriter.WriteRecords(records);

            var result = writer.ToString();
            var expected = "Name,Birthday,Age,Country,IsActive\r\nDana,2005-05-05,50,AU,True\r\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_WithAttributes_ShouldApplyDefaultsAndConstants()
        {
            var inputCsv = "Name,Birthday\r\nDana,2005-05-05";

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StringReader(inputCsv);
            using var csvReader = new CsvReader(reader, config);
            var records = csvReader.GetRecords<CombinedFieldRecord>().ToList();

            Assert.Single(records);
            var record = records[0];
            Assert.Equal("Dana", record.Name);
            Assert.Equal(new DateTime(2005, 5, 5), record.Birthday);
            Assert.Equal(50, record.Age);
            Assert.Equal("AU", record.Country);
            Assert.True(record.IsActive);
        }

        [Fact]
        public void Write_WithClassMap_ShouldOverrideAttributes()
        {
            var records = new List<CombinedFieldRecord>
            {
                new CombinedFieldRecord
                {
                    Name = "Dana",
                    Birthday = new DateTime(2005, 5, 5),
                    Age = 99,
                    Country = "NZ",
                    IsActive = false
                }
            };

            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var writer = new StringWriter();
            using var csvWriter = new CsvWriter(writer, config);
            csvWriter.Context.RegisterClassMap<CombinedFieldRecordMap>();

            csvWriter.WriteRecords(records);

            var result = writer.ToString();
            var expected = "FullName,BirthdayFormatted\r\nDana,05-05-2005\r\n";
            Assert.Equal(expected, result);
        }

        public record CombinedFieldRecord
        {
            public string? Name { get; init; }

            [Format("yyyy-MM-dd")]
            public DateTime Birthday { get; init; }

            [Default(50)]
            public int Age { get; init; }

            [Constant("AU")]
            public string? Country { get; init; }

            [Constant(true)]
            public bool IsActive { get; init; }
        }

        public sealed class CombinedFieldRecordMap : ClassMap<CombinedFieldRecord>
        {
            public CombinedFieldRecordMap()
            {
                Map(m => m.Name).Name("FullName");
                Map(m => m.Birthday).Name("BirthdayFormatted").TypeConverterOption.Format("dd-MM-yyyy");
                Map(m => m.Age).Ignore();
                Map(m => m.Country).Ignore();
                Map(m => m.IsActive).Ignore();
            }
        }
    }
}
