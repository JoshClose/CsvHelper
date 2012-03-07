using Xunit;

namespace CsvHelper.Tests
{
	public class StringExtensionTests
	{
		[Fact]
		public void IsNullOrWhiteSpaceTest()
		{
			string nullString = null;
			Assert.True( nullString.IsNullOrWhiteSpace() );
			Assert.True( "".IsNullOrWhiteSpace() );
			Assert.True( " ".IsNullOrWhiteSpace() );
			Assert.True( "	".IsNullOrWhiteSpace() );
			Assert.False( "a".IsNullOrWhiteSpace() );
		}
	}
}
