using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Xunit;

namespace CsvHelper.Tests.Mappings
{
    public class FieldRecordAttributeMappingTests
    {
        [Fact]
        public void WriteRecord_WithIgnoreAttribute_ShouldNotWriteIgnoredField()
        {
            var records = new List<RecordWithIgnore>
            {
                new("Visible", "ShouldBeIgnored")
            };

            var writer = new StringWriter();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var csv = new CsvWriter(writer, config);
            csv.WriteRecords(records);

            var result = writer.ToString();
            var expected = "VisibleField\r\nVisible\r\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadRecord_WithIgnoreAttribute_ShouldIgnoreFieldWhenReading()
        {
            var csv = "VisibleField,IgnoredField\r\nVisible,ShouldBeIgnored";

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StringReader(csv);
            using var csvReader = new CsvReader(reader, config);
            var records = csvReader.GetRecords<RecordWithIgnore>().ToList();

            Assert.Single(records);
            Assert.Equal("Visible", records[0].VisibleField);
            Assert.Null(records[0].IgnoredField);
        }

        public record RecordWithIgnore(
            [Name("VisibleField")] string VisibleField,
            [Ignore] string? IgnoredField
        );

        [Fact]
        public void Write_FieldStyleRecord_ShouldRespectAttributes()
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
            var writer = new StringWriter();
            using var csvWriter = new CsvWriter(writer, config);
            csvWriter.WriteRecords(records);

            var written = writer.ToString();
            var expected = "Name,Birthday,Age,Country,IsActive\r\nDana,2005-05-05,50,AU,True\r\n";
            Assert.Equal(expected, written);
        }

        [Fact]
        public void Read_FieldStyleRecord_ShouldRespectAttributes()
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
    }
}
