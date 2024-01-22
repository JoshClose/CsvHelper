using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	public class BadDataTests
	{
		[Fact]
		public void GetRecord_BadDataCountNotDuplicted()
		{
			var errorCount = 0;
			var csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ReadingExceptionOccurred = args => false,
				BadDataFound = args =>
				{
					++errorCount;
				},
				Delimiter = ";",
			};
			var csv = "SKU;Min quantity;List price;Sale price\r\nTestSku1;2;10.99;9.99\r\nTestSku2;2;10.99;9\r\nXXX;\"9;10.9;9";
			var stream = new MemoryStream();
			using var writer = new StreamWriter(stream);
				writer.Write(csv);
				writer.Flush();
				stream.Position = 0;

			using var textReader = new StreamReader(stream);
			var csvReader = new CsvReader(textReader, csvConfiguration);

			while (csvReader.Read())
			{
				csvReader.GetRecord<CsvPrice>();
			}

			Assert.Equal(1, errorCount);
		}

		public sealed class CsvPrice
		{
			[Name("SKU")]
			public string Sku { get; set; }

			[Name("Min quantity")]
			public int MinQuantity { get; set; }

			[Name("List price")]
			public decimal ListPrice { get; set; }

			[Name("Sale price")]
			public decimal? SalePrice { get; set; }
		}
	}
}
