using System.Globalization;

using CsvHelper.TypeConversion;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class TypeConverterExceptionTests
	{
		[TestMethod]
		public void ConvertToStringTest()
		{
			var converter = new Int16Converter();
			var typeConverterOptions = new TypeConverterOptions
			{
				CultureInfo = new CultureInfo("en-US")
			};

			try
			{
				converter.ConvertFromString(typeConverterOptions, "not an int");
				Assert.Fail();
			}
			catch (CsvTypeConverterException csvTypeConverterException)
			{
				Assert.AreEqual("The conversion of value [not an int] using converter [CsvHelper.TypeConversion.Int16Converter] cannot be performed. Using CultureInfo [en-US]", csvTypeConverterException.Message);
			}
			
		}

	}
}
