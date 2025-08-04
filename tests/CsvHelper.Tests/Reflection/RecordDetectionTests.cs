using Xunit;

namespace CsvHelper.Tests.Configuration
{
	public class RecordDetectionTests
	{
		private class TestClass
		{
			public int Id { get; set; }
		}

		private record TestRecord(int Id);
		private struct TestStruct
		{
			public int Id { get; }
			public TestStruct(int id)
			{
				Id = id;
			}
		}

		[Fact]
		public void IsRecord_ReturnsFalse_ForNormalClass()
		{
			Assert.False(ReflectionHelper.IsRecord(typeof(TestClass)));
		}

		[Fact]
		public void IsRecord_ReturnsFalse_ForStruct()
		{
			Assert.False(ReflectionHelper.IsRecord(typeof(TestStruct)));
		}

		[Fact]
		public void IsRecord_ReturnsTrue_ForRecord()
		{
			Assert.True(ReflectionHelper.IsRecord(typeof(TestRecord)));
		}
	}
}
