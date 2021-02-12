using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Writing
{
	[TestClass]
    public class FieldTypeTests
    {
		[TestMethod]
        public void Test1()
		{
			Type type = null;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				ShouldQuote = args =>
				{
					type = args.FieldType;
					return ConfigurationFunctions.ShouldQuote(args);
				},
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(string.Empty);
				Assert.AreEqual(typeof(string), type);

				csv.WriteField(1);
				Assert.AreEqual(typeof(int), type);

				csv.WriteField(string.Empty);
				Assert.AreEqual(typeof(string), type);
			}
		}
    }
}
