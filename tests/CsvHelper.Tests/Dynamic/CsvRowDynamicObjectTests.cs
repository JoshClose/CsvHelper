using Xunit;

namespace CsvHelper.Tests.Dynamic
{
	public class CsvRowDynamicObjectTests
	{
		[Fact]
		public void Dynamic_SetAndGet_Works()
		{
			dynamic obj = new FastDynamicObject();
			obj.Id = 1;
			obj.Name = "one";

			var id = obj.Id;
			var name = obj.Name;

			Assert.Equal(1, id);
			Assert.Equal("one", name);
		}
	}
}
