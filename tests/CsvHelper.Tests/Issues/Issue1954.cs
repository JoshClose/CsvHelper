using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;


namespace CsvHelper.Tests.Issues {

	public class Issue1954 {

		[Fact]
		public void Test1() {
			var data = @"field1, field2, field3
1, 2, ""test""
3, 4, ""TEST""";

			var opts = new CsvConfiguration(CultureInfo.InvariantCulture) {
				Delimiter = ",",
				TrimOptions = TrimOptions.Trim,
				BufferSize = 44
			};

			using (var sr = new StringReader(data))
			using (var csv = new CsvReader(sr, opts)) {
				var records = csv.GetRecords<Row>().ToArray();

				Assert.Equal(2, records.Length);

				Assert.Equal(1, records[0].Field1);
				Assert.Equal(2, records[0].Field2);
				Assert.Equal("test", records[0].Field3);

				Assert.Equal(3, records[1].Field1);
				Assert.Equal(4, records[1].Field2);
				Assert.Equal("TEST", records[1].Field3);
			}
		}

		[Fact]
		public void Test2() {
			var data = @"field1, field2, field3
1, 2, ""test""
3, 4, ""TEST""";

			var opts = new CsvConfiguration(CultureInfo.InvariantCulture) {
				Delimiter = ",",
				TrimOptions = TrimOptions.Trim,
				BufferSize = 45
			};

			using (var sr = new StringReader(data))
			using (var csv = new CsvReader(sr, opts)) {
				var records = csv.GetRecords<Row>().ToArray();

				Assert.Equal(2, records.Length);

				Assert.Equal(1, records[0].Field1);
				Assert.Equal(2, records[0].Field2);
				Assert.Equal("test", records[0].Field3);

				Assert.Equal(3, records[1].Field1);
				Assert.Equal(4, records[1].Field2);
				Assert.Equal("TEST", records[1].Field3);
			}
		}

		private class Row {
			[Name("field1")] public int Field1 { get; set; }
			[Name("field2")] public int Field2 { get; set; }
			[Name("field3")] public string Field3 { get; set; }
		}
	}
}
