using System.Data;
using System.Globalization;
using System.IO;
using Xunit;

namespace CsvHelper.Tests.Issues
{
	public class Issue2060
	{
		[Fact]
		public void Test1()
		{
			var data =
				"""
				A
				B
				C
				Id,Name
				1,Jeff
				2,Kevin
				""";
			using var reader = new StringReader(data);
			using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
			while (csv.Read())
			{
				if (csv[0] == "Id")
				{
					break;
				}
			}

			csv.ReadHeader();

			using var dr = new CsvDataReader(csv);
			var dt = new DataTable();
			dt.Load(dr);

			Assert.Equal("Id", dt.Columns[0].ColumnName);
			Assert.Equal("Name", dt.Columns[1].ColumnName);
		}
	}
}
