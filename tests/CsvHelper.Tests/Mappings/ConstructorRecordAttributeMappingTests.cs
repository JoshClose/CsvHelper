using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Runtime;
using Xunit;


namespace CsvHelper.Tests.Mappings
{
    public class ConstructorRecordAttributeMappingTests
    {
        [Fact]
        public void WriteRecord_WithMultipleAttributes_ShouldRespectAll()
        {
            var records = new List<PersonRecordMulti>
            {
                new(true, false, "Alice", new DateTime(1990, 2, 1), "CN", 35)
            };

            var writer = new StringWriter();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var csv = new CsvWriter(writer, config);
            csv.WriteRecords(records);

            var result = writer.ToString();
            var expected = "IsActive,Full Name,IsDeleted,Birthday,Country,Age\r\nTrue,Alice,False,1990-02-01,AU,35\r\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReadRecord_WithMultipleAttributes_ShouldParseCorrectly()
        {
            var csv = "IsActive,Full Name,IsDeleted,Birthday,Country,Age\r\nTrue,Alice,False,1990-02-01,NZ,35";

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            using var reader = new StringReader(csv);
            using var csvReader = new CsvReader(reader, config);
            var records = csvReader.GetRecords<PersonRecordMulti>().ToList();

            Assert.Single(records);
            var r = records[0];
            Assert.True(r.IsActive);
            Assert.False(r.IsDeleted);
            Assert.Equal("Alice", r.Name);
            Assert.Equal(new DateTime(1990, 2, 1), r.Birthday);
            Assert.Equal("AU", r.Country);
            Assert.Equal(35, r.Age);
        }

        public record PersonRecordMulti(
            [Name("IsActive")][Index(0)] bool IsActive,
            [Name("IsDeleted")][Index(2)] bool IsDeleted,
            [Name("Full Name")][Index(1)] string Name,
            [Name("Birthday")][Format("yyyy-MM-dd")][Index(3)] DateTime Birthday,
            [Name("Country")][Constant("AU")][Index(4)] string Country,
            [Name("Age")] int Age,
            [Ignore] string? Ignored = null
        );

        [Fact]
        public void ReadRecord_WithMissingFieldAndDefaultAttribute_ShouldUseDefault()
        {
            var csv = "Full Name,Birthday\r\nBob,1999-12-12";

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StringReader(csv);
            using var csvReader = new CsvReader(reader, config);
            var records = csvReader.GetRecords<PartialRecord>().ToList();

            Assert.Single(records);
            Assert.Equal("Bob", records[0].Name);
            Assert.Equal(new DateTime(1999, 12, 12), records[0].Birthday);
            Assert.Equal(99, records[0].Age);
        }

        public record PartialRecord(
            [Name("Full Name")] string Name,
            [Format("yyyy-MM-dd")] DateTime Birthday,
            [Default(99)] int Age
        );

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

    
    }
}