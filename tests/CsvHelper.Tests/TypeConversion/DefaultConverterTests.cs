using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
    public class DefaultConverterTests
    {
		[TestMethod]
		public void ConvertToString_ValueIsNull_ReturnsEmptyString()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.AreEqual(string.Empty, value);
		}

		[TestMethod]
        public void ConvertToString_SingleNullValue_UsesValue()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { NullValues = { "Foo" } },
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void ConvertToString_MultipleNullValues_UsesFirstValue()
		{
			var converter = new DefaultTypeConverter();

			var memberMapData = new MemberMapData(null)
			{
				TypeConverterOptions = { NullValues = { "Foo", "Bar" } },
			};

			var value = converter.ConvertToString(null, null, memberMapData);

			Assert.AreEqual("Foo", value);
		}

		[TestMethod]
		public void WriteField_NullValue_UsesValue()
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<string>().NullValues.Add("Foo");

				csv.WriteField<string>(null);
				csv.Flush();
				writer.Flush();

				Assert.AreEqual("Foo", writer.ToString());
			}
		}
	}
}
