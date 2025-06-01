using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper.Configuration;
using Xunit;

namespace CsvHelper.Tests.Reading
{
	public class NumberReadingTests
	{
		[Fact]
		public void ReadDecimalDoubleCultureDeInvariantMix()
		{
			var input = new StringReader("""
			                             MyMoney;YourMoney;MoreMoney;BigMoney
			                             1.2;3.4;5.6;7.8
			                             """);

			using var cr = new CsvReader(input, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" });
			var records = cr.GetRecords(new
			{
				MyMoney = default(decimal),
				YourMoney = default(double),
				MoreMoney = default(decimal?),
				BigMoney = default(double?),
			}).ToArray();

			Assert.Equal(1.2m, records.Single().MyMoney);
			Assert.Equal(3.4, records.Single().YourMoney);
			Assert.Equal(5.6m, records.Single().MoreMoney);
			Assert.Equal(7.8, records.Single().BigMoney);
		}
	}
}
