using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.TypeConversion
{
	public class TypeConverter1Tests
	{
		[Fact]
		public void ConvertToString_NullableBoolean_Converts()
		{
			ITypeConverter converter = new GenericBoolConverter();
			var result = converter.ConvertToString(null, null!, null!);

			Assert.Equal(string.Empty, result);
		}

		[Fact]
		public void ConvertFromString_NullableBoolean_Converts()
		{
			ITypeConverter converter = new GenericBoolConverter();
			var result = (bool?)converter.ConvertFromString("true", null!, null!);

			Assert.True(result);
		}

		private class GenericBoolConverter : TypeConverter<bool?>
		{
			public override bool? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
			{
				if (bool.TryParse(text, out var result))
				{
					return result;
				}

				return null;
			}

			public override string ConvertToString(bool? value, IWriterRow row, MemberMapData memberMapData)
			{
				return value?.ToString() ?? "";
			}
		}
	}
}
