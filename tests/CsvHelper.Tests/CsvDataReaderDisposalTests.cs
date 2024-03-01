using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace CsvHelper.Tests
{
	public class CsvDataReaderDisposalTests
	{
		[Fact]
		public void ShouldNotDisposeCsvReaderWhenLeaveOpenParameterIsTrue()
		{
			var s = new StringBuilder();
			s.AppendLine("StringColumn");
			s.AppendLine("one");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var dataReader = new CsvDataReader(csv, leaveOpen: true);
				dataReader.Dispose();

				var record = csv.GetRecord<TestRecord>();
				Assert.NotNull(record);
			}
		}

		[Fact]
		public void DisposeShouldSetIsClosed()
		{
			var s = new StringBuilder();
			s.AppendLine("StringColumn");
			s.AppendLine("one");
			using (var reader = new StringReader(s.ToString()))
			using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
			{
				var dataReader = new CsvDataReader(csv, leaveOpen: true);
				dataReader.Dispose();

				Assert.True(dataReader.IsClosed);
			}
		}

		private class TestRecord()
		{
			public string StringColumn { get; set; }
		}
	}
}
